using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using ListSelectionDialog = Chummer.NewUI.Dialogs.ListSelectionDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab
{
    private async void OnAddComplexFormClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ComplexFormDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedForm is not { } selected)
            return;

        var (blnProceed, strExtra) = await CollectSelectionAsync(window, selected.Name,
            _character.ComplexFormRequiresTextSelection(selected.Name),
            _character.GetComplexFormSkillSelectionOptions(selected.Name),
            Array.Empty<string>());
        if (!blnProceed)
            return;

        // Ported from frmCareer.cs's cmdAddComplexForm_Click: learning a Complex Form after
        // creation costs Karma (category/house-rule dependent) and confirms via
        // Message_ConfirmKarmaExpenseSpend.
        if (_character.Created)
        {
            int intKarmaCost = _character.GetComplexFormCareerKarmaCost(selected.Category);
            string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseSpend"),
                selected.Name, intKarmaCost);
            if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
                return;
        }

        if (_character.AddComplexForm(selected.Name, selected.Category, selected.Source, selected.Page, strExtra))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddComplexFormOptionClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedComplexForm is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var lstChoices = _character.GetComplexFormOptionChoices(selected.Category);
        if (lstChoices.Count == 0)
            return;

        var dialog = new ListSelectionDialog($"„{selected.Label}“ - Programmoption auswählen:", lstChoices);
        if (!await dialog.ShowDialog<bool>(window) || dialog.SelectedValue == null)
            return;

        if (_character.AddComplexFormOption(selected.Guid, dialog.SelectedValue))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteComplexFormClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedComplexForm is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteComplexForm"))
            return;
        if (_character.RemoveComplexForm(selected.Guid))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditComplexFormNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedComplexForm is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetComplexFormNotes(selected.Guid, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }
}
