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
        if (added == true && dialog.SelectedMetamagic is { } selected)
        {
            _character.AddMetamagic(selected.Name, selected.Source, selected.Page);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteMetamagicClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedMetamagic is not { } selected)
            return;

        if (_character.RemoveMetamagic(selected.Guid))
            ViewModel.LoadCharacter(_character);
    }
}
