using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Itorrent.Desktop.Views;

/// <summary>
/// RadioButton ↔ valor (enum, número ou bool): marcado quando o valor é igual ao parâmetro.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is not string s)
            return Binding.DoNothing;
        return targetType.IsEnum
            ? Enum.Parse(targetType, s)
            : System.Convert.ChangeType(s, targetType, CultureInfo.InvariantCulture);
    }
}

/// <summary>Visível quando o valor não é nulo nem string vazia (Invert para o contrário).</summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var has = value switch
        {
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            bool b => b,
            _ => value is not null,
        };
        return has ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
