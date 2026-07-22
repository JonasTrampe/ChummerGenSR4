using Chummer.Core;
using System.Collections.ObjectModel;
using System.Linq;

namespace Chummer.NewUI.ViewModels;

public sealed class CharacterSidebarViewModel : ViewModelBase
{
    private CharacterDocument? _objCharacter;
    private bool _blnUpdatingCommlinks;

    private string _strEssence = string.Empty;
    public string Essence { get => _strEssence; set => SetField(ref _strEssence, value); }

    private string _strPhysicalDamage = string.Empty;
    public string PhysicalDamage { get => _strPhysicalDamage; set => SetField(ref _strPhysicalDamage, value); }

    private string _strStunDamage = string.Empty;
    public string StunDamage { get => _strStunDamage; set => SetField(ref _strStunDamage, value); }

    public DerivedValueViewModel PhysicalMonitor { get; } = new();
    public DerivedValueViewModel StunMonitor { get; } = new();
    public DerivedValueViewModel BallisticArmor { get; } = new();
    public DerivedValueViewModel ImpactArmor { get; } = new();
    public DerivedValueViewModel BallisticEncumbrance { get; } = new();
    public DerivedValueViewModel ImpactEncumbrance { get; } = new();
    public DerivedValueViewModel Initiative { get; } = new();
    public DerivedValueViewModel InitiativePasses { get; } = new();
    public DerivedValueViewModel AstralInitiative { get; } = new();
    public DerivedValueViewModel MatrixInitiative { get; } = new();
    public DerivedValueViewModel MatrixInitiativePasses { get; } = new();
    public DerivedValueViewModel Composure { get; } = new();
    public DerivedValueViewModel JudgeIntentions { get; } = new();
    public DerivedValueViewModel LiftAndCarry { get; } = new();
    public DerivedValueViewModel Memory { get; } = new();
    public DerivedValueViewModel DamageResistance { get; } = new();

    private string _strWoundModifiers = "0";
    public string WoundModifiers { get => _strWoundModifiers; set => SetField(ref _strWoundModifiers, value); }

    private string _strWalkMovement = string.Empty;
    public string WalkMovement { get => _strWalkMovement; set => SetField(ref _strWalkMovement, value); }

    private string _strSwimMovement = string.Empty;
    public string SwimMovement { get => _strSwimMovement; set => SetField(ref _strSwimMovement, value); }

    private string _strFlyMovement = string.Empty;
    public string FlyMovement { get => _strFlyMovement; set => SetField(ref _strFlyMovement, value); }

    private string _strRemainingNuyen = string.Empty;
    public string RemainingNuyen { get => _strRemainingNuyen; set => SetField(ref _strRemainingNuyen, value); }

    private string _strCareerKarma = string.Empty;
    public string CareerKarma { get => _strCareerKarma; set => SetField(ref _strCareerKarma, value); }

    private string _strCareerNuyen = string.Empty;
    public string CareerNuyen { get => _strCareerNuyen; set => SetField(ref _strCareerNuyen, value); }

    public ObservableCollection<CommlinkItemViewModel> Commlinks { get; } = new();

    private CommlinkItemViewModel? _objSelectedCommlink;
    public CommlinkItemViewModel? SelectedCommlink
    {
        get => _objSelectedCommlink;
        set
        {
            if (!SetField(ref _objSelectedCommlink, value) || value == null || _objCharacter == null || _blnUpdatingCommlinks)
                return;

            _objCharacter.SetActiveCommlink(value.Guid);
            ReloadCommlinks(_objCharacter);
            SetFrom(MatrixInitiative, _objCharacter.MatrixInitiative);
            SetFrom(MatrixInitiativePasses, _objCharacter.MatrixInitiativePasses);
        }
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _objCharacter = character;
        RemainingNuyen = character.Nuyen + "¥";
        CareerKarma = character.CareerKarma.ToString();
        CareerNuyen = character.CareerNuyen + "¥";

        CharacterConditionData condition = character.Condition;
        Essence = condition.Essence;
        PhysicalDamage = condition.PhysicalDamage;
        StunDamage = condition.StunDamage;
        SetFrom(PhysicalMonitor, condition.PhysicalCm);
        SetFrom(StunMonitor, condition.StunCm);

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
        SetFrom(BallisticArmor, encumbrance.BallisticRating);
        SetFrom(ImpactArmor, encumbrance.ImpactRating);
        SetFrom(BallisticEncumbrance, encumbrance.BallisticPenalty);
        SetFrom(ImpactEncumbrance, encumbrance.ImpactPenalty);

        SetFrom(Initiative, character.Initiative);
        SetFrom(InitiativePasses, character.InitiativePasses);
        SetFrom(AstralInitiative, character.AstralInitiative);
        SetFrom(MatrixInitiative, character.MatrixInitiative);
        SetFrom(MatrixInitiativePasses, character.MatrixInitiativePasses);
        SetFrom(Composure, character.Composure);
        SetFrom(JudgeIntentions, character.JudgeIntentions);
        SetFrom(LiftAndCarry, character.LiftAndCarry);
        SetFrom(Memory, character.Memory);
        SetFrom(DamageResistance, character.DamageResistance);
        WoundModifiers = character.WoundModifiers.ToString();
        WalkMovement = character.WalkMovement;
        SwimMovement = string.IsNullOrEmpty(character.SwimMovement) ? "0" : character.SwimMovement;
        FlyMovement = string.IsNullOrEmpty(character.FlyMovement) ? "0" : character.FlyMovement;
        ReloadCommlinks(character);
    }

    private void ReloadCommlinks(CharacterDocument character)
    {
        _blnUpdatingCommlinks = true;
        Commlinks.Clear();
        foreach (CharacterCommlinkData objCommlink in character.Commlinks)
            Commlinks.Add(new CommlinkItemViewModel(objCommlink));

        SelectedCommlink = Commlinks.FirstOrDefault(x => x.Active) ?? Commlinks.FirstOrDefault();
        _blnUpdatingCommlinks = false;
    }

    private static void SetFrom(DerivedValueViewModel target, CharacterDerivedValueData data)
    {
        target.Value = data.Value.ToString();
        target.Tooltip = data.Tooltip;
    }

    private static void SetFrom(DerivedValueViewModel target, CharacterInitiativeData data)
    {
        target.Value = data.Display;
        target.Tooltip = data.Tooltip;
    }
}

public sealed class CommlinkItemViewModel
{
    internal CommlinkItemViewModel(CharacterCommlinkData objData)
    {
        Guid = objData.Guid;
        Name = objData.Name;
        Response = objData.Response;
        Equipped = objData.Equipped;
        Active = objData.Active;
    }

    public string Guid { get; }
    public string Name { get; }
    public int Response { get; }
    public bool Equipped { get; }
    public bool Active { get; }
    public string DisplayName => Equipped ? Name + " (R " + Response + ")" : Name + " (nicht ausgerüstet)";
}
