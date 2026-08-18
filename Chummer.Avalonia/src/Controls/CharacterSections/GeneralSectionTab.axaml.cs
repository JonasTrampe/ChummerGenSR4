using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Linq;
using Chummer.Core;
using Chummer.NewUI.Controls;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;
using ContactGroupDialog = Chummer.NewUI.Dialogs.ContactGroupDialog;
using ContactNotesDialog = Chummer.NewUI.Dialogs.ContactNotesDialog;
using QualityDialog = Chummer.NewUI.Dialogs.QualityDialog;
using TextSelectionDialog = Chummer.NewUI.Dialogs.TextSelectionDialog;
using ListSelectionDialog = Chummer.NewUI.Dialogs.ListSelectionDialog;
using MentorSpiritDialog = Chummer.NewUI.Dialogs.MentorSpiritDialog;

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

        var (blnMentorProceed, strMentor, strChoice) = await CollectMentorSpiritAsync(window, selected.Name);
        if (!blnMentorProceed)
            return;

        // Ported from frmCareer.cs's cmdAddQuality_Click: buying a Positive Quality after
        // creation costs Karma and confirms via Message_ConfirmKarmaExpenseSpend; Negative
        // Qualities are free in career mode too, so no prompt needed for those.
        if (_character.Created && selected.Category == "Positive")
        {
            int intKarmaCost = _character.GetQualityCareerKarmaCost(selected.Name);
            string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseSpend"),
                selected.Name, intKarmaCost);
            if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
                return;
        }

        if (_character.AddQuality(selected.Name, selected.Category, strExtra, strMentor, strChoice))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddPacksKitClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var categoryDialog = new ListSelectionDialog("PACKS-Kit-Kategorie auswählen:",
            _character.GetPacksKitCategories());
        if (!await categoryDialog.ShowDialog<bool>(window) || categoryDialog.SelectedValue == null)
            return;
        string strCategory = categoryDialog.SelectedValue;

        var lstKits = _character.GetPacksKitNames(strCategory);
        if (lstKits.Count == 0)
            return;

        var kitDialog = new ListSelectionDialog($"PACKS-Kit auswählen ({strCategory}):", lstKits);
        if (await kitDialog.ShowDialog<bool>(window) && kitDialog.SelectedValue != null
            && _character.AddPacksKit(kitDialog.SelectedValue, strCategory))
        {
            // A PACKS Kit can touch nearly every section (Attributes, Skills, Gear, Cyberware,
            // Armor, Weapons, Spells, Powers, Complex Forms) - refresh the whole CharacterTab
            // rather than just this tab's own ViewModel, matching CharacterTab.LoadCharacter's
            // existing full-refresh use after Options changes.
            if (this.FindAncestorOfType<CharacterTab>() is { } characterTab)
                characterTab.LoadCharacter(_character);
            else
                ViewModel.LoadCharacter(_character);
        }
    }

    /// <summary>Ported from clsImprovement.cs's selectmentorspirit/selectparagon bonus handlers
    /// (frmSelectMentorSpirit.cs): the Mentor Spirit and "The Beast's Way" Qualities prompt for a
    /// Mentor Spirit/Paragon pick when added. Returns (true, "", "") if this Quality doesn't need
    /// one.</summary>
    private async System.Threading.Tasks.Task<(bool Proceed, string Mentor, string Choice)> CollectMentorSpiritAsync(
        Window window, string strQualityName)
    {
        if (_character == null)
            return (true, string.Empty, string.Empty);

        string? strDataFile = _character.QualityMentorSpiritDataFile(strQualityName);
        if (strDataFile == null)
            return (true, string.Empty, string.Empty);

        var dialog = new MentorSpiritDialog(strDataFile);
        bool? picked = await dialog.ShowDialog<bool?>(window);
        return picked == true && dialog.SelectedMentor != null
            ? (true, dialog.SelectedMentor.Name, dialog.Choice ?? string.Empty)
            : (false, string.Empty, string.Empty);
    }

    private async void OnDeleteQualityClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedQualityNode?.Parent == null
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var quality = ViewModel.SelectedQualityNode;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteQuality"))
            return;

        // Ported from frmCareer.cs's cmdDeleteQuality_Click: buying off a Negative Quality after
        // creation costs Karma and confirms via Message_ConfirmKarmaExpenseRemove; removing a
        // Positive Quality is free in career mode, so no prompt needed for those.
        if (_character.Created && quality.Category == "Negative")
        {
            int intKarmaCost = _character.GetQualityCareerKarmaCost(quality.SourceName);
            string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseRemove"),
                quality.SourceName, intKarmaCost);
            if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
                return;
        }

        if (_character.RemoveQuality(quality.SourceName, quality.Category, quality.Rating))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditQualityNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedQualityNode is not { Parent: not null, QualityId: >= 0 } quality
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = quality.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetQualityNotes(quality.QualityId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Swaps the selected Quality for a different one - ported from frmCareer.cs's
    /// cmdSwapQuality_Click. Core performs the removal/addition atomically with respect to the
    /// creation pool, so an equal-cost replacement works even when no points remain. Legacy's
    /// metatype-origin-cannot-be-swapped guard remains outside this port's quality model.</summary>
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

        var (blnMentorProceed, strMentor, strChoice) = await CollectMentorSpiritAsync(window, selected.Name);
        if (!blnMentorProceed)
            return;

        if (_character.ReplaceQuality(quality.SourceName, quality.Category, quality.Rating,
            selected.Name, selected.Category, strExtra, strMentor, strChoice))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRaiseAttributeClick(object? sender, System.EventArgs e)
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
    private async void OnRemoveAttributeClick(object? sender, System.EventArgs e)
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

    private void OnAddContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        if (_character.AddContact("Neue Connection", "1", "1", blnEnemy: false))
            ViewModel.LoadCharacter(_character);
    }

    private void OnAddEnemyClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        if (_character.AddContact("Neuer Feind", "1", "1", blnEnemy: true))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not Button { Tag: int intContactId }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        string strKey = ViewModel.Enemies.Any(c => c.ContactId == intContactId)
            ? "Message_DeleteEnemy" : "Message_DeleteContact";
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, strKey))
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
            if (contact.UpdateGroup(dialog.ViewModel.GroupName, dialog.ViewModel.SelectedMembership?.Value ?? 0,
                dialog.ViewModel.SelectedAreaOfInfluence?.Value ?? 0, dialog.ViewModel.SelectedMagicalResources?.Value ?? 0,
                dialog.ViewModel.SelectedMatrixResources?.Value ?? 0))
                ViewModel.LoadCharacter(_character);
        }
    }
}
