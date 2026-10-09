using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Itorrent.Desktop.Platform;

/// <summary>Ícone na bandeja com menu: Abrir, Pausar tudo, Retomar tudo, Sair.</summary>
public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayService(Action open, Action pauseAll, Action resumeAll, Action exit)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("_Abrir Itorrent", open, bold: true));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("_Pausar tudo", pauseAll));
        menu.Items.Add(Item("_Retomar tudo", resumeAll));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("_Sair", exit));

        using var iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/itorrent.ico")).Stream;
        _icon = new TaskbarIcon
        {
            ToolTipText = "Itorrent",
            Icon = new System.Drawing.Icon(iconStream, 32, 32),
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };
        _icon.TrayLeftMouseUp += (_, _) => open();
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void SetToolTip(string text) => _icon.ToolTipText = text;

    public void Notify(string title, string message) =>
        _icon.ShowNotification(title, message, NotificationIcon.Info);

    public void Dispose() => _icon.Dispose();

    private static MenuItem Item(string header, Action action, bool bold = false)
    {
        var item = new MenuItem { Header = header };
        if (bold)
            item.FontWeight = System.Windows.FontWeights.ExtraBold;
        item.Click += (_, _) => action();
        return item;
    }
}
