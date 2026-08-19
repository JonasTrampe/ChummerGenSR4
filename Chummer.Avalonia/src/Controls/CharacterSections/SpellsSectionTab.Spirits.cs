using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab
{
    private async void OnAddSpiritClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpiritDialog(_character.MaxSpiritForce);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true
            && _character.AddSpirit(dialog.SpiritName, dialog.CritterName, dialog.Type, dialog.Force, dialog.Services))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteSpiritClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedSpirit is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character,
                selected.Type == "Sprite" ? "Message_DeleteSprite" : "Message_DeleteSpirit"))
            return;
        if (_character.RemoveSpirit(selected.Name, selected.Type, selected.Force))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditSpiritNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedSpirit is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetSpiritNotes(selected.SpiritId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }
}
