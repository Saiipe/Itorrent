using Itorrent.Core.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Itorrent.Core.Engine;
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
        d.AddButton(Strings.T("Common.Ok"), isDefault: true, isCancel: true, result: true);
        d.ShowDialog();
    }

    /// <summary>Máximo de links colados de uma vez (cada um abre a sua janela).</summary>
    public const int MaxMagnets = 20;

    /// <summary>Um ou mais links magnet, um por linha. Vazio se cancelou.</summary>
    public static IReadOnlyList<string> AskMagnets(Window? owner)
    {
        var d = new SmallDialog(owner, Strings.T("Magnet.Title"), PixelIcons.Get("Magnet"));
        d.Body.Children.Add(Text(Strings.T("Magnet.Prompt"), bold: true));
        var box = new TextBox
        {
            Width = 460, Height = 110, TextWrapping = TextWrapping.NoWrap, AcceptsReturn = true,
            MaxLength = (LinkValidator.MaxMagnetLength + 2) * MaxMagnets,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        try
        {
            if (Clipboard.ContainsText() && SplitLines(Clipboard.GetText()) is { Count: > 0 and <= MaxMagnets } clip
                && clip.All(l => l.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) && l.Length <= LinkValidator.MaxMagnetLength))
                box.Text = string.Join(Environment.NewLine, clip);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Área de transferência ocupada por outro app.
        }
        d.Body.Children.Add(box);
        var error = Text("");
        error.Foreground = (Brush)d.FindResource("Alert");
        d.Body.Children.Add(error);

        List<string> links = [];
        var ok = d.AddButton(Strings.T("Common.Ok"), isDefault: true);
        ok.Click += (_, _) =>
        {
            var lines = SplitLines(box.Text);
            var bad = lines.FindIndex(l => !LinkValidator.IsValidMagnet(l));
            if (lines.Count == 0)
                error.Text = Strings.T("Magnet.Invalid");
            else if (lines.Count > MaxMagnets)
                error.Text = Strings.T("Magnet.TooMany", MaxMagnets);
            else if (bad >= 0)
                error.Text = lines.Count == 1 ? Strings.T("Magnet.Invalid") : Strings.T("Magnet.InvalidLine", bad + 1);
            else
            {
                links = lines;
                d.DialogResult = true;
            }
        };
        d.AddButton(Strings.T("Common.Cancel"), isCancel: true);
        d.Loaded += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        return d.ShowDialog() == true ? links : [];
    }

    /// <summary>Linhas não vazias, sem espaços nas pontas e sem repetidas.</summary>
    public static List<string> SplitLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Roda o teste de conexão (que já reconecta o que estiver caído) e mostra cada etapa
    /// com um ícone: certo, aviso ou falha. "Testar de novo" repete sem fechar a janela.
    /// </summary>
    public static void ConnectionTest(Window? owner, Func<CancellationToken, Task<ConnectionReport>> test)
    {
        var d = new SmallDialog(owner, Strings.T("Conn.Title"), PixelIcons.Get("Network"));
        using var cts = new CancellationTokenSource();
        var summary = Text("", bold: true);
        var busy = new StackPanel();
        busy.Children.Add(new ContentControl { Style = (Style)d.FindResource("BusyBar"), Width = 380, HorizontalAlignment = HorizontalAlignment.Left });
        busy.Children.Add(new TextBlock { Text = Strings.T("Conn.TestingHint"), Foreground = (Brush)d.FindResource("Shadow"), Margin = new Thickness(0, 6, 0, 0) });
        var results = new Grid { MinWidth = 420 };
        results.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        results.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        results.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var note = new TextBlock
        {
            Text = Strings.T("Conn.AutoNote"), TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)d.FindResource("Shadow"), Margin = new Thickness(0, 10, 0, 0),
        };
        d.Body.Children.Add(summary);
        d.Body.Children.Add(busy);
        d.Body.Children.Add(results);
        d.Body.Children.Add(note);

        var again = d.AddButton(Strings.T("Conn.Again"));
        d.AddButton(Strings.T("Common.Ok"), isDefault: true, isCancel: true, result: true);

        async Task RunAsync()
        {
            again.IsEnabled = false;
            summary.Text = Strings.T("Conn.Testing");
            summary.Foreground = Brushes.Black;
            busy.Visibility = Visibility.Visible;
            results.Visibility = note.Visibility = Visibility.Collapsed;
            try
            {
                var report = await test(cts.Token);
                ShowReport(d, report, summary, results);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Teste de conexão falhou");
                summary.Text = Strings.T("Conn.Error", ex.Message);
                summary.Foreground = (Brush)d.FindResource("Alert");
            }
            busy.Visibility = Visibility.Collapsed;
            note.Visibility = Visibility.Visible;
            again.IsEnabled = true;
        }

        again.Click += async (_, _) => await RunAsync();
        d.Loaded += async (_, _) => await RunAsync();
        d.Closed += (_, _) => cts.Cancel();
        d.ShowDialog();
    }

    private static void ShowReport(SmallDialog d, ConnectionReport report, TextBlock summary, Grid results)
    {
        summary.Text = report.HasFailures ? Strings.T("Conn.HasFailures")
            : report.HasWarnings ? Strings.T("Conn.SomeWarnings")
            : Strings.T("Conn.AllOk");
        summary.Foreground = report.HasFailures ? (Brush)d.FindResource("Alert") : Brushes.Black;

        results.Children.Clear();
        results.RowDefinitions.Clear();
        foreach (var (check, row) in report.Checks.Select((c, i) => (c, i)))
        {
            results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var icon = new Image
            {
                Source = PixelIcons.Get(check.Result switch
                {
                    CheckResult.Ok => "Check",
                    CheckResult.Warning => "Warning",
                    CheckResult.Failed => "Remove",
                    _ => "Info",
                }),
                Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 6, 3),
                Opacity = check.Result == CheckResult.Skipped ? 0.4 : 1,
            };
            var name = new TextBlock { Text = check.Name, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 3, 10, 3) };
            var detail = new TextBlock { Text = check.Detail, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3), MaxWidth = 360 };
            if (check.Result == CheckResult.Failed)
                detail.Foreground = (Brush)d.FindResource("Alert");
            else if (check.Result == CheckResult.Skipped)
                detail.Foreground = (Brush)d.FindResource("Shadow");
            foreach (var (element, column) in new UIElement[] { icon, name, detail }.Select((e, i) => (e, i)))
            {
                Grid.SetRow(element, row);
                Grid.SetColumn(element, column);
                results.Children.Add(element);
            }
        }
        results.Visibility = Visibility.Visible;
    }

    public static (bool Confirmed, bool DeleteFiles) ConfirmRemove(Window? owner, string name)
    {
        var d = new SmallDialog(owner, Strings.T("Remove.Title"), PixelIcons.Get("Remove"));
        d.Body.Children.Add(Text(Strings.T("Remove.Question", name)));
        var delete = new CheckBox { Content = Strings.T("Remove.DeleteFiles"), Margin = new Thickness(0, 4, 0, 0) };
        d.Body.Children.Add(delete);
        d.AddButton(Strings.T("Tool.Remove"), isDefault: true, result: true);
        d.AddButton(Strings.T("Common.Cancel"), isCancel: true);
        return d.ShowDialog() == true ? (true, delete.IsChecked == true) : (false, false);
    }

    public static bool ConfirmDeleteFile(Window? owner, string name)
    {
        var d = new SmallDialog(owner, Strings.T("Delete.Title"), PixelIcons.Get("Trash"));
        d.Body.Children.Add(Text(Strings.T("Delete.Question", name), bold: true));
        d.Body.Children.Add(Text(Strings.T("Delete.Note")));
        d.AddButton(Strings.T("Delete.Button"), isDefault: true, result: true);
        d.AddButton(Strings.T("Common.Cancel"), isCancel: true);
        return d.ShowDialog() == true;
    }

    public static void About(Window? owner)
    {
        var d = new SmallDialog(owner, Strings.T("About.Title"),
            new BitmapImage(new Uri("pack://application:,,,/Itorrent;component/Assets/itorrent-48.png")));
        d.DialogIcon.Width = d.DialogIcon.Height = 48;
        RenderOptions.SetBitmapScalingMode(d.DialogIcon, BitmapScalingMode.HighQuality);
        var version = typeof(SmallDialog).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        d.Body.Children.Add(Text("Itorrent", bold: true));
        d.Body.Children.Add(Text(Strings.T("About.Body", version)));
        d.Body.Children.Add(new Rectangle { Height = 1, Fill = Brushes.Black, Margin = new Thickness(0, 4, 0, 8) });
        d.Body.Children.Add(Text(Strings.T("About.Legal")));
        d.AddButton(Strings.T("Common.Ok"), isDefault: true, isCancel: true, result: true);
        d.ShowDialog();
    }
}
