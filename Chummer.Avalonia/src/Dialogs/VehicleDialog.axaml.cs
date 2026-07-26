using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class VehicleDialog : Window
{
    public VehicleDialogViewModel ViewModel { get; } = new();
    public VehicleOptionViewModel? SelectedVehicle => ViewModel.SelectedVehicle;
    public VehicleDialog() { DataContext = ViewModel; InitializeComponent(); ViewModel.LoadOptions(); }
    private void OnOk(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedVehicle != null) Close(true); }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
