namespace Chummer.NewUI.ViewModels;

public sealed partial class OptionsDialogViewModel
{
    public void RestoreBpDefaults()
    {
        CurrentOptions.BpAttribute = 10;
        CurrentOptions.BpAttributeMax = 15;
        CurrentOptions.BpContact = 1;
        CurrentOptions.BpMartialArt = 5;
        CurrentOptions.BpMartialArtManeuver = 2;
        CurrentOptions.BpSkillGroup = 10;
        CurrentOptions.BpActiveSkill = 4;
        CurrentOptions.BpActiveSkillSpecialization = 2;
        CurrentOptions.BpKnowledgeSkill = 2;
        CurrentOptions.BpSpell = 3;
        CurrentOptions.BpFocus = 1;
        CurrentOptions.BpSpirit = 1;
        CurrentOptions.BpComplexForm = 1;
        CurrentOptions.BpComplexFormOption = 1;
        OnPropertyChanged(nameof(CurrentOptions));
    }

    public void RestoreKarmaDefaults()
    {
        CurrentOptions.KarmaAttribute = 5;
        CurrentOptions.KarmaQuality = 2;
        CurrentOptions.KarmaSpecialization = 2;
        CurrentOptions.KarmaNewKnowledgeSkill = 2;
        CurrentOptions.KarmaNewActiveSkill = 4;
        CurrentOptions.KarmaNewSkillGroup = 10;
        CurrentOptions.KarmaImproveKnowledgeSkill = 1;
        CurrentOptions.KarmaImproveActiveSkill = 2;
        CurrentOptions.KarmaImproveSkillGroup = 5;
        CurrentOptions.KarmaSpell = 5;
        CurrentOptions.KarmaNewComplexForm = 2;
        CurrentOptions.KarmaImproveComplexForm = 1;
        CurrentOptions.KarmaComplexFormOption = 2;
        CurrentOptions.KarmaComplexFormSkillsoft = 1;
        CurrentOptions.KarmaNuyenPer = 2500;
        CurrentOptions.KarmaContact = 2;
        CurrentOptions.KarmaCarryover = 5;
        CurrentOptions.KarmaSpirit = 2;
        CurrentOptions.KarmaManeuver = 4;
        CurrentOptions.KarmaInitiation = 3;
        CurrentOptions.KarmaMetamagic = 15;
        CurrentOptions.KarmaJoinGroup = 5;
        CurrentOptions.KarmaLeaveGroup = 1;
        CurrentOptions.KarmaAnchoringFocus = 6;
        CurrentOptions.KarmaBanishingFocus = 3;
        CurrentOptions.KarmaBindingFocus = 3;
        CurrentOptions.KarmaCenteringFocus = 6;
        CurrentOptions.KarmaCounterspellingFocus = 3;
        CurrentOptions.KarmaDiviningFocus = 6;
        CurrentOptions.KarmaDowsingFocus = 6;
        CurrentOptions.KarmaInfusionFocus = 3;
        CurrentOptions.KarmaMaskingFocus = 6;
        CurrentOptions.KarmaPowerFocus = 6;
        CurrentOptions.KarmaShieldingFocus = 6;
        CurrentOptions.KarmaSpellcastingFocus = 4;
        CurrentOptions.KarmaSummoningFocus = 4;
        CurrentOptions.KarmaSustainingFocus = 2;
        CurrentOptions.KarmaSymbolicLinkFocus = 1;
        CurrentOptions.KarmaWeaponFocus = 3;
        OnPropertyChanged(nameof(CurrentOptions));
    }
}
