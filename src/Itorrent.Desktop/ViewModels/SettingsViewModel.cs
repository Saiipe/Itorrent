using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Itorrent.Core.Security;
using Itorrent.Core.Settings;
using Itorrent.Core.Storage;
using Itorrent.Desktop.Platform;

namespace Itorrent.Desktop.ViewModels;

/// <summary>Cópia editável das configurações; só grava ao clicar em OK.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly Func<string, string?> _pickFolder;
    private string _defaultSavePath;
    private string? _error;
    private string _associationText;

    public SettingsViewModel(ISettingsService settings, Func<string, string?> pickFolder)
    {
        _settings = settings;
        _pickFolder = pickFolder;
        var s = settings.Current;
        _defaultSavePath = s.DefaultSavePath;
        ListenPort = s.ListenPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        StopSeedingOnComplete = s.StopSeedingOnComplete;
        TurboMode = s.TurboMode;
        AllowPortForwarding = s.AllowPortForwarding;
        AllowDht = s.AllowDht;
        AllowPeerExchange = s.AllowPeerExchange;
        AllowLocalPeerDiscovery = s.AllowLocalPeerDiscovery;
        AddPublicTrackers = s.AddPublicTrackers;
        MaxConnectionsPerTorrent = s.MaxConnectionsPerTorrent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        TurboConnectionsPerTorrent = s.TurboConnectionsPerTorrent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MaxDownloadKBps = s.MaxDownloadKBps.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MaxUploadKBps = s.MaxUploadKBps.ToString(System.Globalization.CultureInfo.InvariantCulture);
        StartWithWindows = StartupRegistry.IsEnabled();
        PreventSleepWhileDownloading = s.PreventSleepWhileDownloading;
        NotifyOnComplete = s.NotifyOnComplete;
        MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
        AutoExitIdleMinutes = s.AutoExitIdleMinutes;
        _associationText = AssociationStatus();

        BrowseCommand = new RelayCommand(() =>
        {
            if (_pickFolder(DefaultSavePath) is { } f)
                DefaultSavePath = f;
        });
        AssociateCommand = new RelayCommand(() =>
        {
            try
            {
                ProtocolRegistration.Register();
                AssociationText = AssociationStatus();
            }
            catch (Exception ex)
            {
                Error = "Não foi possível associar: " + ex.Message;
            }
        });
    }

    public RelayCommand BrowseCommand { get; }
    public RelayCommand AssociateCommand { get; }

    public string DefaultSavePath
    {
        get => _defaultSavePath;
        set => SetProperty(ref _defaultSavePath, value);
    }

    public string ListenPort { get; set; }
    public bool StopSeedingOnComplete { get; set; }
    public bool TurboMode { get; set; }
    public bool AllowPortForwarding { get; set; }
    public bool AllowDht { get; set; }
    public bool AllowPeerExchange { get; set; }
    public bool AllowLocalPeerDiscovery { get; set; }
    public bool AddPublicTrackers { get; set; }
    public string MaxConnectionsPerTorrent { get; set; }
    public string TurboConnectionsPerTorrent { get; set; }
    public string MaxDownloadKBps { get; set; }
    public string MaxUploadKBps { get; set; }
    public bool StartWithWindows { get; set; }
    public bool PreventSleepWhileDownloading { get; set; }
    public bool NotifyOnComplete { get; set; }
    public bool MinimizeToTrayOnClose { get; set; }
    public int AutoExitIdleMinutes { get; set; }

    public string AssociationText
    {
        get => _associationText;
        private set => SetProperty(ref _associationText, value);
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    private static string AssociationStatus() =>
        ProtocolRegistration.IsRegistered()
            ? "Links magnet e arquivos .torrent abrem no Itorrent."
            : "Links magnet ainda não abrem no Itorrent.";

    /// <returns>true se as configurações foram salvas.</returns>
    public bool TrySave()
    {
        if (!PathGuard.IsValidSaveDirectory(DefaultSavePath))
            return Fail("Pasta padrão inválida. Use um caminho completo, ex.: C:\\Downloads");
        if (!TryInt(ListenPort, 1024, 65535, out var port))
            return Fail("Porta deve estar entre 1024 e 65535.");
        if (!TryInt(MaxConnectionsPerTorrent, 10, 1000, out var conns))
            return Fail("Conexões por torrent: entre 10 e 1000.");
        if (!TryInt(TurboConnectionsPerTorrent, 200, 500, out var turboConns))
            return Fail("Conexões no Modo Turbo: entre 200 e 500.");
        if (!TryInt(MaxDownloadKBps, 0, 10_000_000, out var down) || !TryInt(MaxUploadKBps, 0, 10_000_000, out var up))
            return Fail("Limites de velocidade devem ser números (0 = sem limite).");

        _settings.Save(_settings.Current with
        {
            DefaultSavePath = DefaultSavePath.Trim(),
            ListenPort = port,
            StopSeedingOnComplete = StopSeedingOnComplete,
            TurboMode = TurboMode,
            AllowPortForwarding = AllowPortForwarding,
            AllowDht = AllowDht,
            AllowPeerExchange = AllowPeerExchange,
            AllowLocalPeerDiscovery = AllowLocalPeerDiscovery,
            AddPublicTrackers = AddPublicTrackers,
            MaxConnectionsPerTorrent = conns,
            TurboConnectionsPerTorrent = turboConns,
            MaxDownloadKBps = down,
            MaxUploadKBps = up,
            StartWithWindows = StartWithWindows,
            PreventSleepWhileDownloading = PreventSleepWhileDownloading,
            NotifyOnComplete = NotifyOnComplete,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            AutoExitIdleMinutes = AutoExitIdleMinutes,
        });
        return true;
    }

    private bool Fail(string message)
    {
        Error = message;
        return false;
    }

    private static bool TryInt(string s, int min, int max, out int value) =>
        int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value)
        && value >= min && value <= max;
}
