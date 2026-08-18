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
    public void AddMartialArt_SnapshotsAdvantagesAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddMartialArt("Krav Maga", new[] { "Extra attack", "+1 die of Subduing" }, "AR", "156");

        CharacterMartialArtData added = Assert.Single(character.MartialArts);
        Assert.Equal("Krav Maga", added.Name);
        Assert.Equal(new[] { "Extra attack", "+1 die of Subduing" }, added.Advantages);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterMartialArtData reloadedArt = Assert.Single(reloaded.MartialArts);
        Assert.Equal(2, reloadedArt.Advantages.Count);
    }

    [Fact]
    public void MartialArtAndManeuverNotes_PersistAndIdBasedRemovalKeepsDuplicates()
    {
        CharacterDocument character = LoadXml("<character><martialarts>"
            + "<martialart><name>Boxing</name></martialart><martialart><name>Boxing</name></martialart>"
            + "</martialarts><martialartmaneuvers>"
            + "<martialartmaneuver><name>Clinch</name></martialartmaneuver>"
            + "<martialartmaneuver><name>Clinch</name></martialartmaneuver>"
            + "</martialartmaneuvers></character>");

        Assert.True(character.SetMartialArtNotes(character.MartialArts[1].MartialArtId, "Second art"));
        Assert.True(character.SetMartialArtManeuverNotes(character.MartialArtManeuvers[1].ManeuverId, "Second maneuver"));
        Assert.True(character.RemoveMartialArt(character.MartialArts[0].MartialArtId));
        Assert.True(character.RemoveMartialArtManeuver(character.MartialArtManeuvers[0].ManeuverId));
        Assert.Equal("Second art", Assert.Single(character.MartialArts).Notes);
        Assert.Equal("Second maneuver", Assert.Single(character.MartialArtManeuvers).Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Second art", Assert.Single(reloaded.MartialArts).Notes);
        Assert.Equal("Second maneuver", Assert.Single(reloaded.MartialArtManeuvers).Notes);
    }

    [Fact]
    public void AddMartialArtManeuver_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.AddMartialArt("Boxing", Array.Empty<string>(), "SR4", "128"));
        character.AddMartialArtManeuver("Sweep", "AR", "160");

        CharacterMartialArtManeuverData added = Assert.Single(character.MartialArtManeuvers);
        Assert.Equal("Sweep", added.Name);
    }

    [Fact]
    public void MartialArtsCreationBudget_ChargesRefundsAndEnforcesManeuverCapacity()
    {
        CharacterDocument character = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod>"
            + "<startingbuildpoints>10</startingbuildpoints><bp>10</bp><karma>0</karma>"
            + "<martialarts /><martialartmaneuvers /></character>");

        Assert.True(character.AddMartialArt("Boxing", Array.Empty<string>(), "SR4", "128"));
        Assert.Equal("5", character.Bp);
        Assert.True(character.AddMartialArtManeuver("Sweep", "AR", "160"));
        Assert.Equal("3", character.Bp);
        Assert.True(character.AddMartialArtManeuver("Clinch", "AR", "160"));
        Assert.Equal("1", character.Bp);
        Assert.False(character.AddMartialArtManeuver("Finishing Move", "AR", "160"));

        Assert.True(character.RemoveMartialArtManeuver("Sweep"));
        Assert.Equal("3", character.Bp);
        Assert.True(character.RemoveMartialArt("Boxing"));
        Assert.Equal("8", character.Bp);

        CharacterCreationBudgetData budget = character.CreationBudget;
        Assert.Contains(budget.Categories, c => c.Name == "Martial art maneuvers" && c.Cost == 2);
    }

    [Fact]
    public void RemoveMartialArt_RemovesOnlyTheMatchingSavedMartialArt()
    {
        CharacterDocument character = LoadXml(
            "<character><martialarts><martialart><name>Krav Maga</name><rating>1</rating></martialart>"
            + "<martialart><name>Capoeira</name><rating>1</rating></martialart></martialarts></character>");

        Assert.True(character.RemoveMartialArt("Krav Maga"));
        Assert.False(character.RemoveMartialArt("Missing style"));
        CharacterMartialArtData remaining = Assert.Single(character.MartialArts);
        Assert.Equal("Capoeira", remaining.Name);
    }

    [Fact]
    public void RemoveMartialArtManeuver_RemovesOnlyTheMatchingSavedManeuver()
    {
        CharacterDocument character = LoadXml(
            "<character><martialartmaneuvers><martialartmaneuver><name>Sweep</name></martialartmaneuver>"
            + "<martialartmaneuver><name>Constrictor's Crush</name></martialartmaneuver></martialartmaneuvers></character>");

        Assert.True(character.RemoveMartialArtManeuver("Sweep"));
        Assert.False(character.RemoveMartialArtManeuver("Missing maneuver"));
        CharacterMartialArtManeuverData remaining = Assert.Single(character.MartialArtManeuvers);
        Assert.Equal("Constrictor's Crush", remaining.Name);
    }

}
