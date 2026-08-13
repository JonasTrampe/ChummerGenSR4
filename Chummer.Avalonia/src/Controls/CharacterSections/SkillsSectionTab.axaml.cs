using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Controls;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SkillsSectionTab : UserControl
{
    private CharacterDocument? _character;

    public SkillsSectionViewModel ViewModel { get; } = new();

    public SkillsSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private void OnAddKnowledgeSkillClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        if (_character.AddKnowledgeSkill("Neue Wissensfertigkeit", "Street"))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteKnowledgeSkillClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not Button { Tag: int intSkillId }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteKnowledgeSkill"))
            return;
        if (_character.RemoveKnowledgeSkill(intSkillId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRaiseSkillClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not SkillRow { DataContext: SkillRowViewModel row }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        int? intCost = _character.GetActiveSkillKarmaCostToIncrease(row.SkillId);
        if (intCost == null || !int.TryParse(row.Rating, out int intRating))
            return;
        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpense"),
            row.SkillName, intRating + 1, intCost.Value);
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (row.Raise())
            ViewModel.LoadCharacter(_character);
    }

    private void OnRollSkillClick(object? sender, System.EventArgs e)
    {
        if (sender is not SkillRow { DataContext: SkillRowViewModel { CanRoll: true } row }
            || !int.TryParse(row.Pool, out int intPool)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (window is MainWindow mainWindow)
            mainWindow.OpenDiceRoller(intPool);
        else
            new DiceRollerDialog(intPool).Show(window);
    }

    private void OnRollKnowledgeSkillClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: KnowledgeSkillRowViewModel { CanRoll: true } row }
            || !int.TryParse(row.Pool, out int intPool)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (window is MainWindow mainWindow)
            mainWindow.OpenDiceRoller(intPool);
        else
            new DiceRollerDialog(intPool).Show(window);
    }

    private async void OnSpecializationCommitted(object? sender, string strSpecialization)
    {
        if (_character == null || sender is not SkillRow { DataContext: SkillRowViewModel row }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        int intCost = _character.GetActiveSkillSpecializationKarmaCost();
        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseSpecialization"),
            strSpecialization, intCost);
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (row.CommitSpecialization(strSpecialization))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRaiseGroupClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not GroupRow { DataContext: GroupRowViewModel row }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        int? intCost = _character.GetSkillGroupKarmaCostToIncrease(row.GroupName);
        if (intCost == null || !int.TryParse(row.Rating, out int intRating))
            return;
        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpense"),
            row.GroupName, intRating + 1, intCost.Value);
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (row.Raise())
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddExoticSkillClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window || _character == null)
            return;

        var dialog = new AddExoticSkillDialog();
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true)
        {
            _character.AddExoticSkill(dialog.SkillName, dialog.SubType, dialog.Category, dialog.Attribute);
            ViewModel.LoadCharacter(_character);
        }
    }
}
