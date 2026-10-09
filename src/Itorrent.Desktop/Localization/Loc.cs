using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using Itorrent.Core.Localization;

namespace Itorrent.Desktop.Localization;

/// <summary>
/// Expõe os textos de <see cref="Strings"/> para bindings. Quando o idioma muda,
/// avisa todos os bindings de uma vez e a interface troca de idioma na hora.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Loc() => Strings.LanguageChanged += (_, _) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Strings.T(key);

    /// <summary>Binding que acompanha o idioma (para textos criados em código).</summary>
    public static Binding Bind(string key) => new($"[{key}]") { Source = Instance, Mode = BindingMode.OneWay };
}

/// <summary>Uso em XAML: Text="{l:L Menu.File}".</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Loc.Bind(Key).ProvideValue(serviceProvider);
}
