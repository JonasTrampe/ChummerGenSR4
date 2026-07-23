using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class ArmorDialog : Window
{
    public ArmorDialogViewModel ViewModel { get; } = new();
    public ArmorOptionViewModel? SelectedArmor => ViewModel.SelectedArmor;

    public ArmorDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadOptions();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedArmor != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
