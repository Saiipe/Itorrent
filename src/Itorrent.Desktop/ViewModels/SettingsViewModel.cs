using Itorrent.Core.Localization;
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
        Language = Strings.Normalize(s.Language) is { Length: > 0 } lang ? lang : Strings.Language;
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
                Error = Strings.T("Settings.AssociateFailed", ex.Message);
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

    /// <summary>"pt-BR" ou "en".</summary>
    public string Language { get; set; }

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
            ? Strings.T("Settings.Associated")
            : Strings.T("Settings.NotAssociated");

    /// <returns>true se as configurações foram salvas.</returns>
    public bool TrySave()
    {
        if (!PathGuard.IsValidSaveDirectory(DefaultSavePath))
            return Fail(Strings.T("Settings.ErrFolder"));
        if (!TryInt(ListenPort, 1024, 65535, out var port))
            return Fail(Strings.T("Settings.ErrPort"));
        if (!TryInt(MaxConnectionsPerTorrent, 10, 1000, out var conns))
            return Fail(Strings.T("Settings.ErrConns"));
        if (!TryInt(TurboConnectionsPerTorrent, 200, 500, out var turboConns))
            return Fail(Strings.T("Settings.ErrTurboConns"));
        if (!TryInt(MaxDownloadKBps, 0, 10_000_000, out var down) || !TryInt(MaxUploadKBps, 0, 10_000_000, out var up))
            return Fail(Strings.T("Settings.ErrSpeed"));

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
            Language = Language,
        });
        Strings.SetLanguage(Language);
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
