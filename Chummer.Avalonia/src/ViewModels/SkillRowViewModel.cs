using System;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

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
    public bool CanRoll { get; }

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
        // Visually groups a meta skill under its base skill's name in the flat WrapPanel list -
        // matches legacy's indented grouping in the career-mode skill panel.
        SkillName = skill.IsMeta ? "↳ " + skill.Name : skill.Name;
        Attribute = skill.Attribute;
        Category = skill.Category;
        SkillGroup = skill.SkillGroup;
        Exotic = skill.Exotic;
        Rating = skill.Rating;
        Pool = skill.TotalValue;
        PoolTooltip = skill.PoolTooltip;
        CanRoll = character.AllowSkillDiceRollingEnabled
            && int.TryParse(skill.TotalValue, out int intPool) && intPool > 0;
        // Meta skills (e.g. "Perception (Visual)") mirror their base skill's rating and can't be
        // raised independently - reuse the same "locked, rating isn't yours to edit" treatment
        // already used for grouped skills rather than adding a parallel disabled-state concept.
        IsGroupLocked = skill.IsGroupLocked || skill.IsMeta;
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
