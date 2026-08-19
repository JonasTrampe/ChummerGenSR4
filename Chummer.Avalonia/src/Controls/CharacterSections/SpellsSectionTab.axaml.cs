using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;
using ListSelectionDialog = Chummer.NewUI.Dialogs.ListSelectionDialog;
using TextSelectionDialog = Chummer.NewUI.Dialogs.TextSelectionDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

// Split by concern into partial-class files: .Spells.cs, .Spirits.cs, .ComplexForms.cs,
// .CritterPowers.cs. This file keeps the shared construction/load surface plus
// CollectSelectionAsync, the selecttext/selectskill/selectattribute prompt helper used by both
// ComplexForms and CritterPowers.
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

    /// <summary>Shared selecttext/selectskill/selectattribute prompt for CritterPower/ComplexForm
    /// adds - ported from clsImprovement.cs's respective handlers, same pattern as
    /// GeneralSectionTab's Quality flow.</summary>
    private static async Task<(bool Proceed, string Extra)> CollectSelectionAsync(
        Window window, string strItemName, bool blnRequiresText,
        IReadOnlyList<string> lstSkillOptions,
        IReadOnlyList<string> lstAttributeOptions)
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
}
