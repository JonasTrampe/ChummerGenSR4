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
    public void CharacterSheetExporter_PrintArcanaAlternates_StaplesOnMetamagicAndArtificingCopies()
    {
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Arcana</name><attribute>LOG</attribute><rating>3</rating><knowledge>False</knowledge><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintArcanaAlternates = true });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        var lstNames = xml.SelectNodes("//skill/name")!.Cast<XmlNode>().Select(n => n.InnerText).ToList();

        Assert.Contains("Arcana, Metamagic", lstNames);
        Assert.Contains("Arcana, Artificing", lstNames);
    }

    [Fact]
    public void AddMetamagic_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddMetamagic("Centering", "SR4", "198");

        CharacterMetamagicData added = Assert.Single(character.Metamagics);
        Assert.Equal("Centering", added.Name);
        Assert.NotEmpty(added.Guid);
        Assert.Equal("SR4 198", added.SourcePage);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Centering", Assert.Single(reloaded.Metamagics).Name);
    }

    [Fact]
    public void RemoveMetamagic_RemovesOnlyTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddMetamagic("Centering", "SR4", "198");
        character.AddMetamagic("Masking", "SR4", "198");
        string strGuid = character.Metamagics[0].Guid;

        Assert.True(character.RemoveMetamagic(strGuid));
        Assert.False(character.RemoveMetamagic(strGuid));
        CharacterMetamagicData remaining = Assert.Single(character.Metamagics);
        Assert.Equal("Masking", remaining.Name);
    }

    [Fact]
    public void MetamagicNotes_PersistForTheMatchingGuid()
    {
        CharacterDocument character = LoadXml("<character><metamagics>"
            + "<metamagic><guid>first</guid><name>Centering</name></metamagic>"
            + "<metamagic><guid>second</guid><name>Centering</name></metamagic>"
            + "</metamagics></character>");

        Assert.True(character.SetMetamagicNotes("second", "Second entry only"));
        Assert.Equal(string.Empty, character.Metamagics[0].Notes);
        Assert.Equal("Second entry only", character.Metamagics[1].Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Second entry only", reloaded.Metamagics[1].Notes);
    }

    [Fact]
    public void AddMetamagic_AttunementAnimal_AppliesThePlayerEnteredTextAsAnImprovement()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.MetamagicRequiresTextSelection("Attunement (Animal)"));
        Assert.False(character.MetamagicRequiresTextSelection("Centering"));

        character.AddMetamagic("Attunement (Animal)", "SM", "53", "Wolf");

        var textImprovement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Text, textImprovement.Type);
        Assert.Equal("Wolf", textImprovement.ImprovedName);
        Assert.Equal("Attunement (Animal)", textImprovement.SourceName);

        string strGuid = character.Metamagics[0].Guid;
        Assert.True(character.RemoveMetamagic(strGuid));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void RaiseInitiateGrade_MetamagicWithoutRatingInBonus_IsNotRebuilt()
    {
        // "Quickening" is a real metamagic.xml entry whose <bonus> doesn't reference "Rating" -
        // the refresh pass's Contains("Rating") guard must leave it alone.
        CharacterDocument character = LoadXml("<character><magician>True</magician><karma>1000</karma>"
            + "<attributes>" + AttributeXml("MAG", "6") + "</attributes></character>");
        character.AddMetamagic("Quickening", "SR4", "198");

        Assert.True(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));

        Assert.DoesNotContain(character.Improvements, i => i.SourceName == "Quickening");
    }

}
