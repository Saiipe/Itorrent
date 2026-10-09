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

    /// <summary>Lista de arquivos de um torrent já adicionado (para escolher de novo).</summary>
    TorrentPreview GetPreview(string id);

    /// <summary>Índices dos arquivos marcados hoje.</summary>
    IReadOnlySet<int> GetSelectedFiles(string id);

    /// <summary>
    /// Troca os arquivos marcados e baixa de novo: confere o que já está no disco
    /// (o que foi apagado volta a ser baixado) e reinicia, mesmo se estava concluído.
    /// </summary>
    Task RedownloadAsync(string id, IReadOnlySet<int> selectedFiles);

    /// <summary>
    /// Marca ou desmarca um arquivo de um torrent da lista. Marcar começa a baixá-lo na hora
    /// (mesmo se o torrent estava concluído), sem reverificar o que já foi baixado.
    /// </summary>
    Task SetFileSelectedAsync(string id, int fileIndex, bool selected);

    /// <summary>
    /// Apaga um arquivo do torrent do disco (pela função <c>FileDeleter</c>, que no Windows manda
    /// para a Lixeira) e para de baixá-lo. Confere depois se o arquivo realmente saiu do disco.
    /// </summary>
    Task DeleteFileAsync(string id, int fileIndex);

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
