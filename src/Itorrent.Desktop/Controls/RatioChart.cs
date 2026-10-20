using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Itorrent.Desktop.Controls;

/// <summary>
/// Gráfico do ratio: duas barras na mesma escala (Baixado e Enviado) e uma linha tracejada
/// na meta (ex.: 1.0 = enviar tudo o que baixou). A barra Enviado fica vermelha até cruzar a
/// linha e verde depois. Desenhado à mão, sem suavização, no estilo das barras de progresso do 3.1.
/// </summary>
public sealed class RatioChart : FrameworkElement
{
    private const double LabelWidth = 58;
    private const double BarHeight = 12;
    private const double RowGap = 5;
    private const double AxisHeight = 14;

    private static readonly Brush Navy = Frozen(Color.FromRgb(0x00, 0x00, 0x80));
    private static readonly Brush Maroon = Frozen(Color.FromRgb(0x80, 0x00, 0x00));
    private static readonly Brush Green = Frozen(Color.FromRgb(0x00, 0x80, 0x00));
    private static readonly Brush Teal = Frozen(Color.FromRgb(0x00, 0x80, 0x80));
    private static readonly Pen Border = FrozenPen(new Pen(Brushes.Black, 1));
    private static readonly Pen GoalPen = FrozenPen(new Pen(Brushes.Black, 1) { DashStyle = new DashStyle([2, 2], 0) });

    public static readonly DependencyProperty DownloadedProperty = Register(nameof(Downloaded), 0.0);
    public static readonly DependencyProperty UploadedProperty = Register(nameof(Uploaded), 0.0);
    public static readonly DependencyProperty GoalProperty = Register(nameof(Goal), 0.0);
    public static readonly DependencyProperty DownLabelProperty = Register(nameof(DownLabel), "");
    public static readonly DependencyProperty UpLabelProperty = Register(nameof(UpLabel), "");

    static RatioChart()
    {
        SnapsToDevicePixelsProperty.OverrideMetadata(typeof(RatioChart), new FrameworkPropertyMetadata(true));
        RenderOptions.EdgeModeProperty.OverrideMetadata(typeof(RatioChart), new FrameworkPropertyMetadata(EdgeMode.Aliased));
    }

    /// <summary>Base do ratio em bytes (o que foi baixado).</summary>
    public double Downloaded
    {
        get => (double)GetValue(DownloadedProperty);
        set => SetValue(DownloadedProperty, value);
    }

    public double Uploaded
    {
        get => (double)GetValue(UploadedProperty);
        set => SetValue(UploadedProperty, value);
    }

    /// <summary>Meta de ratio. 0 = sem meta (não desenha a linha).</summary>
    public double Goal
    {
        get => (double)GetValue(GoalProperty);
        set => SetValue(GoalProperty, value);
    }

    public string DownLabel
    {
        get => (string)GetValue(DownLabelProperty);
        set => SetValue(DownLabelProperty, value);
    }

    public string UpLabel
    {
        get => (string)GetValue(UpLabelProperty);
        set => SetValue(UpLabelProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, BarHeight * 2 + RowGap + AxisHeight + 2);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dc = drawingContext;
        var barX = LabelWidth;
        var barWidth = Math.Floor(ActualWidth - LabelWidth - 1);
        if (barWidth < 10)
            return;

        var down = Math.Max(0, Downloaded);
        var up = Math.Max(0, Uploaded);
        var goalBytes = Goal > 0 ? down * Goal : 0;
        // Folga de 15% para a linha da meta nunca ficar colada na borda.
        var scale = Math.Max(Math.Max(down, up), goalBytes) * 1.15;
        double X(double bytes) => scale <= 0 ? 0 : Math.Round(bytes / scale * barWidth);

        var upTop = BarHeight + RowGap;
        DrawLabel(dc, DownLabel, 0, 0);
        DrawLabel(dc, UpLabel, 0, upTop);
        DrawBar(dc, barX, 0, barWidth, X(down), Navy);
        var reached = Goal > 0 && up >= goalBytes && down > 0;
        DrawBar(dc, barX, upTop, barWidth, X(up), Goal <= 0 ? Teal : reached ? Green : Maroon);

        var axisTop = upTop + BarHeight + 2;
        DrawText(dc, "0", barX, axisTop);
        if (Goal > 0 && down > 0)
        {
            var gx = barX + X(goalBytes) + 0.5;
            dc.DrawLine(GoalPen, new Point(gx, -1), new Point(gx, axisTop));
            DrawText(dc, Goal.ToString("0.0#", CultureInfo.InvariantCulture), gx, axisTop, center: true);
        }
    }

    private static void DrawBar(DrawingContext dc, double x, double y, double width, double fill, Brush brush)
    {
        dc.DrawRectangle(Brushes.White, Border, new Rect(x + 0.5, y + 0.5, width, BarHeight));
        if (fill >= 1)
            dc.DrawRectangle(brush, null, new Rect(x + 1, y + 1, Math.Min(fill, width - 1), BarHeight - 1));
    }

    private void DrawLabel(DrawingContext dc, string text, double x, double y)
    {
        if (!string.IsNullOrEmpty(text))
            DrawText(dc, text, x, y - 1);
    }

    private void DrawText(DrawingContext dc, string text, double x, double y, bool center = false)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            11, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var left = center ? x - ft.Width / 2 : x;
        dc.DrawText(ft, new Point(Math.Max(0, Math.Min(left, ActualWidth - ft.Width)), y));
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(RatioChart),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Pen p)
    {
        p.Freeze();
        return p;
    }
}
