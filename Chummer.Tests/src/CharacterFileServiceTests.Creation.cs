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
    public void CreationBudget_LegacyBpBuildWithoutStartingBuildPointsTreatsBpAsTheFixedStartingTotal()
    {
        // <startingbuildpoints> only exists for characters created through NewCharacterFactory -
        // legacy/imported save files never had it. For those, <bp> is itself the fixed original
        // total the player chose at creation (legacy never decrements it the way this port's own
        // AddQuality/AddSpell/etc. mutators do for wizard-created characters) - there's no
        // separate stored "remaining", so Remaining has to be derived from the categorized spend.
        // Confirmed directly against this project's real example saves under Chummer.Core/data/
        // saves, none of which carry <startingbuildpoints> at all.
        CharacterDocument character = LoadXml("<character><buildmethod>BP</buildmethod><bp>58</bp>"
            + "<metatypebp>10</metatypebp><nuyenbp>1</nuyenbp>"
            + "<attributes><attribute><name>BOD</name><value>3</value><metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes>"
            + "<contacts><contact><type>Contact</type><connection>2</connection><loyalty>1</loyalty><free>False</free></contact></contacts>"
            + "<skills><skill><rating>2</rating><knowledge>False</knowledge><grouped>False</grouped></skill></skills></character>");

        CharacterCreationBudgetData budget = character.CreationBudget;

        Assert.Equal(0, character.StartingBuildPoints);
        Assert.Equal(58, budget.Starting); // <bp> itself, treated as the fixed original total.
        Assert.Equal(42, budget.Spent); // The categorized spend (Metatype 10 + Primary 20 + Contacts 3 + Skills 8 + Nuyen 1).
        Assert.Equal(16, budget.Remaining); // Starting(58) - Spent(42), derived, not stored.
        Assert.DoesNotContain(budget.Categories, c => c.Name == "Other / not yet categorized");
    }

    [Fact]
    public void CreationBudget_LegacyKarmaBuildWithoutStartingBuildPointsUsesBuildKarmaAsTheFixedStartingTotal()
    {
        // Karma-build equivalent of the BP case above: <buildkarma> (not <karma>, which only
        // means "what's left" once a character reaches career mode) is the fixed original total.
        CharacterDocument character = LoadXml("<character><buildmethod>Karma</buildmethod><buildkarma>750</buildkarma><karma>0</karma>"
            + "<metatypebp>0</metatypebp>"
            + "<attributes><attribute><name>BOD</name><value>3</value><metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        CharacterCreationBudgetData budget = character.CreationBudget;

        Assert.Equal(750, budget.Starting);
    }

    [Fact]
    public void CreationBudget_LegacyBuildOverspendingItsPoolShowsANegativeRemainingRatherThanHidingIt()
    {
        // Going over budget during creation is a legitimate, meaningful state (the sidebar's
        // CreationBudgetOverdrawn flag already turns Remaining red for it) - it must never be
        // clamped away, including in the legacy-fallback branch where Starting comes from <bp>/
        // <buildkarma> instead of <startingbuildpoints>.
        CharacterDocument character = LoadXml("<character><buildmethod>BP</buildmethod><bp>5</bp>"
            + "<metatypebp>10</metatypebp></character>");

        CharacterCreationBudgetData budget = character.CreationBudget;

        Assert.Equal(5, budget.Starting);
        Assert.Equal(10, budget.Spent);
        Assert.Equal(-5, budget.Remaining);
    }

    [Fact]
    public void CreationBudget_CreatedCharacterReturnsAnEmptyBudgetInsteadOfComputingAgainstAStaleBp()
    {
        // Once Created, <bp>/<buildkarma> stay frozen at their original creation-time value
        // forever (legacy never touches them again) - everything from that point on is tracked
        // via career-mode Karma expenses instead. Confirmed against a real example save (Alice
        // Bujold.chum) where computing Starting-minus-categorized-spend against her long-frozen
        // <bp>=400 produced a meaningless Remaining of -181, despite her being a valid, complete
        // character with nothing actually wrong.
        CharacterDocument character = LoadXml("<character><created>True</created><buildmethod>BP</buildmethod><bp>400</bp>"
            + "<metatypebp>10</metatypebp>"
            + "<attributes><attribute><name>BOD</name><value>6</value><metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        CharacterCreationBudgetData budget = character.CreationBudget;

        Assert.Equal(0, budget.Starting);
        Assert.Equal(0, budget.Spent);
        Assert.Equal(0, budget.Remaining);
        Assert.Empty(budget.Categories);
    }

    [Fact]
    public void CreationBudget_EveryRealExampleSaveComputesWithoutThrowing()
    {
        // None of this project's real shipped example saves under Chummer.Core/data/saves carry
        // <startingbuildpoints> (confirmed by grep - it's a field only NewCharacterFactory
        // writes), so every one of them exercises the legacy <bp>/<buildkarma> fallback path.
        // Some hand-authored example characters legitimately exceed their nominal build budget
        // (a negative Remaining is a real, meaningful state - not clamped/hidden), so this only
        // checks that the computation itself succeeds, matching the existing "..._AppliesWithout
        // Throwing" smoke-coverage pattern elsewhere in this file rather than asserting specific
        // values that would vary per example character.
        string strSavesDir = Path.Combine(AppContext.BaseDirectory, "data", "saves");
        Assert.True(Directory.Exists(strSavesDir), "Example saves directory not found - check the test project's data\\** copy rule.");

        var lstPaths = Directory.GetFiles(strSavesDir, "*.chum", SearchOption.AllDirectories);
        Assert.True(lstPaths.Length > 0, "No example .chum files found under data/saves.");

        foreach (string strPath in lstPaths)
        {
            using var stream = File.OpenRead(strPath);
            CharacterDocument character = new CharacterFileService().Load(stream, Path.GetFileName(strPath));

            var exception = Record.Exception(() => character.CreationBudget);
            Assert.True(exception == null, $"{strPath}: {exception}");
        }
    }

    [Fact]
    public void Load_EveryRealExampleSaveLoadsSuccessfullyRegardlessOfCreatedState()
    {
        // Broader than the CreationBudget-focused test above: exercises the full Load pipeline
        // (not just the budget calculation) across every shipped example, split by Created state
        // to make sure both creation-mode and career-mode files are actually represented and
        // neither path is silently untested.
        string strSavesDir = Path.Combine(AppContext.BaseDirectory, "data", "saves");
        Assert.True(Directory.Exists(strSavesDir), "Example saves directory not found - check the test project's data\\** copy rule.");

        var lstPaths = Directory.GetFiles(strSavesDir, "*.chum", SearchOption.AllDirectories);
        Assert.True(lstPaths.Length > 0, "No example .chum files found under data/saves.");

        int intCreatedCount = 0, intInProgressCount = 0;
        foreach (string strPath in lstPaths)
        {
            using var stream = File.OpenRead(strPath);
            CharacterDocument character = null;
            var exception = Record.Exception(() =>
                character = new CharacterFileService().Load(stream, Path.GetFileName(strPath)));
            Assert.True(exception == null, $"{strPath}: {exception}");

            if (character.Created) intCreatedCount++;
            else intInProgressCount++;
        }

        Assert.True(intCreatedCount > 0, "No Created=True (career-mode) example saves were exercised.");
        Assert.True(intInProgressCount > 0, "No Created=False (creation-mode) example saves were exercised.");
    }

    [Fact]
    public void SaveAndReload_RealExampleCharactersRoundTripThroughARealTempFileWithNoDiff()
    {
        // A representative sample across both Created states: load from the real shipped file,
        // save to an actual temp file on disk (not just an in-memory stream - exercises the real
        // file I/O path CharacterFileService.Save/Load use in the app), reload from that temp
        // file, and diff the two CharacterDocuments with CharacterDiff (the same comparison this
        // port already uses for cloud-conflict detection) rather than spot-checking a handful of
        // fields by hand - anything Save/Load silently drops or corrupts shows up as a diff entry.
        string strSavesDir = Path.Combine(AppContext.BaseDirectory, "data", "saves");
        var lstAllPaths = Directory.GetFiles(strSavesDir, "*.chum", SearchOption.AllDirectories);
        Assert.True(lstAllPaths.Length > 0, "No example .chum files found under data/saves.");

        // Every 20th file (both Created states are common enough in the list that a stride
        // sample sees both) keeps this reasonably fast while still exercising real, varied data.
        var lstSamplePaths = lstAllPaths.Where((_, i) => i % 20 == 0).ToList();
        Assert.True(lstSamplePaths.Count > 5, "Sample was too small to be meaningful.");

        foreach (string strPath in lstSamplePaths)
        {
            using var readStream = File.OpenRead(strPath);
            CharacterDocument original = new CharacterFileService().Load(readStream, Path.GetFileName(strPath));

            string strTempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".chum");
            try
            {
                using (var writeStream = File.Create(strTempFile))
                    new CharacterFileService().Save(original, writeStream, Path.GetFileName(strPath));

                CharacterDocument reloaded;
                using (var reloadStream = File.OpenRead(strTempFile))
                    reloaded = new CharacterFileService().Load(reloadStream, Path.GetFileName(strPath));

                CharacterDiffResult diff = CharacterDiff.Compare(original, reloaded);
                string strDiffSummary = string.Join("; ", diff.Entries.Select(
                    e => $"{e.Collection}/{e.Name}: {e.Change} ({e.Detail})"));
                Assert.True(!diff.HasChanges, $"{strPath}: round trip introduced changes: {strDiffSummary}");
            }
            finally
            {
                try { File.Delete(strTempFile); }
                catch { /* Best-effort cleanup - a leftover temp file must not fail the test. */ }
            }
        }
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
