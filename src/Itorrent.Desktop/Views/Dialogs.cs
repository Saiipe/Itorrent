using Itorrent.Core.Localization;
using System.IO;
using System.Windows;
using Itorrent.Core.Engine;
using Itorrent.Core.Storage;
using Itorrent.Desktop.ViewModels;
using Microsoft.Win32;

namespace Itorrent.Desktop.Views;

/// <summary>Implementação das janelas usadas pelo MainViewModel.</summary>
public sealed class Dialogs(ISettingsService settings) : IDialogs
{
    private static Window? Owner => Application.Current.MainWindow is { IsVisible: true } w ? w : null;

    public static string? PickFolder(string current)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Strings.T("Pick.Folder"),
            InitialDirectory = Directory.Exists(current) ? current : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string> AskMagnets() => SmallDialog.AskMagnets(Owner);

    public string? PickTorrentFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.T("Pick.Torrent"),
            Filter = Strings.T("Pick.Filter"),
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Abre sem bloquear. Janelas novas ficam em cascata sobre a última aberta.</summary>
    public void ShowAddTorrent(AddTorrentViewModel vm)
    {
        var owner = Owner;
        var dialog = new AddTorrentDialog(vm) { Owner = owner };
        var last = Application.Current.Windows.OfType<AddTorrentDialog>()
            .LastOrDefault(w => w != dialog && w.IsVisible && w.WindowState == WindowState.Normal);
        if (last is not null)
        {
            var area = SystemParameters.WorkArea;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = last.Left + 28 + dialog.Width > area.Right ? area.Left + 28 : last.Left + 28;
            dialog.Top = last.Top + 28 + dialog.Height > area.Bottom ? area.Top + 28 : last.Top + 28;
        }
        else if (owner is null)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        dialog.Show();
        dialog.Activate();
    }

    public void ShowConnectionTest(Func<CancellationToken, Task<ConnectionReport>> test) =>
        SmallDialog.ConnectionTest(Owner, test);

    public (bool Confirmed, bool DeleteFiles) ConfirmRemove(string name) => SmallDialog.ConfirmRemove(Owner, name);

    public bool ConfirmDeleteFile(string name) => SmallDialog.ConfirmDeleteFile(Owner, name);

    public bool ShowSettings()
    {
        var dialog = new SettingsDialog(new SettingsViewModel(settings, PickFolder)) { Owner = Owner };
        return dialog.ShowDialog() == true;
    }

    public void ShowAbout() => SmallDialog.About(Owner);

    public void ShowError(string message) => SmallDialog.Message(Owner, "Itorrent", message, "Warning");
}
