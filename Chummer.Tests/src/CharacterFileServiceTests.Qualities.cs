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
    public void AddQuality_MutatesCharacterAndPersistsTheMinimalSaveShape()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Ambidextrous", "Positive");
        character.AddQuality("Allergy", "Negative", "Silver (Mild)");

        Assert.Collection(character.Qualities,
            quality =>
            {
                Assert.Equal("Ambidextrous", quality.Name);
                Assert.Equal("Positive", quality.Type);
                Assert.Equal(string.Empty, quality.Extra);
            },
            quality =>
            {
                Assert.Equal("Allergy", quality.Name);
                Assert.Equal("Negative", quality.Type);
                Assert.Equal("Silver (Mild)", quality.Extra);
            });

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");

        Assert.Equal(2, reloaded.Qualities.Count);
        Assert.Equal("Allergy (Silver (Mild))", reloaded.Qualities[1].DisplayName);
    }

    [Fact]
    public void AddQuality_AddQualitiesBundlesTheLinkedQualityForFree()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        character.AddQuality("Changeling (Class I SURGE)", "Positive");

        Assert.Collection(character.Qualities,
            quality => Assert.Equal("Changeling (Class I SURGE)", quality.Name),
            quality =>
            {
                Assert.Equal("Distinctive Style", quality.Name);
                Assert.Equal("Negative", quality.Type);
            });
    }

    [Fact]
    public void AddQuality_AddQualitiesSkipsAQualityTheCharacterAlreadyHas()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Distinctive Style", "Negative");

        character.AddQuality("Changeling (Class I SURGE)", "Positive");

        Assert.Equal(2, character.Qualities.Count(q => q.Name == "Distinctive Style" || q.Name == "Changeling (Class I SURGE)"));
    }

    [Fact]
    public void AddQuality_NuyenAmtBonusCreditsStartingNuyenAtCreationOnly()
    {
        CharacterDocument character = LoadXml("<character><nuyen>0</nuyen><buildmethod>Karma</buildmethod>"
            + "<startingbuildpoints>750</startingbuildpoints><karma>750</karma></character>");

        Assert.True(character.AddQuality("In Debt (5,000¥)", "Negative"));

        Assert.Equal("5000", character.Nuyen);

        Assert.True(character.RemoveQuality("In Debt (5,000¥)", "Negative"));
        Assert.Equal("0", character.Nuyen);
    }

    [Fact]
    public void QualitySource_PersistsMetatypeProvenanceAndDefaultsLegacySavesToSelected()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.AddQuality("Ambidextrous", "Positive",
            eQualitySource: QualitySource.MetatypeRemovable));
        Assert.Equal(QualitySource.MetatypeRemovable, Assert.Single(character.Qualities).Source);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(QualitySource.MetatypeRemovable, Assert.Single(reloaded.Qualities).Source);

        CharacterDocument legacy = LoadXml("<character><qualities><quality><name>Ambidextrous</name>"
            + "<qualitytype>Positive</qualitytype></quality></qualities></character>");
        Assert.Equal(QualitySource.Selected, Assert.Single(legacy.Qualities).Source);
    }

    [Fact]
    public void RemoveQuality_MatchesNameTypeAndDetail()
    {
        CharacterDocument character = LoadXml("<character><qualities><quality><name>Allergy</name><qualitytype>Negative</qualitytype><extra>Silver</extra></quality><quality><name>Allergy</name><qualitytype>Negative</qualitytype><extra>Gold</extra></quality></qualities></character>");

        Assert.True(character.RemoveQuality("Allergy", "Negative", "Silver"));
        CharacterQualityData remaining = Assert.Single(character.Qualities);
        Assert.Equal("Gold", remaining.Extra);
    }

    [Fact]
    public void AddQuality_CreationChargesAndRefundsPositiveAndNegativeCosts()
    {
        CharacterDocument bpCreation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>10</startingbuildpoints><bp>5</bp></character>");

        Assert.True(bpCreation.AddQuality("Ambidextrous", "Positive"));
        Assert.Equal("0", bpCreation.Bp);
        Assert.False(bpCreation.AddQuality("Ambidextrous", "Positive"));
        Assert.True(bpCreation.RemoveQuality("Ambidextrous", "Positive"));
        Assert.Equal("5", bpCreation.Bp);

        Assert.True(bpCreation.AddQuality("Allergy (Uncommon, Mild)", "Negative"));
        Assert.Equal("10", bpCreation.Bp);
        Assert.True(bpCreation.RemoveQuality("Allergy (Uncommon, Mild)", "Negative"));
        Assert.Equal("5", bpCreation.Bp);
    }

    [Fact]
    public void AddQuality_CreationEnforcesThePositiveQualityLimit()
    {
        CharacterDocument character = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod>"
            + "<startingbuildpoints>100</startingbuildpoints><bp>100</bp><qualities /></character>");

        Assert.True(character.AddQuality("Aptitude", "Positive", "Pistols"));
        Assert.True(character.AddQuality("Aptitude", "Positive", "Blades"));
        Assert.True(character.AddQuality("Aptitude", "Positive", "Unarmed Combat"));
        Assert.False(character.AddQuality("Aptitude", "Positive", "Hacking"));
        Assert.Equal("70", character.Bp);
    }

    [Fact]
    public void RemoveQuality_CreationRejectsRemovingNegativeQualityWithoutItsCost()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>10</startingbuildpoints><bp>0</bp><qualities><quality><name>Allergy (Uncommon, Mild)</name><qualitytype>Negative</qualitytype><creationcost>-5</creationcost></quality></qualities></character>");

        Assert.False(creation.RemoveQuality("Allergy (Uncommon, Mild)", "Negative"));
        Assert.Single(creation.Qualities);
        Assert.Equal("0", creation.Bp);
    }

    [Fact]
    public void AddQuality_CareerMode_PositiveQualityChargesKarmaAndLogsAnExpense()
    {
        // Ported from frmCareer.cs's cmdAddQuality_Click: Ambidextrous is bp=5, default
        // KarmaQuality is 2 -> 10 Karma.
        CharacterDocument character = LoadXml("<character><created>True</created><karma>15</karma></character>");

        Assert.True(character.AddQuality("Ambidextrous", "Positive"));

        Assert.Equal("5", character.Karma);
        Assert.Contains(character.KarmaExpenses, e => e.Reason.Contains("Ambidextrous") && e.Amount == "-10");
    }

    [Fact]
    public void AddQuality_CareerMode_InsufficientKarmaRejectsThePositivePurchase()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><karma>5</karma></character>");

        Assert.False(character.AddQuality("Ambidextrous", "Positive"));
        Assert.Empty(character.Qualities);
        Assert.Equal("5", character.Karma);
    }

    [Fact]
    public void AddQuality_CareerMode_NegativeQualityIsFree()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><karma>5</karma></character>");

        Assert.True(character.AddQuality("Astral Beacon", "Negative"));

        Assert.Equal("5", character.Karma);
    }

    [Fact]
    public void RemoveQuality_CareerMode_BuyingOffANegativeQualityChargesKarma()
    {
        // Astral Beacon is bp=-5, default KarmaQuality is 2 -> 10 Karma to buy off.
        CharacterDocument character = LoadXml("<character><created>True</created><karma>15</karma>"
            + "<qualities><quality><name>Astral Beacon</name><qualitytype>Negative</qualitytype></quality></qualities></character>");

        Assert.True(character.RemoveQuality("Astral Beacon", "Negative"));

        Assert.Equal("5", character.Karma);
        Assert.Contains(character.KarmaExpenses, e => e.Reason.Contains("Astral Beacon") && e.Amount == "-10");
    }

    [Fact]
    public void RemoveQuality_CareerMode_InsufficientKarmaRejectsBuyingOffTheNegativeQuality()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><karma>5</karma>"
            + "<qualities><quality><name>Astral Beacon</name><qualitytype>Negative</qualitytype></quality></qualities></character>");

        Assert.False(character.RemoveQuality("Astral Beacon", "Negative"));
        Assert.Single(character.Qualities);
        Assert.Equal("5", character.Karma);
    }

    [Fact]
    public void RemoveQuality_CareerMode_RemovingAPositiveQualityIsFree()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><karma>5</karma>"
            + "<qualities><quality><name>Ambidextrous</name><qualitytype>Positive</qualitytype></quality></qualities></character>");

        Assert.True(character.RemoveQuality("Ambidextrous", "Positive"));

        Assert.Empty(character.Qualities);
        Assert.Equal("5", character.Karma);
    }

    [Fact]
    public void ReplaceQuality_CreationAllowsAnEqualCostSwapWithoutFreePoints()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>10</startingbuildpoints><bp>0</bp><qualities><quality><name>Ambidextrous</name><qualitytype>Positive</qualitytype><creationcost>5</creationcost></quality></qualities></character>");

        Assert.True(creation.ReplaceQuality("Ambidextrous", "Positive", string.Empty,
            "Adept", "Positive"));
        Assert.Equal("0", creation.Bp);
        Assert.Equal("Adept", Assert.Single(creation.Qualities).Name);
    }

    [Fact]
    public void RemoveQuality_AnalyticalMind_RemovesItsBonusImprovements()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Analytical Mind", "Positive");

        Assert.True(character.RemoveQuality("Analytical Mind", "Positive"));

        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void QualityRequiresTextSelection_TrueForSelecttextQualities_FalseOtherwise()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        Assert.True(character.QualityRequiresTextSelection("Codeslinger")); // Real selecttext-only quality.
        Assert.False(character.QualityRequiresTextSelection("Analytical Mind"));
    }

    [Fact]
    public void AddQuality_Codeslinger_AppliesThePlayerEnteredTextAsAnImprovement()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Codeslinger", "Positive", "Hacking (Firewall)");

        var textImprovement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Text, textImprovement.Type);
        Assert.Equal("Hacking (Firewall)", textImprovement.ImprovedName);
        Assert.Equal("Codeslinger", textImprovement.SourceName);

        Assert.True(character.RemoveQuality("Codeslinger", "Positive", "Hacking (Firewall)"));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void AddQuality_Codeslinger_NoImprovementWithoutAnEnteredExtra()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Codeslinger", "Positive");

        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void GetQualityAttributeSelectionOptions_ExceptionalAttribute_ExcludesEdgMagRes()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        var options = character.GetQualityAttributeSelectionOptions("Exceptional Attribute");

        Assert.Contains("STR", options);
        Assert.DoesNotContain("EDG", options);
        Assert.DoesNotContain("MAG", options); // Not a Magician, so excluded anyway - but also explicitly excluded.
        Assert.DoesNotContain("RES", options);
    }

    [Fact]
    public void AddQuality_ExceptionalAttribute_AppliesSelectedAttributesMaxBonus()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Exceptional Attribute", "Positive", "STR");

        var improvement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Attribute, improvement.Type);
        Assert.Equal("STR", improvement.ImprovedName);
        Assert.Equal(1, improvement.Maximum); // Exceptional Attribute's <max>1</max>.
    }

    [Fact]
    public void AddQuality_MentorSpirit_AppliesTheChosenChoiceBonusAndCanBeRemoved()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Cat's own <bonus> is a <spellcategory> node (+2 dice, Illusion). Its chosen <choice>'s
        // <specificskill> bonus is a separate Improvement.
        Assert.False(character.QualityRequiresTextSelection("Mentor Spirit"));
        Assert.Equal("mentors.xml", character.QualityMentorSpiritDataFile("Mentor Spirit"));

        character.AddQuality("Mentor Spirit", "Positive", strMentorSpirit: "Cat",
            strMentorChoice1: "+2 dice to Gynmastics Tests");

        Assert.Equal(2, character.Improvements.Count);

        Improvement spellCategoryImprovement = character.Improvements.Single(i => i.Type == ImprovementType.SpellCategory);
        Assert.Equal(ImprovementSource.Quality, spellCategoryImprovement.Source);
        Assert.Equal("Mentor Spirit", spellCategoryImprovement.SourceName);
        Assert.Equal("Illusion", spellCategoryImprovement.ImprovedName);
        Assert.Equal(2, spellCategoryImprovement.Value);

        Improvement skillImprovement = character.Improvements.Single(i => i.Type == ImprovementType.Skill);
        Assert.Equal(ImprovementSource.Quality, skillImprovement.Source);
        Assert.Equal("Mentor Spirit", skillImprovement.SourceName);
        Assert.Equal("Gymnastics", skillImprovement.ImprovedName);
        Assert.Equal(2, skillImprovement.Value);

        Assert.True(character.RemoveQuality("Mentor Spirit", "Positive"));
        Assert.Empty(character.Improvements);
    }

    private static string ImprovementXml(string strType, string strValue) =>
        "<improvement><improvementttype>" + strType + "</improvementttype><improvementsource>Quality</improvementsource>"
        + "<val>" + strValue + "</val><enabled>True</enabled></improvement>";

    private static string ImprovementAugXml(string strType, string strAug) =>
        "<improvement><improvementttype>" + strType + "</improvementttype><improvementsource>Quality</improvementsource>"
        + "<aug>" + strAug + "</aug><enabled>True</enabled></improvement>";

}
