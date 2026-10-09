using System.Security;
using Itorrent.Core.Policies;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;
using MonoTorrent;
using MonoTorrent.Client;
using Serilog;

namespace Itorrent.Core.Engine;

public sealed class TorrentService : ITorrentService
{
    private static readonly TimeSpan ScrapeInterval = TimeSpan.FromMinutes(3);

    private readonly ISettingsService _settings;
    private readonly TorrentRepository _repository;
    private readonly string _cacheDirectory;
    private readonly PolicyEngine _policy;
    private readonly ILogger _log = Log.ForContext<TorrentService>();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private ClientEngine? _engine;

    public TorrentService(ISettingsService settings, TorrentRepository repository, string dataDirectory)
    {
        _settings = settings;
        _repository = repository;
        _cacheDirectory = Path.Combine(dataDirectory, "cache");
        _policy = new PolicyEngine(() => _settings.Current.StopSeedingOnComplete);
    }

    public event EventHandler<TorrentCompletedEventArgs>? TorrentCompleted;

    private ClientEngine Engine => _engine ?? throw new InvalidOperationException("Motor não iniciado");

    public static string IdOf(InfoHashes hashes) => hashes.V1OrV2.ToHex().ToUpperInvariant();

    public async Task StartAsync()
    {
        Directory.CreateDirectory(_cacheDirectory);
        _engine = new ClientEngine(EngineFactory.CreateEngineSettings(_settings.Current, _cacheDirectory));
        _engine.CriticalException += (_, e) => _log.Error(e.Exception, "Erro crítico no motor");

        foreach (var record in _repository.GetAll())
        {
            try
            {
                await RestoreAsync(record);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Falha ao restaurar torrent {Id}", record.Id);
            }
        }
    }

    private async Task RestoreAsync(TorrentRecord record)
    {
        if (!LinkValidator.TryLoadTorrent(record.Metadata, out var torrent))
            throw new TorrentInputException("Metadados salvos corrompidos");

        var manager = await AddToEngineAsync(torrent, record.SavePath, record.SkippedFiles, record.ExtraTrackers);
        var entry = Track(manager, record);
        entry.CompletionHandled = record.CompletedAt is not null;
        if (record.Status == StoredStatus.Active)
            await manager.StartAsync();
    }

