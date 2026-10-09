using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Itorrent.Core.Security;
using Itorrent.Desktop.Assets;
using Itorrent.Desktop.Controls;

namespace Itorrent.Desktop.Views;

/// <summary>
/// Caixa de diálogo genérica no estilo 3.1 (ícone à esquerda, texto, botões centralizados).
/// Usada para mensagens, link magnet, remoção e "Sobre".
/// </summary>
public partial class SmallDialog : RetroWindow
{
    private SmallDialog(Window? owner, string title, ImageSource icon)
    {
        InitializeComponent();
        Owner = owner is { IsVisible: true } ? owner : null;
        if (Owner is null)
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = title;
        DialogIcon.Source = icon;
    }

    private Button AddButton(string text, bool isDefault = false, bool isCancel = false, bool? result = null)
    {
        var b = new Button { Content = text, Width = 96, Margin = new Thickness(4, 0, 4, 0), IsDefault = isDefault, IsCancel = isCancel };
        if (result is not null)
            b.Click += (_, _) => DialogResult = result;
        Buttons.Children.Add(b);
        return b;
    }

    private static TextBlock Text(string text, bool bold = false) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 6) };

    public static void Message(Window? owner, string title, string message, string icon = "Info")
    {
        var d = new SmallDialog(owner, title, PixelIcons.Get(icon));
        d.Body.Children.Add(Text(message));
        d.AddButton("OK", isDefault: true, isCancel: true, result: true);
        d.ShowDialog();
    }

    public static string? AskMagnet(Window? owner)
    {
        var d = new SmallDialog(owner, "Adicionar Link Magnet", PixelIcons.Get("Magnet"));
        d.Body.Children.Add(Text("Cole o link magnet:", bold: true));
        var box = new TextBox
        {
            Width = 440, Height = 64, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false,
            MaxLength = LinkValidator.MaxMagnetLength, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText().Trim() is { } clip
                && clip.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) && clip.Length <= LinkValidator.MaxMagnetLength)
                box.Text = clip;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Área de transferência ocupada por outro app.
        }
        d.Body.Children.Add(box);
        var error = Text("");
        error.Foreground = (Brush)d.FindResource("Alert");
        d.Body.Children.Add(error);

        var ok = d.AddButton("OK", isDefault: true);
        ok.Click += (_, _) =>
        {
            if (LinkValidator.IsValidMagnet(box.Text))
                d.DialogResult = true;
            else
                error.Text = "Isto não parece um link magnet válido (deve começar com magnet:?xt=...).";
        };
        d.AddButton("Cancelar", isCancel: true);
        d.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        return d.ShowDialog() == true ? box.Text.Trim() : null;
    }

    public static (bool Confirmed, bool DeleteFiles) ConfirmRemove(Window? owner, string name)
    {
        var d = new SmallDialog(owner, "Remover Transferência", PixelIcons.Get("Remove"));
        d.Body.Children.Add(Text($"Remover \"{name}\" da lista?"));
        var delete = new CheckBox { Content = "Apagar também os arquivos baixados do disco", Margin = new Thickness(0, 4, 0, 0) };
        d.Body.Children.Add(delete);
        d.AddButton("Remover", isDefault: true, result: true);
        d.AddButton("Cancelar", isCancel: true);
        return d.ShowDialog() == true ? (true, delete.IsChecked == true) : (false, false);
    }

    public static void About(Window? owner)
    {
        var d = new SmallDialog(owner, "Sobre o Itorrent",
            new BitmapImage(new Uri("pack://application:,,,/Itorrent;component/Assets/itorrent-48.png")));
        d.DialogIcon.Width = d.DialogIcon.Height = 48;
        RenderOptions.SetBitmapScalingMode(d.DialogIcon, BitmapScalingMode.HighQuality);
        var version = typeof(SmallDialog).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        d.Body.Children.Add(Text("Itorrent", bold: true));
        d.Body.Children.Add(Text($"Versão {version}\nCliente BitTorrent para Windows\nMotor: MonoTorrent · .NET 10"));
        d.Body.Children.Add(new Rectangle { Height = 1, Fill = Brushes.Black, Margin = new Thickness(0, 4, 0, 8) });
        d.Body.Children.Add(Text(
            "Criar e usar um cliente BitTorrent é legal. Baixar ou compartilhar conteúdo protegido por direitos " +
            "autorais sem autorização não é, e a responsabilidade é de quem usa.\n\n" +
            "Lembre-se: todo peer do swarm vê o seu endereço IP."));
        d.AddButton("OK", isDefault: true, isCancel: true, result: true);
        d.ShowDialog();
    }
}
