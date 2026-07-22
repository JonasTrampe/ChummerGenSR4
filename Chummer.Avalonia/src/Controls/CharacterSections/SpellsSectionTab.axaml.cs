using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using SpellDialog = Chummer.NewUI.Dialogs.SpellDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

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

    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpellDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedSpell != null)
        {
            var spell = dialog.SelectedSpell;
            _character.AddSpell(spell.Name, spell.Category, spell.Type, spell.Range, spell.Damage, spell.Duration,
                spell.DrainValue, spell.Source, spell.Page);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteSpellClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedSpellNode?.Parent == null)
            return;

        if (_character.RemoveSpell(ViewModel.SelectedSpellNode.Name))
            ViewModel.LoadCharacter(_character);
    }
}
