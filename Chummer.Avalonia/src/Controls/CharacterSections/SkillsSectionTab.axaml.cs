using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
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

        _character.AddKnowledgeSkill("Neue Wissensfertigkeit", "Street");
        ViewModel.LoadCharacter(_character);
    }

    private void OnDeleteKnowledgeSkillClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not Button { Tag: int intSkillId })
            return;

        if (_character.RemoveKnowledgeSkill(intSkillId))
            ViewModel.LoadCharacter(_character);
    }
}
