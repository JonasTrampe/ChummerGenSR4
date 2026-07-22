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

    private void OnRaiseSkillClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not SkillRow { DataContext: SkillRowViewModel row })
            return;

        if (row.Raise())
            ViewModel.LoadCharacter(_character);
    }

    private void OnSpecializationCommitted(object? sender, string strSpecialization)
    {
        if (_character == null || sender is not SkillRow { DataContext: SkillRowViewModel row })
            return;

        if (row.CommitSpecialization(strSpecialization))
            ViewModel.LoadCharacter(_character);
    }

    private void OnRaiseGroupClick(object? sender, System.EventArgs e)
    {
        if (_character == null || sender is not GroupRow { DataContext: GroupRowViewModel row })
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
