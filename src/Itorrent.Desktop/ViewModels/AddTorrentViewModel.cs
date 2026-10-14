using Itorrent.Core.Localization;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Itorrent.Core.Engine;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;

namespace Itorrent.Desktop.ViewModels;

/// <summary>
/// Confirmação antes de baixar: nome, tamanho, arquivos (desmarcáveis) e pasta de destino.
/// Para magnet, mostra "obtendo lista de arquivos" enquanto os metadados chegam dos peers,
/// com cronômetro e o botão "Tentar novamente" (cancela a busca e procura peers do zero).
/// Várias janelas destas podem ficar abertas ao mesmo tempo, cada uma com seu torrent.
/// </summary>
public sealed class AddTorrentViewModel : ObservableObject, IDisposable
{
    private readonly ITorrentService _service;
    private readonly ISettingsService _settings;
    private readonly Func<string, string?> _pickFolder;
    private string? _existingId;
    private static readonly TimeSpan SlowAfter = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _cts = new();
    private readonly DispatcherTimer _clock;
    private string? _magnet;
    private CancellationTokenSource? _loadCts;
    private Task? _loadTask;
    private DateTime _loadStartedAt;
    private TorrentPreview? _preview;
    private FileNode? _tree;
    private bool _isLoading;
    private bool _isBusy;
    private string? _error;
    private string _savePath;
    private bool _rememberFolder;

    public AddTorrentViewModel(ITorrentService service, ISettingsService settings, Func<string, string?> pickFolder)
    {
        _service = service;
        _settings = settings;
        _pickFolder = pickFolder;
        _savePath = settings.Current.DefaultSavePath;

        BrowseCommand = new RelayCommand(Browse, () => !IsBusy && !IsEditing);
        SelectAllCommand = new RelayCommand(() => SetAll(true), () => _tree is not null);
        SelectNoneCommand = new RelayCommand(() => SetAll(false), () => _tree is not null);
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync, CanConfirm);
        RetryCommand = new AsyncRelayCommand(RetryAsync, () => CanRetry);

