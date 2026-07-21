using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CharacterSidebarViewModel : ViewModelBase
{
    private string _strEssence = string.Empty;
    public string Essence { get => _strEssence; set => SetField(ref _strEssence, value); }

    private string _strPhysicalDamage = string.Empty;
    public string PhysicalDamage { get => _strPhysicalDamage; set => SetField(ref _strPhysicalDamage, value); }

    private string _strStunDamage = string.Empty;
    public string StunDamage { get => _strStunDamage; set => SetField(ref _strStunDamage, value); }

    public DerivedValueViewModel PhysicalMonitor { get; } = new();
    public DerivedValueViewModel StunMonitor { get; } = new();
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

    public void LoadCharacter(CharacterDocument character)
    {
        CharacterConditionData condition = character.Condition;
        Essence = condition.Essence;
        PhysicalDamage = condition.PhysicalDamage;
        StunDamage = condition.StunDamage;
        SetFrom(PhysicalMonitor, condition.PhysicalCm);
        SetFrom(StunMonitor, condition.StunCm);

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
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
