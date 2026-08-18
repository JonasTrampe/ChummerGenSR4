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
    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
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
    public void AddSpell_ExtendedDetectionSpell_PersistsFlagAndDisplaysLegacyPlusTwoDrain()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ExtendAnyDetectionSpell = true });

        Assert.True(character.AddSpell("Detect Life", "Detection", "M", "T", "", "S", "(F/2)", "SR4", "206",
            blnExtended: true));

        CharacterSpellData spell = Assert.Single(character.Spells);
        Assert.True(spell.Extended);
        Assert.Equal("Detect Life, Extended", spell.DisplayName);
        Assert.Equal("(F/2)+2", spell.Dv);
        Assert.True(character.RemoveSpell(spell.DisplayName));
        Assert.Empty(character.Spells);
    }

    [Fact]
    public void AddSpell_ExtendedSpellRejectsDisabledRuleAndNonDetectionCategories()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.False(character.AddSpell("Detect Life", "Detection", "M", "T", "", "S", "(F/2)", "SR4", "206",
            blnExtended: true));

        character.SetCharacterOptionsForTesting(new CharacterOptions { ExtendAnyDetectionSpell = true });
        Assert.False(character.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204",
            blnExtended: true));
        Assert.Empty(character.Spells);
    }

    [Fact]
    public void AddSpell_CreationChargesAndRefundsTheActivePool()
    {
        CharacterDocument karmaCreation = LoadXml("<character><created>False</created><buildmethod>Karma</buildmethod><startingbuildpoints>10</startingbuildpoints><karma>10</karma></character>");
        karmaCreation.SetCharacterOptionsForTesting(new CharacterOptions { KarmaSpell = 4 });

        Assert.True(karmaCreation.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204"));
        Assert.Equal("6", karmaCreation.Karma);
        Assert.True(karmaCreation.RemoveSpell("Acid Stream"));
        Assert.Equal("10", karmaCreation.Karma);

        CharacterDocument bpCreation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>3</startingbuildpoints><bp>3</bp></character>");
        Assert.True(bpCreation.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204"));
        Assert.Equal("0", bpCreation.Bp);
        Assert.False(bpCreation.AddSpell("Clout", "Combat", "P", "LOS", "P", "I", "(F/2)+1", "SR4", "204"));
    }

    [Fact]
    public void SpellDialog_ExtendedDetectionRuleOffersBaseSpellAndPreviewsExtendedValues()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ExtendAnyDetectionSpell = true });
        var viewModel = new SpellDialogViewModel();

        viewModel.LoadOptions(character);
        SpellOptionViewModel detectLife = Assert.Single(viewModel.SpellOptions, s => s.Name == "Detect Life");
        Assert.DoesNotContain(viewModel.SpellOptions, s => s.Name == "Detect Life, Extended");

        viewModel.SelectedSpell = detectLife;
        Assert.True(viewModel.CanSelectExtendedSpell);
        viewModel.IsExtendedSpell = true;
        Assert.Equal("(F/2)+2", viewModel.SelectedSpellDrainValue);
        Assert.Contains("Extended Area", viewModel.SelectedSpellDescriptor);
    }

    [Fact]
    public void ComputeCustomSpellDv_ManaLosTouchless_MatchesTheBaseFPlus2Formula()
    {
        // Mana (+0), LOS (+0), no Area, not Restricted, Instant duration (+0) - just the (F/2) base.
        string strDv = CharacterDocument.ComputeCustomSpellDv("Combat", "M", "LOS", blnArea: false,
            blnRestricted: false, blnVeryRestricted: false, "I", new HashSet<string>(), intNumberOfEffects: 0);
        Assert.Equal("(F/2)", strDv);
    }

    [Fact]
    public void ComputeCustomSpellDv_PhysicalTouchPermanentRestricted_SumsAllTheModifiers()
    {
        // Physical (+1), Touch (-2), Permanent duration (+2), Restricted (-1) = +0 net, still shown blank.
        string strDv = CharacterDocument.ComputeCustomSpellDv("Combat", "P", "T", blnArea: false,
            blnRestricted: true, blnVeryRestricted: false, "P", new HashSet<string>(), intNumberOfEffects: 0);
        Assert.Equal("(F/2)", strDv);
    }

    [Fact]
    public void ComputeCustomSpellDv_AreaAndModifiers_AddsThemIn()
    {
        var setKeys = new HashSet<string> { "direct", "physicaldamage" };
        // Physical (+1) + LOS (+0) + Area (+2) + Direct (+0) + Physical damage (+0) = +3.
        string strDv = CharacterDocument.ComputeCustomSpellDv("Combat", "P", "LOS", blnArea: true,
            blnRestricted: false, blnVeryRestricted: false, "I", setKeys, intNumberOfEffects: 0);
        Assert.Equal("(F/2)+3", strDv);
    }

    [Fact]
    public void ComputeCustomSpellDv_CombatElementalMultipliesByNumberOfEffects()
    {
        var setKeys = new HashSet<string> { "direct", "physicaldamage", "elemental" };
        // Physical (+1) + LOS (+0) + Direct/Physical damage (+0) + Elemental (+2 * 3 effects) = +7.
        string strDv = CharacterDocument.ComputeCustomSpellDv("Combat", "P", "LOS", blnArea: false,
            blnRestricted: false, blnVeryRestricted: false, "I", setKeys, intNumberOfEffects: 3);
        Assert.Equal("(F/2)+7", strDv);
    }

    [Fact]
    public void ComputeCustomSpellDv_HealthCurative_UsesDamageValueBaseAndSkipsThePermanentPenalty()
    {
        var setKeys = new HashSet<string> { "curative" };
        // Mana (+0) + LOS (+0) + Curative Permanent duration exemption (+0) = +0 net.
        string strDv = CharacterDocument.ComputeCustomSpellDv("Health", "M", "LOS", blnArea: false,
            blnRestricted: false, blnVeryRestricted: false, "P", setKeys, intNumberOfEffects: 0);
        Assert.Equal("(Damage Value)", strDv);
    }

    [Fact]
    public void AddCustomSpell_BuildsAndAddsARealSpellRow()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        var setKeys = new HashSet<string> { "direct", "physicaldamage" };

        Assert.True(character.AddCustomSpell("Homebrew Bolt", "Combat", "P", "LOS", blnArea: false,
            blnRestricted: false, blnVeryRestricted: false, "I", setKeys, intNumberOfEffects: 0));

        CharacterSpellData added = Assert.Single(character.Spells);
        Assert.Equal("Homebrew Bolt", added.Name);
        Assert.Equal("Combat", added.Category);
        Assert.Equal("P", added.Damage);
        Assert.Equal("(F/2)+1", added.Dv);
        Assert.Equal("SM", added.Source);
        Assert.Equal("159", added.Page);
    }

    [Fact]
    public void AddCustomSpell_RejectsAnEmptyName()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.False(character.AddCustomSpell("", "Combat", "P", "LOS", false, false, false, "I",
            new HashSet<string>(), 0));
        Assert.Empty(character.Spells);
    }

    [Fact]
    public void GetSpellModifierOptions_ReturnsTheRealCatalogForEachCategory()
    {
        Assert.Contains(CharacterDocument.GetSpellModifierOptions("Combat"), o => o.Key == "elemental" && o.Dv == 2);
        Assert.Contains(CharacterDocument.GetSpellModifierOptions("Detection"), o => o.Key == "extendedarea" && o.Dv == 2);
        Assert.Contains(CharacterDocument.GetSpellModifierOptions("Health"), o => o.Key == "curative" && o.Dv == 0);
        Assert.Contains(CharacterDocument.GetSpellModifierOptions("Illusion"), o => o.Key == "obvious" && o.Dv == -1);
        Assert.Contains(CharacterDocument.GetSpellModifierOptions("Manipulation"), o => o.Key == "environmental" && o.Dv == -2);
    }

    [Fact]
    public void Spells_DicePool_IsSpellcastingRatingPlusSpecializationPlusSpellCategoryImprovements()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><skills>"
            + "<skill><name>Spellcasting</name><attribute>MAG</attribute><rating>4</rating>"
            + "<knowledge>False</knowledge><spec>Combat</spec><allowdelete>True</allowdelete></skill>"
            + "</skills><improvements><improvement><improvementttype>SpellCategory</improvementttype>"
            + "<improvementsource>Quality</improvementsource><improvedname>Combat</improvedname>"
            + "<val>1</val><enabled>True</enabled></improvement></improvements></character>");
        character.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204");
        character.AddSpell("Detect Life", "Detection", "M", "LOS", "", "S", "L", "SR4", "207");

        CharacterSpellData acidStream = character.Spells.Single(s => s.Name == "Acid Stream");
        CharacterSpellData detectLife = character.Spells.Single(s => s.Name == "Detect Life");

        // Acid Stream (Combat): Spellcasting(4) + Specialization match(+2) + SpellCategory Improvement(+1) = 7.
        Assert.Equal("7", acidStream.DicePool);
        // Detect Life (Detection): Spellcasting(4) only, no specialization/category match = 4.
        Assert.Equal("4", detectLife.DicePool);
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
    public void Save_PreservesCompactFormattingAcrossARoundTrip()
    {
        CharacterDocument character = LoadFixture();

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");

        // XmlDocument.Save(Stream) (what this used to do) re-indents with wider whitespace and -
        // worse - expands empty elements like <children /> into <children>\n\t</children>, quietly
        // bloating every re-saved file. Assert the self-closing form survives a round-trip - the
        // fixture's innermost gear (no nested children of its own) still has an empty one.
        string strSaved = Encoding.Unicode.GetString(stream.ToArray());
        Assert.Contains("<children />", strSaved);

        // The stream must still be usable after Save() returns (callers like
        // CloudDocumentsDialogViewModel.SerializeActiveCharacter read it back immediately).
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(character.Name, reloaded.Name);
    }

    [Fact]
    public void DrainResistance_HermeticTradition_UsesWilPlusLogFormula()
    {
        CharacterDocument character = LoadXml("<character><magician>True</magician><attributes>"
            + AttributeXml("WIL", "4") + AttributeXml("LOG", "5") + "</attributes></character>");
        character.Tradition = "Hermetic";

        var drain = character.DrainResistance;

        Assert.NotNull(drain);
        Assert.Equal(9, drain.Value); // WIL(4) + LOG(5).
        Assert.Contains("Willenskraft: 4", drain.Tooltip);
        Assert.Contains("Logik: 5", drain.Tooltip);
    }

    [Fact]
    public void DrainResistance_NullWithoutMagicianOrTradition()
    {
        CharacterDocument nonMagician = LoadXml("<character><attributes>" + AttributeXml("WIL", "4")
            + AttributeXml("LOG", "5") + "</attributes></character>");
        nonMagician.Tradition = "Hermetic";
        Assert.Null(nonMagician.DrainResistance);

        CharacterDocument noTraditionPicked = LoadXml("<character><magician>True</magician></character>");
        Assert.Null(noTraditionPicked.DrainResistance);
    }

    [Fact]
    public void FadingResistance_DefaultStream_UsesWilPlusResFormula()
    {
        CharacterDocument character = LoadXml("<character><technomancer>True</technomancer><attributes>"
            + AttributeXml("WIL", "3") + AttributeXml("RES", "6") + "</attributes></character>");
        character.Stream = "Default";

        var fading = character.FadingResistance;

        Assert.NotNull(fading);
        Assert.Equal(9, fading.Value); // WIL(3) + RES(6).
    }

}
