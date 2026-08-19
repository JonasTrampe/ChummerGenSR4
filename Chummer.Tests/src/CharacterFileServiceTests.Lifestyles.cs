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
    public void LifestyleNotes_AndIdBasedRemovalPreserveDuplicateNames()
    {
        CharacterDocument character = LoadXml("<character><lifestyles>"
            + "<lifestyle><lifestylename>Low</lifestylename></lifestyle>"
            + "<lifestyle><lifestylename>Low</lifestylename></lifestyle>"
            + "</lifestyles></character>");

        Assert.True(character.SetLifestyleNotes(character.Lifestyles[1].LifestyleId, "Second home"));
        Assert.True(character.RemoveLifestyle(character.Lifestyles[0].LifestyleId));
        Assert.Equal("Second home", Assert.Single(character.Lifestyles).Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Second home", Assert.Single(reloaded.Lifestyles).Notes);
    }

    [Fact]
    public void Lifestyles_CanBeAddedRemovedAndPersisted()
    {
        CharacterDocument character = LoadXml("<character />");
        character.AddLifestyle("Low", "2000");
        Assert.Single(character.Lifestyles);
        Assert.Equal("Low", character.Lifestyles[0].Name);
        Assert.True(character.RemoveLifestyle("Low"));
        Assert.Empty(character.Lifestyles);
    }

    [Fact]
    public void AddAdvancedLifestyle_ComputesLpCostAndDiceMultiplierFromRealData()
    {
        CharacterDocument character = LoadXml("<character />");

        // All five aspects "Middle" (3 LP each) = 15 LP -> 5000 nuyen, Middle tier (dice 4,
        // multiplier 100). Adding Commercial Zone (+1 LP) and AI in Residence (-3 LP) brings the
        // total to 13 LP, which real lifestyles.xml prices at 3800 and still maps to the Middle
        // tier (11-15 LP).
        var preview = character.PreviewAdvancedLifestyle("Middle", "Middle", "Middle", "Middle", "Middle",
            intRoommates: 0, intPercentage: 100, new[] { "Commercial Zone" }, new[] { "AI in Residence" });
        Assert.Equal(13, preview.Lp);
        Assert.Equal(3800, preview.Cost);
        Assert.Equal(4, preview.Dice);
        Assert.Equal(100, preview.Multiplier);

        character.AddAdvancedLifestyle("Safehouse Alpha", "Middle", "Middle", "Middle", "Middle", "Middle",
            intRoommates: 0, intPercentage: 100, new[] { "Commercial Zone" }, new[] { "AI in Residence" });

        CharacterLifestyleData added = Assert.Single(character.Lifestyles);
        Assert.Equal("Safehouse Alpha", added.Name);
        Assert.Equal("3800", added.Cost);
        Assert.Equal("4", added.Dice);
        Assert.Equal("100", added.Multiplier);
    }

    [Fact]
    public void AddAdvancedLifestyle_FeedsItsOwnDiceMultiplierIntoTheNuyenRoll()
    {
        // GetLifestyleNuyenRollInfo's by-name lifestyles.xml re-lookup can't find a custom
        // Advanced Lifestyle name - it must use the Dice/Multiplier persisted directly on add.
        CharacterDocument character = LoadXml("<character><nuyen>0</nuyen></character>");
        character.AddAdvancedLifestyle("My Penthouse", "High", "High", "High", "High", "High",
            intRoommates: 0, intPercentage: 100, Array.Empty<string>(), Array.Empty<string>());

        var info = character.GetLifestyleNuyenRollInfo();
        Assert.NotNull(info);
        Assert.True(info!.Dice > 0);
        Assert.True(info.Multiplier > 0);
    }

}
