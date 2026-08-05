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

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedItem is not { } selected)
            return;

        bool removed = selected.Kind switch
        {
            MartialArtsItemKind.MartialArt => _character.RemoveMartialArt(selected.Name),
            MartialArtsItemKind.Maneuver => _character.RemoveMartialArtManeuver(selected.Name),
            _ => false
        };
        if (removed)
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddMartialArtClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new MartialArtDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true && dialog.SelectedMartialArt is { } selected)
        {
            _character.AddMartialArt(selected.Name, selected.Advantages, selected.Source, selected.Page);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddManeuverClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new MartialArtManeuverDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true && dialog.SelectedManeuver is { } selected)
        {
            _character.AddMartialArtManeuver(selected.Name, selected.Source, selected.Page);
            ViewModel.LoadCharacter(_character);
        }
    }
}
