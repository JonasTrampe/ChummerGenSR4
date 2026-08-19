using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class VehiclesSectionTab
{
    private async void OnReloadVehicleWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: not null } weapon
            || !Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ReloadDialog(_character, guiWeaponId);
        if (await dialog.ShowDialog<bool>(window)
            && _character.ReloadWeapon(guiWeaponId, dialog.SelectedAmmoGearId, dialog.SelectedCount))
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
        var dialog = new ArmorSetDialog { Title = App.LanguageCatalog.GetString("UI_AddVehicleLocationTitle") };
        if (await dialog.ShowDialog<bool>(window) && _character.AddVehicleLocation(guiVehicleId, dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRemoveVehicleLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: null } vehicle
            || !Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)
            || string.IsNullOrWhiteSpace(ViewModel.SelectedVehicleLocation)
            || TopLevel.GetTopLevel(this) is not Window window) return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteVehicleLocation"))
            return;
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
}
