using System.ComponentModel;
using System.Windows;
using Itorrent.Core.Storage;
using Itorrent.Desktop.Controls;
using Itorrent.Desktop.ViewModels;

namespace Itorrent.Desktop.Views;

public partial class MainWindow : RetroWindow
{
    private readonly ISettingsService _settings;
    private bool _trayHintShown;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        Drop += OnDrop;
        Loaded += (_, _) => TorrentList.Focus();
    }

    public MainViewModel ViewModel { get; }

    /// <summary>Pedido de encerrar o app (menu Sair).</summary>
    public event EventHandler? ExitRequested;

    /// <summary>A janela foi para a bandeja pela primeira vez (mostrar aviso).</summary>
    public event EventHandler? HiddenToTray;

    /// <summary>Quando true, fechar a janela realmente fecha (usado ao sair).</summary>
    public bool AllowClose { get; set; }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fechar (X) só esconde; o motor continua rodando na bandeja.
        if (!AllowClose && _settings.Current.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
        }
        else if (!AllowClose)
        {
            e.Cancel = true;
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        base.OnClosing(e);
    }

    private void HideToTray()
    {
        Hide();
        if (!_trayHintShown)
        {
            _trayHintShown = true;
            HiddenToTray?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var f in files.Where(f => f.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)).Take(10))
                ViewModel.HandleExternalInput(f);
        }
        else if (e.Data.GetData(DataFormats.UnicodeText) is string text)
        {
            ViewModel.HandleExternalInput(text);
        }
    }
}
