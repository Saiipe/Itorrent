using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Desktop.Assets;

namespace Itorrent.Desktop.ViewModels;

public sealed class TorrentItemViewModel(string id) : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private TorrentSnapshot? _s;

    public string Id { get; } = id;
    public TorrentSnapshot? Snapshot => _s;

    public string Name => _s?.Name ?? "";
    public TorrentStatus Status => _s?.Status ?? TorrentStatus.Starting;
    public string SizeText => _s is null ? "" : Format.Bytes(_s.SelectedSize);
    public double Progress => _s?.Progress ?? 0;
    public string ProgressText => string.Create(PtBr, $"{Progress:0.0}%");
    public string StatusText => StatusLabel(Status);
    public ImageSource StatusIcon => PixelIcons.Get(StatusIconName(Status));
    public string DownText => _s is null || _s.DownloadRate <= 0 ? "" : Format.Rate(_s.DownloadRate);
    public string UpText => _s is null || _s.UploadRate <= 0 ? "" : Format.Rate(_s.UploadRate);
    public string EtaText => Status == TorrentStatus.Downloading ? Format.Eta(_s?.Eta) : "";
    public string SeedsText => _s is null ? "" : Format.Peers(_s.SeedsConnected, _s.SeedsTotal);
    public string PeersText => _s is null ? "" : Format.Peers(_s.LeechsConnected, _s.LeechsTotal);
    public string DownloadedText => _s is null ? "" : $"{Format.Bytes(_s.Downloaded)} de {Format.Bytes(_s.SelectedSize)}";
    public string AddedText => _s?.AddedAt.ToString("dd/MM/yyyy HH:mm", PtBr) ?? "";
    public string SavePath => _s?.SavePath ?? "";
    public bool HasRiskyFiles => _s?.HasRiskyFiles ?? false;
    public string? ErrorMessage => _s?.ErrorMessage;

    public bool CanPause => Status is TorrentStatus.Downloading or TorrentStatus.Stalled or TorrentStatus.Starting
        or TorrentStatus.FetchingMetadata or TorrentStatus.Checking or TorrentStatus.Seeding;
    public bool CanResume => Status is TorrentStatus.Paused or TorrentStatus.Error;

    public void Update(TorrentSnapshot s)
    {
        if (s == _s)
            return;
        _s = s;
        OnPropertyChanged(string.Empty);
    }

    public static string StatusLabel(TorrentStatus s) => s switch
    {
        TorrentStatus.Starting => "Iniciando",
        TorrentStatus.FetchingMetadata => "Obtendo metadados",
        TorrentStatus.Checking => "Verificando",
        TorrentStatus.Downloading => "Baixando",
        TorrentStatus.Stalled => "Procurando peers",
        TorrentStatus.Seeding => "Semeando",
        TorrentStatus.Paused => "Pausado",
        TorrentStatus.Completed => "Concluído",
        TorrentStatus.Stopping => "Parando",
        TorrentStatus.Error => "Erro",
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
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public FileSnapshot File { get; private set; } = f;

    public string Path => File.Path;
    public string SizeText => Format.Bytes(File.Length);
    public double Progress => File.Selected ? File.Progress : 0;
    public string ProgressText => File.Selected ? string.Create(PtBr, $"{File.Progress:0.0}%") : "ignorado";
    public ImageSource Icon => PixelIcons.Get(File.Risk != FileRisk.None ? "Warning" : "File");
    public string RiskText => File.Risk switch
    {
        FileRisk.DoubleExtension => "Extensão dupla!",
        FileRisk.Executable => "Executável",
        _ => "",
    };

    public void Update(FileSnapshot f)
    {
        if (f == File)
            return;
        File = f;
        OnPropertyChanged(string.Empty);
    }
}
