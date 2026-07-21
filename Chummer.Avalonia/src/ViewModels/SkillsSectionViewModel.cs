using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GroupRowViewModel
{
    public string GroupName { get; }
    public string Rating { get; }

    public GroupRowViewModel(CharacterSkillGroupData group)
    {
        GroupName = group.Name;
        Rating = group.Rating;
    }
}

public sealed class SkillRowViewModel
{
    public string SkillName { get; }
    public string Attribute { get; }
    public string Rating { get; }
    public string Pool { get; }
    public string PoolTooltip { get; }
    public string Specialization { get; }
    public bool IsGroupLocked { get; }

    public SkillRowViewModel(CharacterSkillData skill)
    {
        SkillName = skill.Name;
        Attribute = skill.Attribute;
        Rating = skill.Rating;
        Pool = skill.TotalValue;
        PoolTooltip = skill.PoolTooltip;
        Specialization = skill.Specialization;
        IsGroupLocked = skill.IsGroupLocked;
    }
}

/// <summary>One row of the Wissensfertigkeiten grid - was a shared Grid with hand-placed
/// TextBlocks per cell, now one Grid instance per row (identical column widths) stacked in an
/// ItemsControl so it renders the same way but stays bindable.</summary>
public sealed class KnowledgeSkillRowViewModel
{
    public string SkillName { get; }
    public string Rating { get; }
    public string Pool { get; }
    public string PoolTooltip { get; }
    public string Specialization { get; }
    public string Category { get; }

    public KnowledgeSkillRowViewModel(CharacterSkillData skill)
    {
        SkillName = skill.Name;
        Rating = skill.Rating;
        Pool = skill.TotalValue;
        PoolTooltip = skill.PoolTooltip;
        Specialization = skill.Specialization;
        Category = skill.Category;
    }
}

public sealed class SkillsSectionViewModel : ViewModelBase
{
    public ObservableCollection<GroupRowViewModel> SkillGroups { get; } = new();
    public ObservableCollection<SkillRowViewModel> ActiveSkills { get; } = new();
    public ObservableCollection<KnowledgeSkillRowViewModel> KnowledgeSkills { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        SkillGroups.Clear();
        foreach (CharacterSkillGroupData group in character.SkillGroups)
            SkillGroups.Add(new GroupRowViewModel(group));

        ActiveSkills.Clear();
        foreach (CharacterSkillData skill in character.Skills)
            ActiveSkills.Add(new SkillRowViewModel(skill));

        KnowledgeSkills.Clear();
        foreach (CharacterSkillData skill in character.KnowledgeSkills)
            KnowledgeSkills.Add(new KnowledgeSkillRowViewModel(skill));
    }
}
