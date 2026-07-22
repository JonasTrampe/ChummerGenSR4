using System;
using System.IO;
using System.Linq;
using System.Text;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public class CharacterFileServiceTests
{
    private static CharacterDocument LoadFixture()
    {
        var strPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.chum");
        using var stream = File.OpenRead(strPath);
        return new CharacterFileService().Load(stream, "sample.chum");
    }

    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

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
    public void Contacts_And_Enemies_AreSplitByType()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.Contacts);
        Assert.Equal("Schieber: Stef", character.Contacts[0].Name);

        Assert.Single(character.Enemies);
        Assert.Equal("Lonestar Sergeant", character.Enemies[0].Name);
    }

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
    public void RemoveQuality_MatchesNameTypeAndDetail()
    {
        CharacterDocument character = LoadXml("<character><qualities><quality><name>Allergy</name><qualitytype>Negative</qualitytype><extra>Silver</extra></quality><quality><name>Allergy</name><qualitytype>Negative</qualitytype><extra>Gold</extra></quality></qualities></character>");

        Assert.True(character.RemoveQuality("Allergy", "Negative", "Silver"));
        CharacterQualityData remaining = Assert.Single(character.Qualities);
        Assert.Equal("Gold", remaining.Extra);
    }

    [Fact]
    public void AddSpell_MutatesCharacterAndPersistsRuleFields()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204");

        CharacterSpellData added = Assert.Single(character.Spells);
        Assert.Equal("Combat", added.Category);
        Assert.Equal("(F/2)+3", added.Dv);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");

        CharacterSpellData saved = Assert.Single(reloaded.Spells);
        Assert.Equal("Acid Stream", saved.Name);
        Assert.Equal("SR4", saved.Source);
        Assert.Equal("204", saved.Page);
    }

    [Fact]
    public void AddGear_MutatesCharacterTreeAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Medkit", "Biotech", "6");

        CharacterTreeItemData added = Assert.Single(character.Gear);
        Assert.Equal("Medkit", added.Name);
        Assert.Equal("Biotech", added.Category);
        Assert.Equal("6", added.Rating);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Medkit", Assert.Single(reloaded.Gear).Name);
    }

    [Fact]
    public void RemoveGear_RemovesOnlyMatchingRootLevelEntry()
    {
        CharacterDocument character = LoadXml("<character><gears><gear><name>Medkit</name><category>Biotech</category><rating>6</rating></gear><gear><name>Medkit</name><category>Biotech</category><rating>3</rating></gear></gears></character>");

        Assert.True(character.RemoveGear("Medkit", "Biotech", "6"));
        CharacterTreeItemData remaining = Assert.Single(character.Gear);
        Assert.Equal("3", remaining.Rating);
    }

    [Fact]
    public void RemoveSpell_RemovesOnlyTheMatchingSavedSpell()
    {
        CharacterDocument character = LoadXml("<character><spells><spell><name>Acid Stream</name></spell><spell><name>Clout</name></spell></spells></character>");

        Assert.True(character.RemoveSpell("Acid Stream"));
        Assert.False(character.RemoveSpell("Missing spell"));
        CharacterSpellData remaining = Assert.Single(character.Spells);
        Assert.Equal("Clout", remaining.Name);
    }

    [Fact]
    public void AddExpense_MutatesCharacterAndPersistsSignedHistory()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 4, "Session reward", new DateTime(2026, 7, 22));
        character.AddExpense("Nuyen", -250, "New fake SIN", new DateTime(2026, 7, 23));

        Assert.Equal(4, character.CareerKarma);
        Assert.Equal(0, character.CareerNuyen);
        Assert.Equal("-250", character.NuyenExpenses[0].Amount);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");

        Assert.Equal("Session reward", reloaded.KarmaExpenses[0].Reason);
        Assert.Equal("New fake SIN", reloaded.NuyenExpenses[0].Reason);
    }

    [Fact]
    public void Cyberware_And_Bioware_AreSplitByImprovementSource()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.Cyberware);
        Assert.Equal("Wired Reflexes", character.Cyberware[0].Name);

        Assert.Single(character.Bioware);
        Assert.Equal("Cerebral Booster", character.Bioware[0].Name);
    }

    [Fact]
    public void ArmorAndWeapons_ExposeInstalledItemsAsTrees()
    {
        var character = LoadXml("<character><name>Runner</name><armors><armor><name>Jacket</name>"
            + "<armorname>Night Out</armorname><armormods><armormod><name>Fire Resistance</name>"
            + "</armormod></armormods></armor></armors><weapons><weapon><name>Pistol</name>"
            + "<accessories><accessory><name>Smartlink</name></accessory></accessories>"
            + "<weaponmods><weaponmod><name>Gas Vent</name></weaponmod></weaponmods></weapon></weapons></character>");

        Assert.Single(character.Armor);
        Assert.Equal("Night Out", character.Armor[0].Name);
        Assert.Single(character.Armor[0].Children);
        Assert.Equal("Fire Resistance", character.Armor[0].Children[0].Children[0].Name);

        Assert.Single(character.WeaponTrees);
        Assert.Equal("Pistol", character.WeaponTrees[0].Name);
        Assert.Equal(new[] { "Smartlink", "Gas Vent" }, character.WeaponTrees[0].Children.Select(item => item.Name));
    }

    [Fact]
    public void KarmaAndNuyenExpenses_AreSplitByType()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.KarmaExpenses);
        Assert.Equal("5", character.KarmaExpenses[0].Amount);

        Assert.Single(character.NuyenExpenses);
        Assert.Equal("-500", character.NuyenExpenses[0].Amount);
    }

    [Fact]
    public void Improvements_AreParsed()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal(7, character.Improvements.Count);
        Improvement reaBonus = character.Improvements.Single(i => i.ImprovedName == "REA");
        Assert.Equal(ImprovementType.Attribute, reaBonus.Type);
        Assert.Equal(ImprovementSource.Cyberware, reaBonus.Source);
        Assert.Equal(2, reaBonus.Augmented);
    }

    [Fact]
    public void Save_PreservesCompactFormattingAcrossARoundTrip()
    {
        CharacterDocument character = LoadFixture();

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");

        // XmlDocument.Save(Stream) (what this used to do) re-indents with wider whitespace and -
        // worse - expands empty elements like <children /> into <children>\n\t</children>, quietly
        // bloating every re-saved file. Assert the self-closing form survives a round-trip.
        string strSaved = Encoding.Unicode.GetString(stream.ToArray());
        Assert.Contains("<children />", strSaved);
        Assert.DoesNotContain("<children>\r\n", strSaved);
        Assert.DoesNotContain("<children>\n", strSaved);

        // The stream must still be usable after Save() returns (callers like
        // CloudDocumentsDialogViewModel.SerializeActiveCharacter read it back immediately).
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(character.Name, reloaded.Name);
    }

    [Fact]
    public void Attributes_AugmentedValueIncludesAttributeImprovements()
    {
        CharacterDocument character = LoadFixture();

        // REA totalvalue 4, plus the fixture's Wired Reflexes +2 REA Improvement (stored in
        // Augmented, not Value - it boosts the augmented attribute without raising the base).
        CharacterAttributeData rea = character.Attributes.Single(a => a.Code == "REA");
        Assert.Equal("4", rea.TotalValue);
        Assert.Equal(6, rea.Augmented.Value);
        Assert.Contains("Wired Reflexes", rea.Augmented.Tooltip);

        // BOD has no Attribute-type Improvements in the fixture, so Augmented == TotalValue.
        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(int.Parse(bod.TotalValue), bod.Augmented.Value);
    }

    [Fact]
    public void Attributes_KarmaCostToIncreaseUsesCharacterOptionsKarmaAttribute()
    {
        CharacterDocument character = LoadFixture();

        // default.xml's karmaattribute is 5 and alternatemetatypeattributekarma is False, so cost
        // to raise REA's base Value (4) by one point is (4 + 1) * 5 = 25.
        CharacterAttributeData rea = character.Attributes.Single(a => a.Code == "REA");
        Assert.Equal(25, rea.KarmaCostToIncrease);
    }

    [Fact]
    public void Condition_ComputesEssenceFromCyberwareAndBioware()
    {
        CharacterDocument character = LoadFixture();

        // ESS metatypemax 6, Wired Reflexes (Cyberware) ess 1.5 and Cerebral Booster (Bioware)
        // ess 0.5: the higher cost (1.5) counts in full, the lower (0.5) at half -> 6 - 1.5 - 0.25.
        Assert.Equal("4.25", character.Condition.Essence);
    }

    [Fact]
    public void Condition_ComputesPhysicalAndStunTrackFromImprovements()
    {
        CharacterDocument character = LoadFixture();

        // BOD totalvalue 4 -> ceil(4/2) + 8 = 10, plus the fixture's +1 PhysicalCM improvement.
        Assert.Equal(11, character.Condition.PhysicalCm.Value);
        Assert.Contains("Wired Reflexes", character.Condition.PhysicalCm.Tooltip);
        // WIL totalvalue 3 -> ceil(3/2) + 8 = 10, no StunCM improvements in the fixture.
        Assert.Equal(10, character.Condition.StunCm.Value);
    }

    [Fact]
    public void ArmorEncumbrance_PenalizesOverThreshold_FormFittingCountsHalf()
    {
        CharacterDocument character = LoadFixture();

        // Threshold = BOD(4) * 2 = 8. Total ballistic = 6 (Actioneer) + 3 (Form-Fitting 6/2) = 9,
        // over threshold by 1 -> ceil(1/2) = 1 point of penalty.
        Assert.Equal(-1, character.ArmorEncumbrance.BallisticPenalty.Value);
        Assert.Contains("Actioneer", character.ArmorEncumbrance.BallisticPenalty.Tooltip);
        // Total impact = 4 + 3 (Form-Fitting 6/2) = 7, at/under threshold -> no penalty.
        Assert.Equal(0, character.ArmorEncumbrance.ImpactPenalty.Value);
    }

    [Fact]
    public void ArmorRating_UsesHighestEquippedPiece()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal(6, character.ArmorEncumbrance.BallisticRating.Value);
        Assert.Equal(6, character.ArmorEncumbrance.ImpactRating.Value);
        Assert.Contains("Actioneer Business Clothes: 6", character.ArmorEncumbrance.BallisticRating.Tooltip);
        Assert.Contains("Form-Fitting Bodysuit: 6", character.ArmorEncumbrance.ImpactRating.Tooltip);
    }

    [Fact]
    public void SpecialAttributeTests_SumTheirTwoAttributesPlusImprovements()
    {
        CharacterDocument character = LoadFixture();

        // WIL(3) + CHA(3) + Sixth Sense(+1) + Combat Sense(+2) - two different-sourced
        // Improvements stacking on the same stat, which the tooltip must list separately.
        Assert.Equal(9, character.Composure.Value);
        Assert.Equal(7, character.JudgeIntentions.Value); // INT(4) + CHA(3)
        Assert.Equal(7, character.LiftAndCarry.Value); // STR(3) + BOD(4)
        Assert.Equal(8, character.Memory.Value); // LOG(5) + WIL(3)

        Assert.Contains("Willenskraft: 3", character.Composure.Tooltip);
        Assert.Contains("Sixth Sense: +1", character.Composure.Tooltip);
        Assert.Contains("Combat Sense: +2", character.Composure.Tooltip);
        Assert.Contains("Gesamt: 9", character.Composure.Tooltip);
    }

    [Fact]
    public void Initiative_IsIntPlusRea_WithNoWoundModifiersInFixture()
    {
        CharacterDocument character = LoadFixture();

        Assert.Equal(8, character.Initiative.Base); // INT(4) + REA(4)
        Assert.Equal(8, character.Initiative.Augmented);
        Assert.Equal("8", character.Initiative.Display);
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
    public void Skill_DicePool_StacksRatingAndPoolAugmentationsFromDifferentSources()
    {
        CharacterDocument character = LoadFixture();
        CharacterSkillData pistolen = character.Skills.Single(s => s.Name == "Pistolen");

        // Base rating 4, Muscle Toner adds +1 to the rating itself (addtorating=True) -> "4 (5)".
        Assert.Equal("4", pistolen.BaseRating);
        Assert.Equal("4 (5)", pistolen.Rating);

        // Pool = augmented rating(5) + Smartlink's +2 pool-only bonus + AGI(6) = 13.
        Assert.Equal("13", pistolen.TotalValue);
        Assert.Contains("Muscle Toner: +1", pistolen.PoolTooltip);
        Assert.Contains("Smartlink: +2", pistolen.PoolTooltip);
    }

    [Fact]
    public void KnowledgeSkill_DicePool_ComputedTheSameWayAsActiveSkills()
    {
        CharacterDocument character = LoadFixture();
        CharacterSkillData knowledgeSkill = character.KnowledgeSkills.Single(s => s.Name == "Straßenwissen");

        // Straßenwissen: rating 3, attribute INT(4) -> pool 7, no Improvements targeting it.
        Assert.Equal("3", knowledgeSkill.BaseRating);
        Assert.Equal("3", knowledgeSkill.Rating);
        Assert.Equal("7", knowledgeSkill.TotalValue);
    }

    [Fact]
    public void AstralAndMatrixInitiative_ComputeFromIntuition()
    {
        CharacterDocument character = LoadFixture();

        // INT(4) * 2 = 8, no wound modifiers in the fixture.
        Assert.Equal(8, character.AstralInitiative.Base);
        Assert.Equal(8, character.AstralInitiative.Augmented);

        // Default non-Technomancer path: just INT(4), no MatrixInitiative Improvements.
        Assert.Equal(4, character.MatrixInitiative.Base);
        Assert.Equal(1, character.MatrixInitiativePasses.Base);
    }

    [Fact]
    public void CareerKarmaAndNuyen_SumOnlyPositiveNonRefundEntries()
    {
        CharacterDocument character = LoadFixture();

        // The fixture's one Karma entry is +5 (earned) -> CareerKarma 5.
        Assert.Equal(5, character.CareerKarma);
        // The fixture's one Nuyen entry is -500 (spent, not earned) -> CareerNuyen 0.
        Assert.Equal(0, character.CareerNuyen);
    }

    private static string AttributeXml(string strCode, string strValue) =>
        "<attribute><name>" + strCode + "</name><value>" + strValue + "</value><totalvalue>" + strValue
        + "</totalvalue><metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax></attribute>";

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
    public void MatrixInitiative_DefaultPath_AddsActiveEquippedCommlinkResponse()
    {
        var character = LoadXml("<character><name>Runner</name><metatype>Human</metatype><attributes>"
            + AttributeXml("INT", "4") + "</attributes><gears><gear><name>Fancy Commlink</name>"
            + "<category>Commlinks</category><equipped>True</equipped><active>True</active>"
            + "<response>5</response></gear></gears></character>");

        Assert.Equal(9, character.MatrixInitiative.Base); // INT(4) + Response(5)
        Assert.Contains("Kommlink-Antwort: 5", character.MatrixInitiative.Tooltip);
    }

    [Fact]
    public void MatrixInitiative_DefaultPath_IgnoresInactiveCommlink()
    {
        var character = LoadXml("<character><name>Runner</name><metatype>Human</metatype><attributes>"
            + AttributeXml("INT", "4") + "</attributes><gears><gear><name>Fancy Commlink</name>"
            + "<category>Commlinks</category><equipped>True</equipped><active>False</active>"
            + "<response>5</response></gear></gears></character>");

        Assert.Equal(4, character.MatrixInitiative.Base); // Response not counted - commlink isn't active.
    }
}
