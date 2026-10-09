using System.Windows;
using Itorrent.Desktop.Controls;
using Itorrent.Desktop.ViewModels;

namespace Itorrent.Desktop.Views;

public partial class SettingsDialog : RetroWindow
{
    private readonly SettingsViewModel _vm;

    public SettingsDialog(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // Enter (botão padrão) não tira o foco da caixa de texto: confirma o valor digitado.
        (System.Windows.Input.Keyboard.FocusedElement as System.Windows.Controls.TextBox)?
            .GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        if (_vm.TrySave())
            DialogResult = true;
    }
}
