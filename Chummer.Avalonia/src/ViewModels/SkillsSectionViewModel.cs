using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GroupRowViewModel : ViewModelBase
{
    private readonly CharacterDocument _character;
    private readonly Action? _reload;

    public string GroupName { get; }
    public bool IsCreateMode { get; private set; }
    public bool IsLoading { get; set; }

    private string _strRating = "0";
    public string Rating
    {
        get => _strRating;
        set => SetField(ref _strRating, value);
    }

    private int _intRatingValue;
    public int RatingValue
    {
        get => _intRatingValue;
        set
        {
            if (!SetField(ref _intRatingValue, value))
                return;
            if (IsLoading)
                return;

            int intCurrent = int.TryParse(Rating, out int intParsed) ? intParsed : 0;
            while (intCurrent < value && _character.RaiseSkillGroupCreate(GroupName))
                intCurrent++;
            while (intCurrent > value && _character.LowerSkillGroupCreate(GroupName))
                intCurrent--;
            _reload?.Invoke();
        }
    }

    public GroupRowViewModel(CharacterDocument character, CharacterSkillGroupData group, Action? reload = null)
    {
        _character = character;
        _reload = reload;
        GroupName = group.Name;
        Load(group);
    }

    public void Load(CharacterSkillGroupData group)
    {
        IsLoading = true;
        Rating = group.Rating;
        IsCreateMode = !_character.Created;
        RatingValue = int.TryParse(group.Rating, out int intRating) ? intRating : 0;
        IsLoading = false;
    }

    public bool Raise() => _character.RaiseSkillGroup(GroupName);
}

public sealed class SkillRowViewModel : ViewModelBase
{
    private readonly CharacterDocument _character;
    private readonly Action? _reload;

    public int SkillId { get; }
    public string SkillName { get; }
    public string Attribute { get; }
    public string Category { get; }
    public string SkillGroup { get; }
    public bool Exotic { get; }
    public bool IsGroupLocked { get; }
    public bool IsCreateMode { get; }
    public bool IsLoading { get; set; }

    public string Rating { get; }
    public string Pool { get; }
    public string PoolTooltip { get; }

    private int _intRatingValue;
    public int RatingValue
    {
        get => _intRatingValue;
        set
        {
            if (!SetField(ref _intRatingValue, value))
                return;
            if (IsLoading)
                return;

            int intCurrent = int.TryParse(Rating, out int intParsed) ? intParsed : 0;
            while (intCurrent < value && _character.RaiseActiveSkillCreate(SkillId))
                intCurrent++;
            while (intCurrent > value && _character.LowerActiveSkillCreate(SkillId))
                intCurrent--;
            _reload?.Invoke();
        }
    }

    private string _strSpecialization = string.Empty;
    public string Specialization
    {
        get => _strSpecialization;
        set
        {
            if (!SetField(ref _strSpecialization, value))
                return;
            if (!IsLoading && IsCreateMode)
                _character.SetActiveSkillSpecialization(SkillId, value);
        }
    }

    public SkillRowViewModel(CharacterDocument character, CharacterSkillData skill, Action? reload = null)
    {
        _character = character;
        _reload = reload;
        SkillId = skill.SkillId;
        SkillName = skill.Name;
        Attribute = skill.Attribute;
        Category = skill.Category;
        SkillGroup = skill.SkillGroup;
        Exotic = skill.Exotic;
        Rating = skill.Rating;
        Pool = skill.TotalValue;
        PoolTooltip = skill.PoolTooltip;
        IsGroupLocked = skill.IsGroupLocked;
        IsCreateMode = !character.Created;

        IsLoading = true;
        RatingValue = int.TryParse(skill.BaseRating, out int intRating) ? intRating : 0;
        _strSpecialization = skill.Specialization;
        IsLoading = false;
    }

    public bool Raise() => _character.RaiseActiveSkill(SkillId);

