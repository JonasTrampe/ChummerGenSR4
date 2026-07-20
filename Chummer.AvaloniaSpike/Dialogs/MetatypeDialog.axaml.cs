using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.AvaloniaSpike.Dialogs;

public partial class MetatypeDialog : Window
{
    public MetatypeDialog()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
