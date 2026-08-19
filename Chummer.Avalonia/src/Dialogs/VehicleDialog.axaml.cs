using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class VehicleDialog : Window
{
    public VehicleDialogViewModel ViewModel { get; } = new();
    public VehicleOptionViewModel? SelectedVehicle => ViewModel.SelectedVehicle;
    public VehicleDialog() : this(null) { }
    public VehicleDialog(CharacterDocument? character) { DataContext = ViewModel; InitializeComponent(); ViewModel.LoadOptions(character); }
    private void OnOk(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedVehicle != null) Close(true); }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
