using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class SettingsProfileDialog : Window
{
    public SettingsProfileDialogViewModel ViewModel { get; } = new();

    public SettingsProfileDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
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
