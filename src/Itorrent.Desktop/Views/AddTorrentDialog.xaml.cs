using Itorrent.Desktop.Controls;
using Itorrent.Desktop.ViewModels;

namespace Itorrent.Desktop.Views;

public partial class AddTorrentDialog : RetroWindow
{
    public AddTorrentDialog(AddTorrentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, ok) => DialogResult = ok;
        Closed += (_, _) => viewModel.Dispose();
    }
}
