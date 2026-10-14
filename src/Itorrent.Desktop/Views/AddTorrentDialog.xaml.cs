using System.Windows;
using Itorrent.Desktop.Controls;
using Itorrent.Desktop.ViewModels;

namespace Itorrent.Desktop.Views;

/// <summary>
/// Janela não modal: dá para ter várias abertas (um magnet procurando peers enquanto outro já
/// mostra os arquivos) e continuar usando a janela principal.
/// </summary>
public partial class AddTorrentDialog : RetroWindow
{
    public AddTorrentDialog(AddTorrentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.ActivateRequested += (_, _) => BringToFront();
        Closed += (_, _) => viewModel.Dispose();
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
