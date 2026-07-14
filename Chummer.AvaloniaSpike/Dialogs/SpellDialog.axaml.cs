using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.AvaloniaSpike.Dialogs;

public partial class SpellDialog : Window
{
    public SpellDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
