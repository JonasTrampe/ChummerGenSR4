using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class SpellDialog : Window
{
    public SpellDialog()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
