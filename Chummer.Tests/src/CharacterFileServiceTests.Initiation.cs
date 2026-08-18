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
    public void AddCustomImprovement_RequiresANameAndASelectionWhereApplicable()
    {
        CharacterDocument character = LoadXml("<character></character>");

        Assert.False(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Initiative, "", intVal: 1));
        Assert.False(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Attribute, "No selection", intVal: 1));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void Initiative_IsIntPlusRea_MinusFixturesWoundModifier()
    {
        CharacterDocument character = LoadFixture();

        // The fixture has 1 filled physical CM box -> -((1+2)/3) = -1 wound modifier.
        Assert.Equal(8, character.Initiative.Base); // INT(4) + REA(4)
        Assert.Equal(7, character.Initiative.Augmented);
        Assert.Equal("8 (7)", character.Initiative.Display);
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
    public void AstralAndMatrixInitiative_ComputeFromIntuition()
    {
        CharacterDocument character = LoadFixture();

        // INT(4) * 2 = 8, minus the fixture's -1 wound modifier (1 filled physical CM box).
        Assert.Equal(8, character.AstralInitiative.Base);
        Assert.Equal(7, character.AstralInitiative.Augmented);

        // Default non-Technomancer path: just INT(4), no MatrixInitiative Improvements.
        Assert.Equal(4, character.MatrixInitiative.Base);
        Assert.Equal(1, character.MatrixInitiativePasses.Base);
    }

    [Fact]
    public void MatrixInitiative_TechnomancerPath_UsesIntTimesTwoPlusOne()
    {
        var character = LoadXml("<character><name>Tech</name><metatype>Human</metatype>"
            + "<technomancer>True</technomancer><attributes>" + AttributeXml("INT", "4") + "</attributes></character>");

        Assert.Equal(9, character.MatrixInitiative.Base); // (4 * 2) + 1
        Assert.Equal(3, character.MatrixInitiativePasses.Base);
    }

    [Fact]
    public void MatrixInitiative_AiPath_UsesIntPlusResponse_OverridingEverythingElse()
    {
        // Also marked Technomancer to prove the A.I. branch takes priority (matches the legacy
        // check order: A.I./technocritter/protosapient overrides the Technomancer path too).
        var character = LoadXml("<character><name>Agent</name><metatype>A.I.</metatype>"
            + "<technomancer>True</technomancer><response>4</response><attributes>"
            + AttributeXml("INT", "3") + "</attributes></character>");

        Assert.Equal(7, character.MatrixInitiative.Base); // INT(3) + Response(4)
        Assert.Equal(3, character.MatrixInitiativePasses.Base);
    }

    [Fact]
    public void MatrixInitiative_SpriteUsesSavedIniMetatypeMinimum()
    {
        CharacterDocument character = LoadXml("<character><metatype>Courier Sprite</metatype><attributes>"
            + "<attribute><name>INI</name><totalvalue>1</totalvalue><metatypemin>12</metatypemin></attribute>"
            + AttributeXml("INT", "7") + "</attributes><physicalcmfilled>3</physicalcmfilled></character>");

        Assert.True(character.IsSprite);
        Assert.Equal(12, character.MatrixInitiative.Base);
        Assert.Equal(11, character.MatrixInitiative.Augmented);
        Assert.Contains("Sprite-Metatype-Initiative: 12", character.MatrixInitiative.Tooltip);
    }

    [Fact]
    public void RaiseInitiateGrade_GroupAndOrdealDiscountStack()
    {
        CharacterDocument character = LoadXml("<character><magician>True</magician><karma>100</karma>"
            + "<attributes>" + AttributeXml("MAG", "3") + "</attributes></character>");

        // (10 + 1*3) * 0.6 = 7.8 -> ceil = 8.
        Assert.True(character.RaiseInitiateGrade(blnGroup: true, blnOrdeal: true));
        Assert.Equal("92", character.Karma);
    }

    [Fact]
    public void RaiseInitiateGrade_CannotExceedTheMagOrResAttribute()
    {
        CharacterDocument character = LoadXml("<character><magician>True</magician><karma>1000</karma>"
            + "<attributes>" + AttributeXml("MAG", "1") + "</attributes></character>");

        Assert.True(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
        Assert.False(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
        Assert.Equal(1, character.InitiateGrade);
    }

    [Fact]
    public void RaiseInitiateGrade_ReplacesTheMagBoostingImprovementEachRaise()
    {
        CharacterDocument character = LoadXml("<character><magician>True</magician><karma>1000</karma>"
            + "<attributes>" + AttributeXml("MAG", "6") + "</attributes></character>");

        Assert.True(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
        Assert.Equal(1, Assert.Single(character.Improvements, i => i.SourceName == "Initiation").Maximum);

        Assert.True(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
        // Replaced, not stacked - still exactly one Initiation Improvement, now at Grade 2.
        Assert.Equal(2, Assert.Single(character.Improvements, i => i.SourceName == "Initiation").Maximum);
    }

    [Fact]
    public void RaiseInitiateGrade_RejectedWithoutEnoughKarma()
    {
        CharacterDocument character = LoadXml("<character><magician>True</magician><karma>5</karma>"
            + "<attributes>" + AttributeXml("MAG", "3") + "</attributes></character>");

        Assert.False(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
        Assert.Equal(0, character.InitiateGrade);
    }

    [Fact]
    public void RaiseInitiateGrade_RejectedForNonMagicalNonTechnomancerCharacters()
    {
        CharacterDocument character = LoadXml("<character><karma>1000</karma>"
            + "<attributes>" + AttributeXml("MAG", "3") + "</attributes></character>");

        Assert.False(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
    }

}
