using System.Collections.Generic;
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

public sealed class KnowledgeSkillRowViewModel : ViewModelBase
{
    private readonly CharacterDocument _character;

    public static IReadOnlyList<string> Categories { get; } =
        new[] { "Academic", "Interest", "Language", "Professional", "Street" };

    public KnowledgeSkillRowViewModel(CharacterDocument character, CharacterSkillData skill)
    {
        _character = character;
        SkillId = skill.SkillId;
        AllowDelete = skill.AllowDelete;
        _strSkillName = skill.Name;
        _strRating = skill.BaseRating;
        _strPool = skill.TotalValue;
        _strPoolTooltip = skill.PoolTooltip;
        _strSpecialization = skill.Specialization;
        _strCategory = skill.Category;
    }

    public int SkillId { get; }
    public bool AllowDelete { get; }

    private string _strSkillName = string.Empty;
    public string SkillName
    {
        get => _strSkillName;
        set
        {
            if (!SetField(ref _strSkillName, value))
                return;
            Save();
        }
    }

    private string _strRating = "1";
    public string Rating
    {
        get => _strRating;
        set
        {
            if (!SetField(ref _strRating, value))
                return;
            Save();
        }
    }

    private string _strPool = "0";
    public string Pool
    {
        get => _strPool;
        set => SetField(ref _strPool, value);
    }

    private string _strPoolTooltip = string.Empty;
    public string PoolTooltip
    {
        get => _strPoolTooltip;
        set => SetField(ref _strPoolTooltip, value);
    }

    private string _strSpecialization = string.Empty;
    public string Specialization
    {
        get => _strSpecialization;
        set
        {
            if (!SetField(ref _strSpecialization, value))
                return;
            Save();
        }
    }

    private string _strCategory = "Street";
    public string Category
    {
        get => _strCategory;
        set
        {
            if (!SetField(ref _strCategory, value))
                return;
            Save();
        }
    }

    private void Save()
    {
        _character.UpdateKnowledgeSkill(SkillId, SkillName, Rating, Specialization, Category);
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
            KnowledgeSkills.Add(new KnowledgeSkillRowViewModel(character, skill));
    }
}
