using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Itorrent.Desktop.Controls;

/// <summary>
/// Ordenação por clique no título da coluna. Em XAML:
///   ListView: c:GridSort.Enabled="True"
///   GridViewColumn: c:GridSort.Member="NomeDaPropriedade"
/// 1º clique: crescente; 2º: decrescente; 3º: volta à ordem original (de adição).
/// A lista continua ordenada enquanto os valores mudam (velocidade, progresso…).
/// </summary>
public static class GridSort
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(GridSort), new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty MemberProperty = DependencyProperty.RegisterAttached(
        "Member", typeof(string), typeof(GridSort), new PropertyMetadata(null));

    /// <summary>Direção atual da coluna (para a setinha ▲▼ no título). Null = não ordenada.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.RegisterAttached(
        "Direction", typeof(ListSortDirection?), typeof(GridSort), new PropertyMetadata(null));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);
    public static string? GetMember(DependencyObject o) => (string?)o.GetValue(MemberProperty);
    public static void SetMember(DependencyObject o, string? value) => o.SetValue(MemberProperty, value);
    public static ListSortDirection? GetDirection(DependencyObject o) => (ListSortDirection?)o.GetValue(DirectionProperty);
    public static void SetDirection(DependencyObject o, ListSortDirection? value) => o.SetValue(DirectionProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView list)
            return;
        if ((bool)e.NewValue)
            list.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnHeaderClick));
        else
            list.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnHeaderClick));
    }

    private static void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ListView list || e.OriginalSource is not GridViewColumnHeader { Column: { } column }
            || list.View is not GridView grid || GetMember(column) is not { Length: > 0 } member)
            return;

        // Crescente → decrescente → ordem original (como foram adicionados) → crescente…
        ListSortDirection? direction = GetDirection(column) switch
        {
            null => ListSortDirection.Ascending,
            ListSortDirection.Ascending => ListSortDirection.Descending,
            _ => null,
        };
        foreach (var c in grid.Columns)
            SetDirection(c, null);
        SetDirection(column, direction);

        var items = list.Items;
        using (items.DeferRefresh())
        {
            items.SortDescriptions.Clear();
            if (direction is { } dir)
                items.SortDescriptions.Add(new SortDescription(member, dir));
        }

        if (direction is null)
        {
            if (items is ICollectionViewLiveShaping { CanChangeLiveSorting: true } off)
                off.IsLiveSorting = false;
            return;
        }

        // Reordena sozinho quando o valor muda (ex.: velocidade a cada segundo).
        if (items is ICollectionViewLiveShaping { CanChangeLiveSorting: true } live)
        {
            live.LiveSortingProperties.Clear();
            live.LiveSortingProperties.Add(member);
            live.IsLiveSorting = true;
        }
    }
}
