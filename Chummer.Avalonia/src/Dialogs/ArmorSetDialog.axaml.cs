using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class ArmorSetDialog : Window
{
    public ArmorSetDialogViewModel ViewModel { get; } = new();

    public string SetName => ViewModel.Name.Trim();

    public ArmorSetDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (SetName.Length == 0)
        {
            ViewModel.ErrorMessage = "Bitte einen Namen eingeben.";
            return;
        }
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
