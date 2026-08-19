using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public partial class CharacterFileServiceTests
{
    [Fact]
    public void ReplaceMetatype_PreservesPurchasedAttributeIncrementsAndUpdatesIdentity()
    {
        CharacterDocument character = LoadXml("<character><metatype>Human</metatype><metatypebp>0</metatypebp>"
            + "<metatypecategory>Metahuman</metatypecategory><attributes><attribute><name>BOD</name>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax>"
            + "<value>3</value><totalvalue>3</totalvalue></attribute></attributes></character>");
        var target = new NewCharacterMetatype { Name = "Elf", Category = "Metahuman", Bp = 30, Movement = "10/25/50" };
        target.AttributeRanges["BOD"] = ("1", "6", "9");

        Assert.True(character.TryReplaceMetatype(target));
        Assert.Equal("Elf", character.Metatype);
        Assert.Equal("30", character.Document.SelectSingleNode("/character/metatypebp")!.InnerText);
        Assert.Equal("3", character.Document.SelectSingleNode("/character/attributes/attribute/value")!.InnerText);
    }

    [Fact]
    public void RaiseAttribute_SpecialKarmaCostBasedOnShownValue_UsesEssencePenaltyInsteadOfModifiersForMag()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>100</karma>"
            + "<essence>5</essence><attributes>" + AttributeXml("MAG", "3") + "</attributes></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { SpecialKarmaCostBasedOnShownValue = true });

        // EssencePenalty is 0 here (no cyberware), so the shown-value rule reduces to the same
        // base formula as usual: (3 - 0 + 1) * KarmaAttribute (5) = 20.
        Assert.True(character.RaiseAttribute("MAG"));
        Assert.Equal("80", character.Karma);
    }

    [Fact]
    public void AddCustomImprovement_Attribute_AffectsTheAttributeAndCanBeRemoved()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("BOD", "3")
            + "</attributes></character>");

        Assert.True(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Attribute,
            "GM Bonus", intVal: 2, strSelect: "BOD"));

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(5, bod.Augmented.Value); // base 3 + the Improvement's own Augmented value of 2

        Assert.True(character.RemoveCustomImprovement("GM Bonus"));
        bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(3, bod.Augmented.Value);
    }

    [Fact]
    public void AddCustomImprovement_ConditionMonitorPhysical_AddsBoxes()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("BOD", "3")
            + "</attributes></character>");
        int intBaseline = character.Condition.PhysicalCm.Value;

        Assert.True(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.ConditionMonitorPhysical,
            "Cyberlimb Reinforcement", intVal: 2));

        Assert.Equal(intBaseline + 2, character.Condition.PhysicalCm.Value);
    }

    [Fact]
    public void Attributes_AugmentedValueIncludesAttributeImprovements()
    {
        CharacterDocument character = LoadFixture();

        // REA totalvalue 4, plus the fixture's Wired Reflexes +2 REA Improvement, minus 1 from
        // the fixture's own worn armor pushing ballistic encumbrance to -1 (BOD 4 -> threshold 8;
        // Actioneer Business Clothes b6 + Form-Fitting Bodysuit b6/2 = 9 total, ceil((9-8)/2) = 1).
        CharacterAttributeData rea = character.Attributes.Single(a => a.Code == "REA");
        Assert.Equal("4", rea.TotalValue);
        Assert.Equal(5, rea.Augmented.Value);
        Assert.Contains("Wired Reflexes", rea.Augmented.Tooltip);
        Assert.Contains("Rüstungsbehinderung (ballistisch)", rea.Augmented.Tooltip);

        // BOD has no Attribute-type Improvements in the fixture, so Augmented == TotalValue.
        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(int.Parse(bod.TotalValue), bod.Augmented.Value);
    }

    [Fact]
    public void Attributes_KarmaCostToIncreaseUsesCharacterOptionsKarmaAttribute()
    {
        CharacterDocument character = LoadFixture();

        // default.xml's karmaattribute is 5 and alternatemetatypeattributekarma is False, so cost
        // to raise REA's base Value (4) by one point is (4 + 1) * 5 = 25.
        CharacterAttributeData rea = character.Attributes.Single(a => a.Code == "REA");
        Assert.Equal(25, rea.KarmaCostToIncrease);
    }

    [Fact]
    public void RaiseAttribute_DeductsKarmaAndLogsExpenseWhenAffordable()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>100</karma>"
            + "<attributes><attribute><name>REA</name><value>4</value><totalvalue>4</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        Assert.True(character.RaiseAttribute("REA"));

        Assert.Equal("5", character.Attributes.Single(a => a.Code == "REA").Value);
        Assert.Equal("75", character.Karma);
        Assert.Single(character.KarmaExpenses);
        Assert.Equal("-25", character.KarmaExpenses[0].Amount);
    }

    [Fact]
    public void RaiseAttribute_FailsWithoutMutatingWhenNotEnoughKarma()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>10</karma>"
            + "<attributes><attribute><name>REA</name><value>4</value><totalvalue>4</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        Assert.False(character.RaiseAttribute("REA"));

        Assert.Equal("4", character.Attributes.Single(a => a.Code == "REA").Value);
        Assert.Equal("10", character.Karma);
        Assert.Empty(character.KarmaExpenses);
    }

    [Fact]
    public void RaiseAttributeCreate_SpecialAttributeKarmaLimit_GatesMagWithPrimaryAttributesByDefault()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><buildmethod>Karma</buildmethod>"
            + "<startingbuildpoints>20</startingbuildpoints><karma>20</karma>"
            + "<attributes><attribute><name>MAG</name><value>1</value><totalvalue>1</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        // SpecialAttributeKarmaLimit defaults to false, so MAG counts towards the primary-attribute
        // half-of-starting-Karma cap just like BOD/AGI/etc would.
        Assert.True(character.RaiseAttributeCreate("MAG")); // 1 -> 2, costs 10 of the 10-point half-cap
        Assert.False(character.RaiseAttributeCreate("MAG")); // 2 -> 3 would cost 15, exceeding the cap
        Assert.Equal("2", character.Attributes.Single(a => a.Code == "MAG").Value);
    }

    [Fact]
    public void RaiseAttributeCreate_SpecialAttributeKarmaLimit_ExemptsMagWhenHouseRuleOn()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><buildmethod>Karma</buildmethod>"
            + "<startingbuildpoints>20</startingbuildpoints><karma>30</karma>"
            + "<attributes><attribute><name>MAG</name><value>1</value><totalvalue>1</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { SpecialAttributeKarmaLimit = true });

        Assert.True(character.RaiseAttributeCreate("MAG")); // 1 -> 2, costs 10
        Assert.True(character.RaiseAttributeCreate("MAG")); // 2 -> 3, costs 15 - unrestricted, MAG is exempt
        Assert.Equal("3", character.Attributes.Single(a => a.Code == "MAG").Value);
    }

    [Fact]
    public void SetAttributeValue_SetsBaseValueWithoutTouchingKarma()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>10</karma>"
            + "<attributes><attribute><name>REA</name><value>4</value><totalvalue>4</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        Assert.True(character.SetAttributeValue("REA", 6));

        Assert.Equal("6", character.Attributes.Single(a => a.Code == "REA").Value);
        Assert.Equal("10", character.Karma);
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

    private static string AttributeXml(string strCode, string strValue) =>
        "<attribute><name>" + strCode + "</name><value>" + strValue + "</value><totalvalue>" + strValue
        + "</totalvalue><metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax></attribute>";

    [Fact]
    public void BurnEdge_CannotGoBelowZero()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("EDG", "0") + "</attributes></character>");

        Assert.False(character.BurnEdge());
        Assert.Equal(0, character.Edge.Maximum);
    }

    [Fact]
    public void ConditionDamage_AdjustmentClampsToMonitorAndPersists()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("BOD", "4")
            + AttributeXml("WIL", "2") + "</attributes></character>");

        Assert.True(character.AdjustPhysicalDamage(20));
        Assert.Equal("10", character.Condition.PhysicalDamage);
        Assert.False(character.AdjustPhysicalDamage(1));
        Assert.True(character.AdjustPhysicalDamage(-2));
        Assert.Equal("8", character.Condition.PhysicalDamage);
        Assert.True(character.AdjustStunDamage(1));
        Assert.Equal("1", character.Condition.StunDamage);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("8", reloaded.Condition.PhysicalDamage);
        Assert.Equal("1", reloaded.Condition.StunDamage);
    }

    [Fact]
    public void EssenceMax_ImprovementRaisesTheEffectiveMaximumEssence()
    {
        // Ported from clsImprovement.cs's <essencemax> bonus: a flat delta to the character's
        // Essence maximum, on top of the metatype's own ESS max (6 here).
        var character = LoadXml("<character><attributes>" + AttributeXml("ESS", "6") + "</attributes>"
            + "<improvements><improvement><improvementttype>EssenceMax</improvementttype>"
            + "<val>2</val><improvementsource>Quality</improvementsource><enabled>True</enabled>"
            + "</improvement></improvements></character>");

        Assert.Equal("8", character.Condition.Essence);
        Assert.Equal(0, character.EssencePenalty);
    }

    [Fact]
    public void SetMysticAdeptMagicianMagSplit_ClampsTheAdeptShareToZeroWhenEssencePenaltyExceedsTheRemainder()
    {
        var character = LoadXml("<character><adept>True</adept><magician>True</magician><attributes>"
            + AttributeXml("ESS", "6") + AttributeXml("MAG", "6") + "</attributes><cyberwares>"
            + "<cyberware><name>Wired Reflexes</name><ess>2.5</ess><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");

        Assert.True(character.SetMysticAdeptMagicianMagSplit(4));

        Assert.Equal(4, character.MysticAdeptMagicianMagSplit);
        Assert.Equal(0, character.MysticAdeptAdeptMagSplit); // 6 - 4 - 3 would be negative, clamped to 0.
    }

    [Fact]
    public void CyberlimbAveraging_OneOfSixArms_AveragesWithMeatValuePaddingToLimbCount()
    {
        // Base Cyberlimb Agility is 3; with only 1 of the default 6 limbs replaced, the other 5
        // "limbs" contribute the meat AGI of 4 each: floor((3 + 5*4) / 6) = floor(23/6) = 3.
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "4")
            + "</attributes><cyberwares><cyberware><name>Obvious Full Arm</name>"
            + "<category>Cyberlimb</category><improvementsource>Cyberware</improvementsource>"
            + "<children /></cyberware></cyberwares></character>");

        Assert.Equal("3", character.Attributes.Single(a => a.Code == "AGI").TotalValue);
    }

}
