using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Itorrent.Core.Engine;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;
using Itorrent.Desktop.Platform;
using Serilog;

namespace Itorrent.Desktop.ViewModels;

public enum TorrentFilter
{
    All,
    Downloading,
    Completed,
    Paused,
}

/// <summary>Janelas que o ViewModel principal precisa abrir (implementado pela View).</summary>
public interface IDialogs
{
    string? AskMagnet();
    string? PickTorrentFile();
    void ShowAddTorrent(AddTorrentViewModel vm);
    (bool Confirmed, bool DeleteFiles) ConfirmRemove(string name);
    bool ShowSettings();
    void ShowAbout();
    void ShowError(string message);
}

public sealed class MainViewModel : ObservableObject
{
    private readonly ITorrentService _service;
    private readonly ISettingsService _settings;
    private readonly IDialogs _dialogs;
    private readonly Func<string, string?> _pickFolder;
    private readonly DispatcherTimer _timer;
    private TorrentItemViewModel? _selected;
    private TorrentFilter _filter;
    private GlobalStats? _stats;
    private bool _isTurbo;

    public MainViewModel(ITorrentService service, ISettingsService settings, IDialogs dialogs, Func<string, string?> pickFolder)
    {
        _service = service;
        _settings = settings;
        _dialogs = dialogs;
        _pickFolder = pickFolder;
        _isTurbo = settings.Current.TurboMode;

        TorrentsView = CollectionViewSource.GetDefaultView(Torrents);
        TorrentsView.Filter = o => o is TorrentItemViewModel t && Matches(t, _filter);

        AddFileCommand = new RelayCommand(AddTorrentFile);
        AddMagnetCommand = new RelayCommand(AddMagnet);
        PauseCommand = new AsyncRelayCommand(() => Run(s => _service.PauseAsync(s.Id)), () => Selected?.CanPause == true);
        ResumeCommand = new AsyncRelayCommand(() => Run(s => _service.ResumeAsync(s.Id)), () => Selected?.CanResume == true);
        RemoveCommand = new AsyncRelayCommand(RemoveAsync, () => Selected is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Selected is not null);
        PauseAllCommand = new AsyncRelayCommand(() => _service.PauseAllAsync());
        ResumeAllCommand = new AsyncRelayCommand(() => _service.ResumeAllAsync());
        SettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        AboutCommand = new RelayCommand(_dialogs.ShowAbout);
        FilterCommand = new RelayCommand<TorrentFilter>(f => Filter = f);

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick(),
            Dispatcher.CurrentDispatcher);
    }

    public ObservableCollection<TorrentItemViewModel> Torrents { get; } = [];
    public ICollectionView TorrentsView { get; }
    public ObservableCollection<FileItemViewModel> Files { get; } = [];

    public RelayCommand AddFileCommand { get; }
    public RelayCommand AddMagnetCommand { get; }
    public AsyncRelayCommand PauseCommand { get; }
    public AsyncRelayCommand ResumeCommand { get; }
    public AsyncRelayCommand RemoveCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public AsyncRelayCommand PauseAllCommand { get; }
    public AsyncRelayCommand ResumeAllCommand { get; }
    public AsyncRelayCommand SettingsCommand { get; }
    public RelayCommand AboutCommand { get; }
    public RelayCommand<TorrentFilter> FilterCommand { get; }

    public TorrentItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value))
                return;
            Files.Clear();
            RefreshSelection();
            RefreshCommands();
        }
    }

    public TorrentFilter Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                TorrentsView.Refresh();
                OnPropertyChanged(nameof(FilterTitle));
            }
        }
    }

    public string FilterTitle => Filter switch
    {
        TorrentFilter.Downloading => "Transferências — Baixando",
        TorrentFilter.Completed => "Transferências — Concluídos",
        TorrentFilter.Paused => "Transferências — Pausados",
        _ => "Transferências",
    };

    /// <summary>Botão Modo Turbo: aplica de uma vez todos os ajustes de velocidade.</summary>
    public bool IsTurbo
    {
        get => _isTurbo;
        set
        {
            if (!SetProperty(ref _isTurbo, value))
                return;
            OnPropertyChanged(nameof(TurboLabel));
            _ = ApplyTurboAsync(value);
        }
    }

    public string TurboLabel => IsTurbo ? "TURBO ON" : "Turbo";

    public string DownText => "Down: " + Format.Rate(_stats?.DownloadRate ?? 0);
    public string UpText => "Up: " + Format.Rate(_stats?.UploadRate ?? 0);
    public string CountText => _stats is null ? "" : $"Torrents: {_stats.ActiveTorrents} ativos / {_stats.TotalTorrents}";
    public string PortText => _stats is null ? "" : $"Porta: {_stats.ListenPort}";
    public string DhtText => _stats is null ? "" : "DHT: " + DhtLabel(_stats.DhtStatus);
    public string TurboStatusText => IsTurbo ? "MODO TURBO" : "Normal";

    public string DetailsTitle => Selected is null ? "Arquivos" : $"Arquivos — {Selected.Name}";

    public void Start()
    {
        Tick();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Tick()
    {
        try
        {
            var snapshots = _service.GetSnapshots();
            var byId = Torrents.ToDictionary(t => t.Id);
            foreach (var s in snapshots)
            {
                if (!byId.Remove(s.Id, out var item))
                {
                    item = new TorrentItemViewModel(s.Id);
                    Torrents.Add(item);
                }
                item.Update(s);
            }
            foreach (var gone in byId.Values)
                Torrents.Remove(gone);

            if (Filter != TorrentFilter.All)
                TorrentsView.Refresh();

            _stats = _service.GetGlobalStats();
            OnPropertyChanged(nameof(DownText));
            OnPropertyChanged(nameof(UpText));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(PortText));
            OnPropertyChanged(nameof(DhtText));
            OnPropertyChanged(nameof(TurboStatusText));

            RefreshSelection();
            RefreshCommands();

            if (_settings.Current.PreventSleepWhileDownloading)
                KeepAwake.Set(snapshots.Any(s => s.Status is TorrentStatus.Downloading or TorrentStatus.FetchingMetadata));
            else
                KeepAwake.Set(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao atualizar a interface");
        }
    }

    private void RefreshSelection()
    {
        OnPropertyChanged(nameof(DetailsTitle));
        if (Selected is null)
            return;
        var files = _service.GetFiles(Selected.Id);
        if (Files.Count != files.Count)
        {
            Files.Clear();
            foreach (var f in files)
                Files.Add(new FileItemViewModel(f));
        }
        else
        {
            for (var i = 0; i < files.Count; i++)
                Files[i].Update(files[i]);
        }
    }

    private void RefreshCommands()
    {
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    private static bool Matches(TorrentItemViewModel t, TorrentFilter f) => f switch
    {
        TorrentFilter.Downloading => t.Status is TorrentStatus.Downloading or TorrentStatus.Stalled
            or TorrentStatus.FetchingMetadata or TorrentStatus.Checking or TorrentStatus.Starting,
        TorrentFilter.Completed => t.Status is TorrentStatus.Completed or TorrentStatus.Seeding,
        TorrentFilter.Paused => t.Status is TorrentStatus.Paused or TorrentStatus.Error,
        _ => true,
    };

    private static string DhtLabel(string state) => state switch
    {
        "Ready" => "pronto",
        "Initialising" => "iniciando",
        "NotReady" => "desligado",
        _ => state.ToLowerInvariant(),
    };

    // ===== Entrada externa (navegador, pipe, arrastar e soltar, linha de comando) =====

    /// <summary>Recebe um magnet ou caminho de .torrent de fonte externa. Tudo é validado no Core.</summary>
    public void HandleExternalInput(string input)
    {
        input = input.Trim().Trim('"');
        if (input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            OpenMagnet(input);
        else if (input.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(input))
            OpenTorrentFile(input);
        else
        {
            Log.Warning("Entrada externa recusada");
            _dialogs.ShowError("O Itorrent só abre links magnet e arquivos .torrent.");
        }
    }

    private void AddTorrentFile()
    {
        if (_dialogs.PickTorrentFile() is { } path)
            OpenTorrentFile(path);
    }

    private void AddMagnet()
    {
        if (_dialogs.AskMagnet() is { } link)
            OpenMagnet(link);
    }

    private void OpenTorrentFile(string path)
    {
        try
        {
            var vm = new AddTorrentViewModel(_service, _settings, _pickFolder);
            vm.SetPreview(_service.PrepareTorrentFile(path));
            _dialogs.ShowAddTorrent(vm);
        }
        catch (TorrentInputException ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    private void OpenMagnet(string link)
    {
        if (!LinkValidator.IsValidMagnet(link))
        {
            Log.Warning("Magnet recusado pela validação");
            _dialogs.ShowError("Link magnet inválido ou malformado.");
            return;
        }
        var vm = new AddTorrentViewModel(_service, _settings, _pickFolder);
        _ = vm.LoadMagnetAsync(link);
        _dialogs.ShowAddTorrent(vm);
    }

    // ===== Comandos =====

    private async Task Run(Func<TorrentItemViewModel, Task> action)
    {
        if (Selected is not { } s)
            return;
        try
        {
            await action(s);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Comando falhou");
            _dialogs.ShowError(ex.Message);
        }
        Tick();
    }

    private async Task RemoveAsync()
    {
        if (Selected is not { } s)
            return;
        var (confirmed, deleteFiles) = _dialogs.ConfirmRemove(s.Name);
        if (!confirmed)
            return;
        await Run(t => _service.RemoveAsync(t.Id, deleteFiles));
        Selected = null;
    }

    /// <summary>"Abrir pasta" é o único atalho: o app nunca abre nem executa arquivos baixados.</summary>
    private void OpenFolder()
    {
        if (Selected is not { } s)
            return;
        var dir = Directory.Exists(s.SavePath) ? s.SavePath : Path.GetDirectoryName(s.SavePath);
        if (dir is null || !Directory.Exists(dir))
        {
            _dialogs.ShowError("A pasta ainda não existe.");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { dir }, UseShellExecute = false });
    }

    private async Task ApplyTurboAsync(bool on)
    {
        try
        {
            await _service.SetTurboAsync(on);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao aplicar o Modo Turbo");
            _dialogs.ShowError("Não foi possível aplicar o Modo Turbo: " + ex.Message);
        }
    }

    private async Task OpenSettingsAsync()
    {
        if (!_dialogs.ShowSettings())
            return;
        try
        {
            StartupRegistry.Apply(_settings.Current.StartWithWindows);
            await _service.ApplySettingsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao aplicar configurações");
            _dialogs.ShowError("Algumas configurações não puderam ser aplicadas: " + ex.Message);
        }
        if (_isTurbo != _settings.Current.TurboMode)
        {
            _isTurbo = _settings.Current.TurboMode;
            OnPropertyChanged(nameof(IsTurbo));
            OnPropertyChanged(nameof(TurboLabel));
        }
    }
}
