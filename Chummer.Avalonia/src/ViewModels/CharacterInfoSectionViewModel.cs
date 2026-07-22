using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CharacterInfoSectionViewModel : ViewModelBase
{
    private CharacterDocument? _character;

    private string _strGender = string.Empty;
    public string Gender
    {
        get => _strGender;
        set
        {
            if (!SetField(ref _strGender, value))
                return;
            if (_character != null)
                _character.Gender = value;
        }
    }

    private string _strEyeColor = string.Empty;
    public string EyeColor
    {
        get => _strEyeColor;
        set
        {
            if (!SetField(ref _strEyeColor, value))
                return;
            if (_character != null)
                _character.EyeColor = value;
        }
    }

    private string _strHairColor = string.Empty;
    public string HairColor
    {
        get => _strHairColor;
        set
        {
            if (!SetField(ref _strHairColor, value))
                return;
            if (_character != null)
                _character.HairColor = value;
        }
    }

    private string _strPlayerName = string.Empty;
    public string PlayerName
    {
        get => _strPlayerName;
        set
        {
            if (!SetField(ref _strPlayerName, value))
                return;
            if (_character != null)
                _character.PlayerName = value;
        }
    }

    private string _strHeight = string.Empty;
    public string Height
    {
        get => _strHeight;
        set
        {
            if (!SetField(ref _strHeight, value))
                return;
            if (_character != null)
                _character.Height = value;
        }
    }

    private string _strWeight = string.Empty;
    public string Weight
    {
        get => _strWeight;
        set
        {
            if (!SetField(ref _strWeight, value))
                return;
            if (_character != null)
                _character.Weight = value;
        }
    }

    private string _strSkinColor = string.Empty;
    public string SkinColor
    {
        get => _strSkinColor;
        set
        {
            if (!SetField(ref _strSkinColor, value))
                return;
            if (_character != null)
                _character.SkinColor = value;
        }
    }

    private string _strStreetCred = string.Empty;
    public string StreetCred
    {
        get => _strStreetCred;
        set
        {
            if (!SetField(ref _strStreetCred, value))
                return;
            if (_character != null)
                _character.StreetCred = value;
        }
    }

    private string _strNotoriety = string.Empty;
    public string Notoriety
    {
        get => _strNotoriety;
        set
        {
            if (!SetField(ref _strNotoriety, value))
                return;
            if (_character != null)
                _character.Notoriety = value;
        }
    }

    private string _strPublicAwareness = string.Empty;
    public string PublicAwareness
    {
        get => _strPublicAwareness;
        set
        {
            if (!SetField(ref _strPublicAwareness, value))
                return;
            if (_character != null)
                _character.PublicAwareness = value;
        }
    }

    private string _strDescription = string.Empty;
    public string Description
    {
        get => _strDescription;
        set
        {
            if (!SetField(ref _strDescription, value))
                return;
            if (_character != null)
                _character.Description = value;
        }
    }

    private string _strBackground = string.Empty;
    public string Background
    {
        get => _strBackground;
        set
        {
            if (!SetField(ref _strBackground, value))
                return;
            if (_character != null)
                _character.Background = value;
        }
    }

    private string _strConcept = string.Empty;
    public string Concept
    {
        get => _strConcept;
        set
        {
            if (!SetField(ref _strConcept, value))
                return;
            if (_character != null)
                _character.Concept = value;
        }
    }

    private string _strNotes = string.Empty;
    public string Notes
    {
        get => _strNotes;
        set
        {
            if (!SetField(ref _strNotes, value))
                return;
            if (_character != null)
                _character.Notes = value;
        }
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        Gender = character.Gender;
        EyeColor = character.EyeColor;
        HairColor = character.HairColor;
        PlayerName = character.PlayerName;
        Height = character.Height;
        Weight = character.Weight;
        SkinColor = character.SkinColor;
        StreetCred = character.StreetCred;
        Notoriety = character.Notoriety;
        PublicAwareness = character.PublicAwareness;
        Description = character.Description;
        Background = character.Background;
        Concept = character.Concept;
        Notes = character.Notes;
    }
}
