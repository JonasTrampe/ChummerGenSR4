using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class MartialArtsSectionTab : UserControl
{
    public MartialArtsSectionViewModel ViewModel { get; } = new();

    public MartialArtsSectionTab()
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

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedItem is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteMartialArt"))
            return;

        bool removed = selected.Kind switch
        {
            MartialArtsItemKind.MartialArt => _character.RemoveMartialArt(selected.ItemId),
            MartialArtsItemKind.Maneuver => _character.RemoveMartialArtManeuver(selected.ItemId),
            _ => false
        };
        if (removed)
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedItem is not { ItemId: >= 0 } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = selected.Notes };
        if (await dialog.ShowDialog<bool?>(window) != true)
            return;

        bool saved = selected.Kind switch
        {
            MartialArtsItemKind.MartialArt => _character.SetMartialArtNotes(selected.ItemId, dialog.Notes),
            MartialArtsItemKind.Maneuver => _character.SetMartialArtManeuverNotes(selected.ItemId, dialog.Notes),
            _ => false
        };
        if (saved)
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddMartialArtClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new MartialArtDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added != true || dialog.SelectedMartialArt is not { } selected)
            return;

        // Ported from frmCareer.cs's cmdAddMartialArt_Click: a flat 5*KarmaQuality Karma cost
        // after creation. Legacy shows no confirm dialog here, but this port confirms every
        // career-mode Karma spend for consistency with Qualities/Spells/Complex Forms.
        if (_character.Created)
        {
            int intKarmaCost = _character.GetMartialArtCareerKarmaCost();
            string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseSpend"),
                selected.Name, intKarmaCost);
            if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
                return;
        }

        if (_character.AddMartialArt(selected.Name, selected.Advantages, selected.Source, selected.Page))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddManeuverClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new MartialArtManeuverDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added != true || dialog.SelectedManeuver is not { } selected)
            return;

        // Ported from frmCareer.cs's cmdAddManeuver_Click: a flat KarmaManeuver Karma cost after
        // creation. Legacy shows no confirm dialog here either; confirmed for the same consistency
        // reason as OnAddMartialArtClick above.
        if (_character.Created)
        {
            int intKarmaCost = _character.GetMartialArtManeuverCareerKarmaCost();
            string strMessage = string.Format(App.LanguageCatalog.GetString("Message_ConfirmKarmaExpenseSpend"),
                selected.Name, intKarmaCost);
            if (!await KarmaExpenseConfirmation.ConfirmAsync(window, _character, strMessage))
                return;
        }

        if (_character.AddMartialArtManeuver(selected.Name, selected.Source, selected.Page))
            ViewModel.LoadCharacter(_character);
    }
}
