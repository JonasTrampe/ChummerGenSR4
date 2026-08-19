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
    public void Load_ReadsBasicIdentity()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal("Testrunner", character.Name);
        Assert.Equal("Ghost", character.Alias);
        Assert.Equal("Mensch", character.Metatype);
        Assert.Equal("Jonas", character.PlayerName);
    }

    [Fact]
    public void ConfigureCreationBudget_SwitchesBuildModeAndAvailabilityAtomically()
    {
        CharacterDocument character = LoadXml("<character><created>False</created></character>");

        Assert.True(character.ConfigureCreationBudget("BP", 400, 12, true));
        Assert.Equal("BP", character.BuildMethod);
        Assert.Equal("400", character.Bp);
        Assert.Equal("50", character.Document.SelectSingleNode("/character/nuyenmaxbp")!.InnerText);
        Assert.Equal("12", character.Document.SelectSingleNode("/character/maxavail")!.InnerText);

        Assert.True(character.ConfigureCreationBudget("Karma", 750, 18, false));
        Assert.Equal("0", character.Bp);
        Assert.Equal("750", character.Karma);
        Assert.Equal("100", character.Document.SelectSingleNode("/character/nuyenmaxbp")!.InnerText);
        Assert.False(character.ConfigureCreationBudget("Invalid", 1, 1, false));
    }

    [Fact]
    public void CreationBudget_ExplainsPersistedBpWithDocumentCategories()
    {
        CharacterDocument character = LoadXml("<character><buildmethod>BP</buildmethod><startingbuildpoints>100</startingbuildpoints><bp>58</bp>"
            + "<metatypebp>10</metatypebp><nuyenbp>1</nuyenbp>"
            + "<attributes><attribute><name>BOD</name><value>3</value><metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes>"
            + "<contacts><contact><type>Contact</type><connection>2</connection><loyalty>1</loyalty><free>False</free></contact></contacts>"
            + "<skills><skill><rating>2</rating><knowledge>False</knowledge><grouped>False</grouped></skill></skills></character>");

        CharacterCreationBudgetData budget = character.CreationBudget;

        Assert.Equal(100, budget.Starting);
        Assert.Equal(58, budget.Remaining);
        Assert.Equal(42, budget.Spent);
        Assert.Equal(10, budget.Categories.Single(c => c.Name == "Metatype").Cost);
        Assert.Equal(20, budget.Categories.Single(c => c.Name == "Primary attributes").Cost);
        Assert.Equal(3, budget.Categories.Single(c => c.Name == "Contacts").Cost);
        Assert.Equal(8, budget.Categories.Single(c => c.Name == "Active skills").Cost);
        Assert.Equal(1, budget.Categories.Single(c => c.Name == "Starting Nuyen").Cost);
        Assert.DoesNotContain(budget.Categories, c => c.Name == "Other / not yet categorized");
    }

    [Fact]
    public void RaiseNuyenCreate_UnrestrictedNuyen_UsesStartingBuildPointsInstead()
    {
        CharacterDocument character = LoadXml(
            "<character><buildmethod>Bp</buildmethod><bp>10</bp><nuyenbp>2</nuyenbp><nuyenmaxbp>2</nuyenmaxbp><startingbuildpoints>5</startingbuildpoints></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { UnrestrictedNuyen = true });

        // The saved cap (2) would already block this, but UnrestrictedNuyen substitutes the
        // character's whole starting point budget (5) instead - so a 3rd point still succeeds.
        Assert.True(character.RaiseNuyenCreate());
        Assert.Equal(3, character.NuyenPoints);
    }

}