    public bool CommitSpecialization(string strSpecialization) =>
        _character.AddActiveSkillSpecialization(SkillId, strSpecialization);
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

/// <summary>One entry in the Aktionsfertigkeiten filter dropdown - a label plus the predicate it
/// applies to the unfiltered skill list, matching the legacy frmCareer.cs/frmCreate.cs filter combo.</summary>
public sealed class SkillFilterOption
{
    public string Label { get; }
    public Func<CharacterSkillData, bool> Predicate { get; }

    public SkillFilterOption(string strLabel, Func<CharacterSkillData, bool> predicate)
    {
        Label = strLabel;
        Predicate = predicate;
    }

    public override string ToString() => Label;
}

public sealed class SkillsSectionViewModel : ViewModelBase
{
    public ObservableCollection<GroupRowViewModel> SkillGroups { get; } = new();
    public ObservableCollection<SkillRowViewModel> ActiveSkills { get; } = new();
    public ObservableCollection<KnowledgeSkillRowViewModel> KnowledgeSkills { get; } = new();
    public ObservableCollection<SkillFilterOption> SkillFilters { get; } = new();

    private CharacterDocument? _character;
    private List<CharacterSkillData> _lstAllSkills = new();

    private SkillFilterOption? _selectedSkillFilter;
    public SkillFilterOption? SelectedSkillFilter
    {
        get => _selectedSkillFilter;
        set
        {
            if (!SetField(ref _selectedSkillFilter, value))
                return;
            ApplySkillFilter();
        }
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;

        SkillGroups.Clear();
        foreach (CharacterSkillGroupData group in character.SkillGroups)
            SkillGroups.Add(new GroupRowViewModel(character, group, () => LoadCharacter(character)));

        _lstAllSkills = character.Skills.ToList();
        BuildSkillFilters();
        ApplySkillFilter();

        KnowledgeSkills.Clear();
        foreach (CharacterSkillData skill in character.KnowledgeSkills)
            KnowledgeSkills.Add(new KnowledgeSkillRowViewModel(character, skill));
    }

    private void BuildSkillFilters()
    {
        var strPreviousLabel = _selectedSkillFilter?.Label;
        SkillFilters.Clear();

        SkillFilters.Add(new SkillFilterOption("Show All Active Skills", _ => true));
        SkillFilters.Add(new SkillFilterOption("Show Active Skills Rating > 0",
            s => int.TryParse(s.BaseRating, out int r) && r > 0));
        SkillFilters.Add(new SkillFilterOption("Show Active Skills Total Rating > 0",
            s => int.TryParse(s.TotalValue, out int p) && p > 0));
        SkillFilters.Add(new SkillFilterOption("Show Active Skills Rating = 0",
            s => int.TryParse(s.BaseRating, out int r) && r == 0));

        foreach (string strCategory in _lstAllSkills.Select(s => s.Category).Where(c => !string.IsNullOrEmpty(c))
                     .Distinct().OrderBy(c => c))
            SkillFilters.Add(new SkillFilterOption("Category: " + strCategory, s => s.Category == strCategory));

        foreach (string strAttribute in _lstAllSkills.Select(s => s.Attribute).Where(a => !string.IsNullOrEmpty(a))
                     .Distinct())
            SkillFilters.Add(new SkillFilterOption("Attribute: " + strAttribute, s => s.Attribute == strAttribute));

        foreach (string strGroup in _lstAllSkills.Select(s => s.SkillGroup).Where(g => !string.IsNullOrEmpty(g))
                     .Distinct().OrderBy(g => g))
            SkillFilters.Add(new SkillFilterOption("Skill Group: " + strGroup, s => s.SkillGroup == strGroup));

        SelectedSkillFilter = SkillFilters.FirstOrDefault(f => f.Label == strPreviousLabel) ?? SkillFilters[0];
    }

    private void ApplySkillFilter()
    {
        if (_character == null)
            return;

        Func<CharacterSkillData, bool> predicate = SelectedSkillFilter?.Predicate ?? (_ => true);
        ActiveSkills.Clear();
        foreach (CharacterSkillData skill in _lstAllSkills.Where(predicate))
            ActiveSkills.Add(new SkillRowViewModel(_character, skill, () => LoadCharacter(_character)));
    }
}
