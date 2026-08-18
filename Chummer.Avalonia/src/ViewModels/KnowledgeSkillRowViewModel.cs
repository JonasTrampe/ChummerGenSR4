using System.Collections.Generic;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

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
        CanRoll = character.AllowSkillDiceRollingEnabled
            && int.TryParse(skill.TotalValue, out int intPool) && intPool > 0;
    }

    public int SkillId { get; }
    public bool AllowDelete { get; }
    public bool CanRoll { get; }

    private string _strSkillName = string.Empty;
    public string SkillName
    {
        get => _strSkillName;
        set
        {
            string strPrevious = _strSkillName;
            if (!SetField(ref _strSkillName, value))
                return;
            if (!Save())
                SetField(ref _strSkillName, strPrevious);
        }
    }

    private string _strRating = "1";
    public string Rating
    {
        get => _strRating;
        set
        {
            string strPrevious = _strRating;
            if (!SetField(ref _strRating, value))
                return;
            if (!Save())
                SetField(ref _strRating, strPrevious);
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
            string strPrevious = _strSpecialization;
            if (!SetField(ref _strSpecialization, value))
                return;
            if (!Save())
                SetField(ref _strSpecialization, strPrevious);
        }
    }

    private string _strCategory = "Street";
    public string Category
    {
        get => _strCategory;
        set
        {
            string strPrevious = _strCategory;
            if (!SetField(ref _strCategory, value))
                return;
            if (!Save())
                SetField(ref _strCategory, strPrevious);
        }
    }

    private bool Save() => _character.UpdateKnowledgeSkill(SkillId, SkillName, Rating, Specialization, Category);
}
