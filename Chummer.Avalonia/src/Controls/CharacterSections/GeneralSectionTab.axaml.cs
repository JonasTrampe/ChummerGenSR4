using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Controls;
using Chummer.NewUI.ViewModels;
using ContactGroupDialog = Chummer.NewUI.Dialogs.ContactGroupDialog;
using ContactNotesDialog = Chummer.NewUI.Dialogs.ContactNotesDialog;
using QualityDialog = Chummer.NewUI.Dialogs.QualityDialog;
using TextSelectionDialog = Chummer.NewUI.Dialogs.TextSelectionDialog;
using ListSelectionDialog = Chummer.NewUI.Dialogs.ListSelectionDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GeneralSectionTab : UserControl
{
    private CharacterDocument? _character;

    public GeneralSectionViewModel ViewModel { get; } = new();

    public GeneralSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    /// <summary>Ported from clsImprovement.cs's selecttext/selectskill/selectattribute handlers:
    /// some Qualities (e.g. Allergy, Codeslinger, Aptitude, Exceptional Attribute) prompt the
    /// player for a free-text detail, a skill, or an attribute when added. Returns (true, value)
    /// to proceed (value is "" if no prompt was needed), or (false, "") if the player cancelled.</summary>
    private async System.Threading.Tasks.Task<(bool Proceed, string Extra)> CollectQualityExtraAsync(
        Window window, string strQualityName)
    {
        if (_character == null)
            return (true, string.Empty);

        if (_character.QualityRequiresTextSelection(strQualityName))
        {
            var textDialog = new TextSelectionDialog($"„{strQualityName}“ benötigt eine Detailangabe:");
            return await textDialog.ShowDialog<bool>(window)
                ? (true, textDialog.EnteredText)
                : (false, string.Empty);
        }

        var skillOptions = _character.GetQualitySkillSelectionOptions(strQualityName);
        if (skillOptions.Count > 0)
            return await ShowListSelectionAsync(window, $"„{strQualityName}“ - Fertigkeit auswählen:", skillOptions);

        var attributeOptions = _character.GetQualityAttributeSelectionOptions(strQualityName);
        if (attributeOptions.Count > 0)
            return await ShowListSelectionAsync(window, $"„{strQualityName}“ - Attribut auswählen:", attributeOptions);

        return (true, string.Empty);
    }

    private static async System.Threading.Tasks.Task<(bool Proceed, string Extra)> ShowListSelectionAsync(
        Window window, string strDescription, System.Collections.Generic.IReadOnlyList<string> lstOptions)
    {
        var listDialog = new ListSelectionDialog(strDescription, lstOptions);
        return await listDialog.ShowDialog<bool>(window) && listDialog.SelectedValue != null
            ? (true, listDialog.SelectedValue)
            : (false, string.Empty);
    }

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (_character == null)
            return;

        var dialog = new QualityDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added != true || dialog.SelectedQuality is not { } selected)
            return;

        var (blnProceed, strExtra) = await CollectQualityExtraAsync(window, selected.Name);
        if (!blnProceed)
            return;

        _character.AddQuality(selected.Name, selected.Category, strExtra);
        ViewModel.LoadCharacter(_character);
    }

    private void OnDeleteQualityClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedQualityNode?.Parent == null)
            return;

        var quality = ViewModel.SelectedQualityNode;
        if (_character.RemoveQuality(quality.SourceName, quality.Category, quality.Rating))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Swaps the selected Quality for a different one - ported from frmCareer.cs's
    /// cmdSwapQuality_Click, simplified to a plain remove+add: this port's Quality model doesn't
    /// track BP/cost at all yet (AddQuality never deducts Karma either), so legacy's Karma-cost-
    /// delta charge/refund and its Metatype-origin-cannot-be-swapped guard aren't ported - every
    /// owned Quality is swappable here, same scoped-down treatment as Metamagic/Adept
    /// Power/CritterPower/ComplexForm additions not applying their Improvement bonuses.</summary>
    private async void OnSwapQualityClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedQualityNode?.Parent == null)
            return;

        var quality = ViewModel.SelectedQualityNode;
        var dialog = new QualityDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added != true || dialog.SelectedQuality is not { } selected)
            return;

        var (blnProceed, strExtra) = await CollectQualityExtraAsync(window, selected.Name);
        if (!blnProceed)
            return;

        if (_character.RemoveQuality(quality.SourceName, quality.Category, quality.Rating))
        {
            _character.AddQuality(selected.Name, selected.Category, strExtra);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnRaiseAttributeClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not AttributeRow { Code: { } strCode })
            return;

        if (_character.RaiseAttribute(strCode))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Only wired up for EDG (see AttributeRow.ShowRemove) - ported from
    /// frmCareer.cs's cmdBurnEdge_Click.</summary>
    private void OnRemoveAttributeClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not AttributeRow)
            return;

        if (_character.BurnEdge())
            ViewModel.LoadCharacter(_character);
    }

    private void OnAddContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        _character.AddContact("Neue Connection", "1", "1", blnEnemy: false);
        ViewModel.LoadCharacter(_character);
    }

    private void OnAddEnemyClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        _character.AddContact("Neuer Feind", "1", "1", blnEnemy: true);
        ViewModel.LoadCharacter(_character);
    }

    private void OnDeleteContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not Button { Tag: int intContactId })
            return;

        if (_character.RemoveContact(intContactId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditContactNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || sender is not Button { Tag: ContactRowViewModel contact })
            return;

        var dialog = new ContactNotesDialog { Notes = contact.Notes };
        bool? saved = await dialog.ShowDialog<bool?>(window);
        if (saved == true)
            contact.Notes = dialog.Notes;
    }

    private async void OnEditContactGroupClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || sender is not Button { Tag: ContactRowViewModel contact })
            return;

        var dialog = new ContactGroupDialog();
        dialog.ViewModel.LoadFrom(contact);
        bool? saved = await dialog.ShowDialog<bool?>(window);
        if (saved == true)
        {
            contact.UpdateGroup(dialog.ViewModel.GroupName, dialog.ViewModel.SelectedMembership?.Value ?? 0,
                dialog.ViewModel.SelectedAreaOfInfluence?.Value ?? 0, dialog.ViewModel.SelectedMagicalResources?.Value ?? 0,
                dialog.ViewModel.SelectedMatrixResources?.Value ?? 0);
            ViewModel.LoadCharacter(_character);
        }
    }
}
