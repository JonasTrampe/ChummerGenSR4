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
    public void AddSpirit_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddSpirit("Fire Spirit", "Elemental", "Spirit", "6", "2");

        CharacterSpiritData spirit = Assert.Single(character.Spirits);
        Assert.Equal("Fire Spirit", spirit.Name);
        Assert.Equal("Elemental", spirit.CritterName);
        Assert.Equal("Spirit", spirit.Type);
        Assert.Equal("6", spirit.Force);
        Assert.Equal("2", spirit.Services);
        Assert.False(spirit.Bound);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Fire Spirit", Assert.Single(reloaded.Spirits).Name);
    }

    [Fact]
    public void SpiritCreationBudget_ChargesServicesAndRefundsOnRemoval()
    {
        CharacterDocument character = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod>"
            + "<startingbuildpoints>4</startingbuildpoints><bp>4</bp><karma>0</karma><spirits />"
            + "<attributes>" + AttributeXml("CHA", "3") + "</attributes></character>");

        Assert.True(character.AddSpirit("Fire Spirit", "Elemental", "Spirit", "6", "3"));
        Assert.Equal("1", character.Bp);
        Assert.False(character.AddSpirit("Task Sprite", "", "Sprite", "3", "2"));
        Assert.True(character.RemoveSpirit("Fire Spirit", "Spirit", "6"));
        Assert.Equal("4", character.Bp);
        // Spent is now the live itemized sum (CreationBudget no longer reads <bp> as a
        // live-decrementing pool - see CharacterFileService.Expenses.cs), so after the spirit is
        // fully refunded/removed only the CHA 1->3 attribute purchase remains: (3-1)*10 = 20.
        Assert.Equal(20, character.CreationBudget.Spent);
    }

    [Fact]
    public void AddSpirit_CreationMode_CannotExceedCharismaBoundSpiritCount()
    {
        // Ported from frmCreate.cs's cmdAddSpirit_Click: the number of Spirits/Sprites added
        // during creation cannot exceed CHA.
        CharacterDocument character = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod>"
            + "<startingbuildpoints>20</startingbuildpoints><bp>20</bp><karma>0</karma><spirits />"
            + "<attributes>" + AttributeXml("CHA", "2") + "</attributes></character>");

        Assert.True(character.AddSpirit("Fire Spirit", "Elemental", "Spirit", "6", "0"));
        Assert.True(character.AddSpirit("Air Spirit", "Elemental", "Spirit", "4", "0"));
        Assert.False(character.AddSpirit("Task Sprite", "", "Sprite", "3", "0"));
        Assert.Equal(2, character.Spirits.Count);
    }

    [Fact]
    public void AddSpirit_CreationMode_IgnoreRulesBypassesTheCharismaCap()
    {
        CharacterDocument character = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod>"
            + "<startingbuildpoints>20</startingbuildpoints><bp>20</bp><karma>0</karma><ignorerules>True</ignorerules>"
            + "<spirits /><attributes>" + AttributeXml("CHA", "1") + "</attributes></character>");

        Assert.True(character.AddSpirit("Fire Spirit", "Elemental", "Spirit", "6", "0"));
        Assert.True(character.AddSpirit("Air Spirit", "Elemental", "Spirit", "4", "0"));
        Assert.Equal(2, character.Spirits.Count);
    }

    [Fact]
    public void AddSpirit_CareerMode_NoCharismaCapApplies()
    {
        // Ported from frmCareer.cs's cmdAddSpirit_Click: career mode never checks the CHA cap.
        CharacterDocument character = LoadXml("<character><created>True</created><spirits />"
            + "<attributes>" + AttributeXml("CHA", "1") + "</attributes></character>");

        Assert.True(character.AddSpirit("Fire Spirit", "Elemental", "Spirit", "6", "0"));
        Assert.True(character.AddSpirit("Air Spirit", "Elemental", "Spirit", "4", "0"));
        Assert.Equal(2, character.Spirits.Count);
    }

    [Fact]
    public void SpiritNotes_PersistForDuplicateSummonsByListIdentity()
    {
        CharacterDocument character = LoadXml("<character><spirits>"
            + "<spirit><name>Fire Spirit</name><type>Spirit</type><force>4</force></spirit>"
            + "<spirit><name>Fire Spirit</name><type>Spirit</type><force>4</force></spirit>"
            + "</spirits></character>");
        int spiritId = character.Spirits[1].SpiritId;
        Assert.True(character.SetSpiritNotes(spiritId, "Bound for the next run."));
        Assert.Equal(string.Empty, character.Spirits[0].Notes);
        Assert.Equal("Bound for the next run.", character.Spirits[1].Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Bound for the next run.", reloaded.Spirits[1].Notes);
    }

    [Fact]
    public void RemoveSpirit_RemovesOnlyTheMatchingSpirit()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddSpirit("Fire Spirit", "Elemental", "Spirit", "6", "2");
        character.AddSpirit("Task Sprite", "", "Sprite", "3", "1");

        Assert.True(character.RemoveSpirit("Fire Spirit", "Spirit", "6"));
        Assert.False(character.RemoveSpirit("Fire Spirit", "Spirit", "6"));
        CharacterSpiritData remaining = Assert.Single(character.Spirits);
        Assert.Equal("Task Sprite", remaining.Name);
    }

    [Fact]
    public void MaxSpiritForce_PureMagician_UsesFullMag()
    {
        var character = LoadXml("<character><adept>False</adept><magician>True</magician><attributes>"
            + AttributeXml("MAG", "5") + "</attributes></character>");

        Assert.Equal(5, character.MaxSpiritForce);
    }

    [Fact]
    public void MaxSpiritForce_MysticAdept_UsesMagicianMagSplitByDefault()
    {
        var character = LoadXml("<character><adept>True</adept><magician>True</magician>"
            + "<magsplitadept>2</magsplitadept><magsplitmagician>4</magsplitmagician><attributes>"
            + AttributeXml("MAG", "6") + "</attributes></character>");

        Assert.Equal(4, character.MaxSpiritForce);
    }

    [Fact]
    public void MaxSpiritForce_MysticAdept_UsesFullMagWhenHouseRuleEnabled()
    {
        var character = LoadXml("<character><adept>True</adept><magician>True</magician>"
            + "<magsplitadept>2</magsplitadept><magsplitmagician>4</magsplitmagician><attributes>"
            + AttributeXml("MAG", "6") + "</attributes></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { SpiritForceBasedOnTotalMag = true });

        Assert.Equal(6, character.MaxSpiritForce);
    }

    [Fact]
    public void MaxSpiritForce_Technomancer_UsesRes()
    {
        var character = LoadXml("<character><adept>False</adept><magician>False</magician><technomancer>True</technomancer><attributes>"
            + AttributeXml("RES", "3") + "</attributes></character>");

        Assert.Equal(3, character.MaxSpiritForce);
    }

    [Fact]
    public void MaxSpiritForce_MundaneCharacter_IsZero()
    {
        var character = LoadXml("<character><adept>False</adept><magician>False</magician></character>");

        Assert.Equal(0, character.MaxSpiritForce);
    }

    [Fact]
    public void FreeSpiritPowerPoints_UsesEdgByDefault_MagUnderHouseRule()
    {
        var character = LoadXml("<character><metatype>Free Spirit</metatype><attributes>"
            + AttributeXml("EDG", "5") + AttributeXml("MAG", "8") + "</attributes><critterpowers>"
            + "<critterpower><name>Concealment</name><points>2</points></critterpower>"
            + "</critterpowers></character>");

        var edgBased = character.FreeSpiritPowerPoints;
        Assert.NotNull(edgBased);
        Assert.Equal(3, edgBased.Value); // 5 EDG - 2 used.

        character.SetCharacterOptionsForTesting(new CharacterOptions { FreeSpiritPowerPointsMag = true });
        var magBased = character.FreeSpiritPowerPoints;
        Assert.Equal(6, magBased.Value); // 8 MAG - 2 used.
    }

    [Fact]
    public void FreeSpiritPowerPoints_NullForNonFreeSpiritsAndCritters()
    {
        var nonFreeSpirit = LoadXml("<character><metatype>Human</metatype></character>");
        Assert.Null(nonFreeSpirit.FreeSpiritPowerPoints);

        var critterFreeSpirit = LoadXml("<character><metatype>Free Spirit</metatype><critter>True</critter></character>");
        Assert.Null(critterFreeSpirit.FreeSpiritPowerPoints);
    }

}
