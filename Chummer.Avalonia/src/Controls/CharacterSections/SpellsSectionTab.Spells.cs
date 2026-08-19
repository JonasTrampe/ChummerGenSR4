using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using SpellDialog = Chummer.NewUI.Dialogs.SpellDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab
{
    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpellDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedSpell != null)
        {
            var spell = dialog.SelectedSpell;
            if (!await ConfirmSpellKarmaExpenseAsync(window, spell.Name))
                return;
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
        if (await dialog.ShowDialog<bool>(window) != true)
            return;
        if (!await ConfirmSpellKarmaExpenseAsync(window, dialog.ResultName))
            return;
        if (_character.AddCustomSpell(dialog.ResultName, dialog.ResultCategory, dialog.ResultType,
                dialog.ResultRange, dialog.ResultArea, dialog.ResultRestricted, dialog.ResultVeryRestricted,
                dialog.ResultDuration, dialog.ResultCheckedKeys, dialog.ResultNumberOfEffects))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Ported from frmCareer.cs's cmdAddSpell_Click: learning a Spell after creation
    /// costs a flat KarmaSpell Karma and confirms via Message_ConfirmKarmaExpenseSpend.</summary>
    private async System.Threading.Tasks.Task<bool> ConfirmSpellKarmaExpenseAsync(Window window, string strSpellName)
    {
        if (_character == null || !_character.Created)
            return true;

        int intKarmaCost = _character.GetSpellCareerKarmaCost();
        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseSpend"),
            strSpellName, intKarmaCost);
        return await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage);
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
}
