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
    public void AddAdeptPower_ImprovedPhysicalAttribute_RaisesShownValueAndKarmaCostToIncrease()
    {
        // Ported from clsUnique.cs's AttributeValueModifiers: a selectattribute bonus with
        // <affectbase> (like Improved Physical Attribute) raises both the shown/augmented value
        // and the Karma cost to increase the attribute further, unlike a plain Attribute bonus.
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>100</karma><attributes>"
            + AttributeXml("BOD", "3") + "</attributes></character>");

        int intBaseCost = character.Attributes.Single(a => a.Code == "BOD").KarmaCostToIncrease;

        character.AddAdeptPower("Improved Physical Attribute", "1", ".75", "BOD");

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(4, bod.Augmented.Value); // 3 base + 1 from the power.
        Assert.Equal(intBaseCost + 5, bod.KarmaCostToIncrease); // (3+1+1)*5 vs (3+1)*5.
    }

    [Fact]
    public void AddAdeptPower_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddAdeptPower("Improved Reflexes", "2", "1.5");

        CharacterPowerData added = Assert.Single(character.AdeptPowers);
        Assert.Equal("Improved Reflexes", added.Name);
        Assert.Equal("2", added.Rating);
        Assert.Equal("3.00", added.TotalPoints);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Improved Reflexes", Assert.Single(reloaded.AdeptPowers).Name);
    }

    [Fact]
    public void AddAdeptPower_ImprovedReflexes2_AppliesInitiativePassAndReaAttributeBonuses()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddAdeptPower("Improved Reflexes 2", "1", "2.5");

        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.InitiativePass));
        Assert.Equal(2, ImprovementManager.AugmentedValueOf(character.Improvements, ImprovementType.Attribute, "REA"));
    }

    [Fact]
    public void AddImprovedSensePower_AppliesSelectedSensewaresBonusAtRatingOne()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        Assert.True(character.AddImprovedSensePower("Improved Sense", "1", ".25", "Olfactory Booster"));

        Assert.Single(character.AdeptPowers);
        // Olfactory Booster's own bonus is +Rating to Perception (Smell) - applied at Rating 1
        // (the ImprovedSenseFullRating house rule is off by default).
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Perception (Smell)"));

        Assert.True(character.RemoveAdeptPower("Improved Sense"));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void AddImprovedSensePower_RejectsUnlistedSenseware()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        Assert.False(character.AddImprovedSensePower("Improved Sense", "1", ".25", "Not A Real Item"));
        Assert.Empty(character.AdeptPowers);
    }

    [Fact]
    public void RemoveAdeptPower_RemovesOnlyTheMatchingPower()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddAdeptPower("Improved Reflexes", "1", "1.5");
        character.AddAdeptPower("Killing Hands", "1", "0.5");

        Assert.True(character.RemoveAdeptPower("Improved Reflexes"));
        Assert.False(character.RemoveAdeptPower("Improved Reflexes"));
        CharacterPowerData remaining = Assert.Single(character.AdeptPowers);
        Assert.Equal("Killing Hands", remaining.Name);
    }

    [Fact]
    public void AdeptPowerNotes_AndIdBasedRemovalPreserveDuplicateEntries()
    {
        CharacterDocument character = LoadXml("<character><powers>"
            + "<power><name>Improved Ability</name><extra>Pistols</extra></power>"
            + "<power><name>Improved Ability</name><extra>Blades</extra></power>"
            + "</powers></character>");

        Assert.True(character.SetAdeptPowerNotes(character.AdeptPowers[1].PowerId, "Blades only"));
        Assert.True(character.RemoveAdeptPower(character.AdeptPowers[0].PowerId));
        CharacterPowerData remaining = Assert.Single(character.AdeptPowers);
        Assert.Equal("Blades", remaining.Extra);
        Assert.Equal("Blades only", remaining.Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Blades only", Assert.Single(reloaded.AdeptPowers).Notes);
    }

    [Fact]
    public void AdeptPowerPoints_PureAdept_UsesFullMagAttribute()
    {
        var character = LoadXml("<character><adept>True</adept><magician>False</magician><attributes>"
            + AttributeXml("MAG", "4") + "</attributes><powers>"
            + "<power><name>Astral Perception</name><rating>1</rating><pointsperlevel>1</pointsperlevel></power>"
            + "<power><name>Killing Hands</name><rating>1</rating><pointsperlevel>0.5</pointsperlevel></power>"
            + "</powers></character>");

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        // 4 MAG - (1 + 0.5) used = 2.5 remaining, truncated to 2.
        Assert.Equal(2, points.Value);
        Assert.Contains("Verbraucht: 1,5", points.Tooltip);
    }

    [Fact]
    public void AdeptPowerPoints_MysticAdept_UsesAdeptMagSplitNotFullMag()
    {
        var character = LoadXml("<character><adept>True</adept><magician>True</magician>"
            + "<magsplitadept>3</magsplitadept><magsplitmagician>3</magsplitmagician><attributes>"
            + AttributeXml("MAG", "6") + "</attributes><powers>"
            + "<power><name>Astral Perception</name><rating>1</rating><pointsperlevel>1</pointsperlevel></power>"
            + "</powers></character>");

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        // Only the 3-point Adept split applies, not the full MAG of 6.
        Assert.Equal(2, points.Value);
    }

    [Fact]
    public void AdeptPowerPoints_AddsAdeptPowerPointsImprovementBonus()
    {
        var character = LoadXml("<character><adept>True</adept><magician>False</magician><attributes>"
            + AttributeXml("MAG", "2") + "</attributes><powers></powers><improvements>"
            + ImprovementAugXml("AdeptPowerPoints", "2") + "</improvements></character>");

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        Assert.Equal(4, points.Value);
        Assert.Contains("Verfügbar: 4", points.Tooltip);
    }

}
