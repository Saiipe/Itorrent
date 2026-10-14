using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Itorrent.Desktop.Controls;

/// <summary>
/// Barra de rolagem horizontal mesmo com a lista vazia. Em XAML:
///   ListView: c:GridScroll.Enabled="True"
/// O WPF só mede a largura pelas linhas; sem nenhuma linha, as colunas que não cabem
/// (Peers, Adicionado…) ficavam inalcançáveis. Aqui, enquanto vazia, o painel recebe a
/// largura somada das colunas e a rolagem passa a ser por pixel, então o título rola junto.
/// </summary>
public static class GridScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(GridScroll), new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyPropertyDescriptor ColumnWidth =
        DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView list || !(bool)e.NewValue)
            return;

        void Update(object? sender, EventArgs args) => Refresh(list);

        list.Loaded += (_, _) =>
        {
            if (list.View is GridView grid)
            {
                foreach (var column in grid.Columns)
                    ColumnWidth.AddValueChanged(column, Update);
            }
            Refresh(list);
        };
        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => Refresh(list);
    }

    private static void Refresh(ListView list)
    {
        if (list.View is not GridView grid || FindPanel(list) is not { } panel)
            return;

        var empty = list.Items.Count == 0;
        // Com linhas, a rolagem virtualizada já mede a largura certa; o MinWidth só atrapalharia.
        ScrollViewer.SetCanContentScroll(list, !empty);
        panel.MinWidth = empty
            ? grid.Columns.Sum(c => double.IsNaN(c.Width) ? c.ActualWidth : c.Width)
            : 0;
    }

    private static Panel? FindPanel(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ItemsPresenter presenter)
                return VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as Panel : null;
            if (FindPanel(child) is { } found)
                return found;
        }
        return null;
    }
}
