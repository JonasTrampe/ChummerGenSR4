using System;
using System.Linq;
using Avalonia.Controls;
using Chummer.Core;
using Chummer.NewUI.Controls;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GeneralSectionTab
{
    private async void OnRaiseAttributeClick(object? sender, EventArgs e)
    {
        if (_character == null || sender is not AttributeRow { Code: { } strCode }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        CharacterAttributeData? attribute = _character.Attributes.FirstOrDefault(a => a.Code == strCode);
        if (attribute == null || !int.TryParse(attribute.Value, out int intCurrentValue))
            return;

        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpense"), strCode,
            intCurrentValue + 1, attribute.KarmaCostToIncrease);
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (_character.RaiseAttribute(strCode))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Only wired up for EDG (see AttributeRow.ShowRemove) - ported from
    /// frmCareer.cs's cmdBurnEdge_Click: an unconditional (not gated by ConfirmDelete/
    /// ConfirmKarmaExpense) Yes/No prompt before the irreversible burn, since legacy always asks
    /// regardless of the character's confirmation settings.</summary>
    private async void OnRemoveAttributeClick(object? sender, EventArgs e)
    {
        if (_character == null || sender is not AttributeRow || TopLevel.GetTopLevel(this) is not Window window)
            return;

        CharacterAttributeData? edge = _character.Attributes.FirstOrDefault(a => a.Code == "EDG");
        if (edge == null || !int.TryParse(edge.Value, out int intCurrentEdge) || intCurrentEdge <= 0)
        {
            var cannotDialog = new MessageBoxDialog(App.LanguageCatalog.GetString("MessageTitle_CannotBurnEdge"),
                App.LanguageCatalog.GetString("Message_CannotBurnEdge"));
            await cannotDialog.ShowDialog(window);
            return;
        }

        var dialog = new ConfirmationDialog(App.LanguageCatalog.GetString("MessageTitle_BurnEdge"),
            App.LanguageCatalog.GetString("Message_BurnEdge"));
        if (!await dialog.ShowDialog<bool>(window))
            return;

        if (_character.BurnEdge())
            ViewModel.LoadCharacter(_character);
    }
}
