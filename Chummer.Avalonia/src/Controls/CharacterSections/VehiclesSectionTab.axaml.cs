using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Globalization;
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
        var dialog = new VehicleDialog(_character);
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
        if (_character == null || ViewModel.SelectedVehicle is not { } vehicle)
            return;
        if (vehicle.Parent == null && Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)
            && _character.RemoveVehicle(guiVehicleId))
            ViewModel.LoadCharacter(_character);
        else if (vehicle.Parent is { Parent: null } vehicleRoot
            && Guid.TryParse(vehicleRoot.VehicleGuid, out guiVehicleId)
            && Guid.TryParse(vehicle.ItemGuid, out Guid guiItemId)
            && (_character.RemoveVehicleMod(guiVehicleId, guiItemId)
                || _character.RemoveVehicleGear(guiVehicleId, guiItemId)
                || _character.RemoveVehicleWeapon(guiVehicleId, guiItemId)))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddVehicleModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedVehicle is not { } selected)
            return;

        TreeNodeViewModel vehicle = selected.Parent == null ? selected : selected.Parent;
        if (!Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId))
            return;

        var dialog = new VehicleModDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedMod == null)
            return;

        VehicleModOptionViewModel mod = dialog.SelectedMod;
        if (_character.AddVehicleMod(guiVehicleId, mod.Name, mod.Category,
                dialog.Rating.ToString(CultureInfo.InvariantCulture), mod.Slots, mod.Availability,
                mod.Cost, mod.Source, mod.Page, mod.Limit))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddVehicleWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedVehicle is not { } selected)
            return;
        TreeNodeViewModel vehicle = selected.Parent == null ? selected : selected.Parent;
        if (!Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)) return;

        var dialog = new WeaponDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedWeapon is not { } weapon) return;
        if (_character.AddVehicleWeapon(guiVehicleId, weapon.Name, weapon.Category, weapon.Damage, weapon.Ap,
                weapon.Mode, weapon.Rc, weapon.Ammo, weapon.Cost, weapon.Availability, weapon.Source, weapon.Page))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddVehicleLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedVehicle is not { Parent: null } vehicle
            || !Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)) return;
        var dialog = new ArmorSetDialog { Title = "Fahrzeugort hinzufügen" };
        if (await dialog.ShowDialog<bool>(window) && _character.AddVehicleLocation(guiVehicleId, dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private void OnRemoveVehicleLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: null } vehicle
            || !Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)
            || string.IsNullOrWhiteSpace(ViewModel.SelectedVehicleLocation)) return;
        if (_character.RemoveVehicleLocation(guiVehicleId, ViewModel.SelectedVehicleLocation))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddVehicleGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedVehicle is not { } selected)
            return;
        TreeNodeViewModel vehicle = selected.Parent == null ? selected : selected.Parent;
        if (!Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)) return;

        var dialog = new GearDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedGear is not { } gear) return;
        if (_character.AddVehicleGear(guiVehicleId, gear.SourceName, gear.Category, gear.Rating,
                gear.Quantity.ToString(), gear.Cost, gear.Availability, gear.Source, gear.Page, gear.Capacity,
                gear.Response, gear.Signal, gear.SystemRating, gear.Firewall))
            ViewModel.LoadCharacter(_character);
    }

    private void OnAssignVehicleGearLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: { } vehicle } selected
            || !Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)
            || !Guid.TryParse(selected.ItemGuid, out Guid guiGearId))
            return;
        if (_character.AssignVehicleGearLocation(guiVehicleId, guiGearId, ViewModel.SelectedVehicleLocation ?? string.Empty))
            ViewModel.LoadCharacter(_character);
    }

    private void OnDamageVehicleClick(object? sender, RoutedEventArgs e) => AdjustSelectedVehicleDamage(1);
    private void OnRepairVehicleClick(object? sender, RoutedEventArgs e) => AdjustSelectedVehicleDamage(-1);

    private void AdjustSelectedVehicleDamage(int delta)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: null } vehicle
            || !Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)) return;
        if (_character.AdjustVehicleDamage(guiVehicleId, delta)) ViewModel.LoadCharacter(_character);
    }
}