        _clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            OnPropertyChanged(nameof(ElapsedText));
            OnPropertyChanged(nameof(IsSlow));
        }, Dispatcher.CurrentDispatcher);
    }

    public event EventHandler<bool>? CloseRequested;

    /// <summary>Pedido de trazer a janela para frente (o mesmo torrent foi aberto de novo).</summary>
    public event EventHandler? ActivateRequested;

    /// <summary>A janela fechou e este ViewModel foi descartado.</summary>
    public event EventHandler? Closed;

    public AsyncRelayCommand RetryCommand { get; }

    /// <summary>Magnet ainda sem lista de arquivos (procurando ou falhou): dá para tentar de novo.</summary>
    public bool CanRetry => _magnet is not null && _preview is null && !IsBusy;

    public string ElapsedText
    {
        get
        {
            var t = DateTime.UtcNow - _loadStartedAt;
            return Strings.T("Add.Elapsed", $"{(int)t.TotalMinutes}:{t.Seconds:00}");
        }
    }

    public bool IsSlow => IsLoading && DateTime.UtcNow - _loadStartedAt >= SlowAfter;

    public RelayCommand BrowseCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public AsyncRelayCommand ConfirmCommand { get; }

    public ObservableCollection<FileNode> Roots { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
                return;
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsSlow));
            OnPropertyChanged(nameof(CanRetry));
            RetryCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsReady => !IsLoading && _preview is not null;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                RefreshCommands();
        }
    }

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public string Title => _preview?.Name ?? Strings.T("Add.Loading");
    public string TotalSizeText => _preview is null ? "?" : Format.Bytes(_preview.TotalSize);
    public string FileCountText => _preview is null ? "" : Strings.T("Add.FileCount", _preview.Files.Count);
    public string PrivateText => _preview?.IsPrivate == true ? Strings.T("Add.PrivateYes") : Strings.T("Add.PrivateNo");
    public string? Comment => string.IsNullOrWhiteSpace(_preview?.Comment) ? null : _preview.Comment;
    public bool HasRiskyFiles => _preview?.HasRiskyFiles ?? false;

    public string RiskText
    {
        get
        {
            if (_preview is null)
                return "";
            var risky = _preview.Files.Where(f => f.Risk != FileRisk.None).ToList();
            var dbl = risky.Count(f => f.Risk == FileRisk.DoubleExtension);
            var names = string.Join(", ", risky.Take(3).Select(f => Path.GetFileName(f.Path)));
            if (risky.Count > 3)
                names += Strings.T("Add.RiskMore", risky.Count - 3);
            return (dbl > 0 ? Strings.T("Add.RiskDouble", dbl) : "") + Strings.T("Add.RiskBody", names);
        }
    }

    public string SelectionText
    {
        get
        {
            if (_tree is null)
                return "";
            var leaves = _tree.Leaves().ToList();
            var chosen = leaves.Where(l => l.IsChecked == true).ToList();
            return Strings.T("Add.Selection", chosen.Count, leaves.Count, Format.Bytes(chosen.Sum(l => l.Length)));
        }
    }

    public string SavePath
    {
        get => _savePath;
        set
        {
            if (SetProperty(ref _savePath, value))
            {
                OnPropertyChanged(nameof(FreeSpaceText));
                ConfirmCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool RememberFolder
    {
        get => _rememberFolder;
        set => SetProperty(ref _rememberFolder, value);
    }

    public string FreeSpaceText
    {
        get
        {
            try
            {
                if (!PathGuard.IsValidSaveDirectory(SavePath))
                    return Strings.T("Add.InvalidFolder");
                var root = Path.GetPathRoot(Path.GetFullPath(SavePath));
                var drive = new DriveInfo(root!);
                return drive.IsReady ? Strings.T("Add.FreeSpace", root, Format.Bytes(drive.AvailableFreeSpace)) : Strings.T("Add.DriveUnavailable");
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                return "";
            }
        }
    }

    public Task LoadMagnetAsync(string magnet)
    {
        _magnet = magnet;
        _loadTask = FetchAsync();
        return _loadTask;
    }

    public void Activate() => ActivateRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Cancela a busca atual (espera ela soltar o torrent no motor) e procura de novo.</summary>
    private async Task RetryAsync()
    {
        if (_magnet is null)
            return;
        Serilog.Log.Information("Magnet: tentando obter a lista de arquivos de novo");
        if (_loadCts is not null)
            await _loadCts.CancelAsync();
        if (_loadTask is not null)
        {
            try
            {
                await _loadTask;
            }
            catch (OperationCanceledException)
            {
                // Busca anterior cancelada.
            }
        }
        if (_cts.IsCancellationRequested || _preview is not null)
            return;
        _loadTask = FetchAsync();
        await _loadTask;
    }

    private async Task FetchAsync()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _loadCts = cts;
        _loadStartedAt = DateTime.UtcNow;
        Error = null;
        IsLoading = true;
        _clock.Start();
        OnPropertyChanged(nameof(ElapsedText));
        try
        {
            SetPreview(await _service.PrepareMagnetAsync(_magnet!, cts.Token));
        }
        catch (OperationCanceledException)
        {
            // Usuário cancelou ou pediu para tentar de novo.
        }
        catch (TorrentInputException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Falha ao obter metadados do magnet");
            Error = Strings.T("Add.MetadataFailed");
        }
        finally
        {
            _clock.Stop();
            _loadCts = null;
            IsLoading = false;
        }
    }

    /// <summary>
    /// Modo "baixar novamente": torrent que já está na lista. A pasta não muda
    /// (os arquivos já estão lá) e a seleção atual vem marcada.
    /// </summary>
    public void SetExisting(string id, TorrentPreview preview, string savePath, IReadOnlySet<int> selected)
    {
        _existingId = id;
        _savePath = savePath;
        SetPreview(preview);
        foreach (var leaf in _tree!.Leaves())
            leaf.IsChecked = selected.Contains(leaf.File!.Index);
        OnPropertyChanged(string.Empty);
        RefreshCommands();
    }

    public bool IsEditing => _existingId is not null;
    public bool IsNew => !IsEditing;
    public string DialogTitle => IsEditing ? Strings.T("Add.TitleAgain") : Strings.T("Add.TitleNew");

    public string? EditingHint => IsEditing
        ? Strings.T("Add.AgainHint")
        : null;

    public void SetPreview(TorrentPreview preview)
    {
        _preview = preview;
        _tree = FileNode.BuildTree(preview, () =>
        {
            OnPropertyChanged(nameof(SelectionText));
            ConfirmCommand.NotifyCanExecuteChanged();
        });

        Roots.Clear();
        foreach (var node in preview.Files.Count == 1 ? _tree.Children : [_tree])
            Roots.Add(node);

        OnPropertyChanged(string.Empty);
        RefreshCommands();
    }

    private void SetAll(bool value)
    {
        if (_tree is not null)
            _tree.IsChecked = value;
    }

    private void Browse()
    {
        if (_pickFolder(SavePath) is { } folder)
            SavePath = folder;
    }

    private bool CanConfirm() =>
        !IsBusy && !IsLoading && _tree is not null
        && _tree.Leaves().Any(l => l.IsChecked == true)
        && PathGuard.IsValidSaveDirectory(SavePath);

    private async Task ConfirmAsync()
    {
        if (_preview is null || _tree is null)
            return;
        IsBusy = true;
        Error = null;
        try
        {
            var selected = _tree.Leaves().Where(l => l.IsChecked == true).Select(l => l.File!.Index).ToHashSet();
            if (_existingId is not null)
            {
                await _service.RedownloadAsync(_existingId, selected);
                CloseRequested?.Invoke(this, true);
                return;
            }
            await _service.AddAsync(_preview, SavePath, selected);
            if (RememberFolder)
                _settings.Save(_settings.Current with { DefaultSavePath = SavePath });
            CloseRequested?.Invoke(this, true);
        }
        catch (TorrentInputException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Falha ao adicionar torrent");
            Error = Strings.T("Add.StartFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommands()
    {
        OnPropertyChanged(nameof(CanRetry));
        RetryCommand.NotifyCanExecuteChanged();
        BrowseCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    public void Cancel() => _cts.Cancel();

    public void Dispose()
    {
        _clock.Stop();
        _cts.Cancel();
        _cts.Dispose();
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
