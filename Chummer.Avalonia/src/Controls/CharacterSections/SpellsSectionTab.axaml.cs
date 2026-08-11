using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
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

    private async void OnAddSpiritClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpiritDialog();
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true)
        {
            _character.AddSpirit(dialog.SpiritName, dialog.CritterName, dialog.Type, dialog.Force, dialog.Services);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteSpiritClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedSpirit is not { } selected)
            return;

        if (_character.RemoveSpirit(selected.Name, selected.Type, selected.Force))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddCritterPowerClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CritterPowerDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedPower is { } selected)
        {
            _character.AddCritterPower(selected.Name, selected.Points, selected.Source, selected.Page);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteCritterPowerClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedCritterPower is not { } selected)
            return;

        if (_character.RemoveCritterPower(selected.Guid))
            ViewModel.LoadCharacter(_character);
    }
}
