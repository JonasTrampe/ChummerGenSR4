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
    public void ComplexForms_ReadNameExtraRatingAndProgramOptions()
    {
        CharacterDocument character = LoadXml(
            "<character><techprograms><techprogram><name>Diagnostics</name><extra>Firewall</extra>"
            + "<rating>4</rating><programoptions><programoption><name>Optimization</name>"
            + "<rating>2</rating></programoption></programoptions></techprogram></techprograms></character>");

        CharacterComplexFormData form = Assert.Single(character.ComplexForms);
        Assert.Equal("Diagnostics", form.Name);
        Assert.Equal("Firewall", form.Extra);
        Assert.Equal("4", form.Rating);
        (string strName, string strRating) = Assert.Single(form.Options);
        Assert.Equal("Optimization", strName);
        Assert.Equal("2", strRating);
    }

    [Fact]
    public void AddComplexForm_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddComplexForm("Editor", "Common Use", "SR4", "232");

        CharacterComplexFormData added = Assert.Single(character.ComplexForms);
        Assert.Equal("Editor", added.Name);
        Assert.Equal("1", added.Rating);
        Assert.NotEmpty(added.Guid);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Editor", Assert.Single(reloaded.ComplexForms).Name);
    }

    [Fact]
    public void AddComplexForm_ChargesTheActiveCareerOrCreationPool()
    {
        CharacterDocument career = LoadXml("<character><created>True</created><karma>5</karma></character>");
        career.SetCharacterOptionsForTesting(new CharacterOptions { KarmaNewComplexForm = 2 });
        Assert.True(career.AddComplexForm("Editor", "Common Use", "SR4", "232"));
        Assert.Equal("3", career.Karma);

        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>Karma</buildmethod><startingbuildpoints>5</startingbuildpoints><karma>5</karma></character>");
        creation.SetCharacterOptionsForTesting(new CharacterOptions { KarmaNewComplexForm = 2 });
        Assert.True(creation.AddComplexForm("Editor", "Common Use", "SR4", "232"));
        Assert.Equal("3", creation.Karma);
        Assert.True(creation.RemoveComplexForm(Assert.Single(creation.ComplexForms).Guid));
        Assert.Equal("5", creation.Karma);

        CharacterDocument bpCreation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>1</startingbuildpoints><bp>1</bp></character>");
        Assert.True(bpCreation.AddComplexForm("Editor", "Common Use", "SR4", "232"));
        Assert.Equal("0", bpCreation.Bp);
        Assert.False(bpCreation.AddComplexForm("Analyze", "Common Use", "SR4", "232"));
    }

    [Fact]
    public void RemoveComplexForm_RemovesOnlyTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddComplexForm("Editor", "Common Use", "SR4", "232");
        character.AddComplexForm("Analyze", "Common Use", "SR4", "232");
        string strGuid = character.ComplexForms[0].Guid;

        Assert.True(character.RemoveComplexForm(strGuid));
        Assert.False(character.RemoveComplexForm(strGuid));
        CharacterComplexFormData remaining = Assert.Single(character.ComplexForms);
        Assert.Equal("Analyze", remaining.Name);
    }

    [Fact]
    public void AddComplexFormOption_IsFilteredByCategoryAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddComplexForm("Editor", "Common Use", "SR4", "232");
        string strGuid = character.ComplexForms[0].Guid;

        // Copy Protection's real <programtypes> include "Common Use" (Editor's own category);
        // Biofeedback's don't (Hacking/Simsense/Skillsofts only).
        var lstChoices = character.GetComplexFormOptionChoices("Common Use");
        Assert.Contains("Copy Protection", lstChoices);
        Assert.DoesNotContain("Biofeedback", lstChoices);

        Assert.True(character.AddComplexFormOption(strGuid, "Copy Protection"));

        (string Name, string Rating) option = Assert.Single(character.ComplexForms[0].Options);
        Assert.Equal("Copy Protection", option.Name);
        Assert.Equal("1", option.Rating);
    }

    [Fact]
    public void CharacterSheetExporter_IncludesComplexFormsAndCritterPowersInTheExportXml()
    {
        CharacterDocument character = LoadXml(
            "<character><techprograms><techprogram><name>Diagnostics</name><extra></extra>"
            + "<rating>4</rating></techprogram></techprograms>"
            + "<critterpowers><critterpower><name>Fear</name><extra></extra><points>2</points></critterpower>"
            + "</critterpowers></character>");

        string html = CharacterSheetExporter.RenderSheet(character, "Text-Only.xsl");

        Assert.Contains("Diagnostics", html);
        Assert.Contains("Fear", html);
    }

}
