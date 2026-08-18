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
    public void CritterPowers_ReadNameExtraAndPoints()
    {
        CharacterDocument character = LoadXml(
            "<character><critterpowers><critterpower><name>Fear</name><extra></extra>"
            + "<points>2</points></critterpower></critterpowers></character>");

        CharacterCritterPowerData power = Assert.Single(character.CritterPowers);
        Assert.Equal("Fear", power.Name);
        Assert.Equal("2", power.Points);
    }

    [Fact]
    public void AddCritterPower_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Fear", "2", "SM", "54");

        CharacterCritterPowerData added = Assert.Single(character.CritterPowers);
        Assert.Equal("Fear", added.Name);
        Assert.Equal("2", added.Points);
        Assert.NotEmpty(added.Guid);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Fear", Assert.Single(reloaded.CritterPowers).Name);
    }

    [Fact]
    public void ConvertSpriteToFreeSprite_RejectsNonSprites()
    {
        CharacterDocument character = LoadXml("<character><metatype>Human</metatype>"
            + "<metatypecategory>Human</metatypecategory></character>");

        Assert.False(character.ConvertSpriteToFreeSprite());
        Assert.Empty(character.CritterPowers);
    }

    [Fact]
    public void RemoveCritterPower_RemovesOnlyTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Fear", "2", "SM", "54");
        character.AddCritterPower("Paralyzing Howl", "3", "SM", "55");
        string strGuid = character.CritterPowers[0].Guid;

        Assert.True(character.RemoveCritterPower(strGuid));
        Assert.False(character.RemoveCritterPower(strGuid));
        CharacterCritterPowerData remaining = Assert.Single(character.CritterPowers);
        Assert.Equal("Paralyzing Howl", remaining.Name);
    }

    [Fact]
    public void AddCritterPower_Fear_IgnoresRatingParameterSinceItHasNoRating()
    {
        // "Fear" has no <rating>yes</rating> in critterpowers.xml, so a passed-in strRating must
        // be ignored (persisted Rating stays "0", matching legacy's disabled nudCritterPowerRating).
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Fear", "2", "SM", "54", strRating: "5");

        Assert.Equal("0", character.CritterPowers[0].Rating);
    }

    [Fact]
    public void AddCritterPower_ElementalAttack_AppliesThePlayerEnteredTextAsAnImprovement()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.CritterPowerRequiresTextSelection("Elemental Attack"));

        character.AddCritterPower("Elemental Attack", "1", "RW", "210", "Fire");

        var textImprovement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Text, textImprovement.Type);
        Assert.Equal("Fire", textImprovement.ImprovedName);
    }

}
