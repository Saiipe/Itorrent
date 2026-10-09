using Itorrent.Core.Localization;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Desktop.Assets;

namespace Itorrent.Desktop.ViewModels;

public sealed class TorrentItemViewModel(string id) : ObservableObject
{
    private TorrentSnapshot? _s;

    public string Id { get; } = id;
    public TorrentSnapshot? Snapshot => _s;

    public string Name => _s?.Name ?? "";
    public TorrentStatus Status => _s?.Status ?? TorrentStatus.Starting;
    public string SizeText => _s is null ? "" : Format.Bytes(_s.SelectedSize);
    public double Progress => _s?.Progress ?? 0;
    public string ProgressText => string.Create(Strings.Culture, $"{Progress:0.0}%");
    public string StatusText => StatusLabel(Status);
    public ImageSource StatusIcon => PixelIcons.Get(StatusIconName(Status));
    public string DownText => _s is null || _s.DownloadRate <= 0 ? "" : Format.Rate(_s.DownloadRate);
    public string UpText => _s is null || _s.UploadRate <= 0 ? "" : Format.Rate(_s.UploadRate);
    public string EtaText => Status == TorrentStatus.Downloading ? Format.Eta(_s?.Eta) : "";
    public string SeedsText => _s is null ? "" : Format.Peers(_s.SeedsConnected, _s.SeedsTotal);
    public string PeersText => _s is null ? "" : Format.Peers(_s.LeechsConnected, _s.LeechsTotal);
    public string DownloadedText => _s is null ? "" : Strings.T("Item.DownloadedOf", Format.Bytes(_s.Downloaded), Format.Bytes(_s.SelectedSize));
    public string AddedText => _s?.AddedAt.ToString("g", Strings.Culture) ?? "";
    public string SavePath => _s?.SavePath ?? "";
    public bool HasRiskyFiles => _s?.HasRiskyFiles ?? false;

    // Valores "crus" usados para ordenar as colunas (o texto formatado não ordena certo).
    public long SortSize => _s?.SelectedSize ?? 0;
    public long SortDown => _s?.DownloadRate ?? 0;
    public long SortUp => _s?.UploadRate ?? 0;
    public double SortEta => _s?.Eta?.TotalSeconds ?? double.MaxValue;
    public int SortSeeds => _s?.SeedsConnected ?? 0;
    public int SortPeers => _s?.LeechsConnected ?? 0;
    public DateTimeOffset SortAdded => _s?.AddedAt ?? DateTimeOffset.MinValue;
    public string? ErrorMessage => _s?.ErrorMessage;

    public bool CanPause => Status is TorrentStatus.Downloading or TorrentStatus.Stalled or TorrentStatus.Starting
        or TorrentStatus.FetchingMetadata or TorrentStatus.Checking or TorrentStatus.Seeding;
    public bool CanResume => Status is TorrentStatus.Paused or TorrentStatus.Error;

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Update(TorrentSnapshot s)
    {
        if (s == _s)
            return;
        _s = s;
        OnPropertyChanged(string.Empty);
    }

    public static string StatusLabel(TorrentStatus s) => s switch
    {
        TorrentStatus.Starting => Strings.T("Status.Starting"),
        TorrentStatus.FetchingMetadata => Strings.T("Status.FetchingMetadata"),
        TorrentStatus.Checking => Strings.T("Status.Checking"),
        TorrentStatus.Downloading => Strings.T("Status.Downloading"),
        TorrentStatus.Stalled => Strings.T("Status.Stalled"),
        TorrentStatus.Seeding => Strings.T("Status.Seeding"),
        TorrentStatus.Paused => Strings.T("Status.Paused"),
        TorrentStatus.Completed => Strings.T("Status.Completed"),
        TorrentStatus.Stopping => Strings.T("Status.Stopping"),
        TorrentStatus.Error => Strings.T("Status.Error"),
        _ => s.ToString(),
    };

    private static string StatusIconName(TorrentStatus s) => s switch
    {
        TorrentStatus.Downloading or TorrentStatus.Stalled => "Down",
        TorrentStatus.Seeding => "Up",
        TorrentStatus.Paused or TorrentStatus.Stopping => "Pause",
        TorrentStatus.Completed => "Check",
        TorrentStatus.Error => "Remove",
        _ => "Hourglass",
    };
}

public sealed class FileItemViewModel(FileSnapshot f) : ObservableObject
{
    public FileSnapshot File { get; private set; } = f;

    public string Path => File.Path;
    public bool IsSelected => File.Selected;
    public bool IsDeleted => File.Deleted;
    public bool CanDelete => File.ExistsOnDisk;
    public long SortSize => File.Length;
    public string SizeText => Format.Bytes(File.Length);
    public double Progress => File.Selected ? File.Progress : 0;
    public string ProgressText => File.Deleted ? Strings.T("File.Missing") : File.Selected ? string.Create(Strings.Culture, $"{File.Progress:0.0}%") : Strings.T("File.Ignored");
    public ImageSource Icon => PixelIcons.Get(File.Risk != FileRisk.None ? "Warning" : "File");
    public string RiskText => File.Deleted ? Strings.T("File.MissingAlert") : File.Risk switch
    {
        FileRisk.DoubleExtension => Strings.T("File.DoubleExt"),
        FileRisk.Executable => Strings.T("File.Executable"),
        _ => "",
    };

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Update(FileSnapshot f)
    {
        if (f == File)
            return;
        File = f;
        OnPropertyChanged(string.Empty);
    }
}
