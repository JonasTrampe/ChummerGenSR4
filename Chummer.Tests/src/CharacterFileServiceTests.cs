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
        Assert.Equal(11, character.Condition.PhysicalCm);
        // WIL totalvalue 3 -> ceil(3/2) + 8 = 10, no StunCM improvements in the fixture.
        Assert.Equal(10, character.Condition.StunCm);
    }

    [Fact]
    public void ArmorEncumbrance_PenalizesOverThreshold_FormFittingCountsHalf()
    {
        CharacterDocument character = LoadFixture();

        // Threshold = BOD(4) * 2 = 8. Total ballistic = 6 (Actioneer) + 3 (Form-Fitting 6/2) = 9,
        // over threshold by 1 -> ceil(1/2) = 1 point of penalty.
        Assert.Equal(-1, character.ArmorEncumbrance.BallisticPenalty);
        // Total impact = 4 + 3 (Form-Fitting 6/2) = 7, at/under threshold -> no penalty.
        Assert.Equal(0, character.ArmorEncumbrance.ImpactPenalty);
    }
}
