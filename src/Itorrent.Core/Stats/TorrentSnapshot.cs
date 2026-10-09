using Itorrent.Core.Security;

namespace Itorrent.Core.Stats;

public enum TorrentStatus
{
    Starting,
    FetchingMetadata,
    Checking,
    Downloading,
    Stalled,
    Seeding,
    Paused,
    Completed,
    Stopping,
    Error,
}

/// <summary>
/// Foto imutável de um torrent, entregue à interface uma vez por segundo.
/// </summary>
public sealed record TorrentSnapshot(
    string Id,
    string Name,
    TorrentStatus Status,
    double Progress,
    long SelectedSize,
    long TotalSize,
    long Downloaded,
    long DownloadRate,
    long UploadRate,
    TimeSpan? Eta,
    int SeedsConnected,
    int LeechsConnected,
    int SeedsTotal,
    int LeechsTotal,
    string SavePath,
    bool HasRiskyFiles,
    string? ErrorMessage,
    DateTimeOffset AddedAt);

public sealed record FileSnapshot(
    int Index,
    string Path,
    long Length,
    double Progress,
    bool Selected,
    FileRisk Risk,
    bool ExistsOnDisk = false,
    bool Deleted = false);

public sealed record GlobalStats(
    long DownloadRate,
    long UploadRate,
    int ActiveTorrents,
    int TotalTorrents,
    bool TurboMode,
    int ListenPort,
    string DhtStatus);
