using Itorrent.Core.Localization;
using System.IO;
using System.Windows;
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

    public string? AskMagnet() => SmallDialog.AskMagnet(Owner);

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

    public void ShowAddTorrent(AddTorrentViewModel vm)
    {
        var owner = Owner;
        var dialog = new AddTorrentDialog(vm) { Owner = owner };
        if (owner is null)
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
    }

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