    public async Task<TorrentPreview> PrepareMagnetAsync(string magnetLink, CancellationToken cancellationToken)
    {
        if (!LinkValidator.TryParseMagnet(magnetLink, out var magnet))
        {
            _log.Warning("Magnet recusado pela validação (tamanho {Length})", magnetLink?.Length ?? 0);
            throw new TorrentInputException("Link magnet inválido ou malformado.");
        }

        var id = IdOf(magnet.InfoHashes);
        if (Contains(id))
            throw new TorrentInputException("Este torrent já está na lista de transferências.");

        var trackers = magnet.AnnounceUrls.ToList();
        var forMetadata = trackers.Concat(EngineFactory.Profile(_settings.Current).PublicTrackers
            ? EngineFactory.PublicTrackers
            : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lookup = new MagnetLink(magnet.InfoHashes, magnet.Name, forMetadata, magnet.Webseeds, magnet.Size);

        var metadata = await Engine.DownloadMetadataAsync(lookup, cancellationToken);
        if (!LinkValidator.TryLoadTorrent(metadata.Span, out var torrent))
            throw new TorrentInputException("Os metadados recebidos dos peers são inválidos.");

        return new TorrentPreview(torrent, metadata.ToArray(), trackers);
    }

    public TorrentPreview PrepareTorrentFile(string path)
    {
        if (!LinkValidator.TryLoadTorrentFile(path, out var torrent, out var data))
        {
            _log.Warning("Arquivo .torrent recusado pela validação");
            throw new TorrentInputException("Arquivo .torrent inválido, corrompido ou maior que 10 MB.");
        }
        if (Contains(IdOf(torrent.InfoHashes)))
            throw new TorrentInputException("Este torrent já está na lista de transferências.");

        return new TorrentPreview(torrent, data, []);
    }

    public async Task<string> AddAsync(TorrentPreview preview, string saveDirectory, IReadOnlySet<int> selectedFiles)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(selectedFiles);

        if (!PathGuard.IsValidSaveDirectory(saveDirectory))
            throw new TorrentInputException("Pasta de destino inválida.");
        if (selectedFiles.Count == 0)
            throw new TorrentInputException("Selecione pelo menos um arquivo.");
        if (Contains(preview.Id))
            throw new TorrentInputException("Este torrent já está na lista de transferências.");

        saveDirectory = Path.GetFullPath(saveDirectory);
        EnsureSafePaths(preview.Torrent, saveDirectory);
        Directory.CreateDirectory(saveDirectory);

        var skipped = preview.Files.Select(f => f.Index).Where(i => !selectedFiles.Contains(i)).ToHashSet();
        var selectedSize = preview.Files.Where(f => !skipped.Contains(f.Index)).Sum(f => f.Length);
        EnsureFreeSpace(saveDirectory, selectedSize);

        var manager = await AddToEngineAsync(preview.Torrent, saveDirectory, skipped, preview.ExtraTrackers);

        var record = new TorrentRecord(
            preview.Id, preview.Name, saveDirectory, preview.Metadata, preview.ExtraTrackers, skipped,
            StoredStatus.Active, preview.TotalSize, selectedSize, DateTimeOffset.Now, null);
        _repository.Upsert(record);
        Track(manager, record);

        await manager.StartAsync();
        _log.Information("Torrent adicionado {Id} ({Files} de {Total} arquivos)",
            record.Id, selectedFiles.Count, preview.Files.Count);
        return record.Id;
    }

    private async Task<TorrentManager> AddToEngineAsync(
        Torrent torrent, string saveDirectory, IReadOnlySet<int> skipped, IEnumerable<string> extraTrackers)
    {
        EnsureSafePaths(torrent, saveDirectory);
        var settings = EngineFactory.CreateTorrentSettings(_settings.Current, torrent.IsPrivate);
        var manager = await Engine.AddAsync(torrent, saveDirectory, settings);

        // Segunda checagem: o caminho final calculado pelo motor tem de ficar dentro da pasta.
        var root = saveDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (manager.Files.Any(f => !Path.GetFullPath(f.FullPath).StartsWith(root, StringComparison.OrdinalIgnoreCase)))
        {
            await Engine.RemoveAsync(manager, RemoveMode.CacheDataOnly);
            throw new TorrentInputException("O torrent contém caminhos fora da pasta de download.");
        }

        for (var i = 0; i < manager.Files.Count; i++)
        {
            if (skipped.Contains(i))
                await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
        }

        await ConfigureTrackersAsync(manager, extraTrackers);
        return manager;
    }

