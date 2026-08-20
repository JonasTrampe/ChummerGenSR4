using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class VehiclesSectionTab
{
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

    private async void OnSellVehicleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { Parent: null } vehicle
            || !Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellVehicle(guiVehicleId, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    private void OnCopyVehicleClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && ViewModel.SelectedVehicle is { Parent: null } vehicle
            && Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId))
            _character.CopyVehicle(guiVehicleId);
    }

    private void OnPasteVehicleClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && _character.PasteVehicle())
            ViewModel.LoadCharacter(_character);
    }

    private void OnVehiclesTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            OnDeleteVehicleClick(sender, e);
    }

    private async void OnDeleteVehicleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { } vehicle
            || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteVehicle"))
            return;
        if (vehicle.Parent == null && Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId)
            && _character.RemoveVehicle(guiVehicleId))
            ViewModel.LoadCharacter(_character);
        else if (vehicle.Parent is { Parent: null } vehicleRoot
            && Guid.TryParse(vehicleRoot.VehicleGuid, out guiVehicleId)
            && Guid.TryParse(vehicle.ItemGuid, out Guid guiItemId))
        {
            bool blnIsRetrofitCandidate = string.Equals(vehicle.Name, "Obsolete", StringComparison.Ordinal)
                || (string.Equals(vehicle.Name, "Obsolescent", StringComparison.Ordinal)
                    && _character.AllowObsolescentUpgradeEnabled);
            if (blnIsRetrofitCandidate)
            {
                var dialog = new RetrofitDialog();
                if (await dialog.ShowDialog<bool>(window)
                    && _character.RetrofitVehicleObsolescence(guiVehicleId, guiItemId, dialog.Percentage))
                    ViewModel.LoadCharacter(_character);
                return;
            }

            if (_character.RemoveVehicleMod(guiVehicleId, guiItemId)
                || _character.RemoveVehicleGear(guiVehicleId, guiItemId)
                || _character.RemoveVehicleWeapon(guiVehicleId, guiItemId))
                ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnEditVehicleNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedVehicle is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        TreeNodeViewModel vehicle = selected;
        while (vehicle.Parent != null)
            vehicle = vehicle.Parent;
        if (!Guid.TryParse(vehicle.VehicleGuid, out Guid guiVehicleId))
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (await dialog.ShowDialog<bool?>(window) != true)
            return;

        bool blnSaved = selected.Parent == null
            ? _character.SetVehicleNotes(guiVehicleId, dialog.Notes)
            : Guid.TryParse(selected.ItemGuid, out Guid guiItemId)
                && _character.SetVehicleItemNotes(guiVehicleId, guiItemId, dialog.Notes);
        if (blnSaved)
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
