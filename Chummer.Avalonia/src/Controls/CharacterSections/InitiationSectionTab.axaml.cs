using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class InitiationSectionTab : UserControl
{
    public InitiationSectionViewModel ViewModel { get; } = new();

    public InitiationSectionTab()
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

    private async void OnAddMetamagicClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new MetamagicDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added != true || dialog.SelectedMetamagic is not { } selected)
            return;

        string strSelected = string.Empty;
        if (_character.MetamagicRequiresTextSelection(selected.Name))
        {
            var textDialog = new TextSelectionDialog($"„{selected.Name}“ benötigt eine Detailangabe:");
            if (!await textDialog.ShowDialog<bool>(window))
                return;
            strSelected = textDialog.EnteredText;
        }

        _character.AddMetamagic(selected.Name, selected.Source, selected.Page, strSelected);
        ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteMetamagicClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedMetamagic is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteMetamagic"))
            return;
        if (_character.RemoveMetamagic(selected.Guid))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditMetamagicNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedMetamagic is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (await dialog.ShowDialog<bool?>(window) == true
            && _character.SetMetamagicNotes(selected.Guid, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRaiseInitiateGradeClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        int? intCost = _character.GetInitiateKarmaCostToIncrease(ViewModel.IsGroup, ViewModel.IsOrdeal);
        if (intCost == null)
            return;
        string strTarget = App.LanguageCatalog.GetString(_character.Technomancer
            ? "String_SubmersionGrade" : "String_InitiateGrade");
        string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpense"),
            strTarget, _character.InitiateGrade + 1, intCost.Value);
        if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
            return;

        if (_character.RaiseInitiateGrade(ViewModel.IsGroup, ViewModel.IsOrdeal))
            ViewModel.LoadCharacter(_character);
    }
}
