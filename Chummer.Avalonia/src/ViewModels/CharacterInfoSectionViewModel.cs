using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CharacterInfoSectionViewModel : ViewModelBase
{
    private string _strGender = string.Empty;
    public string Gender { get => _strGender; set => SetField(ref _strGender, value); }

    private string _strEyeColor = string.Empty;
    public string EyeColor { get => _strEyeColor; set => SetField(ref _strEyeColor, value); }

    private string _strHairColor = string.Empty;
    public string HairColor { get => _strHairColor; set => SetField(ref _strHairColor, value); }

    private string _strPlayerName = string.Empty;
    public string PlayerName { get => _strPlayerName; set => SetField(ref _strPlayerName, value); }

    private string _strHeight = string.Empty;
    public string Height { get => _strHeight; set => SetField(ref _strHeight, value); }

    private string _strWeight = string.Empty;
    public string Weight { get => _strWeight; set => SetField(ref _strWeight, value); }

    private string _strSkinColor = string.Empty;
    public string SkinColor { get => _strSkinColor; set => SetField(ref _strSkinColor, value); }

    private string _strStreetCred = string.Empty;
    public string StreetCred { get => _strStreetCred; set => SetField(ref _strStreetCred, value); }

    private string _strNotoriety = string.Empty;
    public string Notoriety { get => _strNotoriety; set => SetField(ref _strNotoriety, value); }

    private string _strPublicAwareness = string.Empty;
    public string PublicAwareness { get => _strPublicAwareness; set => SetField(ref _strPublicAwareness, value); }

    private string _strDescription = string.Empty;
    public string Description { get => _strDescription; set => SetField(ref _strDescription, value); }

    private string _strBackground = string.Empty;
    public string Background { get => _strBackground; set => SetField(ref _strBackground, value); }

    private string _strConcept = string.Empty;
    public string Concept { get => _strConcept; set => SetField(ref _strConcept, value); }

    private string _strNotes = string.Empty;
    public string Notes { get => _strNotes; set => SetField(ref _strNotes, value); }

    public void LoadCharacter(CharacterDocument character)
    {
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
