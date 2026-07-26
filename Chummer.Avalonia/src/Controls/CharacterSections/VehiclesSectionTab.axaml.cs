using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class VehiclesSectionTab : UserControl
{
    public VehiclesSectionViewModel ViewModel { get; } = new();
    private CharacterDocument? _character;

    public VehiclesSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddVehicleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        var dialog = new VehicleDialog();
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedVehicle == null)
            return;
        var vehicle = dialog.SelectedVehicle;
        _character.AddVehicle(vehicle.Name, vehicle.Category, vehicle.Handling, vehicle.Acceleration, vehicle.Speed,
            vehicle.Pilot, vehicle.Body, vehicle.Armor, vehicle.Sensor, vehicle.DeviceRating, vehicle.Availability,
            vehicle.Cost, vehicle.Source, vehicle.Page);
        ViewModel.LoadCharacter(_character);
    }

    private void OnDeleteVehicleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: null } vehicle)
            return;
        if (_character.RemoveVehicle(vehicle.Name, vehicle.Category))
            ViewModel.LoadCharacter(_character);
    }

    private void OnDamageVehicleClick(object? sender, RoutedEventArgs e) => AdjustSelectedVehicleDamage(1);
    private void OnRepairVehicleClick(object? sender, RoutedEventArgs e) => AdjustSelectedVehicleDamage(-1);

    private void AdjustSelectedVehicleDamage(int delta)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: null } vehicle) return;
        if (_character.AdjustVehicleDamage(vehicle.Name, vehicle.Category, delta)) ViewModel.LoadCharacter(_character);
    }
}
