using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using GearDialog = Chummer.NewUI.Dialogs.GearDialog;
using NexusDialog = Chummer.NewUI.Dialogs.NexusDialog;
using ArmorSetDialog = Chummer.NewUI.Dialogs.ArmorSetDialog;
using SellItemDialog = Chummer.NewUI.Dialogs.SellItemDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    private async void OnAddGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        bool continueAdding;
        do
        {
            var dialog = new GearDialog(_character);
            bool added = await dialog.ShowDialog<bool>(window);
            if (added && dialog.SelectedGear != null)
            {
                var gear = dialog.SelectedGear;
                _character.AddGear(gear.SourceName, gear.Category, gear.Rating, gear.Quantity.ToString(),
                    gear.Cost, gear.Availability, gear.SourcePage, string.Empty,
                    gear.Capacity, gear.Response, gear.Signal, gear.SystemRating, gear.Firewall);
                // Ignoring the return value here is intentional: a rejected Stick-n-Shock pickup
                // is a rare, self-explanatory (no owned eligible weapon) house-rule edge case, not
                // worth a dedicated error dialog for - matches this port's existing convention of
                // silently no-op'ing rejected adds elsewhere (e.g. weapon accessory mounts).
            }
            continueAdding = added && dialog.ContinueAdding;
        } while (continueAdding);

        ViewModel.LoadCharacter(_character);
    }

    private async void OnAddNexusClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new NexusDialog();
        if (await dialog.ShowDialog<bool>(window))
        {
            _character.AddNexus(dialog.Processor, dialog.Response, dialog.System, dialog.Firewall,
                dialog.Signal, dialog.Persona, dialog.Free);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddGearLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var dialog = new ArmorSetDialog { Title = App.LanguageCatalog.GetString("UI_AddGearLocationTitle") };
        if (await dialog.ShowDialog<bool>(window) && _character.AddGearLocation(dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRemoveGearLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { Category: "Gear location" } location
            || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteGearLocation"))
            return;
        if (_character.RemoveGearLocation(location.Name))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Adds gear nested under the currently selected gear item (e.g. a Certified
    /// Credstick under a Commlink) instead of at the root of the Ausrüstung tree.</summary>
    private async void OnAddChildGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedGear is not { GearId: >= 0 } parent)
            return;

        bool continueAdding;
        do
        {
            var dialog = new GearDialog(_character);
            bool added = await dialog.ShowDialog<bool>(window);
            if (added && dialog.SelectedGear != null)
            {
                var gear = dialog.SelectedGear;
                _character.AddChildGear(parent.GearId, gear.SourceName, gear.Category, gear.Rating, gear.Quantity.ToString(),
                    gear.Cost, gear.Availability, gear.SourcePage, string.Empty,
                    gear.Capacity, gear.Response, gear.Signal, gear.SystemRating, gear.Firewall);
            }
            continueAdding = added && dialog.ContinueAdding;
        } while (continueAdding);

        ViewModel.LoadCharacter(_character);
    }

    private void OnGearTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            OnDeleteGearClick(sender, e);
    }

    private async void OnDeleteGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteGear"))
            return;
        if (_character.RemoveGear(node.GearId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditGearNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = node.Notes };
        if (await dialog.ShowDialog<bool>(window) && _character.SetGearNotes(node.GearId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRenameGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new TextSelectionDialog("Name für „" + node.TranslatedName + "“:", node.CustomName,
            blnAllowEmpty: true);
        if (await dialog.ShowDialog<bool>(window) && _character.SetGearCustomName(node.GearId, dialog.EnteredText))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellGear(node.GearId, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    private void OnCopyGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && ViewModel.SelectedGear is { GearId: >= 0 } node)
            _character.CopyGear(node.GearId);
    }

    private void OnPasteGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && _character.PasteGear())
            ViewModel.LoadCharacter(_character);
    }

    private void OnToggleGearEquippedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node || sender is not CheckBox checkBox)
            return;

        if (_character.SetGearEquipped(node.GearId, checkBox.IsChecked == true))
            ViewModel.LoadCharacter(_character);
    }
}
