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
    private static CharacterDocument LoadFixture()
    {
        var strPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.chum");
        using var stream = File.OpenRead(strPath);
        return new CharacterFileService().Load(stream, "sample.chum");
    }

    [Fact]
    public void CharacterHistory_CapturesImmutableSnapshotsAndRestoresFreshDocuments()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><alias>Before</alias></character>");
        var history = new CharacterHistory(2);
        CharacterSnapshot snapshot = history.Capture(character, "Before change");
        character.Alias = "After";

        CharacterDocument restored = history.Restore(snapshot);
        Assert.Equal("Before", restored.Alias);
        restored.Alias = "Restored change";
        Assert.Equal("After", character.Alias);
        Assert.Single(history.Snapshots);
        Assert.Equal("Before change", snapshot.Label);
    }

    [Fact]
    public void ConvertSpriteToFreeSprite_GrantsNonCountingDenialAndTracksRemainingPowerSlots()
    {
        CharacterDocument character = LoadXml("<character><metatype>Machine Sprite</metatype>"
            + "<metatypecategory>Sprites</metatypecategory><attributes><attribute><name>EDG</name>"
            + "<value>4</value><totalvalue>4</totalvalue></attribute></attributes></character>");

        Assert.True(character.ConvertSpriteToFreeSprite());
        Assert.True(character.IsFreeSprite);
        Assert.Equal("Free Sprite", character.MetatypeCategory);
        CharacterCritterPowerData denial = Assert.Single(character.CritterPowers);
        Assert.Equal("Denial", denial.Name);
        Assert.False(denial.CountsTowardsLimit);
        Assert.Equal(4, character.FreeSpritePowerPoints!.Value);

        character.AddCritterPower("Fear", "0", "SM", "54");
        Assert.Equal(3, character.FreeSpritePowerPoints!.Value);
        Assert.False(character.ConvertSpriteToFreeSprite());
    }

    [Fact]
    public void CharacterSheetExporter_RendersMultipleCharactersThroughOneCombinedGameMasterSheet()
    {
        CharacterDocument alice = LoadXml("<character><name>Alice</name></character>");
        CharacterDocument bob = LoadXml("<character><name>Bob</name></character>");

        XmlDocument exportXml = CharacterSheetExporter.BuildExportXml(new[] { alice, bob });
        Assert.Equal(2, exportXml.SelectNodes("/characters/character")!.Count);

        string html = CharacterSheetExporter.RenderSheet(new[] { alice, bob }, "Game Master Summary.xsl");
        Assert.Contains("Alice", html);
        Assert.Contains("Bob", html);
    }

    [Fact]
    public void CharacterSheetExporter_RenderSheetToPdf_ThrowsAClearErrorWhenNoHeadlessBrowserIsInstalled()
    {
        // This test environment (and many CI/dev machines) has no Chromium/Chrome-family browser
        // on PATH - RenderSheetToPdf should fail with a clear, actionable message rather than an
        // obscure ProcessStartInfo/FileNotFoundException, regardless of whether a browser happens
        // to be present. Skip the "no browser" assertion when one actually is found, since then
        // the method should succeed instead.
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        string strOutputPath = Path.Combine(Path.GetTempPath(), $"chummer-pdf-test-{Guid.NewGuid():N}.pdf");

        if (CharacterSheetExporter.FindHeadlessBrowserExecutable() == null)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                CharacterSheetExporter.RenderSheetToPdf(character, "Text-Only.xsl", strOutputPath));
            Assert.Contains("headless", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            CharacterSheetExporter.RenderSheetToPdf(character, "Text-Only.xsl", strOutputPath);
            Assert.True(File.Exists(strOutputPath));
            File.Delete(strOutputPath);
        }
    }

    [Fact]
    public void CharacterSheetExporter_GetExportTemplateNames_IncludesSquadManager()
    {
        Assert.Contains("Squad Manager", CharacterSheetExporter.GetExportTemplateNames());
    }

    [Fact]
    public void CharacterSheetExporter_GetExportTemplateExtension_ReadsTheExtComment()
    {
        Assert.Equal("xml", CharacterSheetExporter.GetExportTemplateExtension("Squad Manager"));
    }

    [Fact]
    public void CharacterSheetExporter_RenderExport_TransformsThroughSquadManager()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><alias>Ghost</alias></character>");

        string strExport = CharacterSheetExporter.RenderExport(character, "Squad Manager");

        Assert.Contains("Ghost", strExport);
        Assert.Contains("<Shadowrun", strExport);
    }

    [Fact]
    public void CharacterSheetExporter_RenderExport_ThrowsForAMissingTemplate()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.Throws<FileNotFoundException>(() => CharacterSheetExporter.RenderExport(character, "Does Not Exist"));
    }

    [Fact]
    public void CharacterSheetExporter_ThrowsForAMissingSheetFile()
    {
        CharacterDocument character = LoadFixture();

        Assert.Throws<FileNotFoundException>(() =>
            CharacterSheetExporter.RenderSheet(character, "Does Not Exist.xsl"));
    }

    [Fact]
    public void CharacterSheetExporter_PrintLeadershipAlternates_StaplesOnCommandAndDirectFireCopies()
    {
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Leadership</name><attribute>CHA</attribute><rating>3</rating><knowledge>False</knowledge><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintLeadershipAlternates = true });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        var lstNames = xml.SelectNodes("//skill/name")!.Cast<XmlNode>().Select(n => n.InnerText).ToList();

        Assert.Contains("Leadership, Command", lstNames);
        Assert.Contains("Leadership, Direct Fire", lstNames);
    }

    [Fact]
    public void CharacterSheetExporter_PrintNotes_GatesTheGeneralNotesField()
    {
        CharacterDocument character = LoadXml("<character><notes>Secret backstory</notes></character>");

        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintNotes = false });
        Assert.Equal(string.Empty, CharacterSheetExporter.BuildExportXml(character).SelectSingleNode("//notes")!.InnerText);

        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintNotes = true });
        Assert.Equal("Secret backstory", CharacterSheetExporter.BuildExportXml(character).SelectSingleNode("//notes")!.InnerText);
    }

    [Fact]
    public void RatingExpression_EvaluatesFlatNumbersAndRatingFormulasAlike()
    {
        Assert.Equal(0.2, RatingExpression.Evaluate("0.2", "3"));
        Assert.Equal(3000, RatingExpression.Evaluate("Rating * 1000", "3"));
        Assert.Equal(0, RatingExpression.Evaluate("", "3"));
    }

    [Fact]
    public void AddImprovedSensePower_ImprovedSenseFullRating_UsesTheSelectedItemsOwnRating()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ImprovedSenseFullRating = true });

        Assert.True(character.AddImprovedSensePower("Improved Sense", "1", ".25", "Olfactory Booster"));

        // Olfactory Booster's own <rating> is 6, so the full-rating house rule applies +6 instead of +1.
        Assert.Equal(6, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Perception (Smell)"));
    }

    private static CharacterDocument LoadUnlockedFirearmsGroup(string strPistolenRating, string strAutomatikRating)
        => LoadXml("<character><name>Runner</name><karma>100</karma>"
            + "<skillgroups><skillgroup><name>Firearms</name><rating>0</rating></skillgroup></skillgroups>"
            + "<skills><skill><name>Pistolen</name><attribute>AGI</attribute><skillcategory>Combat Active</skillcategory>"
            + "<skillgroup>Firearms</skillgroup><grouped>False</grouped><rating>" + strPistolenRating
            + "</rating><knowledge>False</knowledge><exotic>False</exotic><spec /><allowdelete>True</allowdelete></skill>"
            + "<skill><name>Automatik</name><attribute>AGI</attribute><skillcategory>Combat Active</skillcategory>"
            + "<skillgroup>Firearms</skillgroup><grouped>False</grouped><rating>" + strAutomatikRating
            + "</rating><knowledge>False</knowledge><exotic>False</exotic><spec /><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");

    [Fact]
    public void CalendarWeeks_CanBeAddedEditedAndMoved()
    {
        CharacterDocument character = LoadXml("<character />");
        CalendarWeek first = character.AddCalendarWeek(2072, 52, "Start");
        character.AddCalendarWeek(2073, 1, "Second");

        Assert.True(character.UpdateCalendarWeekNotes(first.InternalId, "Edited"));
        Assert.True(character.ChangeCalendarStart(2073, 1));
        Assert.Collection(character.Calendar,
            week => { Assert.Equal(2073, week.Year); Assert.Equal(1, week.Week); Assert.Equal("Edited", week.Notes); },
            week => { Assert.Equal(2073, week.Year); Assert.Equal(2, week.Week); Assert.Equal("Second", week.Notes); });
    }

    [Fact]
    public void CalculatedSensor_FallsBackToTheSavedValueWithoutAQualifyingSensorArray()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><guid>" + vehicleId
            + "</guid><name>Americar</name><category>Cars</category><body>6</body><sensor>2</sensor><mods />"
            + "</vehicle></vehicles></character>");

        Assert.Equal(2, character.Vehicles.Single().CalculatedSensor);
    }

    [Fact]
    public void IsBookEnabled_TrueForEnabledBook_FalseForDisabledBook_TrueForBlank()
    {
        var character = LoadXml("<character><nuyen>1000</nuyen></character>");
        var objOptions = new CharacterOptions();
        objOptions.Books.Clear();
        objOptions.Books.Add("SR4");
        character.SetCharacterOptionsForTesting(objOptions);

        Assert.True(character.IsBookEnabled("SR4"));
        Assert.False(character.IsBookEnabled("Arsenal"));
        Assert.True(character.IsBookEnabled(""));
    }

    [Fact]
    public void ConfirmDeleteEnabled_UsesCharacterSettingsProfile()
    {
        CharacterDocument character = LoadXml("<character />");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ConfirmDelete = false });
        Assert.False(character.ConfirmDeleteEnabled);

        character.SetCharacterOptionsForTesting(new CharacterOptions { ConfirmDelete = true });
        Assert.True(character.ConfirmDeleteEnabled);
    }

}
