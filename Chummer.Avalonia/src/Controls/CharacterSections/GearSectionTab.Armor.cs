using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;
using Chummer.NewUI.Dialogs;
using ArmorDialog = Chummer.NewUI.Dialogs.ArmorDialog;
using ArmorModDialog = Chummer.NewUI.Dialogs.ArmorModDialog;
using ArmorSetDialog = Chummer.NewUI.Dialogs.ArmorSetDialog;
using SellItemDialog = Chummer.NewUI.Dialogs.SellItemDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    private async void OnAddArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ArmorDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedArmor != null)
        {
            var armor = dialog.SelectedArmor;
            _character.AddArmor(armor.Name, armor.Category, armor.Ballistic, armor.Impact, armor.Capacity,
                armor.Cost, armor.Availability, armor.SourcePage, string.Empty);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnRenameArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { ArmorId: >= 0 } armor
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new TextSelectionDialog("Name für „" + armor.Name + "“:", armor.CustomName,
            blnAllowEmpty: true);
        if (await dialog.ShowDialog<bool>(window)
            && _character.SetArmorCustomName(armor.ArmorId, dialog.EnteredText))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditArmorNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        TreeNodeViewModel? armor = selected.ArmorId >= 0 ? selected : selected.Parent;
        if (armor is not { ArmorId: >= 0 })
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (!await dialog.ShowDialog<bool>(window))
            return;

        bool saved = selected.ArmorId >= 0
            ? _character.SetArmorNotes(selected.ArmorId, dialog.Notes)
            : System.Guid.TryParse(selected.ItemGuid, out System.Guid itemGuid)
                && _character.SetArmorChildNotes(armor.ArmorId, itemGuid, dialog.Notes);
        if (saved)
            ViewModel.LoadCharacter(_character);
    }

    private void OnArmorTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            OnDeleteArmorClick(sender, e);
    }

    private async void OnDeleteArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (ViewModel.SelectedArmor.Category == "Armor set")
        {
            if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmorLocation"))
                return;
            if (_character.RemoveArmorSet(ViewModel.SelectedArmor.Name)) ViewModel.LoadCharacter(_character);
            return;
        }
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmor"))
            return;
        if (_character.RemoveArmor(ViewModel.SelectedArmor.SourceName, ViewModel.SelectedArmor.Category))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { } selected || selected.Category == "Armor set"
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellArmor(selected.SourceName, selected.Category, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    private void OnCopyArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && ViewModel.SelectedArmor is { ArmorId: >= 0 } armor)
            _character.CopyArmor(armor.ArmorId);
    }

    private void OnPasteArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && _character.PasteArmor())
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Adds an Armor Modification under whichever Armor is selected - if the selection is
    /// itself one of that Armor's own children (a previously-added mod/gear), walks up to the
    /// parent Armor first, same as how legacy always operates on the owning Armor regardless of
    /// which of its rows is focused.</summary>
    private async void OnAddArmorModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        TreeNodeViewModel? armorNode = ViewModel.SelectedArmor;
        while (armorNode?.Parent is { Category: not "Armor set" })
            armorNode = armorNode.Parent;
        if (armorNode == null || armorNode.Category == "Armor set")
            return;

        var dialog = new ArmorModDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedMod != null)
        {
            var mod = dialog.SelectedMod;
            if (_character.AddArmorMod(armorNode.SourceName, armorNode.Category, mod.Name,
                    dialog.Rating.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    mod.Ballistic, mod.Impact, mod.Availability, mod.Cost, mod.Source, mod.Page))
                ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnDeleteArmorModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { Parent.Category: not "Armor set" } mod
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmor"))
            return;
        if (_character.RemoveArmorMod(mod.SourceName))
            ViewModel.LoadCharacter(_character);
    }

    private void OnArmorBallisticDamageClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(1, 0);
    private void OnArmorBallisticRepairClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(-1, 0);
    private void OnArmorImpactDamageClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(0, 1);
    private void OnArmorImpactRepairClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(0, -1);

    private void AdjustSelectedArmorDegradation(int intBallisticDelta, int intImpactDelta)
    {
        if (_character == null || ViewModel.SelectedArmor is not { Category: not "Armor set" } armor)
            return;
        if (_character.AdjustArmorDegradation(armor.SourceName, armor.Category, intBallisticDelta, intImpactDelta))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddArmorSetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        var dialog = new ArmorSetDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.AddArmorSet(dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRemoveArmorSetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { Category: "Armor set" } armorSet
            || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmorLocation"))
            return;
        if (_character.RemoveArmorSet(armorSet.Name)) ViewModel.LoadCharacter(_character);
    }

    private void OnToggleArmorEquippedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor == null || sender is not CheckBox checkBox)
            return;

        bool blnEquipped = checkBox.IsChecked == true;
        if (_character.SetArmorEquipped(ViewModel.SelectedArmor.SourceName, ViewModel.SelectedArmor.Category, blnEquipped))
            ViewModel.LoadCharacter(_character);
    }
}
