using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using LifestyleDialog = Chummer.NewUI.Dialogs.LifestyleDialog;
using AdvancedLifestyleDialog = Chummer.NewUI.Dialogs.AdvancedLifestyleDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    private async void OnAddLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var dialog = new LifestyleDialog(_character);
        if (await dialog.ShowDialog<bool>(window) && dialog.SelectedLifestyle != null)
        {
            var lifestyle = dialog.SelectedLifestyle;
            _character.AddLifestyle(lifestyle.Name, lifestyle.Cost, "1");
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddAdvancedLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new AdvancedLifestyleDialog(_character);
        if (await dialog.ShowDialog<bool>(window))
        {
            _character.AddAdvancedLifestyle(dialog.LifestyleName, dialog.Comforts, dialog.Entertainment, dialog.Necessities,
                dialog.Neighborhood, dialog.Security, dialog.Roommates, dialog.Percentage, dialog.PositiveQualities,
                dialog.NegativeQualities);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnDeleteLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedLifestyle == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteLifestyle"))
            return;
        if (_character.RemoveLifestyle(ViewModel.SelectedLifestyle.LifestyleId)) ViewModel.LoadCharacter(_character);
    }

    // Ported from frmCreate.cs's per-collection Copy/Paste menu commands (mnuEditCopy_Click/
    // mnuEditPaste_Click): the shared, process-wide CharacterClipboard.Instance lets a Lifestyle/
    // Armor/Weapon/Gear item copied from one open character be pasted into any other.
    private void OnCopyLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && ViewModel.SelectedLifestyle is { } lifestyle)
            _character.CopyLifestyle(lifestyle.LifestyleId);
    }

    private void OnPasteLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character != null && _character.PasteLifestyle())
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditLifestyleNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedLifestyle is not { } lifestyle
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = lifestyle.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetLifestyleNotes(lifestyle.LifestyleId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }
}
