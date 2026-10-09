using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using MonoTorrent;

namespace Itorrent.Core.Engine;

/// <summary>
/// Único ponto de contato da interface com o motor. A interface só envia comandos e lê snapshots.
/// </summary>
public interface ITorrentService : IAsyncDisposable
{
    Task StartAsync();

    /// <summary>Valida o magnet e baixa os metadados (lista de arquivos) dos peers.</summary>
    Task<TorrentPreview> PrepareMagnetAsync(string magnetLink, CancellationToken cancellationToken);

    /// <summary>Valida e carrega um arquivo .torrent (máx. 10 MB).</summary>
    TorrentPreview PrepareTorrentFile(string path);

    /// <summary>Adiciona e inicia o download só dos arquivos selecionados.</summary>
    Task<string> AddAsync(TorrentPreview preview, string saveDirectory, IReadOnlySet<int> selectedFiles);

    Task PauseAsync(string id);
    Task ResumeAsync(string id);
    Task RemoveAsync(string id, bool deleteFiles);
    Task PauseAllAsync();
    Task ResumeAllAsync();

    Task SetTurboAsync(bool enabled);
    Task ApplySettingsAsync();

    bool Contains(string id);
    IReadOnlyList<TorrentSnapshot> GetSnapshots();
    IReadOnlyList<FileSnapshot> GetFiles(string id);
    GlobalStats GetGlobalStats();

    event EventHandler<TorrentCompletedEventArgs>? TorrentCompleted;
}

public sealed class TorrentCompletedEventArgs(string id, string name, string savePath) : EventArgs
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string SavePath { get; } = savePath;
}

public sealed record PreviewFile(int Index, string Path, long Length, FileRisk Risk);

/// <summary>
/// Torrent já validado, aguardando a confirmação do usuário (nome, tamanho, arquivos, pasta).
/// </summary>
public sealed class TorrentPreview
{
    internal TorrentPreview(Torrent torrent, byte[] metadata, IReadOnlyList<string> extraTrackers)
    {
        Torrent = torrent;
        Metadata = metadata;
        ExtraTrackers = extraTrackers;
        Files = torrent.Files
            .Select((f, i) => new PreviewFile(i, f.Path, f.Length, FileRiskChecker.GetRisk(f.Path)))
            .ToList();
    }

    internal Torrent Torrent { get; }
    internal byte[] Metadata { get; }
    internal IReadOnlyList<string> ExtraTrackers { get; }

    public string Id => TorrentService.IdOf(Torrent.InfoHashes);
    public string Name => Torrent.Name;
    public long TotalSize => Torrent.Size;
    public bool IsPrivate => Torrent.IsPrivate;
    public string? Comment => Torrent.Comment;
    public IReadOnlyList<PreviewFile> Files { get; }
    public bool HasRiskyFiles => Files.Any(f => f.Risk != FileRisk.None);
}

/// <summary>Entrada recusada pela validação; a mensagem é mostrada ao usuário.</summary>
public class TorrentInputException : Exception
{
    public TorrentInputException() : base("Entrada inválida") { }
    public TorrentInputException(string message) : base(message) { }
    public TorrentInputException(string message, Exception innerException) : base(message, innerException) { }
}
