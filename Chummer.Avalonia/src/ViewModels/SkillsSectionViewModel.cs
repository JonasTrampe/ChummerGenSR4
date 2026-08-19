using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

// Split out into their own files: GroupRowViewModel.cs, SkillRowViewModel.cs,
// KnowledgeSkillRowViewModel.cs, SkillFilterOption.cs.
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
