using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.AvaloniaSpike.Dialogs;

public partial class QualityDialog : Window
{
    public QualityDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
