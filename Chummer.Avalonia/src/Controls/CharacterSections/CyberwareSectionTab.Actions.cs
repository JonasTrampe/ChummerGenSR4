using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using CyberwareDialog = Chummer.NewUI.Dialogs.CyberwareDialog;
using ContactNotesDialog = Chummer.NewUI.Dialogs.ContactNotesDialog;
using ListSelectionDialog = Chummer.NewUI.Dialogs.ListSelectionDialog;
using SellItemDialog = Chummer.NewUI.Dialogs.SellItemDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CyberwareSectionTab
{
    private async void OnAddCyberwareClick(object? sender, RoutedEventArgs e) => await AddAsync(blnBioware: false);

    private async void OnAddBiowareClick(object? sender, RoutedEventArgs e) => await AddAsync(blnBioware: true);

    private async void OnAddCyberwareSuiteClick(object? sender, RoutedEventArgs e) => await AddSuiteAsync(blnBioware: false);

    private async void OnAddBiowareSuiteClick(object? sender, RoutedEventArgs e) => await AddSuiteAsync(blnBioware: true);

    private async Task AddSuiteAsync(bool blnBioware)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var lstSuites = _character.GetCyberwareSuiteNames(blnBioware);
        if (lstSuites.Count == 0)
            return;

        var dialog = new ListSelectionDialog(blnBioware ? "Bioware-Suite auswählen:" : "Cyberware-Suite auswählen:",
            lstSuites);
        if (await dialog.ShowDialog<bool>(window) && dialog.SelectedValue != null
            && _character.AddCyberwareSuite(dialog.SelectedValue, blnBioware))
            ViewModel.LoadCharacter(_character);
    }

    private async Task AddAsync(bool blnBioware)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CyberwareDialog(_character, blnBioware);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedCyberware != null)
        {
            var item = dialog.SelectedCyberware;
            var viewModel = dialog.ViewModel;

            string strSide = string.Empty;
            if (_character.CyberwareRequiresSideSelection(item.Name, blnBioware))
            {
                var sideDialog = new ListSelectionDialog($"„{item.Name}“ - Seite auswählen:",
                    new[] { "Left", "Right" });
                if (!await sideDialog.ShowDialog<bool>(window) || sideDialog.SelectedValue == null)
                    return;
                strSide = sideDialog.SelectedValue;
            }

            string strSkillGroup = string.Empty;
            if (_character.CyberwareRequiresSkillGroupSelection(item.Name, blnBioware))
            {
                var groupDialog = new ListSelectionDialog($"„{item.Name}“ - Fertigkeitsgruppe auswählen:",
                    _character.GetCyberwareSkillGroupOptions(item.Name, blnBioware));
                if (!await groupDialog.ShowDialog<bool>(window) || groupDialog.SelectedValue == null)
                    return;
                strSkillGroup = groupDialog.SelectedValue;
            }

            _character.AddCyberware(item.Name, item.Category, item.Rating, viewModel.FinalEssence,
                viewModel.FinalCost, viewModel.FinalAvailability, item.SourcePage, string.Empty,
                viewModel.SelectedGrade?.Name ?? "Standard", blnBioware: blnBioware, strSide: strSide,
                strSelectedSkillGroup: strSkillGroup, blnTransgenic: viewModel.IsTransgenic);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnCyberwareTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            OnDeleteClick(sender, e);
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        // Roots is CyberwareRoot/BiowareRoot, two synthetic header nodes - actual saved items are
        // their immediate children, so only allow deleting a node one level under one of those
        // (deeper nesting would be an installed mod, not deletable this way yet).
        if (_character == null || ViewModel.SelectedNode is not { Parent: { Parent: null } parent } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        bool blnBioware = ReferenceEquals(parent, ViewModel.BiowareRoot);
        if (!await DeleteConfirmation.ConfirmAsync(window, _character,
                blnBioware ? "Message_DeleteBioware" : "Message_DeleteCyberware"))
            return;
        if (_character.RemoveCyberware(node.SourceName, node.Category, node.Rating, blnBioware))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedNode is not { CyberwareId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = node.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetCyberwareNotes(node.CyberwareId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedNode is not { Parent: { Parent: null } parent } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        bool blnBioware = ReferenceEquals(parent, ViewModel.BiowareRoot);
        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window)
            && _character.SellCyberware(node.SourceName, node.Category, node.Rating, dialog.SellPercent, blnBioware))
            ViewModel.LoadCharacter(_character);
    }

    // Ported from frmCareer.cs's per-collection Copy/Paste menu commands: which of the two
    // synthetic root nodes the selection belongs to decides Cyberware vs. Bioware content type,
    // same as OnDeleteClick/OnSellClick.
    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedNode is not { Parent: { Parent: null } parent, CyberwareId: >= 0 } node)
            return;
        _character.CopyCyberware(node.CyberwareId, ReferenceEquals(parent, ViewModel.BiowareRoot));
    }

    private void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;
        bool blnBioware = CharacterClipboard.Instance.Item?.ContentType == ClipboardContentType.Bioware;
        if (_character.PasteCyberware(blnBioware))
            ViewModel.LoadCharacter(_character);
    }
}