    private async Task ConfigureTrackersAsync(TorrentManager manager, IEnumerable<string> extraTrackers)
    {
        var existing = manager.TrackerManager.Tiers.SelectMany(t => t.Trackers).ToList();

        // Descarta trackers com esquema estranho (file://, ftp://...).
        foreach (var tracker in existing.Where(t => !LinkValidator.IsAllowedTracker(t.Uri.ToString())))
            await manager.TrackerManager.RemoveTrackerAsync(tracker);

        var known = existing.Select(t => t.Uri.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wanted = LinkValidator.FilterTrackers(extraTrackers).ToList();
        if (!manager.Torrent!.IsPrivate && EngineFactory.Profile(_settings.Current).PublicTrackers)
            wanted.AddRange(EngineFactory.PublicTrackers);

        foreach (var url in wanted.Where(known.Add))
        {
            try
            {
                await manager.TrackerManager.AddTrackerAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Tracker ignorado");
            }
        }
    }

    private static void EnsureSafePaths(Torrent torrent, string saveDirectory)
    {
        try
        {
            PathGuard.SafePath(saveDirectory, torrent.Name);
            foreach (var f in torrent.Files)
            {
                PathGuard.SafePath(saveDirectory, f.Path);
                PathGuard.SafePath(saveDirectory, Path.Combine(torrent.Name, f.Path));
            }
        }
        catch (SecurityException ex)
        {
            Log.Warning("Torrent recusado: {Reason}", ex.Message);
            throw new TorrentInputException($"Torrent recusado por segurança: {ex.Message}.", ex);
        }
    }

    private static void EnsureFreeSpace(string directory, long required)
    {
        try
        {
            var root = Path.GetPathRoot(directory);
            if (string.IsNullOrEmpty(root))
                return;
            var free = new DriveInfo(root).AvailableFreeSpace;
            if (free < required)
                throw new TorrentInputException(
                    $"Espaço insuficiente no disco {root} ({Format.Bytes(free)} livres, {Format.Bytes(required)} necessários).");
        }
        catch (IOException)
        {
            // Unidade de rede/indisponível: segue e deixa o motor reportar erro de escrita.
        }
    }

    private Entry Track(TorrentManager manager, TorrentRecord record)
    {
        var entry = new Entry(manager, record);
        manager.TorrentStateChanged += async (_, e) =>
        {
            if (PolicyEngine.IsCompletion(e.OldState, e.NewState))
                await HandleCompletionAsync(entry);
            if (e.NewState == TorrentState.Error)
                _log.Error(manager.Error?.Exception, "Torrent {Id} entrou em erro", entry.Record.Id);
        };
        lock (_gate)
            _entries[record.Id] = entry;
        return entry;
    }

    private async Task HandleCompletionAsync(Entry entry)
    {
        lock (entry)
        {
            if (entry.CompletionHandled)
                return;
            entry.CompletionHandled = true;
        }

        try
        {
            // Primeiro marca e avisa; parar pode demorar (anúncio "stopped" aos trackers).
            var status = _settings.Current.StopSeedingOnComplete ? StoredStatus.Completed : StoredStatus.Active;
            entry.Record = entry.Record with { Status = status, CompletedAt = DateTimeOffset.Now };
            _repository.SetStatus(entry.Record.Id, status, entry.Record.CompletedAt);

            foreach (var (file, index) in entry.Manager.Files.Select((f, i) => (f, i)))
            {
                if (!entry.Record.SkippedFiles.Contains(index))
                    FileRiskChecker.WriteMarkOfTheWeb(file.FullPath);
            }

            _log.Information("Torrent concluído {Id}", entry.Record.Id);
            TorrentCompleted?.Invoke(this, new TorrentCompletedEventArgs(entry.Record.Id, entry.Record.Name, entry.Record.SavePath));

            await _policy.OnCompletedAsync(new ManagerControl(entry.Manager));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Falha ao finalizar torrent {Id}", entry.Record.Id);
        }
    }

    public async Task PauseAsync(string id)
    {
        if (Find(id) is not { } entry || entry.Record.Status == StoredStatus.Completed)
            return;
        entry.Record = entry.Record with { Status = StoredStatus.Paused };
        _repository.SetStatus(id, StoredStatus.Paused);
        if (entry.Manager.State is not (TorrentState.Stopped or TorrentState.Stopping))
            await entry.Manager.StopAsync(TimeSpan.FromSeconds(10));
    }

    public async Task ResumeAsync(string id)
    {
        if (Find(id) is not { } entry || entry.Record.Status == StoredStatus.Completed)
            return;
        entry.Record = entry.Record with { Status = StoredStatus.Active };
        _repository.SetStatus(id, StoredStatus.Active);
        if (entry.Manager.State is TorrentState.Stopped or TorrentState.Error or TorrentState.Paused)
            await entry.Manager.StartAsync();
    }

    public async Task RemoveAsync(string id, bool deleteFiles)
    {
        if (Find(id) is not { } entry)
            return;
        lock (_gate)
            _entries.Remove(id);

        if (entry.Manager.State is not (TorrentState.Stopped or TorrentState.Stopping or TorrentState.Error))
            await entry.Manager.StopAsync(TimeSpan.FromSeconds(10));
        await Engine.RemoveAsync(entry.Manager,
            deleteFiles ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly);
        _repository.Delete(id);
        _log.Information("Torrent removido {Id} (apagar arquivos: {Delete})", id, deleteFiles);
    }

    public async Task PauseAllAsync()
    {
        foreach (var id in Ids())
            await PauseAsync(id);
    }

    public async Task ResumeAllAsync()
    {
        foreach (var id in Ids())
            await ResumeAsync(id);
    }

    public async Task SetTurboAsync(bool enabled)
    {
        _settings.Save(_settings.Current with { TurboMode = enabled });
        await ApplySettingsAsync();
        _log.Information("Modo Turbo {State}", enabled ? "ligado" : "desligado");
    }

    public async Task ApplySettingsAsync()
    {
        var s = _settings.Current;
        await Engine.UpdateSettingsAsync(EngineFactory.CreateEngineSettings(s, _cacheDirectory));
        foreach (var entry in AllEntries())
        {
            var isPrivate = entry.Manager.Torrent?.IsPrivate ?? false;
            await entry.Manager.UpdateSettingsAsync(EngineFactory.CreateTorrentSettings(s, isPrivate));
            await ConfigureTrackersAsync(entry.Manager, entry.Record.ExtraTrackers);
        }
    }

    public bool Contains(string id)
    {
        lock (_gate)
            return _entries.ContainsKey(id);
    }

    public IReadOnlyList<TorrentSnapshot> GetSnapshots()
    {
        var now = DateTime.UtcNow;
        return AllEntries().Select(e => BuildSnapshot(e, now)).ToList();
    }

    private TorrentSnapshot BuildSnapshot(Entry e, DateTime now)
    {
        var m = e.Manager;
        var r = e.Record;

        if (!e.CompletionHandled && PolicyEngine.IsPartialCompletion(m.State, m.PartialProgress))
            _ = HandleCompletionAsync(e);

        if (m.State is TorrentState.Downloading or TorrentState.Seeding && now - e.LastScrape > ScrapeInterval)
        {
            e.LastScrape = now;
            _ = ScrapeAsync(m);
        }

        var completed = r.Status == StoredStatus.Completed;
        var progress = completed ? 100.0 : Math.Clamp(m.PartialProgress, 0, 100);
        var downloaded = (long)(r.SelectedSize * progress / 100.0);
        var rate = m.Monitor.DownloadRate;
        var status = MapStatus(m, r);
        TimeSpan? eta = status == TorrentStatus.Downloading && rate > 0
            ? TimeSpan.FromSeconds((r.SelectedSize - downloaded) / (double)rate)
            : null;

        var (seedsTotal, leechsTotal) = ScrapeTotals(m);
        var risky = m.Files.Select((f, i) => (f, i))
            .Any(x => !r.SkippedFiles.Contains(x.i) && FileRiskChecker.IsDangerous(x.f.Path));

        return new TorrentSnapshot(
            r.Id, r.Name, status, progress, r.SelectedSize, r.TotalSize, downloaded,
            rate, m.Monitor.UploadRate, eta,
            m.Peers.Seeds, m.Peers.Leechs, seedsTotal, leechsTotal,
            m.ContainingDirectory, risky,
            m.State == TorrentState.Error ? m.Error?.Exception?.Message ?? "Erro de disco" : null,
            r.AddedAt);
    }

    private static TorrentStatus MapStatus(TorrentManager m, TorrentRecord r)
    {
        if (r.Status == StoredStatus.Completed)
            return TorrentStatus.Completed;
        return m.State switch
        {
            TorrentState.Metadata => TorrentStatus.FetchingMetadata,
            TorrentState.Hashing or TorrentState.HashingPaused or TorrentState.FetchingHashes => TorrentStatus.Checking,
            TorrentState.Downloading when m.OpenConnections == 0 && m.Monitor.DownloadRate == 0 => TorrentStatus.Stalled,
            TorrentState.Downloading => TorrentStatus.Downloading,
            TorrentState.Seeding => TorrentStatus.Seeding,
            TorrentState.Paused or TorrentState.Stopped => TorrentStatus.Paused,
            TorrentState.Stopping => TorrentStatus.Stopping,
            TorrentState.Error => TorrentStatus.Error,
            _ => TorrentStatus.Starting,
        };
    }

    /// <summary>Seeds/leechers totais na rede, do scrape do tracker (-1 = desconhecido).</summary>
    private static (int Seeds, int Leechs) ScrapeTotals(TorrentManager m)
    {
        int seeds = -1, leechs = -1;
        foreach (var info in m.TrackerManager.Tiers.SelectMany(t => t.ScrapeInfo.Values))
        {
            seeds = Math.Max(seeds, info.Complete);
            leechs = Math.Max(leechs, info.Incomplete);
        }
        return (seeds, leechs);
    }

    private async Task ScrapeAsync(TorrentManager m)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await m.TrackerManager.ScrapeAsync(cts.Token);
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Scrape falhou");
        }
    }

    public IReadOnlyList<FileSnapshot> GetFiles(string id)
    {
        if (Find(id) is not { } entry)
            return [];
        var completed = entry.Record.Status == StoredStatus.Completed;
        return entry.Manager.Files.Select((f, i) =>
        {
            var selected = !entry.Record.SkippedFiles.Contains(i);
            var progress = completed && selected ? 100.0 : f.BitField.PercentComplete;
            return new FileSnapshot(i, f.Path, f.Length, progress, selected, FileRiskChecker.GetRisk(f.Path));
        }).ToList();
    }

    public GlobalStats GetGlobalStats()
    {
        var entries = AllEntries();
        var engine = Engine;
        return new GlobalStats(
            engine.TotalDownloadRate,
            engine.TotalUploadRate,
            entries.Count(e => e.Manager.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Metadata),
            entries.Count,
            _settings.Current.TurboMode,
            _settings.Current.ListenPort,
            engine.Dht.State.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_engine is null)
            return;
        try
        {
            await _engine.StopAllAsync(TimeSpan.FromSeconds(8));
            // Desliga o encaminhamento para remover o mapeamento UPnP/NAT-PMP do roteador.
            var s = _settings.Current with { TurboMode = false, AllowPortForwarding = false };
            await _engine.UpdateSettingsAsync(EngineFactory.CreateEngineSettings(s, _cacheDirectory));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Erro ao encerrar o motor");
        }
        finally
        {
            _engine.Dispose();
            _engine = null;
        }
    }

    private Entry? Find(string id)
    {
        lock (_gate)
            return _entries.GetValueOrDefault(id);
    }

    private List<Entry> AllEntries()
    {
        lock (_gate)
            return [.. _entries.Values];
    }

    private List<string> Ids()
    {
        lock (_gate)
            return [.. _entries.Keys];
    }

    private sealed class Entry(TorrentManager manager, TorrentRecord record)
    {
        public TorrentManager Manager { get; } = manager;
        public TorrentRecord Record { get; set; } = record;
        public bool CompletionHandled { get; set; }
        public DateTime LastScrape { get; set; } = DateTime.MinValue;
    }

    private sealed class ManagerControl(TorrentManager manager) : ITorrentControl
    {
        public Task StopAsync() => manager.StopAsync(TimeSpan.FromSeconds(10));
    }
}
