using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;
using Chummer.NewUI.Dialogs;
using WeaponDialog = Chummer.NewUI.Dialogs.WeaponDialog;
using WeaponAccessoryDialog = Chummer.NewUI.Dialogs.WeaponAccessoryDialog;
using WeaponModDialog = Chummer.NewUI.Dialogs.WeaponModDialog;
using SellItemDialog = Chummer.NewUI.Dialogs.SellItemDialog;
using NaturalWeaponDialog = Chummer.NewUI.Dialogs.NaturalWeaponDialog;
using ReloadDialog = Chummer.NewUI.Dialogs.ReloadDialog;
using ArmorSetDialog = Chummer.NewUI.Dialogs.ArmorSetDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    private async void OnAddWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new WeaponDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedWeapon != null)
        {
            var weapon = dialog.SelectedWeapon;
            _character.AddWeapon(weapon.Name, weapon.Category, weapon.Damage, weapon.Ap, weapon.Mode, weapon.Rc,
                weapon.Ammo, weapon.Cost, weapon.Availability, weapon.SourcePage, string.Empty);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnRenameWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null, WeaponId: >= 0 } weapon
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new TextSelectionDialog("Name für „" + weapon.Name + "“:", weapon.CustomName,
            blnAllowEmpty: true);
        if (await dialog.ShowDialog<bool>(window)
            && _character.SetWeaponCustomName(weapon.WeaponId, dialog.EnteredText))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditWeaponNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { } weapon
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = weapon.Notes };
        if (await dialog.ShowDialog<bool>(window) && SetWeaponNotes(weapon, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    private bool SetWeaponNotes(TreeNodeViewModel weapon, string strNotes)
    {
        if (_character == null)
            return false;
        if (weapon.WeaponId >= 0)
            return _character.SetWeaponNotes(weapon.WeaponId, strNotes);
        return weapon is { Parent: { } parent }
            && Guid.TryParse(parent.ItemGuid, out Guid guiWeaponId)
            && Guid.TryParse(weapon.ItemGuid, out Guid guiPartId)
            && _character.SetWeaponChildNotes(guiWeaponId, guiPartId, strNotes);
    }

    private async void OnDeleteWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteWeapon"))
            return;

        if (selected.Parent == null)
        {
            if (_character.RemoveWeapon(selected.SourceName, selected.Category))
                ViewModel.LoadCharacter(_character);
            return;
        }

        if ((selected.IsWeaponAccessory || selected.IsWeaponMod) && selected.Parent is { } weapon
            && Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId) && Guid.TryParse(selected.ItemGuid, out Guid guiChildId))
        {
            bool removed = selected.IsWeaponAccessory
                ? _character.RemoveWeaponAccessory(guiWeaponId, guiChildId)
                : _character.RemoveWeaponMod(guiWeaponId, guiChildId);
            if (removed)
                ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddNaturalWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new NaturalWeaponDialog(_character);
        if (await dialog.ShowDialog<bool>(window)
            && _character.AddNaturalWeapon(dialog.ResultName, dialog.ResultSkill, dialog.ResultDvBase,
                dialog.ResultDvMod, dialog.ResultDvType, dialog.ResultAp, dialog.ResultReach))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnReloadWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null } selected
            || !Guid.TryParse(selected.ItemGuid, out Guid guiWeaponId)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ReloadDialog(_character, guiWeaponId);
        if (await dialog.ShowDialog<bool>(window)
            && _character.ReloadWeapon(guiWeaponId, dialog.SelectedAmmoGearId, dialog.SelectedCount))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellWeapon(selected.SourceName, selected.Category, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    private void OnCopyWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && ViewModel.SelectedWeapon is { Parent: null, WeaponId: >= 0 } weapon)
            _character.CopyWeapon(weapon.WeaponId);
    }

    private void OnPasteWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && _character.PasteWeapon())
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Resolves the root weapon a "Zubehör/Mod hinzufügen" click applies to: the selected
    /// node itself if it's already a weapon (root-level, or nested under a Weapon location), or its
    /// direct parent if an accessory/mod/gear/ammo child is selected instead.</summary>
    private static TreeNodeViewModel? ResolveWeaponNode(TreeNodeViewModel selected)
    {
        if (selected.Category == "Weapon location")
            return null;
        return selected.Parent == null || selected.Parent.Category == "Weapon location" ? selected : selected.Parent;
    }

    private async void OnAddWeaponAccessoryClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedWeapon is not { } selected || ResolveWeaponNode(selected) is not { } weapon
            || !Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId))
            return;

        var dialog = new WeaponAccessoryDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedAccessory is not { } accessory) return;
        if (_character.AddWeaponAccessory(guiWeaponId, accessory.Name, accessory.Mount, accessory.Rc,
                accessory.Availability, accessory.Cost, accessory.Source, accessory.Page, accessory.RcGroup))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddWeaponModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedWeapon is not { } selected || ResolveWeaponNode(selected) is not { } weapon
            || !Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId))
            return;

        var dialog = new WeaponModDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedMod is not { } mod) return;
        if (_character.AddWeaponMod(guiWeaponId, mod.Name, dialog.Rating.ToString(System.Globalization.CultureInfo.InvariantCulture),
                mod.Slots, mod.Availability, mod.Cost, mod.Source, mod.Page, mod.Rc, mod.RcGroup))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddWeaponLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var dialog = new ArmorSetDialog { Title = App.LanguageCatalog.GetString("UI_AddWeaponLocationTitle") };
        if (await dialog.ShowDialog<bool>(window) && _character.AddWeaponLocation(dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private void OnToggleWeaponEquippedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null } weapon || sender is not CheckBox checkBox)
            return;
        if (_character.SetWeaponEquipped(weapon.SourceName, weapon.Category, checkBox.IsChecked == true))
            ViewModel.LoadCharacter(_character);
    }

    private void OnToggleWeaponPartIncludedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not CheckBox { IsChecked: { } blnIncluded }
            || ViewModel.SelectedWeapon is not { IsWeaponPart: true, Parent: { } parent } part
            || !Guid.TryParse(parent.ItemGuid, out Guid guiWeaponId)
            || !Guid.TryParse(part.ItemGuid, out Guid guiPartId))
            return;

        if (_character.SetWeaponPartIncluded(guiWeaponId, guiPartId, blnIncluded))
            ViewModel.LoadCharacter(_character);
    }
}
