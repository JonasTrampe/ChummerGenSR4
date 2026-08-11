using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class ImprovementsTab : UserControl
{
    public ImprovementsSectionViewModel ViewModel { get; } = new();
    private CharacterDocument? _character;

    public ImprovementsTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private void OnDeleteImprovementClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedImprovement is not { IsCustom: true } selected)
            return;
        if (_character.RemoveCustomImprovement(selected.SourceName))
            ViewModel.LoadCharacter(_character);
    }
}
