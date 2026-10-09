using Itorrent.Core.Localization;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Itorrent.Core.Engine;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;

namespace Itorrent.Desktop.ViewModels;

/// <summary>
/// Confirmação antes de baixar: nome, tamanho, arquivos (desmarcáveis) e pasta de destino.
/// Para magnet, mostra "obtendo lista de arquivos" enquanto os metadados chegam dos peers.
/// </summary>
public sealed class AddTorrentViewModel : ObservableObject, IDisposable
{
    private readonly ITorrentService _service;
    private readonly ISettingsService _settings;
    private readonly Func<string, string?> _pickFolder;
    private string? _existingId;
    private readonly CancellationTokenSource _cts = new();
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
    }

    public event EventHandler<bool>? CloseRequested;

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
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(IsReady));
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

    public async Task LoadMagnetAsync(string magnet)
    {
        IsLoading = true;
        try
        {
            SetPreview(await _service.PrepareMagnetAsync(magnet, _cts.Token));
        }
        catch (OperationCanceledException)
        {
            // Usuário cancelou.
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
        BrowseCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    public void Cancel() => _cts.Cancel();

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
