using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;
using SpellDialog = Chummer.NewUI.Dialogs.SpellDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab : UserControl
{
    public SpellsSectionViewModel ViewModel { get; } = new();

    public SpellsSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private CharacterDocument? _character;

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpellDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedSpell != null)
        {
            var spell = dialog.SelectedSpell;
            if (_character.AddSpell(spell.Name, spell.Category, spell.Type, spell.Range, spell.Damage, spell.Duration,
                spell.DrainValue, spell.Source, spell.Page, dialog.Extended))
                ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnCreateSpellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CreateSpellDialog(_character);
        if (await dialog.ShowDialog<bool>(window)
            && _character.AddCustomSpell(dialog.ResultName, dialog.ResultCategory, dialog.ResultType,
                dialog.ResultRange, dialog.ResultArea, dialog.ResultRestricted, dialog.ResultVeryRestricted,
                dialog.ResultDuration, dialog.ResultCheckedKeys, dialog.ResultNumberOfEffects))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteSpellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedSpellNode?.Parent == null
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteSpell"))
            return;
        if (_character.RemoveSpell(ViewModel.SelectedSpellNode.Name))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditSpellNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedSpellNode is not { Parent: not null, SpellId: >= 0 } spell
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = spell.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetSpellNotes(spell.SpellId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddSpiritClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpiritDialog(_character.MaxSpiritForce);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true)
        {
            _character.AddSpirit(dialog.SpiritName, dialog.CritterName, dialog.Type, dialog.Force, dialog.Services);
            ViewModel.LoadCharacter(_character);
        }
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

        if (_character.AddComplexForm(selected.Name, selected.Category, selected.Source, selected.Page, strExtra))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Shared selecttext/selectskill/selectattribute prompt for CritterPower/ComplexForm
    /// adds - ported from clsImprovement.cs's respective handlers, same pattern as
    /// GeneralSectionTab's Quality flow.</summary>
    private static async System.Threading.Tasks.Task<(bool Proceed, string Extra)> CollectSelectionAsync(
        Window window, string strItemName, bool blnRequiresText,
        System.Collections.Generic.IReadOnlyList<string> lstSkillOptions,
        System.Collections.Generic.IReadOnlyList<string> lstAttributeOptions)
    {
        if (blnRequiresText)
        {
            var textDialog = new TextSelectionDialog($"„{strItemName}“ benötigt eine Detailangabe:");
            return await textDialog.ShowDialog<bool>(window)
                ? (true, textDialog.EnteredText)
                : (false, string.Empty);
        }

        if (lstSkillOptions.Count > 0)
        {
            var listDialog = new ListSelectionDialog($"„{strItemName}“ - Fertigkeit auswählen:", lstSkillOptions);
            return await listDialog.ShowDialog<bool>(window) && listDialog.SelectedValue != null
                ? (true, listDialog.SelectedValue)
                : (false, string.Empty);
        }

        if (lstAttributeOptions.Count > 0)
        {
            var listDialog = new ListSelectionDialog($"„{strItemName}“ - Attribut auswählen:", lstAttributeOptions);
            return await listDialog.ShowDialog<bool>(window) && listDialog.SelectedValue != null
                ? (true, listDialog.SelectedValue)
                : (false, string.Empty);
        }

        return (true, string.Empty);
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
