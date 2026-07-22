using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class GearDialog : Window
{
    public GearDialogViewModel ViewModel { get; } = new();
    public GearOptionViewModel? SelectedGear => ViewModel.SelectedGear;

    public GearDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadOptions();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGear != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
