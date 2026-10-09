using Itorrent.Core.Localization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Itorrent.Core.Engine;
using Itorrent.Core.Policies;
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
    bool ConfirmDeleteFile(string name);
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
    private DateTimeOffset _idleSince = DateTimeOffset.Now;

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
        ToggleFileCommand = new AsyncRelayCommand<FileItemViewModel>(ToggleFileAsync);
        DeleteFileCommand = new AsyncRelayCommand<FileItemViewModel>(DeleteFileAsync);
        RedownloadCommand = new RelayCommand(Redownload,
            () => Selected is { Status: not (TorrentStatus.FetchingMetadata or TorrentStatus.Stopping) });
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Selected is not null);
        PauseAllCommand = new AsyncRelayCommand(() => _service.PauseAllAsync());
        ResumeAllCommand = new AsyncRelayCommand(() => _service.ResumeAllAsync());
        SettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        AboutCommand = new RelayCommand(_dialogs.ShowAbout);
        FilterCommand = new RelayCommand<TorrentFilter>(f => Filter = f);

        // Troca de idioma: todos os textos calculados aqui são refeitos na hora.
        Strings.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(string.Empty);
            foreach (var t in Torrents)
                t.Refresh();
            foreach (var f in Files)
                f.Refresh();
        };

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick(),
            Dispatcher.CurrentDispatcher);
    }

    /// <summary>O app pode se encerrar sozinho agora? (janela escondida/minimizada, sem diálogo aberto)</summary>
    public Func<bool> CanAutoExit { get; set; } = () => false;

    /// <summary>Ficou o tempo configurado em segundo plano sem baixar nada.</summary>
    public event EventHandler? AutoExitRequested;

    public ObservableCollection<TorrentItemViewModel> Torrents { get; } = [];
    public ICollectionView TorrentsView { get; }
    public ObservableCollection<FileItemViewModel> Files { get; } = [];

    public RelayCommand AddFileCommand { get; }
    public RelayCommand AddMagnetCommand { get; }
    public AsyncRelayCommand PauseCommand { get; }
    public AsyncRelayCommand ResumeCommand { get; }
    public AsyncRelayCommand RemoveCommand { get; }
    public RelayCommand RedownloadCommand { get; }
    public AsyncRelayCommand<FileItemViewModel> ToggleFileCommand { get; }
    public AsyncRelayCommand<FileItemViewModel> DeleteFileCommand { get; }
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
        TorrentFilter.Downloading => Strings.T("Main.TransfersDownloading"),
        TorrentFilter.Completed => Strings.T("Main.TransfersCompleted"),
        TorrentFilter.Paused => Strings.T("Main.TransfersPaused"),
        _ => Strings.T("Main.Transfers"),
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
    public string CountText => _stats is null ? "" : Strings.T("StatusBar.Torrents", _stats.ActiveTorrents, _stats.TotalTorrents);
    public string PortText => _stats is null ? "" : Strings.T("StatusBar.Port", _stats.ListenPort);
    public string DhtText => _stats is null ? "" : Strings.T("StatusBar.Dht", DhtLabel(_stats.DhtStatus));
    public string TurboStatusText => IsTurbo ? Strings.T("StatusBar.Turbo") : Strings.T("StatusBar.Normal");

    public string DetailsTitle => Selected is null ? Strings.T("Main.Files") : Strings.T("Main.FilesOf", Selected.Name);

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

            CheckAutoExit(snapshots);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao atualizar a interface");
        }
    }

    /// <summary>
    /// Conta o tempo em segundo plano sem nenhuma transferência ativa. Qualquer download,
    /// a janela aberta ou um diálogo zeram a contagem.
    /// </summary>
    private void CheckAutoExit(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        var now = DateTimeOffset.Now;
        if (snapshots.Any(s => PolicyEngine.KeepsAppBusy(s.Status)) || !CanAutoExit())
        {
            _idleSince = now;
            return;
        }
        var minutes = _settings.Current.AutoExitIdleMinutes;
        if (PolicyEngine.ShouldAutoExit(_idleSince, now, minutes))
        {
            Log.Information("Encerrando: {Minutes} min em segundo plano sem baixar nada", minutes);
            _idleSince = now;
            AutoExitRequested?.Invoke(this, EventArgs.Empty);
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
        RedownloadCommand.NotifyCanExecuteChanged();
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
        "Ready" => Strings.T("Dht.Ready"),
        "Initialising" => Strings.T("Dht.Starting"),
        "NotReady" => Strings.T("Dht.Off"),
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
            _dialogs.ShowError(Strings.T("Main.OnlyMagnet"));
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
            _dialogs.ShowError(Strings.T("Err.InvalidMagnet"));
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

    /// <summary>
    /// Caixinha "Baixar" na lista de Arquivos: marcar um arquivo ignorado começa a baixá-lo na hora;
    /// desmarcar para de baixá-lo.
    /// </summary>
    private async Task ToggleFileAsync(FileItemViewModel? file)
    {
        if (file is null || Selected is not { } torrent)
            return;
        try
        {
            await _service.SetFileSelectedAsync(torrent.Id, file.File.Index, !file.File.Selected);
        }
        catch (TorrentInputException ex)
        {
            _dialogs.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao marcar arquivo");
            _dialogs.ShowError(ex.Message);
        }
        // Se não mudou nada (ex.: era o último arquivo marcado), a caixinha volta ao estado real.
        file.Refresh();
        Tick();
    }

    /// <summary>Lixeira na lista de Arquivos: confirma, manda para a Lixeira e confere se saiu do disco.</summary>
    private async Task DeleteFileAsync(FileItemViewModel? file)
    {
        if (file is null || Selected is not { } torrent)
            return;
        if (!_dialogs.ConfirmDeleteFile(Path.GetFileName(file.Path)))
            return;
        await RunFileAction(file, () => _service.DeleteFileAsync(torrent.Id, file.File.Index));
    }

    private async Task RunFileAction(FileItemViewModel file, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (TorrentInputException ex)
        {
            _dialogs.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha na ação do arquivo");
            _dialogs.ShowError(ex.Message);
        }
        file.Refresh();
        Tick();
    }

    /// <summary>Reabre a seleção de arquivos de um torrent da lista e baixa de novo.</summary>
    private void Redownload()
    {
        if (Selected is not { Snapshot: { } snapshot } s)
            return;
        try
        {
            var vm = new AddTorrentViewModel(_service, _settings, _pickFolder);
            vm.SetExisting(s.Id, _service.GetPreview(s.Id), snapshot.SavePath, _service.GetSelectedFiles(s.Id));
            _dialogs.ShowAddTorrent(vm);
        }
        catch (TorrentInputException ex)
        {
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
            _dialogs.ShowError(Strings.T("Main.FolderMissing"));
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
            _dialogs.ShowError(Strings.T("Main.TurboFailed", ex.Message));
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
            _dialogs.ShowError(Strings.T("Main.SettingsFailed", ex.Message));
        }
        if (_isTurbo != _settings.Current.TurboMode)
        {
            _isTurbo = _settings.Current.TurboMode;
            OnPropertyChanged(nameof(IsTurbo));
            OnPropertyChanged(nameof(TurboLabel));
        }
    }
}
