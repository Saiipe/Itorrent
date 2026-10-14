using System.Net;
using System.Net.NetworkInformation;
using Itorrent.Core.Localization;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;
using MonoTorrent.Client;
using MonoTorrent.Dht;

namespace Itorrent.Core.Engine;

/// <summary>
/// Vigia da conexão: percebe quando a rede cai e, quando ela volta, reconecta sozinho
/// (anuncia de novo aos trackers e à DHT, reinicia DHT/UPnP se travaram e retoma torrents com erro).
/// Também faz o teste de conexão pedido pelo usuário.
/// </summary>
public sealed partial class TorrentService
{
    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NoPeersLimit = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan AutoReconnectCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DhtStartupGrace = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AnnounceTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _reconnectGate = new(1, 1);
    private Timer? _watchdog;
    private int _watching;
    private volatile ConnectionStatus _connection = ConnectionStatus.Online;
    private long _networkChangedTicks;
    private DateTime _lastReconnect = DateTime.MinValue;
    private DateTime _noPeersSince = DateTime.MaxValue;
    private DateTime? _dhtStartedAt;

    private void StartWatchdog()
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        _watchdog = new Timer(_ => _ = WatchAsync(), null, WatchInterval, WatchInterval);
    }

    private void StopWatchdog()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        _watchdog?.Dispose();
        _watchdog = null;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) =>
        Interlocked.Exchange(ref _networkChangedTicks, DateTime.UtcNow.Ticks);

    /// <summary>
    /// A cada 15 s: sem rede, marca "offline". Rede voltou ou mudou (Wi-Fi trocado, VPN, IP novo):
    /// reconecta. Downloads ativos sem nenhum peer há 3 min: reconecta também (no máximo a cada 10 min).
    /// </summary>
    private async Task WatchAsync()
    {
        if (Interlocked.Exchange(ref _watching, 1) == 1)
            return;
        try
        {
            if (_engine is null)
                return;
            TrackEngineRunning();
            var now = DateTime.UtcNow;
            if (!HasNetwork())
            {
                if (_connection != ConnectionStatus.Offline)
                    _log.Warning("Conexão de rede caiu; aguardando voltar");
                _connection = ConnectionStatus.Offline;
                _noPeersSince = DateTime.MaxValue;
                return;
            }

            // Rede acabou de mudar: espera assentar (DHCP, DNS) antes de reconectar.
            var changedAt = new DateTime(Interlocked.Read(ref _networkChangedTicks), DateTimeKind.Utc);
            if (now - changedAt < SettleDelay)
                return;

            var cameBack = _connection == ConnectionStatus.Offline;
            if (cameBack || changedAt > _lastReconnect)
            {
                _log.Information(cameBack ? "Rede voltou; reconectando" : "Rede mudou; reconectando");
                await ReconnectAsync(forceRestart: cameBack, CancellationToken.None);
                return;
            }

            var downloading = AllEntries()
                .Where(e => e.Record.Status == StoredStatus.Active
                            && e.Manager.State is TorrentState.Downloading or TorrentState.Metadata)
                .ToList();
            if (downloading.Count == 0 || downloading.Any(e => e.Manager.OpenConnections > 0))
            {
                _noPeersSince = DateTime.MaxValue;
                return;
            }
            if (_noPeersSince == DateTime.MaxValue)
                _noPeersSince = now;
            if (now - _noPeersSince >= NoPeersLimit && now - _lastReconnect >= AutoReconnectCooldown)
            {
                _log.Information("Downloads sem nenhum peer há {Minutes} min; reconectando", (int)(now - _noPeersSince).TotalMinutes);
                await ReconnectAsync(forceRestart: false, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Falha no vigia de conexão");
        }
        finally
        {
            Interlocked.Exchange(ref _watching, 0);
        }
    }

    /// <summary>
    /// O MonoTorrent só abre a porta e liga a DHT quando algum torrent está rodando.
    /// Anota quando isso começou, para dar à DHT um tempo de partida antes de achar que travou.
    /// </summary>
    private void TrackEngineRunning()
    {
        if (!Engine.IsRunning)
            _dhtStartedAt = null;
        else
            _dhtStartedAt ??= DateTime.UtcNow;
    }

    /// <summary>Há um adaptador de rede ligado com gateway (cabo/Wi-Fi conectado de verdade)?</summary>
    private static bool HasNetwork()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable()
                && NetworkInterface.GetAllNetworkInterfaces().Any(n =>
                    n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    && n.GetIPProperties().GatewayAddresses.Any(g =>
                        !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any)));
        }
        catch (NetworkInformationException)
        {
            return true;
        }
    }

    /// <summary>
    /// Reconecta: reinicia DHT e encaminhamento de porta se estiverem travados (ou se a rede voltou),
    /// retoma torrents ativos que deram erro e anuncia de novo aos trackers, DHT e rede local.
    /// </summary>
    private async Task<ReconnectResult> ReconnectAsync(bool forceRestart, CancellationToken cancellationToken)
    {
        await _reconnectGate.WaitAsync(cancellationToken);
        _connection = ConnectionStatus.Reconnecting;
        _lastReconnect = DateTime.UtcNow;
        _noPeersSince = DateTime.MaxValue;
        try
        {
            var engine = Engine;
            var profile = EngineFactory.Profile(_settings.Current);
            TrackEngineRunning();
            // Motor parado (nada baixando): não há DHT nem porta para consertar.
            var dhtStuck = engine.IsRunning
                && DateTime.UtcNow - _dhtStartedAt > DhtStartupGrace
                && (engine.Dht.State != DhtState.Ready || engine.Dht.NodeCount == 0);
            var restartDht = profile.Dht && engine.IsRunning && (forceRestart || dhtStuck);
            // Sem mapeamento costuma ser roteador sem UPnP: só refaz quando a rede mudou de verdade.
            var restartForwarding = profile.PortForwarding && engine.IsRunning && forceRestart;

            if (restartDht || restartForwarding)
            {
                // Desliga e liga de novo: o motor recria a DHT e redescobre o roteador.
                var on = EngineFactory.CreateEngineSettings(_settings.Current, _cacheDirectory);
                var off = new EngineSettingsBuilder(on)
                {
                    DhtEndPoint = restartDht ? null : on.DhtEndPoint,
                    AllowPortForwarding = !restartForwarding && on.AllowPortForwarding,
                }.ToSettings();
                await engine.UpdateSettingsAsync(off);
                await engine.UpdateSettingsAsync(on);
                if (restartDht)
                    _dhtStartedAt = DateTime.UtcNow;
                _log.Information("Reconexão: DHT reiniciada {Dht}, encaminhamento reiniciado {Forwarding}", restartDht, restartForwarding);
            }

            var resumed = 0;
            foreach (var e in AllEntries().Where(e => e.Record.Status == StoredStatus.Active && e.Manager.State == TorrentState.Error))
            {
                try
                {
                    await e.Manager.StartAsync();
                    resumed++;
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Reconexão: não foi possível retomar {Id}", e.Record.Id);
                }
            }

            var running = AllEntries()
                .Select(e => e.Manager)
                .Where(m => m.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Metadata)
                .ToList();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await Task.WhenAny(
                Task.WhenAll(running.Select(m => AnnounceEverywhereAsync(m, timeout.Token))),
                Task.Delay(AnnounceTimeout, cancellationToken));
            await timeout.CancelAsync();

            var tiers = running.SelectMany(m => m.TrackerManager.Tiers).ToList();
            var result = new ReconnectResult(restartDht, restartForwarding, resumed, running.Count,
                tiers.Count(t => t.LastAnnounceSucceeded), tiers.Count);
            _log.Information("Reconexão concluída: {Running} torrent(s) anunciado(s), trackers {Ok}/{Total}, {Resumed} retomado(s)",
                result.Announced, result.TrackersOk, result.TrackersTotal, resumed);
            return result;
        }
        finally
        {
            _connection = HasNetwork() ? ConnectionStatus.Online : ConnectionStatus.Offline;
            _reconnectGate.Release();
        }
    }

    public async Task ReannounceAsync(string id)
    {
        if (Find(id) is not { } entry || entry.Record.Status != StoredStatus.Active)
            return;
        var manager = entry.Manager;
        _log.Information("Procurando peers de novo para {Id}", id);
        if (manager.State is TorrentState.Error or TorrentState.Stopped)
        {
            await manager.StartAsync();
            return;
        }
        using var timeout = new CancellationTokenSource(AnnounceTimeout);
        await AnnounceEverywhereAsync(manager, timeout.Token);
    }

    private async Task AnnounceEverywhereAsync(TorrentManager manager, CancellationToken cancellationToken)
    {
        try
        {
            await manager.TrackerManager.AnnounceAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Anúncio aos trackers falhou");
        }
        try
        {
            if (manager.CanUseDht)
                await manager.DhtAnnounceAsync();
            if (manager.CanUseLocalPeerDiscovery)
                await manager.LocalPeerAnnounceAsync();
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Anúncio DHT/rede local falhou");
        }
    }

    public async Task<ConnectionReport> TestConnectionAsync(CancellationToken cancellationToken)
    {
        _log.Information("Teste de conexão iniciado");
        var checks = new List<ConnectionCheck>();

        if (!HasNetwork())
        {
            _connection = ConnectionStatus.Offline;
            checks.Add(new(Strings.T("Conn.Network"), CheckResult.Failed, Strings.T("Conn.NetworkFail")));
            foreach (var name in new[] { "Conn.Internet", "Conn.Port", "Conn.Router", "Conn.Dht", "Conn.Trackers" })
                checks.Add(new(Strings.T(name), CheckResult.Skipped, Strings.T("Conn.SkippedNoNetwork")));
            return Finish(checks);
        }
        checks.Add(new(Strings.T("Conn.Network"), CheckResult.Ok, Strings.T("Conn.NetworkOk")));

        var (resolved, hosts) = await ResolveTrackerHostsAsync(cancellationToken);
        var hasInternet = resolved > 0;
        checks.Add(hasInternet
            ? new(Strings.T("Conn.Internet"), CheckResult.Ok, Strings.T("Conn.InternetOk", resolved, hosts))
            : new(Strings.T("Conn.Internet"), CheckResult.Failed, Strings.T("Conn.InternetFail")));

        // Nada rodando: o motor ainda não abriu a porta nem ligou a DHT.
        var idle = !Engine.IsRunning;
        if (idle)
        {
            checks.Add(new(Strings.T("Conn.Port"), CheckResult.Skipped, Strings.T("Conn.EngineIdle")));
            checks.Add(new(Strings.T("Conn.Router"), CheckResult.Skipped, Strings.T("Conn.EngineIdle")));
        }
        else
        {
            checks.Add(CheckPort());
            // Antes de reconectar: reiniciar o encaminhamento apaga o resultado do roteador.
            checks.Add(CheckRouter());
        }

        if (!hasInternet)
        {
            checks.Add(new(Strings.T("Conn.Dht"), CheckResult.Skipped, Strings.T("Conn.SkippedNoInternet")));
            checks.Add(new(Strings.T("Conn.Trackers"), CheckResult.Skipped, Strings.T("Conn.SkippedNoInternet")));
            return Finish(checks);
        }

        var result = await ReconnectAsync(forceRestart: false, cancellationToken);
        checks.Add(idle && !Engine.IsRunning
            ? new(Strings.T("Conn.Dht"), CheckResult.Skipped, Strings.T("Conn.EngineIdle"))
            : CheckDht(result));
        checks.Add(CheckTrackers(result));
        checks.Add(CheckPeers(result));
        checks.Add(new(Strings.T("Conn.Reconnect"), CheckResult.Ok, DescribeReconnect(result)));
        return Finish(checks);
    }

    private ConnectionReport Finish(List<ConnectionCheck> checks)
    {
        _log.Information("Teste de conexão: {Results}",
            string.Join(", ", checks.Select(c => $"{c.Name}={c.Result}")));
        return new ConnectionReport(checks);
    }

    /// <summary>Internet: resolve o nome dos trackers públicos (o mesmo DNS que o BitTorrent já usa).</summary>
    private static async Task<(int Resolved, int Total)> ResolveTrackerHostsAsync(CancellationToken cancellationToken)
    {
        var hosts = EngineFactory.PublicTrackers
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri.Host : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DnsTimeout);
        var results = await Task.WhenAll(hosts.Select(async host =>
        {
            try
            {
                return (await Dns.GetHostAddressesAsync(host, timeout.Token)).Length > 0;
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or OperationCanceledException)
            {
                return false;
            }
        }));
        cancellationToken.ThrowIfCancellationRequested();
        return (results.Count(r => r), hosts.Count);
    }

    private ConnectionCheck CheckPort()
    {
        var port = _settings.Current.ListenPort;
        bool listening;
        try
        {
            listening = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);
        }
        catch (NetworkInformationException)
        {
            listening = true;
        }
        return listening
            ? new(Strings.T("Conn.Port"), CheckResult.Ok, Strings.T("Conn.PortOk", port))
            : new(Strings.T("Conn.Port"), CheckResult.Failed, Strings.T("Conn.PortFail", port));
    }

    private ConnectionCheck CheckRouter()
    {
        var name = Strings.T("Conn.Router");
        if (!EngineFactory.Profile(_settings.Current).PortForwarding)
            return new(name, CheckResult.Skipped, Strings.T("Conn.RouterOff"));
        var mappings = Engine.PortMappings;
        if (mappings.Created.Count > 0)
            return new(name, CheckResult.Ok, Strings.T("Conn.RouterOk"));
        return mappings.Failed.Count > 0
            ? new(name, CheckResult.Warning, Strings.T("Conn.RouterRefused"))
            : new(name, CheckResult.Warning, Strings.T("Conn.RouterNone"));
    }

    private ConnectionCheck CheckDht(ReconnectResult result)
    {
        var name = Strings.T("Conn.Dht");
        if (!EngineFactory.Profile(_settings.Current).Dht)
            return new(name, CheckResult.Skipped, Strings.T("Conn.DhtOff"));
        if (result.DhtRestarted)
            return new(name, CheckResult.Warning, Strings.T("Conn.DhtRestarted"));
        var dht = Engine.Dht;
        return dht.State == DhtState.Ready
            ? new(name, CheckResult.Ok, Strings.T("Conn.DhtOk", dht.NodeCount))
            : new(name, CheckResult.Warning, Strings.T("Conn.DhtStarting", dht.NodeCount));
    }

    private static ConnectionCheck CheckTrackers(ReconnectResult result)
    {
        var name = Strings.T("Conn.Trackers");
        if (result.Announced == 0)
            return new(name, CheckResult.Skipped, Strings.T("Conn.NoActive"));
        if (result.TrackersTotal == 0)
            return new(name, CheckResult.Warning, Strings.T("Conn.TrackersNone"));
        return result.TrackersOk > 0
            ? new(name, CheckResult.Ok, Strings.T("Conn.TrackersOk", result.TrackersOk, result.TrackersTotal))
            : new(name, CheckResult.Failed, Strings.T("Conn.TrackersFail", result.TrackersTotal));
    }

    private ConnectionCheck CheckPeers(ReconnectResult result)
    {
        var name = Strings.T("Conn.Peers");
        var open = Engine.ConnectionManager.OpenConnections;
        if (open > 0)
            return new(name, CheckResult.Ok, Strings.T("Conn.PeersOk", open));
        return result.Announced == 0
            ? new(name, CheckResult.Skipped, Strings.T("Conn.NoActive"))
            : new(name, CheckResult.Warning, Strings.T("Conn.PeersNone"));
    }

    private static string DescribeReconnect(ReconnectResult r)
    {
        var parts = new List<string>();
        if (r.Announced > 0)
            parts.Add(Strings.T("Conn.DidAnnounce", r.Announced));
        if (r.DhtRestarted)
            parts.Add(Strings.T("Conn.DidDht"));
        if (r.ForwardingRestarted)
            parts.Add(Strings.T("Conn.DidRouter"));
        if (r.Resumed > 0)
            parts.Add(Strings.T("Conn.DidResume", r.Resumed));
        return parts.Count == 0 ? Strings.T("Conn.NothingToDo") : string.Join("; ", parts) + ".";
    }

    private sealed record ReconnectResult(
        bool DhtRestarted, bool ForwardingRestarted, int Resumed, int Announced, int TrackersOk, int TrackersTotal);
}
