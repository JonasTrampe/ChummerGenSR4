using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    /// <summary>Ported from frmCareer.cs's treFoci check-handler (normal-Focus branch): binding
    /// costs Rating*per-type-Karma-multiplier Karma and confirms via Message_ConfirmKarmaExpenseFocus.
    /// The selected Gear tree node's own guid is a Focus/Metamagic Focus Gear item's guid.</summary>
    private async void OnBindFocusClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { } node
            || !Guid.TryParse(node.ItemGuid, out Guid guiGearId)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        int? intKarmaCost = _character.GetFocusBindingKarmaCost(guiGearId);
        if (intKarmaCost is not > 0)
            return;

        string strMessage = App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseFocus");
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (_character.BindFocus(guiGearId))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Ported from frmCareer.cs's treFoci uncheck-handler: unbinding grants no Karma
    /// refund, matching legacy.</summary>
    private async void OnUnbindFocusClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { } node
            || !Guid.TryParse(node.ItemGuid, out Guid guiGearId)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        System.Collections.Generic.IReadOnlyList<CharacterFocusData> lstFoci = _character.Foci;
        CharacterFocusData? objFocus = null;
        foreach (CharacterFocusData objCandidate in lstFoci)
        {
            if (Guid.TryParse(objCandidate.GearId, out Guid guiCandidateGearId) && guiCandidateGearId == guiGearId)
            {
                objFocus = objCandidate;
                break;
            }
        }
        if (objFocus == null || !Guid.TryParse(objFocus.Guid, out Guid guiFocusId))
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_UnbindFocus"))
            return;

        if (_character.UnbindFocus(guiFocusId))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Ported from frmCareer.cs's treFoci check-handler (Stacked Focus branch): binding
    /// costs the sum of each component's Rating*multiplier. The selected Gear tree node is the
    /// stack's composite "Stacked Focus" Gear item, whose guid matches
    /// CharacterStackedFocusData.GearId.</summary>
    private async void OnBindStackedFocusClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        CharacterStackedFocusData? objStack = FindStackedFocusByCompositeGearGuid(node.ItemGuid);
        if (objStack == null || !Guid.TryParse(objStack.Guid, out Guid guiStackId))
            return;

        int? intKarmaCost = _character.GetStackedFocusBindingKarmaCost(guiStackId);
        if (intKarmaCost is not > 0)
            return;

        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseFocus"),
            objStack.DisplayName, intKarmaCost);
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (_character.BindStackedFocus(guiStackId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnUnbindStackedFocusClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        CharacterStackedFocusData? objStack = FindStackedFocusByCompositeGearGuid(node.ItemGuid);
        if (objStack == null || !Guid.TryParse(objStack.Guid, out Guid guiStackId))
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_UnbindFocus"))
            return;

        if (_character.UnbindStackedFocus(guiStackId))
            ViewModel.LoadCharacter(_character);
    }

    private CharacterStackedFocusData? FindStackedFocusByCompositeGearGuid(string strCompositeGearGuid)
    {
        if (_character == null || string.IsNullOrEmpty(strCompositeGearGuid))
            return null;
        foreach (CharacterStackedFocusData objStack in _character.StackedFoci)
        {
            if (string.Equals(objStack.GearId, strCompositeGearGuid, StringComparison.OrdinalIgnoreCase))
                return objStack;
        }
        return null;
    }
}
