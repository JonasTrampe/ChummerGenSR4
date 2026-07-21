using System;
using System.IO;
using System.Linq;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public class CharacterFileServiceTests
{
    private static CharacterDocument LoadFixture()
    {
        var strPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.chum");
        using var stream = File.OpenRead(strPath);
        return new CharacterFileService().Load(stream, "sample.chum");
    }

    [Fact]
    public void Load_ReadsBasicIdentity()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal("Testrunner", character.Name);
        Assert.Equal("Ghost", character.Alias);
        Assert.Equal("Mensch", character.Metatype);
        Assert.Equal("Jonas", character.PlayerName);
    }

    [Fact]
    public void Contacts_And_Enemies_AreSplitByType()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.Contacts);
        Assert.Equal("Schieber: Stef", character.Contacts[0].Name);

        Assert.Single(character.Enemies);
        Assert.Equal("Lonestar Sergeant", character.Enemies[0].Name);
    }

    [Fact]
    public void Cyberware_And_Bioware_AreSplitByImprovementSource()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.Cyberware);
        Assert.Equal("Wired Reflexes", character.Cyberware[0].Name);

        Assert.Single(character.Bioware);
        Assert.Equal("Cerebral Booster", character.Bioware[0].Name);
    }

    [Fact]
    public void KarmaAndNuyenExpenses_AreSplitByType()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.KarmaExpenses);
        Assert.Equal("5", character.KarmaExpenses[0].Amount);

        Assert.Single(character.NuyenExpenses);
        Assert.Equal("-500", character.NuyenExpenses[0].Amount);
    }

    [Fact]
    public void Improvements_AreParsed()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal(3, character.Improvements.Count);
        Improvement reaBonus = character.Improvements.Single(i => i.ImprovedName == "REA");
        Assert.Equal(ImprovementType.Attribute, reaBonus.Type);
        Assert.Equal(ImprovementSource.Cyberware, reaBonus.Source);
        Assert.Equal(2, reaBonus.Augmented);
    }

    [Fact]
    public void Condition_ComputesPhysicalAndStunTrackFromImprovements()
    {
        CharacterDocument character = LoadFixture();

        // BOD totalvalue 4 -> ceil(4/2) + 8 = 10, plus the fixture's +1 PhysicalCM improvement.
        Assert.Equal(11, character.Condition.PhysicalCm.Value);
        Assert.Contains("Wired Reflexes", character.Condition.PhysicalCm.Tooltip);
        // WIL totalvalue 3 -> ceil(3/2) + 8 = 10, no StunCM improvements in the fixture.
        Assert.Equal(10, character.Condition.StunCm.Value);
    }

    [Fact]
    public void ArmorEncumbrance_PenalizesOverThreshold_FormFittingCountsHalf()
    {
        CharacterDocument character = LoadFixture();

        // Threshold = BOD(4) * 2 = 8. Total ballistic = 6 (Actioneer) + 3 (Form-Fitting 6/2) = 9,
        // over threshold by 1 -> ceil(1/2) = 1 point of penalty.
        Assert.Equal(-1, character.ArmorEncumbrance.BallisticPenalty.Value);
        Assert.Contains("Actioneer", character.ArmorEncumbrance.BallisticPenalty.Tooltip);
        // Total impact = 4 + 3 (Form-Fitting 6/2) = 7, at/under threshold -> no penalty.
        Assert.Equal(0, character.ArmorEncumbrance.ImpactPenalty.Value);
    }

    [Fact]
    public void SpecialAttributeTests_SumTheirTwoAttributesPlusImprovements()
    {
        CharacterDocument character = LoadFixture();

        // WIL(3) + CHA(3) + Sixth Sense(+1) + Combat Sense(+2) - two different-sourced
        // Improvements stacking on the same stat, which the tooltip must list separately.
        Assert.Equal(9, character.Composure.Value);
        Assert.Equal(7, character.JudgeIntentions.Value); // INT(4) + CHA(3)
        Assert.Equal(7, character.LiftAndCarry.Value); // STR(3) + BOD(4)
        Assert.Equal(8, character.Memory.Value); // LOG(5) + WIL(3)

        Assert.Contains("Willenskraft: 3", character.Composure.Tooltip);
        Assert.Contains("Sixth Sense: +1", character.Composure.Tooltip);
        Assert.Contains("Combat Sense: +2", character.Composure.Tooltip);
        Assert.Contains("Gesamt: 9", character.Composure.Tooltip);
    }

    [Fact]
    public void Initiative_IsIntPlusRea_WithNoWoundModifiersInFixture()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal(8, character.Initiative.Base); // INT(4) + REA(4)
        Assert.Equal(8, character.Initiative.Augmented);
        Assert.Equal("8", character.Initiative.Display);
    }

    [Fact]
    public void InitiativePasses_DefaultsToOne()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal(1, character.InitiativePasses.Base);
        Assert.Equal(1, character.InitiativePasses.Augmented);
        Assert.Equal("1", character.InitiativePasses.Display);
    }

    [Fact]
    public void Skill_DicePool_StacksRatingAndPoolAugmentationsFromDifferentSources()
    {
        CharacterDocument character = LoadFixture();
        CharacterSkillData pistolen = character.Skills.Single(s => s.Name == "Pistolen");

        // Base rating 4, Muscle Toner adds +1 to the rating itself (addtorating=True) -> "4 (5)".
        Assert.Equal("4", pistolen.BaseRating);
        Assert.Equal("4 (5)", pistolen.Rating);

        // Pool = augmented rating(5) + Smartlink's +2 pool-only bonus + AGI(6) = 13.
        Assert.Equal("13", pistolen.TotalValue);
        Assert.Contains("Muscle Toner: +1", pistolen.PoolTooltip);
        Assert.Contains("Smartlink: +2", pistolen.PoolTooltip);
    }

    [Fact]
    public void KnowledgeSkill_DicePool_ComputedTheSameWayAsActiveSkills()
    {
        CharacterDocument character = LoadFixture();
        CharacterSkillData knowledgeSkill = character.KnowledgeSkills.Single(s => s.Name == "Straßenwissen");

        // Straßenwissen: rating 3, attribute INT(4) -> pool 7, no Improvements targeting it.
        Assert.Equal("3", knowledgeSkill.BaseRating);
        Assert.Equal("3", knowledgeSkill.Rating);
        Assert.Equal("7", knowledgeSkill.TotalValue);
    }

    [Fact]
    public void AstralAndMatrixInitiative_ComputeFromIntuition()
    {
        CharacterDocument character = LoadFixture();

        // INT(4) * 2 = 8, no wound modifiers in the fixture.
        Assert.Equal(8, character.AstralInitiative.Base);
        Assert.Equal(8, character.AstralInitiative.Augmented);

        // Default non-Technomancer path: just INT(4), no MatrixInitiative Improvements.
        Assert.Equal(4, character.MatrixInitiative.Base);
        Assert.Equal(1, character.MatrixInitiativePasses.Base);
    }

    [Fact]
    public void CareerKarmaAndNuyen_SumOnlyPositiveNonRefundEntries()
    {
        CharacterDocument character = LoadFixture();

        // The fixture's one Karma entry is +5 (earned) -> CareerKarma 5.
        Assert.Equal(5, character.CareerKarma);
        // The fixture's one Nuyen entry is -500 (spent, not earned) -> CareerNuyen 0.
        Assert.Equal(0, character.CareerNuyen);
    }
}
