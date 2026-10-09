using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Itorrent.Desktop.Controls;

/// <summary>
/// Janela com moldura e barra de título no estilo Windows 3.1.
/// Clique duplo na caixa de controle (canto esquerdo) fecha a janela, como no original.
/// </summary>
public class RetroWindow : Window
{
    public static readonly DependencyProperty IsDialogProperty =
        DependencyProperty.Register(nameof(IsDialog), typeof(bool), typeof(RetroWindow), new PropertyMetadata(false));

    public RetroWindow()
    {
        SetResourceReference(StyleProperty, "RetroWindowStyle");
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/itorrent.ico"));

        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => Close()));
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.ShowSystemMenuCommand, (_, _) =>
        {
            var p = PointToScreen(new Point(4, 24));
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            SystemCommands.ShowSystemMenu(this, new Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY));
        }));
    }

    public bool IsDialog
    {
        get => (bool)GetValue(IsDialogProperty);
        set => SetValue(IsDialogProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("ControlBox") is Button box)
            box.MouseDoubleClick += (_, _) => Close();
    }
}

/// <summary>Painel com barra de título, como os grupos do Gerenciador de Programas.</summary>
public class ChildWindow : HeaderedContentControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ChildWindow), new PropertyMetadata(true));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(ChildWindow));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }
}
