using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab
{
    private async void OnAddCritterPowerClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CritterPowerDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedPower is not { } selected)
            return;

        var (blnProceed, strExtra) = await CollectSelectionAsync(window, selected.Name,
            _character.CritterPowerRequiresTextSelection(selected.Name),
            _character.GetCritterPowerSkillSelectionOptions(selected.Name),
            _character.GetCritterPowerAttributeSelectionOptions(selected.Name));
        if (!blnProceed)
            return;

        _character.AddCritterPower(selected.Name, selected.Points, selected.Source, selected.Page, strExtra,
            dialog.SelectedRating);
        ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteCritterPowerClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedCritterPower is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteCritterPower"))
            return;
        if (_character.RemoveCritterPower(selected.Guid))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditCritterPowerNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedCritterPower is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetCritterPowerNotes(selected.Guid, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }
}
