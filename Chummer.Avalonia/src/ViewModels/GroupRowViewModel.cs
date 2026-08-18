using System;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GroupRowViewModel : ViewModelBase
{
    private readonly CharacterDocument _character;
    private readonly Action? _reload;

    public string GroupName { get; }
    public bool IsCreateMode { get; private set; }
    public bool IsLoading { get; set; }

    /// <summary>Whether the BreakSkillGroupsInCreateMode house rule and a nonzero rating make
    /// breaking/regrouping this group possible right now.</summary>
    public bool CanToggleBroken { get; private set; }

    private bool _blnIsBroken;
    public bool IsBroken
    {
        get => _blnIsBroken;
        set
        {
            if (!SetField(ref _blnIsBroken, value))
                return;
            if (IsLoading)
                return;

            bool blnOk = value ? _character.BreakSkillGroup(GroupName) : _character.RegroupSkillGroup(GroupName);
            if (!blnOk)
                _blnIsBroken = !value; // revert the bound value without re-triggering the setter
            _reload?.Invoke();
        }
    }

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
        IsBroken = group.Broken;
        CanToggleBroken = IsCreateMode && _character.BreakSkillGroupsInCreateModeEnabled
            && (group.Broken || RatingValue > 0);
        IsLoading = false;
    }

    public bool Raise() => _character.RaiseSkillGroup(GroupName);
}
