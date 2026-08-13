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
    public void PetCharacterLink_PersistsAcrossSaveReload()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddPet("Sparky");
        int intPetId = Assert.Single(character.Pets).ContactId;

        Assert.True(character.UpdateContactFile(intPetId, "/characters/sparky.chum", "../characters/sparky.chum"));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterContactData pet = Assert.Single(reloaded.Pets);

        Assert.Equal("/characters/sparky.chum", pet.FileName);
        Assert.Equal("../characters/sparky.chum", pet.RelativeFileName);
    }

    [Fact]
    public void PetCharacterLink_ClearedByUpdatingWithEmptyPaths()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddPet("Sparky");
        int intPetId = Assert.Single(character.Pets).ContactId;
        character.UpdateContactFile(intPetId, "/characters/sparky.chum", "../characters/sparky.chum");

        Assert.True(character.UpdateContactFile(intPetId, string.Empty, string.Empty));

        CharacterContactData pet = Assert.Single(character.Pets);
        Assert.Equal(string.Empty, pet.FileName);
        Assert.Equal(string.Empty, pet.RelativeFileName);
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
    public void AddQuality_AnalyticalMind_AppliesItsSpecificSkillBonuses()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddQuality("Analytical Mind", "Positive");

        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Data Search"));
        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Software"));
        Assert.Equal(2, character.Improvements.Count(i => i.SourceName == "Analytical Mind"));
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
    public void GetQualitySkillSelectionOptions_Aptitude_ListsAllOwnedActiveSkills()
    {
        CharacterDocument character = LoadCharacterWithSkill(3); // "Pistolen", Combat Active, Firearms group.

        var options = character.GetQualitySkillSelectionOptions("Aptitude"); // No filter on real data.

        Assert.Contains("Pistolen", options);
    }

    [Fact]
    public void AddQuality_Aptitude_AppliesSelectedSkillsMaxBonus()
    {
        CharacterDocument character = LoadCharacterWithSkill(3);
        character.AddQuality("Aptitude", "Positive", "Pistolen");

        var improvement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Skill, improvement.Type);
        Assert.Equal("Pistolen", improvement.ImprovedName);
        Assert.Equal(1, improvement.Maximum); // Aptitude's <max>1</max>.

        Assert.True(character.RemoveQuality("Aptitude", "Positive", "Pistolen"));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void GetQualitySkillSelectionOptions_ExoticSkillsSharingAName_AreOfferedAsDistinctSpecializations()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><skills>"
            + "<skill><name>Exotic Ranged Weapon</name><attribute>AGI</attribute>"
            + "<skillcategory>Combat Active</skillcategory><rating>3</rating><knowledge>False</knowledge>"
            + "<exotic>True</exotic><spec>Bow</spec><allowdelete>True</allowdelete></skill>"
            + "<skill><name>Exotic Ranged Weapon</name><attribute>AGI</attribute>"
            + "<skillcategory>Combat Active</skillcategory><rating>2</rating><knowledge>False</knowledge>"
            + "<exotic>True</exotic><spec>Grenade Launcher</spec><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");

        var options = character.GetQualitySkillSelectionOptions("Aptitude");

        Assert.Contains("Exotic Ranged Weapon (Bow)", options);
        Assert.Contains("Exotic Ranged Weapon (Grenade Launcher)", options);
        Assert.DoesNotContain("Exotic Ranged Weapon", options); // Bare name is ambiguous, not offered.
    }

    [Fact]
    public void AddQuality_Aptitude_OnAnExoticSkill_OnlyBoostsTheSelectedSpecializationsPool()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><skills>"
            + "<skill><name>Exotic Ranged Weapon</name><attribute>AGI</attribute>"
            + "<skillcategory>Combat Active</skillcategory><rating>3</rating><knowledge>False</knowledge>"
            + "<exotic>True</exotic><spec>Bow</spec><allowdelete>True</allowdelete></skill>"
            + "<skill><name>Exotic Ranged Weapon</name><attribute>AGI</attribute>"
            + "<skillcategory>Combat Active</skillcategory><rating>2</rating><knowledge>False</knowledge>"
            + "<exotic>True</exotic><spec>Grenade Launcher</spec><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");

        character.AddQuality("Aptitude", "Positive", "Exotic Ranged Weapon (Bow)");

        var improvement = Assert.Single(character.Improvements);
        // Stored under the specialized key (Aptitude's <max>1</max>), so it only targets the Bow
        // instance - not every Exotic Ranged Weapon skill sharing the same bare name.
        Assert.Equal("Exotic Ranged Weapon (Bow)", improvement.ImprovedName);
        Assert.Equal(1, improvement.Maximum);
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
    public void AddAdeptPower_ImprovedAbilityCombat_AppliesRatingScaledSkillBonusToSelectedSkill()
    {
        CharacterDocument character = LoadCharacterWithSkill(3); // "Pistolen", Combat Active.
        character.AddAdeptPower("Improved Ability (Combat)", "2", ".5", "Pistolen");

        var improvement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Skill, improvement.Type);
        Assert.Equal("Pistolen", improvement.ImprovedName);
        Assert.Equal(2, improvement.Value); // val = Rating, Rating = 2.
        Assert.True(improvement.AddToRating);
    }

    [Fact]
    public void GetAdeptPowerSkillSelectionOptions_ImprovedAbilityCombat_OnlyListsCombatActiveSkills()
    {
        CharacterDocument character = LoadCharacterWithSkill(3); // "Pistolen", Combat Active.

        var combatOptions = character.GetAdeptPowerSkillSelectionOptions("Improved Ability (Combat)");
        Assert.Contains("Pistolen", combatOptions);

        var nonCombatOptions = character.GetAdeptPowerSkillSelectionOptions("Improved Ability (Non-Combat)");
        Assert.DoesNotContain("Pistolen", nonCombatOptions); // excludecategory="Combat Active,...".
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
    public void Spells_DicePool_IsZeroWithoutASpellcastingSkill()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204");

        Assert.Equal("0", Assert.Single(character.Spells).DicePool);
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
    public void AddNexus_BuildsAndAddsTheAssembledGear()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        // Processor 10, Response 3, System 2, Firewall 2, Signal 3, Persona 3 - matches
        // frmSelectNexus.cs's CalculateNexus: Response<=3 -> 3*10*50=1500; System<=3 ->
        // 2*3*25=150; Firewall<=3 -> 2*10*25=500; Signal 3 -> 150. Total 2300.
        Assert.True(character.AddNexus(intProcessor: 10, intResponse: 3, intSystem: 2, intFirewall: 2,
            intSignal: 3, intPersona: 3));

        CharacterTreeItemData added = Assert.Single(character.Gear);
        Assert.Equal("Nexus (Processor 10)", added.Name);
        Assert.Equal("Nexus", added.Category);
        Assert.Equal("2300", added.Cost);
        Assert.Equal("0", added.Avail);
        Assert.Equal("3", added.Response);
        Assert.Equal("3", added.Signal);
        Assert.Equal("2", added.System);
        Assert.Equal("2", added.Firewall);
    }

    [Fact]
    public void AddNexus_Free_SkipsTheCost()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.AddNexus(intProcessor: 10, intResponse: 3, intSystem: 2, intFirewall: 2,
            intSignal: 3, intPersona: 3, blnFree: true));

        Assert.Equal("0", Assert.Single(character.Gear).Cost);
    }

    [Fact]
    public void Vehicles_ReadSavedStatsAndInstalledItemTree()
    {
        CharacterDocument character = LoadXml(
            "<character><vehicles><vehicle>"
            + "<name>Hyundai Shin-Hyung</name><category>Cars</category><handling>3</handling>"
            + "<accel>15</accel><speed>180</speed><pilot>2</pilot><body>10</body><armor>6</armor>"
            + "<sensor>2</sensor><devicerating>3</devicerating><avail>4</avail><cost>16000</cost><addslots>2</addslots>"
            + "<source>SR4</source><page>351</page><physicalcmfilled>1</physicalcmfilled>"
            + "<mods><mod><name>Armor</name><category>Vehicle Mod</category><rating>2</rating><cost>1000</cost></mod></mods>"
            + "<gears><gear><name>Vehicle Toolkit</name><category>Tools</category><qty>1</qty></gear></gears>"
            + "<weapons><weapon><name>LMG</name><category>Machine Guns</category><damage>6P</damage></weapon></weapons>"
            + "</vehicle></vehicles></character>");

        CharacterVehicleData vehicle = Assert.Single(character.Vehicles);
        Assert.Equal("180", vehicle.Speed);
        Assert.Equal("16000", vehicle.Cost);
        Assert.Equal(3, vehicle.Children.Count);
        Assert.Equal("Armor", vehicle.Children[0].Name);
        Assert.Equal("Vehicle Toolkit", vehicle.Children[1].Name);
        Assert.Equal("LMG", vehicle.Children[2].Name);
    }

    [Fact]
    public void AddAndRemoveVehicle_PersistsTheLegacyVehicleShapeAndDeductsCost()
    {
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen></character>");
        character.AddVehicle("Hyundai Shin-Hyung", "Cars", "3", "15", "180", "2", "10", "6", "2", "3",
            "4", "16000", "SR4", "351");

        CharacterVehicleData vehicle = Assert.Single(character.Vehicles);
        Assert.Equal("180", vehicle.Speed);
        Assert.Equal("34000", character.Nuyen);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Hyundai Shin-Hyung", Assert.Single(reloaded.Vehicles).Name);
        Assert.True(reloaded.RemoveVehicle("Hyundai Shin-Hyung", "Cars"));
        Assert.Empty(reloaded.Vehicles);
    }

    [Fact]
    public void RemoveGear_RemovesOnlyMatchingRootLevelEntry()
    {
        CharacterDocument character = LoadXml("<character><gears><gear><name>Medkit</name><category>Biotech</category><rating>6</rating></gear><gear><name>Medkit</name><category>Biotech</category><rating>3</rating></gear></gears></character>");

        int intGearId = character.Gear[0].GearId;
        Assert.True(character.RemoveGear(intGearId));
        CharacterTreeItemData remaining = Assert.Single(character.Gear);
        Assert.Equal("3", remaining.Rating);
    }

    [Fact]
    public void AddGear_WritesQuantityCostAvailAndSource()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Stim Patch", "Biotech", "0", "5", "50", "4", "SR4 60");

        CharacterTreeItemData added = Assert.Single(character.Gear);
        Assert.Equal("5", added.Qty);
        Assert.Equal("50", added.Cost);
        Assert.Equal("4", added.Avail);
        Assert.True(added.GearId >= 0);
    }

    [Fact]
    public void AddChildGear_NestsUnderTheParentAndCanBeRemovedById()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Commlink", "Commlink", "0");
        int intParentId = character.Gear[0].GearId;

        Assert.True(character.AddChildGear(intParentId, "Certified Credstick, Silver", "Commlink Accessory", "0"));
        CharacterTreeItemData parent = character.Gear[0];
        CharacterTreeItemData child = Assert.Single(parent.Children);
        Assert.Equal("Certified Credstick, Silver", child.Name);
        Assert.True(child.GearId > intParentId);

        Assert.True(character.RemoveGear(child.GearId));
        Assert.Empty(character.Gear[0].Children);
    }

    [Fact]
    public void AddChildGear_EnforcesCapacityWhenTheHouseRuleIsOn()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Commlink", "Commlink", "0", "1", "", "", "", "", "2");
        int intParentId = character.Gear[0].GearId;

        // Fits exactly within the parent's capacity of 2.
        Assert.True(character.AddChildGear(intParentId, "Sim Module", "Commlink Accessory", "0", "1", "", "", "", "",
            "2"));
        // No capacity left for a second item.
        Assert.False(character.AddChildGear(intParentId, "Simsense Booster", "Commlink Accessory", "0", "1", "", "",
            "", "", "1"));
        Assert.Single(character.Gear[0].Children);

        character.SetCharacterOptionsForTesting(new CharacterOptions { EnforceCapacity = false });
        Assert.True(character.AddChildGear(intParentId, "Simsense Booster", "Commlink Accessory", "0", "1", "", "",
            "", "", "1"));
        Assert.Equal(2, character.Gear[0].Children.Count);
    }

    [Fact]
    public void SetGearQuantity_UpdatesAnExistingItemsCount()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Stim Patch", "Biotech", "0", "1");
        int intGearId = character.Gear[0].GearId;

        Assert.True(character.SetGearQuantity(intGearId, "10"));
        Assert.Equal("10", character.Gear[0].Qty);
    }

    [Fact]
    public void SetGearNotes_UpdatesNestedGearAndSurvivesSaveReload()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Commlink", "Commlink", "0");
        character.AddChildGear(character.Gear[0].GearId, "Credstick", "ID/Credsticks", "0");
        int intNestedGearId = character.Gear[0].Children[0].GearId;

        Assert.True(character.SetGearNotes(intNestedGearId, "Paid for by the Johnson."));
        Assert.Equal("Paid for by the Johnson.", character.Gear[0].Children[0].Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Paid for by the Johnson.", reloaded.Gear[0].Children[0].Notes);
    }

    [Fact]
    public void SetGearCustomName_UpdatesNestedGearWithoutChangingRulesName()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Commlink", "Commlink", "0");
        character.AddChildGear(character.Gear[0].GearId, "Credstick", "ID/Credsticks", "0");
        int intNestedGearId = character.Gear[0].Children[0].GearId;

        Assert.True(character.SetGearCustomName(intNestedGearId, "Fake identity #3"));
        CharacterTreeItemData nested = character.Gear[0].Children[0];
        Assert.Equal("Credstick", nested.Name);
        Assert.Equal("Fake identity #3", nested.CustomName);
    }

    [Fact]
    public void WeaponMetadata_UsesLegacyFieldsAndKeepsDuplicatePurchasesDistinct()
    {
        CharacterDocument character = LoadXml("<character><weapons>"
            + "<weapon><name>Ares Predator IV</name><category>Pistols</category></weapon>"
            + "<weapon><name>Ares Predator IV</name><category>Pistols</category></weapon>"
            + "</weapons></character>");

        Assert.True(character.SetWeaponCustomName(1, "Backup pistol"));
        Assert.True(character.SetWeaponNotes(1, "Keep loaded."));

        Assert.Equal(string.Empty, character.WeaponTrees[0].CustomName);
        Assert.Equal("Backup pistol", character.WeaponTrees[1].CustomName);
        Assert.Equal("Keep loaded.", character.WeaponTrees[1].Notes);
        Assert.Equal("Ares Predator IV", character.WeaponTrees[1].Name);
    }

    [Fact]
    public void MoveGear_ReordersRootLevelSiblingsAndPersists()
    {
        CharacterDocument character = LoadXml("<character><gears>"
            + "<gear><name>A</name><category>Biotech</category></gear>"
            + "<gear><name>B</name><category>Biotech</category></gear>"
            + "<gear><name>C</name><category>Biotech</category></gear>"
            + "</gears></character>");
        int intAId = character.Gear[0].GearId;
        int intCId = character.Gear[2].GearId;

        // Move C in front of A.
        Assert.True(character.MoveGear(intCId, intAId, blnReparent: false));
        Assert.Equal(new[] { "C", "A", "B" }, character.Gear.Select(g => g.Name));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(new[] { "C", "A", "B" }, reloaded.Gear.Select(g => g.Name));
    }

    [Fact]
    public void MoveGear_ReparentsAsAChildAndPersists()
    {
        CharacterDocument character = LoadXml("<character><gears>"
            + "<gear><name>Commlink</name><category>Commlink</category></gear>"
            + "<gear><name>Credstick</name><category>Commlink Accessory</category></gear>"
            + "</gears></character>");
        int intCommlinkId = character.Gear[0].GearId;
        int intCredstickId = character.Gear[1].GearId;

        Assert.True(character.MoveGear(intCredstickId, intCommlinkId, blnReparent: true));
        CharacterTreeItemData root = Assert.Single(character.Gear);
        Assert.Equal("Commlink", root.Name);
        Assert.Equal("Credstick", Assert.Single(root.Children).Name);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterTreeItemData reloadedRoot = Assert.Single(reloaded.Gear);
        Assert.Equal("Credstick", Assert.Single(reloadedRoot.Children).Name);
    }

    [Fact]
    public void MoveGear_RejectsMovingAnItemIntoItsOwnSubtree()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Commlink", "Commlink", "0");
        int intParentId = character.Gear[0].GearId;
        character.AddChildGear(intParentId, "Credstick", "Commlink Accessory", "0");
        int intChildId = character.Gear[0].Children[0].GearId;

        Assert.False(character.MoveGear(intParentId, intChildId, blnReparent: true));
        Assert.False(character.MoveGear(intParentId, intChildId, blnReparent: false));
        Assert.Single(character.Gear);
        Assert.Single(character.Gear[0].Children);
    }

    [Fact]
    public void MoveWeapon_ReordersSiblingsAndPersists()
    {
        CharacterDocument character = LoadXml("<character><weapons>"
            + "<weapon><guid>" + Guid.NewGuid() + "</guid><name>A</name><category>Pistols</category></weapon>"
            + "<weapon><guid>" + Guid.NewGuid() + "</guid><name>B</name><category>Pistols</category></weapon>"
            + "<weapon><guid>" + Guid.NewGuid() + "</guid><name>C</name><category>Pistols</category></weapon>"
            + "</weapons></character>");
        Guid guiA = Guid.Parse(character.WeaponTrees[0].ItemGuid);
        Guid guiC = Guid.Parse(character.WeaponTrees[2].ItemGuid);

        Assert.True(character.MoveWeapon(guiC, guiA));
        Assert.Equal(new[] { "C", "A", "B" }, character.WeaponTrees.Select(w => w.Name));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(new[] { "C", "A", "B" }, reloaded.WeaponTrees.Select(w => w.Name));
    }

    [Fact]
    public void MoveArmor_ReordersSiblingsAndPersists()
    {
        CharacterDocument character = LoadXml("<character><armors>"
            + "<armor><name>A</name><category>Armor</category></armor>"
            + "<armor><name>B</name><category>Armor</category></armor>"
            + "<armor><name>C</name><category>Armor</category></armor>"
            + "</armors></character>");
        int intA = character.Armor[0].ArmorId;
        int intC = character.Armor[2].ArmorId;

        Assert.True(character.MoveArmor(intC, intA));
        Assert.Equal(new[] { "C", "A", "B" }, character.Armor.Select(a => a.Name));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(new[] { "C", "A", "B" }, reloaded.Armor.Select(a => a.Name));
    }

    [Fact]
    public void MoveCyberware_ReordersRootLevelSiblingsAndPersists()
    {
        CharacterDocument character = LoadXml("<character><cyberwares>"
            + "<cyberware><name>A</name><category>Headware</category><improvementsource>Cyberware</improvementsource></cyberware>"
            + "<cyberware><name>B</name><category>Headware</category><improvementsource>Cyberware</improvementsource></cyberware>"
            + "<cyberware><name>C</name><category>Headware</category><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");
        int intAId = character.Cyberware[0].CyberwareId;
        int intCId = character.Cyberware[2].CyberwareId;

        Assert.True(character.MoveCyberware(intCId, intAId, blnReparent: false));
        Assert.Equal(new[] { "C", "A", "B" }, character.Cyberware.Select(c => c.Name));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(new[] { "C", "A", "B" }, reloaded.Cyberware.Select(c => c.Name));
    }

    [Fact]
    public void MoveCyberware_ReparentsAsAChildAndPersists()
    {
        CharacterDocument character = LoadXml("<character><cyberwares>"
            + "<cyberware><name>Cybereyes</name><category>Cyberlimb</category><improvementsource>Cyberware</improvementsource></cyberware>"
            + "<cyberware><name>Vision Enhancement</name><category>Eyeware</category><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");
        int intEyesId = character.Cyberware[0].CyberwareId;
        int intEnhancementId = character.Cyberware[1].CyberwareId;

        Assert.True(character.MoveCyberware(intEnhancementId, intEyesId, blnReparent: true));
        CharacterTreeItemData root = Assert.Single(character.Cyberware);
        Assert.Equal("Cybereyes", root.Name);
        Assert.Equal("Vision Enhancement", Assert.Single(root.Children).Name);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterTreeItemData reloadedRoot = Assert.Single(reloaded.Cyberware);
        Assert.Equal("Vision Enhancement", Assert.Single(reloadedRoot.Children).Name);
    }

    [Fact]
    public void MoveCyberware_RejectsMovingAnItemIntoItsOwnSubtree()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        int intParentId = character.Cyberware[0].CyberwareId;

        character.AddCyberware("Vision Enhancement", "Eyeware", "0", "0.1", "500", "4R", "SR4", "339");
        int intOtherId = character.Cyberware[1].CyberwareId;
        Assert.True(character.MoveCyberware(intOtherId, intParentId, blnReparent: true));
        int intChildId = character.Cyberware[0].Children[0].CyberwareId;

        Assert.False(character.MoveCyberware(intParentId, intChildId, blnReparent: true));
        Assert.False(character.MoveCyberware(intParentId, intChildId, blnReparent: false));
    }

    [Fact]
    public void MoveCyberware_UsesConsistentIdsAcrossCyberwareAndBioware()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        character.AddCyberware("Muscle Toner", "Basic", "2", "0.4", "8000", "8R", "SR4", "339", blnBioware: true);

        // Both trees share one global CyberwareId numbering, so reordering across the shared
        // underlying <cyberwares> list works regardless of which tree (Cyberware or Bioware) a
        // caller reads the ID from.
        int intCyberwareId = Assert.Single(character.Cyberware).CyberwareId;
        int intBiowareId = Assert.Single(character.Bioware).CyberwareId;
        Assert.NotEqual(intCyberwareId, intBiowareId);
    }

    [Fact]
    public void AddCyberware_CustomTransgenic_RequiresRuleAndPersistsForcedCategoryAndStandardGrade()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        Assert.Throws<InvalidOperationException>(() => character.AddCyberware("Muscle Toner", "Basic", "2",
            "0.4", "16000", "8R", "SR4", "339", "Betaware", blnBioware: true, blnTransgenic: true));

        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowCustomTransgenics = true });
        character.AddCyberware("Muscle Toner", "Basic", "2", "0.4", "16000", "8R", "SR4", "339",
            "Betaware", blnBioware: true, blnTransgenic: true);

        CharacterTreeItemData item = Assert.Single(character.Bioware);
        Assert.Equal("Genetech: Transgenics", item.Category);
        Assert.True(item.IsTransgenic);
        Assert.Equal("Standard", character.Document.SelectSingleNode("/character/cyberwares/cyberware/grade")?.InnerText);
    }

    [Fact]
    public void AddWeapon_MutatesCharacterTreeAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15",
            "350", "4R", "SR4", "313");

        CharacterTreeItemData added = Assert.Single(character.WeaponTrees);
        Assert.Equal("Ares Predator IV", added.Name);
        Assert.Equal("Heavy Pistols", added.Category);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterWeaponData savedWeapon = Assert.Single(reloaded.Weapons);
        Assert.Equal("Ares Predator IV", savedWeapon.Name);
        Assert.Equal("6P", savedWeapon.Damage);
    }

    [Fact]
    public void AddWeaponAccessory_NestsUnderTheWeaponDeductsCostAndCanBeRemoved()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        Assert.True(character.AddWeaponAccessory(guiWeaponId, "Laser Sight", "Top", string.Empty, "4", "200", "SR4", "321"));
        CharacterTreeItemData weapon = character.WeaponTrees.Single();
        CharacterTreeItemData accessory = Assert.Single(weapon.Children);
        Assert.Equal("Laser Sight", accessory.Name);
        Assert.True(accessory.IsWeaponAccessory);
        Assert.False(accessory.IsWeaponMod);
        Assert.Equal("450", character.Nuyen); // 1000 - 350 (weapon) - 200

        Assert.True(Guid.TryParse(accessory.ItemGuid, out Guid guiAccessoryId));
        Assert.True(character.RemoveWeaponAccessory(guiWeaponId, guiAccessoryId));
        Assert.Empty(character.WeaponTrees.Single().Children);
    }

    [Fact]
    public void Weapons_DicePool_IncludesInstalledAccessoryDicePoolBonus()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>4</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        string strWithoutAccessory = character.Weapons.Single().DicePool;

        // Red Dot Sight's real rules-data <dicepool> is 1 (distinct from the same-named Mod).
        Assert.True(character.AddWeaponAccessory(guiWeaponId, "Red Dot Sight", "Top", string.Empty, "3", "200", "GH", "22"));

        string strWithAccessory = character.Weapons.Single().DicePool;
        Assert.Equal(int.Parse(strWithoutAccessory) + 1, int.Parse(strWithAccessory));
    }

    [Fact]
    public void Weapons_DicePool_IncludesRatingScaledModDicePoolBonus()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>4</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        string strBaseline = character.Weapons.Single().DicePool;

        // Weapon Focus's real rules-data <dicepool> is "Rating".
        Assert.True(character.AddWeaponMod(guiWeaponId, "Weapon Focus", "3", "0", "0", "5000", "SR4", "313"));

        string strWithMod = character.Weapons.Single().DicePool;
        Assert.Equal(int.Parse(strBaseline) + 3, int.Parse(strWithMod));
    }

    [Fact]
    public void AddWeaponMod_ResolvesWeaponCostAndRatingTokensInTheCostFormula()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        // "Weapon Cost * Rating" with the weapon's own cost (350) at rating 2 -> 700.
        Assert.True(character.AddWeaponMod(guiWeaponId, "Custom Look", "2", "1", "0", "Weapon Cost * Rating", "SR4", "148"));
        CharacterTreeItemData weapon = character.WeaponTrees.Single();
        CharacterTreeItemData mod = Assert.Single(weapon.Children);
        Assert.Equal("Custom Look", mod.Name);
        Assert.True(mod.IsWeaponMod);
        Assert.False(mod.IsWeaponAccessory);
        Assert.Equal("-50", character.Nuyen); // 1000 - 350 (weapon) - 700

        Assert.True(Guid.TryParse(mod.ItemGuid, out Guid guiModId));
        Assert.True(character.RemoveWeaponMod(guiWeaponId, guiModId));
        Assert.Empty(character.WeaponTrees.Single().Children);
    }

    [Fact]
    public void SetWeaponPartIncluded_RequiresHouseRuleAndPersistsForAccessoryAndMod()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        Assert.True(character.AddWeaponAccessory(guiWeaponId, "Laser Sight", "Top", "", "4", "200", "SR4", "321"));
        Assert.True(character.AddWeaponMod(guiWeaponId, "Custom Look", "1", "1", "0", "100", "SR4", "148"));

        CharacterTreeItemData weapon = character.WeaponTrees.Single();
        Guid guiAccessoryId = Guid.Parse(weapon.Children.Single(c => c.IsWeaponAccessory).ItemGuid);
        Guid guiModId = Guid.Parse(weapon.Children.Single(c => c.IsWeaponMod).ItemGuid);
        Assert.False(character.SetWeaponPartIncluded(guiWeaponId, guiAccessoryId, true));

        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowEditPartOfBaseWeapon = true });
        Assert.True(character.SetWeaponPartIncluded(guiWeaponId, guiAccessoryId, true));
        Assert.True(character.SetWeaponPartIncluded(guiWeaponId, guiModId, true));
        weapon = character.WeaponTrees.Single();
        Assert.All(weapon.Children.Where(c => c.IsWeaponPart), c => Assert.True(c.IncludedInWeapon));
    }

    [Fact]
    public void SetWeaponPartIncluded_DoesNotBypassModificationSlotCapacity()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowEditPartOfBaseWeapon = true, EnforceCapacity = false });
        Assert.True(character.AddWeaponMod(guiWeaponId, "Custom Look", "1", "6", "0", "100", "SR4", "148"));
        Guid guiModId = Guid.Parse(character.WeaponTrees.Single().Children.Single().ItemGuid);
        Assert.True(character.SetWeaponPartIncluded(guiWeaponId, guiModId, true));

        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowEditPartOfBaseWeapon = true, EnforceCapacity = true });
        Assert.True(character.AddWeaponMod(guiWeaponId, "Custom Look", "1", "6", "0", "100", "SR4", "148"));
        Assert.False(character.SetWeaponPartIncluded(guiWeaponId, guiModId, false));
    }

    [Fact]
    public void Weapon_DicePool_MatchesTheLinkedActiveSkillsTotalValue()
    {
        // "Assault Rifles" maps to Automatics per clsEquipment.cs's Weapon.DicePool switch.
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "5")
            + "</attributes><skills><skill><name>Automatics</name><attribute>AGI</attribute><rating>4</rating>"
            + "<skillcategory>Combat Active</skillcategory><knowledge>False</knowledge></skill></skills>"
            + "<weapons><weapon><name>Ares Alpha</name><category>Assault Rifles</category>"
            + "<damage>7P</damage><ap>-1</ap><rc>1</rc><accessories /></weapon></weapons></character>");

        CharacterWeaponData weapon = character.Weapons.Single();
        CharacterSkillData skill = character.Skills.Single(s => s.Name == "Automatics");
        Assert.Equal(skill.TotalValue, weapon.DicePool);
        Assert.Equal("-1", weapon.Ap);
        Assert.Equal("1", weapon.Rc);
    }

    [Fact]
    public void Weapon_TotalRc_SumsAccessoryAndModBonusesOutsideAnyRcGroup()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Alpha</name>"
            + "<category>Assault Rifles</category><rc>1</rc>"
            + "<accessories><accessory><name>Some Sight</name><rc>1</rc><rcgroup>0</rcgroup><installed>True</installed></accessory></accessories>"
            + "<weaponmods><weaponmod><name>Some Mod</name><rc>1</rc><rcgroup>0</rcgroup><installed>True</installed></weaponmod></weaponmods>"
            + "</weapon></weapons></character>");

        // Base 1 + accessory 1 + mod 1 = 3 (RcGroup 0 items are never grouped, they always stack).
        Assert.Equal("3", character.Weapons.Single().Rc);
    }

    [Fact]
    public void Weapon_TotalRc_RestrictRecoilOn_OnlyHighestPerGroupCounts()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Alpha</name>"
            + "<category>Assault Rifles</category><rc>0</rc>"
            + "<accessories><accessory><name>Bipod</name><rc>(2)</rc><rcgroup>1</rcgroup><installed>True</installed></accessory>"
            + "<accessory><name>Some Other Group 1 Item</name><rc>1</rc><rcgroup>1</rcgroup><installed>True</installed></accessory>"
            + "</accessories></weapon></weapons></character>");

        // RestrictRecoil defaults to true (CharacterOptions._blnRestrictRecoil) - only the higher
        // of the two Group 1 items (Bipod's 2) counts, not both summed (which would be 3).
        Assert.Equal("2", character.Weapons.Single().Rc);
    }

    [Fact]
    public void Weapon_TotalRc_RestrictRecoilOff_GroupedItemsStackInstead()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Alpha</name>"
            + "<category>Assault Rifles</category><rc>0</rc>"
            + "<accessories><accessory><name>Bipod</name><rc>(2)</rc><rcgroup>1</rcgroup><installed>True</installed></accessory>"
            + "<accessory><name>Some Other Group 1 Item</name><rc>1</rc><rcgroup>1</rcgroup><installed>True</installed></accessory>"
            + "</accessories></weapon></weapons></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { RestrictRecoil = false });

        // With the house rule off, RC Group is ignored entirely and both items' RC just stacks: 2 + 1 = 3.
        Assert.Equal("3", character.Weapons.Single().Rc);
    }

    [Fact]
    public void Weapon_TotalRc_ForegripAndSlingComboGuaranteesAtLeastTwoInGroup1()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Alpha</name>"
            + "<category>Assault Rifles</category><rc>0</rc>"
            + "<accessories><accessory><name>Foregrip</name><rc>1</rc><rcgroup>1</rcgroup><installed>True</installed></accessory>"
            + "<accessory><name>Sling</name><rc>1</rc><rcgroup>1</rcgroup><installed>True</installed></accessory>"
            + "</accessories></weapon></weapons></character>");

        // Each item alone only grants 1, but SR4 83's Foregrip+Sling combo guarantees 2 in Group 1.
        Assert.Equal("2", character.Weapons.Single().Rc);
    }

    [Fact]
    public void Weapon_TotalRc_StrengthAffectsRecoilHouseRule_AddsStrBasedBonus()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("STR", "10")
            + "</attributes><weapons><weapon><name>Ares Alpha</name><category>Assault Rifles</category>"
            + "<rc>1</rc><accessories /></weapon></weapons></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { StrengthAffectsRecoil = true });

        // STR 10-13 grants +2 per the house rule's tiered bonus table.
        Assert.Equal("3", character.Weapons.Single().Rc);
    }

    [Fact]
    public void Weapon_TotalRc_StrengthAffectsRecoilHouseRule_OffByDefault()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("STR", "18")
            + "</attributes><weapons><weapon><name>Ares Alpha</name><category>Assault Rifles</category>"
            + "<rc>1</rc><accessories /></weapon></weapons></character>");

        // StrengthAffectsRecoil defaults to false - no bonus even at very high Strength.
        Assert.Equal("1", character.Weapons.Single().Rc);
    }

    [Fact]
    public void Weapon_DicePool_AddsSmartlinkBonusWhenAccessoryAndImprovementBothPresent()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "4")
            + "</attributes><skills><skill><name>Pistols</name><attribute>AGI</attribute><rating>3</rating>"
            + "<skillcategory>Combat Active</skillcategory><knowledge>False</knowledge></skill></skills>"
            + "<weapons><weapon><name>Ares Predator IV</name><category>Heavy Pistols</category>"
            + "<accessories><accessory><name>Smartgun System</name><installed>True</installed></accessory></accessories>"
            + "</weapon></weapons><improvements>" + SmartlinkImprovementXml() + "</improvements></character>");

        CharacterWeaponData weapon = character.Weapons.Single();
        CharacterSkillData skill = character.Skills.Single(s => s.Name == "Pistols");
        int intSkillTotal = int.Parse(skill.TotalValue);
        // Skill total + Smartgun's +2 (the fixed Smartlink Improvement value SR4 uses).
        Assert.Equal((intSkillTotal + 2).ToString(), weapon.DicePool);
    }

    private static string SmartlinkImprovementXml() =>
        "<improvement><improvementttype>Smartlink</improvementttype><improvementsource>Gear</improvementsource>"
        + "<val>2</val><enabled>True</enabled></improvement>";

    [Fact]
    public void Weapon_DicePool_NoMatchingSkill_IsEmpty()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Katana</name>"
            + "<category>Blades</category><accessories /></weapon></weapons></character>");

        CharacterWeaponData weapon = character.Weapons.Single();
        Assert.Equal(string.Empty, weapon.DicePool);
    }

    [Fact]
    public void Weapon_DicePool_SpecializationMatchingWeaponNameAddsTwoToTheDisplay()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "4")
            + "</attributes><skills><skill><name>Pistols</name><attribute>AGI</attribute><rating>3</rating>"
            + "<spec>Ares Predator IV</spec><skillcategory>Combat Active</skillcategory>"
            + "<knowledge>False</knowledge></skill></skills>"
            + "<weapons><weapon><name>Ares Predator IV</name><category>Heavy Pistols</category>"
            + "<accessories /></weapon></weapons></character>");

        CharacterWeaponData weapon = character.Weapons.Single();
        Assert.Contains("(", weapon.DicePool);
    }

    [Fact]
    public void RemoveWeapon_RemovesOnlyMatchingRootLevelEntry()
    {
        CharacterDocument character = LoadXml(
            "<character><weapons>"
            + "<weapon><name>Ares Predator IV</name><category>Heavy Pistols</category></weapon>"
            + "<weapon><name>Ares Predator IV</name><category>Exotic Ranged Weapon</category></weapon>"
            + "</weapons></character>");

        Assert.True(character.RemoveWeapon("Ares Predator IV", "Heavy Pistols"));
        CharacterTreeItemData remaining = Assert.Single(character.WeaponTrees);
        Assert.Equal("Exotic Ranged Weapon", remaining.Category);
    }

    [Fact]
    public void AddArmor_MutatesCharacterTreeAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");

        CharacterTreeItemData added = Assert.Single(character.Armor);
        Assert.Equal("Leather Jacket", added.Name);
        Assert.Equal("Clothing", added.Category);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Leather Jacket", Assert.Single(reloaded.Armor).Name);
    }

    [Fact]
    public void AddArmorMod_NestsUnderTheArmorDeductsRatingScaledCostAndCanBeRemoved()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");

        // Chemical Protection's real rules-data <cost> is "Rating * 250".
        Assert.True(character.AddArmorMod("Leather Jacket", "Clothing", "Chemical Protection", "3",
            "0", "0", "9", "Rating * 250", "SR4", "327"));

        CharacterTreeItemData armor = character.Armor.Single();
        CharacterTreeItemData mod = armor.Children.Single();
        Assert.Equal("Chemical Protection", mod.Name);
        Assert.Equal("9050", character.Nuyen); // 10000 - 200(jacket) - 750(3 * 250)

        Assert.True(character.RemoveArmorMod("Chemical Protection"));
        Assert.Empty(character.Armor.Single().Children);
    }

    [Fact]
    public void ArmorSuitCapacity_TracksRulesDataModCapacityAndRejectsOverflow()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ArmorSuitCapacity = true });
        character.AddArmor("Capacity Suit", "Armor", "2", "2", "4", "100", "0", "SR4", "326");

        Assert.True(character.AddArmorMod("Capacity Suit", "Armor", "Auto-Injector", "1",
            "0", "0", "4", "1500", "AR", "50")); // [2] capacity
        Assert.True(character.AddArmorMod("Capacity Suit", "Armor", "Chemical Protection", "1",
            "0", "0", "9", "Rating * 250", "SR4", "327")); // [2] capacity
        Assert.Equal("4", character.Armor.Single().Capacity);
        Assert.Equal("0", character.Armor.Single().CapacityRemaining);
        Assert.False(character.AddArmorMod("Capacity Suit", "Armor", "Auto-Injector", "1",
            "0", "0", "4", "1500", "AR", "50"));
    }

    [Fact]
    public void MaximumArmorModifications_UsesArmorRatingSlotsWhenEnabled()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MaximumArmorModifications = true });
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "100", "0", "SR4", "326");

        // max(6, ceil(max(2, 2) * 1.5)) gives six rating slots.
        Assert.True(character.AddArmorMod("Leather Jacket", "Clothing", "Chemical Protection", "6",
            "0", "0", "9", "Rating * 250", "SR4", "327"));
        Assert.Equal("6", character.Armor.Single().Capacity);
        Assert.Equal("0", character.Armor.Single().CapacityRemaining);
        Assert.False(character.AddArmorMod("Leather Jacket", "Clothing", "Auto-Injector", "1",
            "0", "0", "4", "1500", "AR", "50"));
    }

    [Fact]
    public void ArmorDegradation_AdjustsRatingsOnlyWhenHouseRuleIsEnabled()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "3", "0", "100", "0", "SR4", "326");

        Assert.False(character.AdjustArmorDegradation("Leather Jacket", "Clothing", 1, 0));
        character.SetCharacterOptionsForTesting(new CharacterOptions { ArmorDegradation = true });
        Assert.True(character.AdjustArmorDegradation("Leather Jacket", "Clothing", 1, 2));
        CharacterTreeItemData damaged = Assert.Single(character.Armor);
        Assert.Equal("1", damaged.Ballistic);
        Assert.Equal("1", damaged.Impact);

        Assert.True(character.AdjustArmorDegradation("Leather Jacket", "Clothing", -10, -10));
        CharacterTreeItemData repaired = Assert.Single(character.Armor);
        Assert.Equal("2", repaired.Ballistic);
        Assert.Equal("3", repaired.Impact);
    }

    [Fact]
    public void AddArmorMod_AppliesItsRulesDataBonus()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");

        // YNT SoftWeave's real bonus is a bare <softweave /> node.
        Assert.True(character.AddArmorMod("Leather Jacket", "Clothing", "YNT SoftWeave", "1",
            "0", "0", "11", "Armor Cost * 0.1", "WAR", "160"));

        Improvement improvement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.SoftWeave, improvement.Type);
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

    [Fact]
    public void AddCyberware_Smartlink_AppliesTheFlagBonus()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Smartlink cyberware's real bonus is a bare <smartlink /> flag node.
        character.AddCyberware("Smartlink", "Eyeware", "0", "0.1", "1000", "8R", "SR4", "340");

        Improvement improvement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Smartlink, improvement.Type);
    }

    [Fact]
    public void AddCyberware_BoneLacingAluminum_AppliesArmorAndDamageResistanceBonuses()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Real bonus: <armor><i>1</i></armor> + <damageresistance>2</damageresistance>.
        character.AddCyberware("Bone Lacing (Aluminum)", "Bodyware", "0", "1", "15000", "12F", "SR4", "341");

        Assert.Equal(2, character.Improvements.Count);
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.ImpactArmor, ""));
        Improvement drImprovement = character.Improvements.Single(i => i.Type == ImprovementType.DamageResistance);
        Assert.Equal(2, drImprovement.Value);
    }

    [Fact]
    public void AddArmorMod_ResponsiveInterfaceGearHotSim_AppliesMatrixInitiativeBonuses()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");

        // Real bonus: <matrixinitiative>1</matrixinitiative> + <matrixinitiativepass>2</matrixinitiativepass>
        // + a bare <skillsoftaccess /> flag.
        Assert.True(character.AddArmorMod("Leather Jacket", "Clothing",
            "Responsive Interface Gear (Helmet, Hot Sim)", "1", "0", "0", "8", "2400", "WAR", "161"));

        Assert.Equal(3, character.Improvements.Count);
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.MatrixInitiative, ""));
        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.MatrixInitiativePass, ""));
        Assert.Contains(character.Improvements, i => i.Type == ImprovementType.SkillsoftAccess);
    }

    [Fact]
    public void AddCyberware_SideSelection_IsDetectedPersistedAndRemovable()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Single Cybereye's real bonus is a bare <selectside /> node.
        Assert.True(character.CyberwareRequiresSideSelection("Single Cybereye"));
        Assert.False(character.CyberwareRequiresSideSelection("Datajack"));

        character.AddCyberware("Single Cybereye", "Eyeware", "4", "0.25", "600", "8", "SR4", "", strSide: "Left");

        Assert.Equal("Single Cybereye", Assert.Single(character.Cyberware).Name);

        Assert.True(character.RemoveCyberware("Single Cybereye", "Eyeware", "4"));
        Assert.Empty(character.Cyberware);
    }

    [Fact]
    public void AddCyberware_SkillGroupSelection_IsDetectedAppliedAndRemovable()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Reflex Recorder (Skill Group)'s real bonus is a bare <selectskillgroup
        // excludecategory="Magical Active,Resonance Active,Social Active,Technical
        // Active,Vechicle Active"> node - Firearms (Combat Active) isn't excluded.
        Assert.True(character.CyberwareRequiresSkillGroupSelection("Reflex Recorder (Skill Group)", blnBioware: true));
        Assert.False(character.CyberwareRequiresSkillGroupSelection("Datajack"));

        var lstOptions = character.GetCyberwareSkillGroupOptions(
            "Reflex Recorder (Skill Group)", blnBioware: true);
        Assert.Contains("Firearms", lstOptions);
        Assert.DoesNotContain("Sorcery", lstOptions);

        character.AddCyberware("Reflex Recorder (Skill Group)", "Cultured", "0", "0.2", "25000", "12", "SR4", "",
            blnBioware: true, strSelectedSkillGroup: "Firearms");

        Improvement improvement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.SkillGroup, improvement.Type);
        Assert.Equal("Firearms", improvement.ImprovedName);
        Assert.Equal(1, improvement.Value);
        Assert.True(improvement.AddToRating);

        Assert.True(character.RemoveCyberware("Reflex Recorder (Skill Group)", "Cultured", "0", blnBioware: true));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void AddCyberwareSuite_AddsAllFlatPartsAtTheSuitesGrade()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Real "Aztechnology Topo" Suite (cyberware.xml): Standard grade, five flat (unnested)
        // parts, one of which (Muscle Replacement) has a Rating-scaled ess/cost formula.
        var lstSuites = character.GetCyberwareSuiteNames();
        Assert.Contains("Aztechnology Topo", lstSuites);

        Assert.True(character.AddCyberwareSuite("Aztechnology Topo"));

        var lstNames = character.Cyberware.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "Thermographic Vision", "Damper", "Bone Lacing (Plastic)",
            "Internal Air Tank", "Muscle Replacement" }, lstNames);
        Assert.All(character.Cyberware, c => Assert.Empty(c.Children));

        CharacterTreeItemData muscleReplacement = character.Cyberware.Single(c => c.Name == "Muscle Replacement");
        Assert.Equal("2", muscleReplacement.Rating);
        Assert.Equal("10000", muscleReplacement.Cost); // Rating(2) * 5000, Standard grade x1.
    }

    [Fact]
    public void AddCyberwareSuite_NestsPluginsUnderTheirParent()
    {
        CharacterDocument character = LoadXml("<character></character>");

        // Real "Urban Kshatriya Alpha" Suite: Alphaware grade, Cybereyes Basic System (Rating 3)
        // has six nested vision-mod plugins.
        Assert.True(character.AddCyberwareSuite("Urban Kshatriya Alpha"));

        CharacterTreeItemData eyes = character.Cyberware.Single(c => c.Name == "Cybereyes Basic System");
        Assert.Equal("3", eyes.Rating);
        Assert.Equal(6, eyes.Children.Count);
        Assert.Contains(eyes.Children, c => c.Name == "Smartlink");
    }

    [Fact]
    public void BiowareSuites_RequireTheHouseRuleButCyberwareSuitesDoNot()
    {
        CharacterDocument character = LoadXml("<character></character>");

        Assert.False(character.AllowBiowareSuitesEnabled);
        Assert.Empty(character.GetCyberwareSuiteNames(blnBioware: true));
        Assert.False(character.AddCyberwareSuite("Any Bioware Suite", blnBioware: true));
        Assert.Contains("Aztechnology Topo", character.GetCyberwareSuiteNames());

        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowBiowareSuites = true });
        Assert.True(character.AllowBiowareSuitesEnabled);
    }

    [Fact]
    public void AddPacksKit_Brawler_SetsAllEightAttributes()
    {
        string strAttributes = string.Join(string.Empty, new[] { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" }
            .Select(c => $"<attribute><name>{c}</name><value>1</value><totalvalue>1</totalvalue>"
                + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute>"));
        CharacterDocument character = LoadXml("<character><attributes>" + strAttributes + "</attributes></character>");

        Assert.Contains("Brawler", character.GetPacksKitNames("Attribute Kits"));
        Assert.True(character.AddPacksKit("Brawler", "Attribute Kits"));

        // Real "Brawler" pack: BOD 5, AGI 4, REA 3, STR 5, CHA 3, INT 3, LOG 2, WIL 3.
        Assert.Equal("5", character.Attributes.Single(a => a.Code == "BOD").Value);
        Assert.Equal("4", character.Attributes.Single(a => a.Code == "AGI").Value);
        Assert.Equal("5", character.Attributes.Single(a => a.Code == "STR").Value);
        Assert.Equal("2", character.Attributes.Single(a => a.Code == "LOG").Value);
    }

    [Fact]
    public void AddPacksKit_HandgunTrainee_SetsSkillRatings()
    {
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Clubs</name><knowledge>False</knowledge><rating>0</rating><ratingmax>6</ratingmax>"
            + "<grouped>False</grouped></skill>"
            + "<skill><name>Pistols</name><knowledge>False</knowledge><rating>0</rating><ratingmax>6</ratingmax>"
            + "<grouped>False</grouped></skill>"
            + "</skills></character>");

        Assert.True(character.AddPacksKit("Handgun Trainee", "Skill Kits"));

        Assert.Equal("1", character.Skills.Single(s => s.Name == "Clubs").BaseRating);
        Assert.Equal("2", character.Skills.Single(s => s.Name == "Pistols").BaseRating);
    }

    [Fact]
    public void AddPacksKit_EmergencyIdentity_AddsNestedGearAndNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>100</nuyen></character>");

        Assert.True(character.AddPacksKit("Emergency Identity", "Gear Kits"));

        // Real "Emergency Identity" pack: nuyenbp 1 -> +1 nuyen (Karma build doubles it; default
        // BuildMethod here is "Karma", matching CharacterDocument.BuildMethod's own fallback).
        Assert.Equal("102", character.Nuyen);

        var lstNames = character.Gear.Select(g => g.Name).ToList();
        Assert.Contains("Sony Emperor", lstNames);
        Assert.Contains("Fake SIN", lstNames);

        CharacterTreeItemData commlink = character.Gear.Single(g => g.Name == "Sony Emperor");
        Assert.Equal("Vector Xim", Assert.Single(commlink.Children).Name);

        CharacterTreeItemData fakeSin = character.Gear.Single(g => g.Name == "Fake SIN");
        Assert.Equal("3", fakeSin.Rating);
    }

    [Fact]
    public void AddPacksKit_EveryRealKit_AppliesWithoutThrowing()
    {
        // Broad smoke coverage over all ~172 real packs.xml entries (rather than one test per
        // kit) - catches data-shape surprises the three targeted tests above wouldn't.
        var objPacksDoc = XmlManager.Instance.Load("packs.xml");
        foreach (System.Xml.XmlNode objCategory in objPacksDoc.SelectNodes("/chummer/categories/category")!)
        {
            string strCategory = objCategory.InnerText;
            foreach (System.Xml.XmlNode objPack in objPacksDoc.SelectNodes(
                         $"/chummer/packs/pack[category = '{strCategory}']")!)
            {
                string strName = objPack["name"]!.InnerText;
                CharacterDocument character = LoadXml("<character><nuyen>0</nuyen></character>");
                var exception = Record.Exception(() => character.AddPacksKit(strName, strCategory));
                Assert.True(exception == null, $"{strCategory} / {strName}: {exception}");
            }
        }
    }

    [Fact]
    public void ArmorEncumbrance_ExceedsThreshold_AppliesCeilingHalfPenalty()
    {
        // BOD 4 -> threshold 8. Two Leather Jackets (B2 each, non-stacking category so both count
        // toward the total) push total Ballistic to 4 - under threshold, so add a third heavier
        // piece to push it over: total 10 vs threshold 8 -> ceil((10-8)/2) = 1 penalty.
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>4</value><totalvalue>4</totalvalue></attribute><attribute><name>STR</name><value>0</value><totalvalue>0</totalvalue></attribute></attributes></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        character.AddArmor("Heavy Jacket", "Clothing", "8", "8", "0", "500", "0", "SR4", "326");

        Assert.Equal(-1, character.ArmorEncumbrance.BallisticPenalty.Value);
    }

    [Fact]
    public void ArmorEncumbrance_IgnoreArmorEncumbranceHouseRule_ZeroesThePenalty()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>1</value><totalvalue>1</totalvalue></attribute><attribute><name>STR</name><value>0</value><totalvalue>0</totalvalue></attribute></attributes></character>");
        character.AddArmor("Heavy Jacket", "Clothing", "20", "20", "0", "500", "0", "SR4", "326");
        Assert.NotEqual(0, character.ArmorEncumbrance.BallisticPenalty.Value);

        var objOptions = new CharacterOptions { IgnoreArmorEncumbrance = true };
        character.SetCharacterOptionsForTesting(objOptions);
        Assert.Equal(0, character.ArmorEncumbrance.BallisticPenalty.Value);
    }

    [Fact]
    public void ArmorEncumbrance_NoSingleArmorEncumbranceHouseRule_ZeroesThePenaltyForOnePiece()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>1</value><totalvalue>1</totalvalue></attribute><attribute><name>STR</name><value>0</value><totalvalue>0</totalvalue></attribute></attributes></character>");
        character.AddArmor("Heavy Jacket", "Clothing", "20", "20", "0", "500", "0", "SR4", "326");

        var objOptions = new CharacterOptions { NoSingleArmorEncumbrance = true };
        character.SetCharacterOptionsForTesting(objOptions);
        Assert.Equal(0, character.ArmorEncumbrance.BallisticPenalty.Value);

        // A second piece means it's no longer "a single piece", so the penalty applies again.
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        Assert.NotEqual(0, character.ArmorEncumbrance.BallisticPenalty.Value);
    }

    [Fact]
    public void ArmorEncumbrance_AlternateArmorEncumbranceHouseRule_UsesBodPlusStrThreshold()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>4</value><totalvalue>4</totalvalue></attribute><attribute><name>STR</name><value>4</value><totalvalue>4</totalvalue></attribute></attributes></character>");
        character.AddArmor("Heavy Jacket", "Clothing", "8", "8", "0", "500", "0", "SR4", "326");

        // Standard rule: threshold = BOD*2 = 8, total 8 -> no penalty.
        Assert.Equal(0, character.ArmorEncumbrance.BallisticPenalty.Value);

        // Alternate rule: threshold = BOD*1 + STR = 4 + 4 = 8 - still exactly at threshold here,
        // so bump BOD up via a second piece instead to make the difference observable.
        var objOptions = new CharacterOptions { AlternateArmorEncumbrance = true };
        character.SetCharacterOptionsForTesting(objOptions);
        character.AddArmor("Leather Jacket", "Clothing", "3", "3", "0", "200", "0", "SR4", "326");
        // Total 11, alternate threshold 8 -> ceil((11-8)/2) = 2.
        Assert.Equal(-2, character.ArmorEncumbrance.BallisticPenalty.Value);
    }

    [Fact]
    public void AddArmor_EquippedByDefault_FeedsIntoArmorEncumbranceAndRating()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>4</value></attribute></attributes></character>");

        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
        Assert.Equal(2, encumbrance.BallisticRating.Value);
        Assert.Equal(2, encumbrance.ImpactRating.Value);
    }

    [Fact]
    public void SetArmorEquipped_UnequippingRemovesItFromArmorEncumbrance()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>4</value></attribute></attributes></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        Assert.Equal(2, character.ArmorEncumbrance.BallisticRating.Value);

        Assert.True(character.SetArmorEquipped("Leather Jacket", "Clothing", false));
        Assert.Equal(0, character.ArmorEncumbrance.BallisticRating.Value);

        Assert.True(character.SetArmorEquipped("Leather Jacket", "Clothing", true));
        Assert.Equal(2, character.ArmorEncumbrance.BallisticRating.Value);
    }

    [Fact]
    public void RemoveArmor_RemovesOnlyMatchingRootLevelEntry()
    {
        CharacterDocument character = LoadXml(
            "<character><armors>"
            + "<armor><name>Leather Jacket</name><category>Clothing</category></armor>"
            + "<armor><name>Leather Jacket</name><category>Armor Vest</category></armor>"
            + "</armors></character>");

        Assert.True(character.RemoveArmor("Leather Jacket", "Clothing"));
        CharacterTreeItemData remaining = Assert.Single(character.Armor);
        Assert.Equal("Armor Vest", remaining.Category);
    }

    [Fact]
    public void AddCyberware_FiresChangedEvent_SoTheMainWindowStatusBarRefreshesItsEssenceDisplay()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        int intChangedCount = 0;
        character.Changed += () => intChangedCount++;

        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        Assert.True(intChangedCount > 0);

        Assert.True(character.RemoveCyberware("Cybereyes", "Cyberlimb", "0"));
        Assert.True(intChangedCount > 1);
    }

    [Fact]
    public void AddCyberware_MutatesCharacterTreeAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "Rating * 1000", "8R", "SR4", "339");

        CharacterTreeItemData added = Assert.Single(character.Cyberware);
        Assert.Equal("Cybereyes", added.Name);
        Assert.Equal("Cyberlimb", added.Category);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Cybereyes", Assert.Single(reloaded.Cyberware).Name);
        Assert.Empty(reloaded.Bioware);
    }

    [Fact]
    public void AddCyberware_WiredReflexes_AppliesInitiativePassAndReaBonusesScaledByRating()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCyberware("Wired Reflexes", "Bodyware", "2", "3", "32000", "12R", "SR4", "342");

        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.InitiativePass));
        Assert.Equal(2, ImprovementManager.AugmentedValueOf(character.Improvements, ImprovementType.Attribute, "REA"));

        Assert.True(character.RemoveCyberware("Wired Reflexes", "Bodyware", "2"));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void AddCyberware_Bioware_GoesIntoTheBiowareTreeNotCyberware()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCyberware("Muscle Toner", "Basic", "2", "0.4", "Rating * 8000", "8R", "SR4", "339", blnBioware: true);

        Assert.Empty(character.Cyberware);
        CharacterTreeItemData added = Assert.Single(character.Bioware);
        Assert.Equal("Muscle Toner", added.Name);
    }

    [Fact]
    public void AddCyberware_EssenceCostFeedsIntoComputedEssence()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>ESS</name><metatypemax>6</metatypemax></attribute></attributes></character>");
        Assert.Equal("6", character.Condition.Essence);

        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        Assert.Equal("5.8", character.Condition.Essence);
    }

    [Fact]
    public void RemoveCyberware_RemovesOnlyMatchingRootLevelEntryAndDistinguishesFromBioware()
    {
        CharacterDocument character = LoadXml(
            "<character><cyberwares>"
            + "<cyberware><name>Datajack</name><category>Headware</category><rating>0</rating><improvementsource>Cyberware</improvementsource></cyberware>"
            + "<cyberware><name>Datajack</name><category>Headware</category><rating>0</rating><improvementsource>Bioware</improvementsource></cyberware>"
            + "</cyberwares></character>");

        Assert.True(character.RemoveCyberware("Datajack", "Headware", "0"));
        Assert.Empty(character.Cyberware);
        Assert.Single(character.Bioware);
    }

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
    public void CritterPowers_ReadNameExtraAndPoints()
    {
        CharacterDocument character = LoadXml(
            "<character><critterpowers><critterpower><name>Fear</name><extra></extra>"
            + "<points>2</points></critterpower></critterpowers></character>");

        CharacterCritterPowerData power = Assert.Single(character.CritterPowers);
        Assert.Equal("Fear", power.Name);
        Assert.Equal("2", power.Points);
    }

    [Fact]
    public void AddCritterPower_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Fear", "2", "SM", "54");

        CharacterCritterPowerData added = Assert.Single(character.CritterPowers);
        Assert.Equal("Fear", added.Name);
        Assert.Equal("2", added.Points);
        Assert.NotEmpty(added.Guid);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Fear", Assert.Single(reloaded.CritterPowers).Name);
    }

    [Fact]
    public void RemoveCritterPower_RemovesOnlyTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Fear", "2", "SM", "54");
        character.AddCritterPower("Paralyzing Howl", "3", "SM", "55");
        string strGuid = character.CritterPowers[0].Guid;

        Assert.True(character.RemoveCritterPower(strGuid));
        Assert.False(character.RemoveCritterPower(strGuid));
        CharacterCritterPowerData remaining = Assert.Single(character.CritterPowers);
        Assert.Equal("Paralyzing Howl", remaining.Name);
    }

    [Fact]
    public void AddCritterPower_ArmorBallistic_AppliesItsRatingOneArmorBonus()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Armor (Ballistic)", "1", "RW", "204");

        // <armor><b>Rating</b></armor>, defaulting to Rating 1 when not specified.
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.BallisticArmor));
        Assert.Equal("1", character.CritterPowers[0].Rating);

        string strGuid = character.CritterPowers[0].Guid;
        Assert.True(character.RemoveCritterPower(strGuid));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void AddCritterPower_ArmorBallistic_ScalesItsBonusByThePlayerChosenRating()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Armor (Ballistic)", "1", "RW", "204", strRating: "4");

        Assert.Equal(4, ImprovementManager.ValueOf(character.Improvements, ImprovementType.BallisticArmor));
        Assert.Equal("4", character.CritterPowers[0].Rating);
    }

    [Fact]
    public void AddCritterPower_Fear_IgnoresRatingParameterSinceItHasNoRating()
    {
        // "Fear" has no <rating>yes</rating> in critterpowers.xml, so a passed-in strRating must
        // be ignored (persisted Rating stays "0", matching legacy's disabled nudCritterPowerRating).
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddCritterPower("Fear", "2", "SM", "54", strRating: "5");

        Assert.Equal("0", character.CritterPowers[0].Rating);
    }

    [Fact]
    public void AddCritterPower_ElementalAttack_AppliesThePlayerEnteredTextAsAnImprovement()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.CritterPowerRequiresTextSelection("Elemental Attack"));

        character.AddCritterPower("Elemental Attack", "1", "RW", "210", "Fire");

        var textImprovement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Text, textImprovement.Type);
        Assert.Equal("Fire", textImprovement.ImprovedName);
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
    public void ComputeComplexFormKarmaCost_UsesConfiguredRules()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions
        {
            KarmaNewComplexForm = 2,
            KarmaImproveComplexForm = 3,
            KarmaComplexFormSkillsoft = 4,
            KarmaSpell = 5
        });

        Assert.Equal(17, character.ComputeComplexFormKarmaCost("Common Use", 3));
        Assert.Equal(12, character.ComputeComplexFormKarmaCost("Skillsofts", 3));

        character.SetCharacterOptionsForTesting(new CharacterOptions { AlternateComplexFormCost = true, KarmaSpell = 5 });
        Assert.Equal(5, character.ComputeComplexFormKarmaCost("Common Use", 3));
    }

    [Fact]
    public void AddComplexForm_CareerChargesKarmaButCreationDoesNot()
    {
        CharacterDocument career = LoadXml("<character><created>True</created><karma>5</karma></character>");
        career.SetCharacterOptionsForTesting(new CharacterOptions { KarmaNewComplexForm = 2 });
        Assert.True(career.AddComplexForm("Editor", "Common Use", "SR4", "232"));
        Assert.Equal("3", career.Karma);

        CharacterDocument creation = LoadXml("<character><created>False</created><karma>0</karma></character>");
        creation.SetCharacterOptionsForTesting(new CharacterOptions { KarmaNewComplexForm = 2 });
        Assert.True(creation.AddComplexForm("Editor", "Common Use", "SR4", "232"));
        Assert.Equal("0", creation.Karma);
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
    public void AddComplexForm_EmpathySoftware_AppliesItsSkillCategoryBonus()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddComplexForm("Empathy Software", "Sensor Software", "AR", "1");

        // <skillcategory><name>Social Active</name><bonus>Rating</bonus></skillcategory>,
        // applied at Rating 1 (the always-1 saved techprogram Rating).
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.SkillCategory, "Social Active"));

        string strGuid = character.ComplexForms[0].Guid;
        Assert.True(character.RemoveComplexForm(strGuid));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void AddComplexForm_Knowsoft_AppliesThePlayerEnteredTextAsAnImprovement()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.True(character.ComplexFormRequiresTextSelection("Knowsoft"));

        character.AddComplexForm("Knowsoft", "Skillsofts", "SR4", "330", "Geographic Knowledge");

        var textImprovement = Assert.Single(character.Improvements);
        Assert.Equal(ImprovementType.Text, textImprovement.Type);
        Assert.Equal("Geographic Knowledge", textImprovement.ImprovedName);
    }

    [Fact]
    public void CharacterSheetExporter_IncludesVehiclesWithModsGearAndWeaponsInTheExportXml()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category>"
            + "<body>4</body><mods /><gears /><weapons /></vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "Body * 200", "AR", "132");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        character.AddVehicleGear(vehicleId, "Vehicle Toolkit", "Tools", "0", "1", "250", "4", "SR4", "320");
        character.AddVehicleWeapon(vehicleId, "Mounted Gun", "Machine Pistols", "6P", "0", "SA",
            "0", "20", "500", "8R", "SR4", "100");

        string html = CharacterSheetExporter.RenderSheet(character, "Text-Only.xsl");

        Assert.Contains("Americar", html);
        Assert.Contains("Anti-Theft", html);
        Assert.Contains("Vehicle Toolkit", html);
        Assert.Contains("Mounted Gun", html);
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

    [Fact]
    public void EffectiveResponse_CalculateCommlinkResponseOn_SubtractsFloorOfProgramsOverSystem()
    {
        // TotalSystem 2, 5 running programs -> floor(5/2) = 2 penalty off Response 6.
        CharacterDocument character = LoadXml("<character><gears><gear><guid>g1</guid>"
            + "<name>Commlink</name><category>Commlink</category><response>6</response><system>2</system>"
            + "<children>"
            + "<gear><name>P1</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "<gear><name>P2</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "<gear><name>P3</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "<gear><name>P4</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "<gear><name>P5</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "</children></gear></gears></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { CalculateCommlinkResponse = true });

        Assert.Equal("4", character.Gear.Single().EffectiveResponse);
    }

    [Fact]
    public void EffectiveResponse_CalculateCommlinkResponseOff_NoPenaltyApplied()
    {
        CharacterDocument character = LoadXml("<character><gears><gear><guid>g1</guid>"
            + "<name>Commlink</name><category>Commlink</category><response>6</response><system>2</system>"
            + "<children>"
            + "<gear><name>P1</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "<gear><name>P2</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "<gear><name>P3</name><category>Matrix Programs</category><equipped>True</equipped></gear>"
            + "</children></gear></gears></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { CalculateCommlinkResponse = false });

        Assert.Equal("6", character.Gear.Single().EffectiveResponse);
    }

    [Fact]
    public void EffectiveResponse_UnequippedProgramsAndNonProgramGearDoNotCountTowardsPenalty()
    {
        CharacterDocument character = LoadXml("<character><gears><gear><guid>g1</guid>"
            + "<name>Commlink</name><category>Commlink</category><response>6</response><system>1</system>"
            + "<children>"
            + "<gear><name>Unequipped Program</name><category>Matrix Programs</category><equipped>False</equipped></gear>"
            + "<gear><name>Not A Program</name><category>Certain Kind of Foci</category><equipped>True</equipped></gear>"
            + "</children></gear></gears></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { CalculateCommlinkResponse = true });

        Assert.Equal("6", character.Gear.Single().EffectiveResponse);
    }

    [Fact]
    public void EffectiveResponse_ErgonomicProgramExemptOnlyWhenHouseRuleOn()
    {
        string strXml = "<character><gears><gear><guid>g1</guid>"
            + "<name>Commlink</name><category>Commlink</category><response>6</response><system>1</system>"
            + "<children>"
            + "<gear><name>P1</name><category>Matrix Programs</category><equipped>True</equipped>"
            + "<children><gear><name>Ergonomic</name><category>Program Options</category></gear></children></gear>"
            + "</children></gear></gears></character>";

        CharacterDocument withoutRule = LoadXml(strXml);
        withoutRule.SetCharacterOptionsForTesting(
            new CharacterOptions { CalculateCommlinkResponse = true, ErgonomicProgramLimit = false });
        // By default (house rule off) an Ergonomic program counts like any other: floor(1/1) = 1 penalty.
        Assert.Equal("5", withoutRule.Gear.Single().EffectiveResponse);

        CharacterDocument withRule = LoadXml(strXml);
        withRule.SetCharacterOptionsForTesting(
            new CharacterOptions { CalculateCommlinkResponse = true, ErgonomicProgramLimit = true });
        // With the house rule on, Ergonomic programs are exempted from the count - no penalty.
        Assert.Equal("6", withRule.Gear.Single().EffectiveResponse);
    }

    [Fact]
    public void CharacterSheetExporter_RendersRealFixtureDataThroughTextOnlySheet()
    {
        CharacterDocument character = LoadFixture();

        string html = CharacterSheetExporter.RenderSheet(character, "Text-Only.xsl");

        Assert.Contains("Pistolen", html);
        Assert.Contains("Custom Commlink", html);
        Assert.Contains("Wired Reflexes", html);
        // The character has no positive Response saved on its gear, so nothing should be
        // misclassified into the Commlink section - see the HasCommlinkStats fix.
        Assert.DoesNotContain("== Commlink ==", html);
    }

    [Theory]
    [InlineData("Shadowrun 4.xsl")]
    [InlineData("Shadowrun 4 (Grouped Skills by Name).xsl")]
    [InlineData("Shadowrun 4 (Grouped Skills by Rating).xsl")]
    public void CharacterSheetExporter_RendersSheetsThatXslIncludeASharedBaseStylesheet(string strSheetName)
    {
        // These xsl:include "Shadowrun 4 Base.xslt" - regression test for the XmlResolver fix
        // (XmlReader.Create's default resolver refuses to follow xsl:include as an "external URI").
        CharacterDocument character = LoadFixture();

        string html = CharacterSheetExporter.RenderSheet(character, strSheetName);

        Assert.Contains("Pistolen", html);
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
    public void CharacterSheetExporter_PrintSkillsWithZeroRatingOff_OmitsZeroRatingActiveSkillsButKeepsKnowledge()
    {
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>0</rating><knowledge>False</knowledge><allowdelete>True</allowdelete></skill>"
            + "<skill><name>Automatics</name><attribute>AGI</attribute><rating>3</rating><knowledge>False</knowledge><allowdelete>True</allowdelete></skill>"
            + "<skill><name>Chemistry</name><attribute>LOG</attribute><rating>0</rating><knowledge>True</knowledge><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintSkillsWithZeroRating = false });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        var lstNames = xml.SelectNodes("//skill/name")!.Cast<XmlNode>().Select(n => n.InnerText).ToList();

        Assert.DoesNotContain("Pistols", lstNames);
        Assert.Contains("Automatics", lstNames);
        Assert.Contains("Chemistry", lstNames);
    }

    [Fact]
    public void CharacterSheetExporter_PrintSkillsWithZeroRatingOn_IncludesEveryActiveSkill()
    {
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>0</rating><knowledge>False</knowledge><allowdelete>True</allowdelete></skill>"
            + "</skills></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintSkillsWithZeroRating = true });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        Assert.Contains("Pistols", xml.SelectNodes("//skill/name")!.Cast<XmlNode>().Select(n => n.InnerText));
    }

    private const string ExpenseFixtureXml = "<character><expenses><expense><guid>11111111-1111-1111-1111-111111111111</guid>"
        + "<date>2020-01-01</date><amount>5</amount><reason>Test expense</reason><type>Karma</type><refund>False</refund>"
        + "</expense></expenses></character>";

    [Fact]
    public void CharacterSheetExporter_PrintExpensesOff_OmitsExpenseEntries()
    {
        CharacterDocument character = LoadXml(ExpenseFixtureXml);
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintExpenses = false });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        Assert.Empty(xml.SelectNodes("//expenses/expense")!);
    }

    [Fact]
    public void CharacterSheetExporter_PrintExpensesOn_IncludesExpenseEntries()
    {
        CharacterDocument character = LoadXml(ExpenseFixtureXml);
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintExpenses = true });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        Assert.NotEmpty(xml.SelectNodes("//expenses/expense")!);
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
    public void RemoveSpell_RemovesOnlyTheMatchingSavedSpell()
    {
        CharacterDocument character = LoadXml("<character><spells><spell><name>Acid Stream</name></spell><spell><name>Clout</name></spell></spells></character>");

        Assert.True(character.RemoveSpell("Acid Stream"));
        Assert.False(character.RemoveSpell("Missing spell"));
        CharacterSpellData remaining = Assert.Single(character.Spells);
        Assert.Equal("Clout", remaining.Name);
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
    public void AddAdeptPower_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddAdeptPower("Improved Reflexes", "2", "1.5");

        CharacterPowerData added = Assert.Single(character.AdeptPowers);
        Assert.Equal("Improved Reflexes", added.Name);
        Assert.Equal("2", added.Rating);
        Assert.Equal("3.00", added.TotalPoints);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Improved Reflexes", Assert.Single(reloaded.AdeptPowers).Name);
    }

    [Fact]
    public void AddAdeptPower_ImprovedReflexes2_AppliesInitiativePassAndReaAttributeBonuses()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddAdeptPower("Improved Reflexes 2", "1", "2.5");

        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.InitiativePass));
        Assert.Equal(2, ImprovementManager.AugmentedValueOf(character.Improvements, ImprovementType.Attribute, "REA"));
    }

    [Fact]
    public void GetSenseImprovementOptions_ImprovedSense_ListsEligibleCyberwareBiowareAndGear()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        var options = character.GetSenseImprovementOptions("Improved Sense");

        Assert.NotEmpty(options);
        Assert.Contains(options, o => o.Name == "Olfactory Booster"); // Headware cyberware, senseimprovement=yes.
        // Cyberware items without senseimprovement=yes are excluded (power's requiresenseimprovement="yes").
        Assert.DoesNotContain(options, o => o.Name == "Orientation System");
    }

    [Fact]
    public void AddImprovedSensePower_AppliesSelectedSensewaresBonusAtRatingOne()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        Assert.True(character.AddImprovedSensePower("Improved Sense", "1", ".25", "Olfactory Booster"));

        Assert.Single(character.AdeptPowers);
        // Olfactory Booster's own bonus is +Rating to Perception (Smell) - applied at Rating 1
        // (the ImprovedSenseFullRating house rule is off by default).
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Perception (Smell)"));

        Assert.True(character.RemoveAdeptPower("Improved Sense"));
        Assert.Empty(character.Improvements);
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

    [Fact]
    public void AddImprovedSensePower_RejectsUnlistedSenseware()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        Assert.False(character.AddImprovedSensePower("Improved Sense", "1", ".25", "Not A Real Item"));
        Assert.Empty(character.AdeptPowers);
    }

    [Fact]
    public void RemoveAdeptPower_RemovesOnlyTheMatchingPower()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddAdeptPower("Improved Reflexes", "1", "1.5");
        character.AddAdeptPower("Killing Hands", "1", "0.5");

        Assert.True(character.RemoveAdeptPower("Improved Reflexes"));
        Assert.False(character.RemoveAdeptPower("Improved Reflexes"));
        CharacterPowerData remaining = Assert.Single(character.AdeptPowers);
        Assert.Equal("Killing Hands", remaining.Name);
    }

    [Fact]
    public void AddMartialArt_SnapshotsAdvantagesAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddMartialArt("Krav Maga", new[] { "Extra attack", "+1 die of Subduing" }, "AR", "156");

        CharacterMartialArtData added = Assert.Single(character.MartialArts);
        Assert.Equal("Krav Maga", added.Name);
        Assert.Equal(new[] { "Extra attack", "+1 die of Subduing" }, added.Advantages);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterMartialArtData reloadedArt = Assert.Single(reloaded.MartialArts);
        Assert.Equal(2, reloadedArt.Advantages.Count);
    }

    [Fact]
    public void AddMartialArtManeuver_MutatesCharacterAndPersists()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddMartialArtManeuver("Sweep", "AR", "160");

        CharacterMartialArtManeuverData added = Assert.Single(character.MartialArtManeuvers);
        Assert.Equal("Sweep", added.Name);
    }

    [Fact]
    public void RemoveMartialArt_RemovesOnlyTheMatchingSavedMartialArt()
    {
        CharacterDocument character = LoadXml(
            "<character><martialarts><martialart><name>Krav Maga</name><rating>1</rating></martialart>"
            + "<martialart><name>Capoeira</name><rating>1</rating></martialart></martialarts></character>");

        Assert.True(character.RemoveMartialArt("Krav Maga"));
        Assert.False(character.RemoveMartialArt("Missing style"));
        CharacterMartialArtData remaining = Assert.Single(character.MartialArts);
        Assert.Equal("Capoeira", remaining.Name);
    }

    [Fact]
    public void RemoveMartialArtManeuver_RemovesOnlyTheMatchingSavedManeuver()
    {
        CharacterDocument character = LoadXml(
            "<character><martialartmaneuvers><martialartmaneuver><name>Sweep</name></martialartmaneuver>"
            + "<martialartmaneuver><name>Constrictor's Crush</name></martialartmaneuver></martialartmaneuvers></character>");

        Assert.True(character.RemoveMartialArtManeuver("Sweep"));
        Assert.False(character.RemoveMartialArtManeuver("Missing maneuver"));
        CharacterMartialArtManeuverData remaining = Assert.Single(character.MartialArtManeuvers);
        Assert.Equal("Constrictor's Crush", remaining.Name);
    }

    [Fact]
    public void Mugshot_RoundTripsBase64ThroughSaveAndReload()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        Assert.Equal(string.Empty, character.Mugshot);

        character.Mugshot = "Zm9vYmFy";
        Assert.Equal("Zm9vYmFy", character.Mugshot);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "test");
        Assert.Equal("Zm9vYmFy", reloaded.Mugshot);

        character.Mugshot = string.Empty;
        Assert.Equal(string.Empty, character.Mugshot);
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
    public void UpdateExpense_ChangesReasonAmountAndDateForTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 4, "Session reward", new DateTime(2026, 7, 22));
        character.AddExpense("Karma", 2, "Another entry", new DateTime(2026, 7, 23));
        string strGuid = character.KarmaExpenses[0].Guid;
        Assert.NotEmpty(strGuid);

        Assert.True(character.UpdateExpense(strGuid, "Corrected reward", 6, new DateTime(2026, 7, 24)));
        Assert.False(character.UpdateExpense("missing-guid", "x", 1, DateTime.Now));

        CharacterExpenseData updated = character.KarmaExpenses.Single(e => e.Guid == strGuid);
        Assert.Equal("Corrected reward", updated.Reason);
        Assert.Equal("6", updated.Amount);
        Assert.Equal("24.07.2026", updated.DisplayDate);
        // The other entry is untouched.
        Assert.Equal("Another entry", character.KarmaExpenses.Single(e => e.Guid != strGuid).Reason);
        // Updated entry (6) + the other, untouched entry (2) = 8.
        Assert.Equal(8, character.CareerKarma);
    }

    [Fact]
    public void RemoveExpense_RemovesOnlyTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 4, "Session reward", new DateTime(2026, 7, 22));
        character.AddExpense("Karma", 2, "Another entry", new DateTime(2026, 7, 23));
        string strGuid = character.KarmaExpenses[0].Guid;

        Assert.True(character.RemoveExpense(strGuid));
        Assert.False(character.RemoveExpense(strGuid));
        CharacterExpenseData remaining = Assert.Single(character.KarmaExpenses);
        Assert.Equal("Another entry", remaining.Reason);
    }

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

        CharacterTreeItemData armor = Assert.Single(character.Armor);
        Assert.Equal("Jacket", armor.Name);
        Assert.Equal("Night Out", armor.CustomName);
        Assert.Single(armor.Children);
        Assert.Equal("Fire Resistance", armor.Children[0].Name);

        Assert.Single(character.WeaponTrees);
        Assert.Equal("Pistol", character.WeaponTrees[0].Name);
        Assert.Equal(new[] { "Smartlink", "Gas Vent" }, character.WeaponTrees[0].Children.Select(item => item.Name));
    }

    [Fact]
    public void Weapon_CalculatedCostSumsAccessoriesAndMods()
    {
        // Weapon.Save() writes an already-resolved TotalCost (unlike Gear's raw formula), and
        // accessory/mod cost is likewise pre-resolved - CalculatedCost should still just add them
        // up correctly since the same Rating-substituting evaluator handles plain numbers too.
        var character = LoadXml("<character><name>Runner</name><weapons><weapon><name>Pistol</name>"
            + "<cost>250</cost><avail>4R</avail>"
            + "<accessories><accessory><name>Smartlink</name><cost>200</cost><avail>2</avail></accessory></accessories>"
            + "<weaponmods><weaponmod><name>Gas Vent</name><cost>50</cost><avail>0</avail></weaponmod></weaponmods>"
            + "</weapon></weapons></character>");

        CharacterTreeItemData weapon = character.WeaponTrees.Single();
        Assert.Equal(500, weapon.CalculatedCost);
        Assert.Equal("4R", weapon.CalculatedAvail);
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
    public void RemoveCustomImprovement_RemovesOnlyMatchingCustomSourcedEntries()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><improvements>"
            + "<improvement><unique>gmbonus</unique><improvedname>BOD</improvedname><sourcename>abc-123</sourcename>"
            + "<min>0</min><max>0</max><aug>1</aug><augmax>0</augmax><val>0</val><rating>1</rating>"
            + "<improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource></improvement>"
            + "<improvement><unique>wiredreflexes</unique><improvedname>REA</improvedname><sourcename>Wired Reflexes</sourcename>"
            + "<min>0</min><max>0</max><aug>2</aug><augmax>0</augmax><val>0</val><rating>1</rating>"
            + "<improvementttype>Attribute</improvementttype><improvementsource>Cyberware</improvementsource></improvement>"
            + "</improvements></character>");
        Assert.Equal(2, character.Improvements.Count);

        // A non-Custom sourcename never matches, even if it happens to collide.
        Assert.False(character.RemoveCustomImprovement("Wired Reflexes"));
        Assert.Equal(2, character.Improvements.Count);

        Assert.True(character.RemoveCustomImprovement("abc-123"));
        Improvement remaining = Assert.Single(character.Improvements);
        Assert.Equal("Wired Reflexes", remaining.SourceName);

        Assert.False(character.RemoveCustomImprovement("abc-123"));
    }

    [Fact]
    public void AddCustomImprovement_Attribute_AffectsTheAttributeAndCanBeRemoved()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("BOD", "3")
            + "</attributes></character>");

        Assert.True(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Attribute,
            "GM Bonus", intVal: 2, strSelect: "BOD"));

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(5, bod.Augmented.Value); // base 3 + the Improvement's own Augmented value of 2

        Assert.True(character.RemoveCustomImprovement("GM Bonus"));
        bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(3, bod.Augmented.Value);
    }

    [Fact]
    public void AddCustomImprovement_Skill_AffectsTheNamedSkillsDicePool()
    {
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>4</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills></character>");
        string strBaseline = character.Skills.Single().TotalValue;

        Assert.True(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Skill,
            "Trained by a Mentor", intVal: 2, strSelect: "Pistols"));

        string strWithBonus = character.Skills.Single().TotalValue;
        Assert.Equal(int.Parse(strBaseline) + 2, int.Parse(strWithBonus));
    }

    [Fact]
    public void AddCustomImprovement_ConditionMonitorPhysical_AddsBoxes()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("BOD", "3")
            + "</attributes></character>");
        int intBaseline = character.Condition.PhysicalCm.Value;

        Assert.True(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.ConditionMonitorPhysical,
            "Cyberlimb Reinforcement", intVal: 2));

        Assert.Equal(intBaseline + 2, character.Condition.PhysicalCm.Value);
    }

    [Fact]
    public void AddCustomImprovement_RequiresANameAndASelectionWhereApplicable()
    {
        CharacterDocument character = LoadXml("<character></character>");

        Assert.False(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Initiative, "", intVal: 1));
        Assert.False(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Attribute, "No selection", intVal: 1));
        Assert.Empty(character.Improvements);
    }

    [Fact]
    public void GetActiveSkillNames_IncludesSkillsOutsideCombatActive()
    {
        CharacterDocument character = LoadXml("<character></character>");
        Assert.Contains("Pistols", character.GetActiveSkillNames());
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
    public void Attributes_AugmentedValueIncludesAttributeImprovements()
    {
        CharacterDocument character = LoadFixture();

        // REA totalvalue 4, plus the fixture's Wired Reflexes +2 REA Improvement, minus 1 from
        // the fixture's own worn armor pushing ballistic encumbrance to -1 (BOD 4 -> threshold 8;
        // Actioneer Business Clothes b6 + Form-Fitting Bodysuit b6/2 = 9 total, ceil((9-8)/2) = 1).
        CharacterAttributeData rea = character.Attributes.Single(a => a.Code == "REA");
        Assert.Equal("4", rea.TotalValue);
        Assert.Equal(5, rea.Augmented.Value);
        Assert.Contains("Wired Reflexes", rea.Augmented.Tooltip);
        Assert.Contains("Rüstungsbehinderung (ballistisch)", rea.Augmented.Tooltip);

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
    public void RaiseAttribute_DeductsKarmaAndLogsExpenseWhenAffordable()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>100</karma>"
            + "<attributes><attribute><name>REA</name><value>4</value><totalvalue>4</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        Assert.True(character.RaiseAttribute("REA"));

        Assert.Equal("5", character.Attributes.Single(a => a.Code == "REA").Value);
        Assert.Equal("75", character.Karma);
        Assert.Single(character.KarmaExpenses);
        Assert.Equal("-25", character.KarmaExpenses[0].Amount);
    }

    [Fact]
    public void RaiseAttribute_FailsWithoutMutatingWhenNotEnoughKarma()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>10</karma>"
            + "<attributes><attribute><name>REA</name><value>4</value><totalvalue>4</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        Assert.False(character.RaiseAttribute("REA"));

        Assert.Equal("4", character.Attributes.Single(a => a.Code == "REA").Value);
        Assert.Equal("10", character.Karma);
        Assert.Empty(character.KarmaExpenses);
    }

    [Fact]
    public void SetAttributeValue_SetsBaseValueWithoutTouchingKarma()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>10</karma>"
            + "<attributes><attribute><name>REA</name><value>4</value><totalvalue>4</totalvalue>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax></attribute></attributes></character>");

        Assert.True(character.SetAttributeValue("REA", 6));

        Assert.Equal("6", character.Attributes.Single(a => a.Code == "REA").Value);
        Assert.Equal("10", character.Karma);
    }

    private static CharacterDocument LoadCharacterWithSkill(int intRating, bool blnGrouped = false, string strKarma = "100")
    {
        return LoadXml("<character><name>Runner</name><karma>" + strKarma + "</karma>"
            + "<skills><skill><name>Pistolen</name><attribute>AGI</attribute><skillcategory>Combat Active</skillcategory>"
            + "<skillgroup>Firearms</skillgroup><grouped>" + blnGrouped + "</grouped><rating>" + intRating
            + "</rating><knowledge>False</knowledge><exotic>False</exotic><spec /><allowdelete>True</allowdelete>"
            + "</skill></skills></character>");
    }

    [Fact]
    public void RaiseActiveSkill_NewSkillCostsKarmaNewActiveSkill()
    {
        CharacterDocument character = LoadCharacterWithSkill(intRating: 0);

        Assert.True(character.RaiseActiveSkill(0));

        Assert.Equal("1", character.Skills.Single().BaseRating);
        Assert.Equal("96", character.Karma); // 100 - KarmaNewActiveSkill(4)
    }

    [Fact]
    public void RaiseActiveSkill_ImprovingCostsRatingPlusOneTimesKarmaImproveActiveSkill()
    {
        CharacterDocument character = LoadCharacterWithSkill(intRating: 3);

        Assert.True(character.RaiseActiveSkill(0));

        Assert.Equal("4", character.Skills.Single().BaseRating);
        Assert.Equal("92", character.Karma); // 100 - (3+1)*KarmaImproveActiveSkill(2) = 100-8
    }

    [Fact]
    public void RaiseActiveSkill_DoublesCostAboveRatingSix()
    {
        CharacterDocument character = LoadCharacterWithSkill(intRating: 6);

        Assert.True(character.RaiseActiveSkill(0));

        Assert.Equal("7", character.Skills.Single().BaseRating);
        Assert.Equal("72", character.Karma); // 100 - (6+1)*2*2 = 100-28
    }

    [Fact]
    public void CareerKarmaCostPreviews_MatchTheMutationsTheyDescribe()
    {
        CharacterDocument skill = LoadCharacterWithSkill(intRating: 3);
        Assert.Equal(8, skill.GetActiveSkillKarmaCostToIncrease(0));
        Assert.Equal(2, skill.GetActiveSkillSpecializationKarmaCost());
        Assert.Null(skill.GetActiveSkillKarmaCostToIncrease(99));

        CharacterDocument group = LoadXml("<character><karma>100</karma><skillgroups>"
            + "<skillgroup><name>Firearms</name><rating>2</rating></skillgroup></skillgroups><skills>"
            + "<skill><name>Pistolen</name><skillgroup>Firearms</skillgroup><grouped>True</grouped><rating>2</rating>"
            + "<knowledge>False</knowledge></skill></skills></character>");
        Assert.Equal(15, group.GetSkillGroupKarmaCostToIncrease("Firearms"));

        CharacterDocument initiation = LoadXml("<character><magician>True</magician><karma>100</karma><attributes>"
            + AttributeXml("MAG", "3") + "</attributes></character>");
        Assert.Equal(13, initiation.GetInitiateKarmaCostToIncrease(blnGroup: false, blnOrdeal: false));
        Assert.Equal(8, initiation.GetInitiateKarmaCostToIncrease(blnGroup: true, blnOrdeal: true));
    }

    [Fact]
    public void RaiseActiveSkill_FailsWhenSkillIsGrouped()
    {
        CharacterDocument character = LoadCharacterWithSkill(intRating: 2, blnGrouped: true);

        Assert.False(character.RaiseActiveSkill(0));
        Assert.False(character.SetActiveSkillRating(0, 3));
        Assert.Equal("2", character.Skills.Single().BaseRating);
    }

    [Fact]
    public void RaiseSkillGroup_DeductsKarmaAndSyncsGroupedMemberSkills()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><karma>100</karma>"
            + "<skillgroups><skillgroup><name>Firearms</name><rating>2</rating></skillgroup></skillgroups>"
            + "<skills><skill><name>Pistolen</name><attribute>AGI</attribute><skillcategory>Combat Active</skillcategory>"
            + "<skillgroup>Firearms</skillgroup><grouped>True</grouped><rating>2</rating><knowledge>False</knowledge>"
            + "<exotic>False</exotic><spec /><allowdelete>True</allowdelete></skill></skills></character>");

        Assert.True(character.RaiseSkillGroup("Firearms"));

        Assert.Equal("3", character.SkillGroups.Single().Rating);
        Assert.Equal("3", character.Skills.Single().BaseRating);
        Assert.Equal("85", character.Karma); // 100 - (2+1)*KarmaImproveSkillGroup(5) = 100-15
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
    public void RaiseSkillGroup_RejectsWhenMemberSkillsHaveDivergedFromEachOther()
    {
        // Pistolen and Automatik were raised individually while the group sat at 0 - they no
        // longer agree with each other, so the group is "broken" and can't be raised as a whole
        // regardless of AllowSkillRegrouping.
        CharacterDocument character = LoadUnlockedFirearmsGroup("2", "1");
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowSkillRegrouping = true });

        Assert.False(character.RaiseSkillGroup("Firearms"));
        Assert.Equal("0", character.SkillGroups.Single(g => g.Name == "Firearms").Rating);
        Assert.Equal("100", character.Karma);
    }

    [Fact]
    public void RaiseSkillGroup_RejectsRegroupingWhenTheHouseRuleIsOff()
    {
        // Both member skills already agree (both at 2), but the group's own rating (0) hasn't
        // caught up - without AllowSkillRegrouping, resuming the group is still blocked.
        CharacterDocument character = LoadUnlockedFirearmsGroup("2", "2");

        Assert.False(character.RaiseSkillGroup("Firearms"));
        Assert.Equal("0", character.SkillGroups.Single(g => g.Name == "Firearms").Rating);
    }

    [Fact]
    public void RaiseSkillGroup_AllowsRegroupingFromAUniformRatingWhenTheHouseRuleIsOn()
    {
        CharacterDocument character = LoadUnlockedFirearmsGroup("2", "2");
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowSkillRegrouping = true });

        Assert.True(character.RaiseSkillGroup("Firearms"));

        Assert.Equal("3", character.SkillGroups.Single(g => g.Name == "Firearms").Rating);
        Assert.All(character.Skills, s => Assert.Equal("3", s.BaseRating));
        Assert.Equal("85", character.Karma); // caught up from the common rating 2, not the stale group rating 0
    }

    [Fact]
    public void AddActiveSkillSpecialization_CostsKarmaSpecializationInCareerMode()
    {
        CharacterDocument character = LoadCharacterWithSkill(intRating: 3);

        Assert.True(character.AddActiveSkillSpecialization(0, "Semi-Automatics"));

        Assert.Equal("Semi-Automatics", character.Skills.Single().Specialization);
        Assert.Equal("98", character.Karma); // 100 - KarmaSpecialization(2)
    }

    [Fact]
    public void SetActiveSkillSpecialization_DoesNotChargeKarma()
    {
        CharacterDocument character = LoadCharacterWithSkill(intRating: 3);

        Assert.True(character.SetActiveSkillSpecialization(0, "Semi-Automatics"));

        Assert.Equal("Semi-Automatics", character.Skills.Single().Specialization);
        Assert.Equal("100", character.Karma);
    }

    [Fact]
    public void AddExoticSkill_CreatesNewSkillAtRatingZero()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        character.AddExoticSkill("Exotic Ranged Weapon", "Bow", "Combat Active", "AGI");

        CharacterSkillData skill = character.Skills.Single();
        Assert.Equal("Exotic Ranged Weapon", skill.Name);
        Assert.Equal("Bow", skill.Specialization);
        Assert.True(skill.Exotic);
        Assert.Equal("0", skill.BaseRating);
    }

    [Fact]
    public void Gear_CalculatedCostAndAvailEvaluateRatingFormulasAndSumChildren()
    {
        CharacterDocument character = LoadFixture();

        CharacterTreeItemData commlink = character.Gear.Single(g => g.Name == "Custom Commlink");
        // cost "Rating*100" with Rating 3 -> 300, plus the child's cost 50 * qty 2 = 100 -> 400.
        Assert.Equal(400, commlink.CalculatedCost);
        // avail "6R" has no Rating reference, so it evaluates to 6 with the Restricted suffix kept.
        Assert.Equal("6R", commlink.CalculatedAvail);

        CharacterTreeItemData child = commlink.Children.Single();
        Assert.Equal(100, child.CalculatedCost);
        Assert.Equal("2", child.CalculatedAvail);
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
    public void Skill_DicePool_StacksRatingAndPoolAugmentationsFromDifferentSources()
    {
        CharacterDocument character = LoadFixture();
        CharacterSkillData pistolen = character.Skills.Single(s => s.Name == "Pistolen");

        // Base rating 4, Muscle Toner adds +1 to the rating itself (addtorating=True) -> "4 (5)".
        Assert.Equal("4", pistolen.BaseRating);
        Assert.Equal("4 (5)", pistolen.Rating);

        // Pool = augmented rating(5) + Smartlink's +2 pool-only bonus + AGI(6) - fixture's -1
        // wound modifier (1 filled physical CM box) = 12.
        Assert.Equal("12", pistolen.TotalValue);
        Assert.Contains("Muscle Toner: +1", pistolen.PoolTooltip);
        Assert.Contains("Smartlink: +2", pistolen.PoolTooltip);
    }

    private static string SkillRatingImprovementXml(string strSkillName, string strValue) =>
        "<improvement><improvementttype>Skill</improvementttype><improvementsource>Quality</improvementsource>"
        + "<improvedname>" + strSkillName + "</improvedname><addtorating>True</addtorating>"
        + "<val>" + strValue + "</val><enabled>True</enabled></improvement>";

    [Fact]
    public void Skill_DicePool_EnforceMaximumSkillRatingModifierHouseRule_CapsAugmentedRatingAt1Point5x()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "3")
            + "</attributes><skills><skill><name>Schleichen</name><attribute>AGI</attribute><rating>2</rating>"
            + "<skillcategory>Physisch</skillcategory><knowledge>False</knowledge></skill></skills>"
            + "<improvements>" + SkillRatingImprovementXml("Schleichen", "3") + "</improvements></character>");

        // EnforceMaximumSkillRatingModifier defaults to True (it's SR4's core rule, not really a
        // house rule) - disable it first to see the uncapped value: augmented 5 + AGI(3) = 8.
        character.SetCharacterOptionsForTesting(new CharacterOptions { EnforceMaximumSkillRatingModifier = false });
        CharacterSkillData uncapped = character.Skills.Single(s => s.Name == "Schleichen");
        Assert.Equal("2 (5)", uncapped.Rating);
        Assert.Equal("8", uncapped.TotalValue);

        var objOptions = new CharacterOptions { EnforceMaximumSkillRatingModifier = true };
        character.SetCharacterOptionsForTesting(objOptions);

        // floor(2 * 1.5) = 3 caps the augmented-rating contribution -> pool = 3 + AGI(3) = 6.
        CharacterSkillData capped = character.Skills.Single(s => s.Name == "Schleichen");
        Assert.Equal("6", capped.TotalValue);
        Assert.Contains("Hausregel", capped.PoolTooltip);
    }

    private static string SkillPoolImprovementXml(string strSkillName, string strValue) =>
        "<improvement><improvementttype>Skill</improvementttype><improvementsource>Quality</improvementsource>"
        + "<improvedname>" + strSkillName + "</improvedname><addtorating>False</addtorating>"
        + "<val>" + strValue + "</val><enabled>True</enabled></improvement>";

    [Fact]
    public void Skill_DicePool_CapSkillRatingHouseRule_CapsPoolAgainstNaturalAttributePlusBaseRating()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "2")
            + "</attributes><skills><skill><name>Schleichen</name><attribute>AGI</attribute><rating>1</rating>"
            + "<skillcategory>Physisch</skillcategory><knowledge>False</knowledge></skill></skills>"
            + "<improvements>" + SkillPoolImprovementXml("Schleichen", "25") + "</improvements></character>");

        // Uncapped: rating(1) + pool Improvement(+25) + AGI(2) = 28.
        CharacterSkillData uncapped = character.Skills.Single(s => s.Name == "Schleichen");
        Assert.Equal("28", uncapped.TotalValue);

        var objOptions = new CharacterOptions { CapSkillRating = true };
        character.SetCharacterOptionsForTesting(objOptions);

        // Natural AGI(2) + base Rating(1) = 3, doubled = 6; the house rule floors the cap at 20,
        // so the pool is capped to 20 even though 6 < 28.
        CharacterSkillData capped = character.Skills.Single(s => s.Name == "Schleichen");
        Assert.Equal("20", capped.TotalValue);
    }

    [Fact]
    public void Skill_DicePool_RatingZero_DefaultsOffLinkedAttributeMinusOneWhenAllowed()
    {
        // "Animal Handling" is <default>Yes</default> in skills.xml.
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("CHA", "4")
            + "</attributes><skills><skill><name>Animal Handling</name><attribute>CHA</attribute><rating>0</rating>"
            + "<skillcategory>Physisch</skillcategory><knowledge>False</knowledge></skill></skills></character>");

        CharacterSkillData skill = character.Skills.Single(s => s.Name == "Animal Handling");
        // CHA(4) - 1 = 3, no wound modifier in this synthetic character.
        Assert.Equal("3", skill.TotalValue);
        Assert.Contains("default", skill.PoolTooltip);
    }

    [Fact]
    public void Skill_DicePool_RatingZero_StaysZeroWhenSkillDoesNotAllowDefaulting()
    {
        // "Arcana" is <default>No</default> in skills.xml.
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("LOG", "6")
            + "</attributes><skills><skill><name>Arcana</name><attribute>LOG</attribute><rating>0</rating>"
            + "<skillcategory>Magisch</skillcategory><knowledge>False</knowledge></skill></skills></character>");

        CharacterSkillData skill = character.Skills.Single(s => s.Name == "Arcana");
        Assert.Equal("0", skill.TotalValue);
    }

    [Fact]
    public void Skill_DicePool_RatingZero_KnowledgeSkillsAlwaysDefault()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("INT", "5")
            + "</attributes><skills><skill><name>Erfundenes Wissen</name><attribute>INT</attribute><rating>0</rating>"
            + "<skillcategory>Interest</skillcategory><knowledge>True</knowledge></skill></skills></character>");

        CharacterSkillData skill = character.KnowledgeSkills.Single(s => s.Name == "Erfundenes Wissen");
        // INT(5) - 1 = 4, since Knowledge Skills can always default regardless of skills.xml.
        Assert.Equal("4", skill.TotalValue);
    }

    [Fact]
    public void Skill_DicePool_RatingZero_SkillDefaultingIncludesModifiersHouseRule_AddsRatingAndPoolBonuses()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("CHA", "4")
            + "</attributes><skills><skill><name>Animal Handling</name><attribute>CHA</attribute><rating>0</rating>"
            + "<skillcategory>Physisch</skillcategory><knowledge>False</knowledge></skill></skills>"
            + "<improvements>" + SkillRatingImprovementXml("Animal Handling", "1")
            + SkillPoolImprovementXml("Animal Handling", "2") + "</improvements></character>");

        // Without the house rule, Rating/Pool Improvements are ignored while defaulting: CHA(4) - 1 = 3.
        CharacterSkillData withoutModifiers = character.Skills.Single(s => s.Name == "Animal Handling");
        Assert.Equal("3", withoutModifiers.TotalValue);

        var objOptions = new CharacterOptions { SkillDefaultingIncludesModifiers = true };
        character.SetCharacterOptionsForTesting(objOptions);

        // With the house rule: CHA(4) - 1 + 1 (rating) + 2 (pool) = 6.
        CharacterSkillData withModifiers = character.Skills.Single(s => s.Name == "Animal Handling");
        Assert.Equal("6", withModifiers.TotalValue);
    }

    [Fact]
    public void KnowledgeSkill_DicePool_ComputedTheSameWayAsActiveSkills()
    {
        CharacterDocument character = LoadFixture();
        CharacterSkillData knowledgeSkill = character.KnowledgeSkills.Single(s => s.Name == "Straßenwissen");

        // Straßenwissen: rating 3, attribute INT(4) -> pool 7, no Improvements targeting it,
        // minus the fixture's -1 wound modifier (1 filled physical CM box) = 6.
        Assert.Equal("3", knowledgeSkill.BaseRating);
        Assert.Equal("3", knowledgeSkill.Rating);
        Assert.Equal("6", knowledgeSkill.TotalValue);
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
    public void MatrixInitiative_DefaultPath_AddsActiveEquippedCommlinkResponse()
    {
        var character = LoadXml("<character><name>Runner</name><metatype>Human</metatype><attributes>"
            + AttributeXml("INT", "4") + "</attributes><gears><gear><name>Fancy Commlink</name>"
            + "<category>Commlink</category><equipped>True</equipped><active>True</active>"
            + "<response>5</response></gear></gears></character>");

        Assert.Equal(9, character.MatrixInitiative.Base); // INT(4) + Response(5)
        Assert.Contains("Kommlink-Antwort: 5", character.MatrixInitiative.Tooltip);
    }

    [Fact]
    public void MatrixInitiative_DefaultPath_IgnoresInactiveCommlink()
    {
        var character = LoadXml("<character><name>Runner</name><metatype>Human</metatype><attributes>"
            + AttributeXml("INT", "4") + "</attributes><gears><gear><name>Fancy Commlink</name>"
            + "<category>Commlink</category><equipped>True</equipped><active>False</active>"
            + "<response>5</response></gear></gears></character>");

        Assert.Equal(4, character.MatrixInitiative.Base); // Response not counted - commlink isn't active.
    }

    [Fact]
    public void Movement_AppliesLandSwimAndFlyImprovements()
    {
        CharacterDocument character = LoadXml("<character><movement>10/25,Swim 4/8,Fly 20/40</movement><improvements>"
            + ImprovementXml("MovementPercent", "10") + ImprovementXml("SwimPercent", "25")
            + ImprovementXml("FlyPercent", "50") + "</improvements></character>");

        Assert.Equal("11/27", character.WalkMovement);
        Assert.Equal("5/10", character.SwimMovement);
        Assert.Equal("30/60", character.FlyMovement);
    }

    [Fact]
    public void Movement_FlySpeedCanUseAMultipleOfWalkMovement()
    {
        CharacterDocument character = LoadXml("<character><movement>8/20</movement><improvements>"
            + ImprovementXml("FlySpeed", "-2") + "</improvements></character>");

        Assert.Equal("16/40", character.FlyMovement);
    }

    [Fact]
    public void Edge_SpendAndRegain_PersistAcrossSaveReload()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("EDG", "3") + "</attributes></character>");
        Assert.Equal(3, character.Edge.Remaining);
        Assert.True(character.SpendEdge());
        Assert.Equal(2, character.Edge.Remaining);
        Assert.True(character.RegainEdge());
        Assert.Equal(3, character.Edge.Remaining);
    }

    [Fact]
    public void BurnEdge_PermanentlyLowersTheMaximumAndPersistsAcrossSaveReload()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("EDG", "3") + "</attributes></character>");
        Assert.Equal(3, character.Edge.Maximum);

        Assert.True(character.BurnEdge());
        Assert.Equal(2, character.Edge.Maximum);
        Assert.Equal(2, character.Edge.Remaining);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(2, reloaded.Edge.Maximum);
    }

    [Fact]
    public void BurnEdge_CannotGoBelowZero()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("EDG", "0") + "</attributes></character>");

        Assert.False(character.BurnEdge());
        Assert.Equal(0, character.Edge.Maximum);
    }

    [Fact]
    public void RaiseInitiateGrade_DeductsKarmaAndPersistsAcrossSaveReload()
    {
        CharacterDocument character = LoadXml("<character><magician>True</magician><karma>100</karma>"
            + "<attributes>" + AttributeXml("MAG", "3") + "</attributes></character>");
        Assert.Equal(0, character.InitiateGrade);

        // No Group/Ordeal discount: ceil(10 + 1*3) = 13.
        Assert.True(character.RaiseInitiateGrade(blnGroup: false, blnOrdeal: false));
        Assert.Equal(1, character.InitiateGrade);
        Assert.Equal("87", character.Karma);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(1, reloaded.InitiateGrade);
        CharacterInitiationGradeData grade = Assert.Single(reloaded.InitiationGrades);
        Assert.Equal("1", grade.Grade);
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

    [Fact]
    public void WoundModifiers_ApplyBothConditionMonitorTracks()
    {
        CharacterDocument character = LoadXml("<character><physicalcmfilled>3</physicalcmfilled><stuncmfilled>4</stuncmfilled></character>");

        Assert.Equal(-3, character.WoundModifiers);
    }

    [Fact]
    public void ConditionDamage_AdjustmentClampsToMonitorAndPersists()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("BOD", "4")
            + AttributeXml("WIL", "2") + "</attributes></character>");

        Assert.True(character.AdjustPhysicalDamage(20));
        Assert.Equal("10", character.Condition.PhysicalDamage);
        Assert.False(character.AdjustPhysicalDamage(1));
        Assert.True(character.AdjustPhysicalDamage(-2));
        Assert.Equal("8", character.Condition.PhysicalDamage);
        Assert.True(character.AdjustStunDamage(1));
        Assert.Equal("1", character.Condition.StunDamage);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("8", reloaded.Condition.PhysicalDamage);
        Assert.Equal("1", reloaded.Condition.StunDamage);
    }

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
    public void Lifestyles_CanBeAddedRemovedAndPersisted()
    {
        CharacterDocument character = LoadXml("<character />");
        character.AddLifestyle("Low", "2000");
        Assert.Single(character.Lifestyles);
        Assert.Equal("Low", character.Lifestyles[0].Name);
        Assert.True(character.RemoveLifestyle("Low"));
        Assert.Empty(character.Lifestyles);
    }

    [Fact]
    public void AddAdvancedLifestyle_ComputesLpCostAndDiceMultiplierFromRealData()
    {
        CharacterDocument character = LoadXml("<character />");

        // All five aspects "Middle" (3 LP each) = 15 LP -> 5000 nuyen, Middle tier (dice 4,
        // multiplier 100). Adding Commercial Zone (+1 LP) and AI in Residence (-3 LP) brings the
        // total to 13 LP, which real lifestyles.xml prices at 3800 and still maps to the Middle
        // tier (11-15 LP).
        var preview = character.PreviewAdvancedLifestyle("Middle", "Middle", "Middle", "Middle", "Middle",
            intRoommates: 0, intPercentage: 100, new[] { "Commercial Zone" }, new[] { "AI in Residence" });
        Assert.Equal(13, preview.Lp);
        Assert.Equal(3800, preview.Cost);
        Assert.Equal(4, preview.Dice);
        Assert.Equal(100, preview.Multiplier);

        character.AddAdvancedLifestyle("Safehouse Alpha", "Middle", "Middle", "Middle", "Middle", "Middle",
            intRoommates: 0, intPercentage: 100, new[] { "Commercial Zone" }, new[] { "AI in Residence" });

        CharacterLifestyleData added = Assert.Single(character.Lifestyles);
        Assert.Equal("Safehouse Alpha", added.Name);
        Assert.Equal("3800", added.Cost);
        Assert.Equal("4", added.Dice);
        Assert.Equal("100", added.Multiplier);
    }

    [Fact]
    public void AddAdvancedLifestyle_FeedsItsOwnDiceMultiplierIntoTheNuyenRoll()
    {
        // GetLifestyleNuyenRollInfo's by-name lifestyles.xml re-lookup can't find a custom
        // Advanced Lifestyle name - it must use the Dice/Multiplier persisted directly on add.
        CharacterDocument character = LoadXml("<character><nuyen>0</nuyen></character>");
        character.AddAdvancedLifestyle("My Penthouse", "High", "High", "High", "High", "High",
            intRoommates: 0, intPercentage: 100, Array.Empty<string>(), Array.Empty<string>());

        var info = character.GetLifestyleNuyenRollInfo();
        Assert.NotNull(info);
        Assert.True(info!.Dice > 0);
        Assert.True(info.Multiplier > 0);
    }

    [Fact]
    public void WeaponEquippedState_CanBeChanged()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Predator</name><category>Pistols</category><equipped>True</equipped></weapon></weapons></character>");
        Assert.True(character.SetWeaponEquipped("Ares Predator", "Pistols", false));
        Assert.False(character.WeaponTrees.Single().Equipped);
    }

    [Fact]
    public void VehicleDamage_CanBeAdjustedWithoutGoingBelowZero()
    {
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><name>Americar</name><category>Cars</category><physicalcmfilled>1</physicalcmfilled></vehicle></vehicles></character>");
        Assert.True(character.AdjustVehicleDamage("Americar", "Cars", 2));
        Assert.Equal("3", character.Vehicles.Single().PhysicalCmFilled);
        Assert.True(character.AdjustVehicleDamage("Americar", "Cars", -5));
        Assert.Equal("0", character.Vehicles.Single().PhysicalCmFilled);
    }

    [Fact]
    public void VehicleMods_CanBeAddedRemovedAndChargeBodyBasedCost()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>4</body><mods />"
            + "</vehicle></vehicles></character>");

        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R",
            "Body * 200", "AR", "132"));
        CharacterTreeItemData mod = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal("Anti-Theft", mod.Name);
        Assert.Equal("Standard", mod.Category);
        Assert.Equal(4200d, double.Parse(character.Nuyen, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(Guid.TryParse(mod.ItemGuid, out Guid modId));
        Assert.True(character.RemoveVehicleMod(vehicleId, modId));
        Assert.Empty(character.Vehicles.Single().Children);
    }

    [Fact]
    public void ObsoleteVehicleMod_CanBeRetrofittedAtSelectedPercentage()
    {
        Guid vehicleId = Guid.NewGuid();
        Guid modId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><cost>7000</cost><mods><mod>"
            + "<guid>" + modId + "</guid><name>Obsolete</name><category>Special</category><slots>0</slots>"
            + "</mod></mods></vehicle></vehicles></character>");

        Assert.True(character.RetrofitVehicleObsolescence(vehicleId, modId, 50));
        CharacterTreeItemData retrofit = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal("Retrofit", retrofit.Name);
        Assert.Equal("1500", character.Nuyen);
        CharacterExpenseData expense = Assert.Single(character.NuyenExpenses);
        Assert.Equal("-3500", expense.Amount);
    }

    [Fact]
    public void ObsolescentVehicleMod_RequiresItsHouseRule()
    {
        Guid vehicleId = Guid.NewGuid();
        Guid modId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><cost>1000</cost><mods><mod>"
            + "<guid>" + modId + "</guid><name>Obsolescent</name><category>Special</category><slots>0</slots>"
            + "</mod></mods></vehicle></vehicles></character>");

        Assert.False(character.RetrofitVehicleObsolescence(vehicleId, modId, 10));
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowObsolescentUpgrade = true });
        Assert.True(character.RetrofitVehicleObsolescence(vehicleId, modId, 10));
        Assert.Equal("Retrofit", Assert.Single(character.Vehicles.Single().Children).Name);
    }

    [Fact]
    public void SkillDiceRollingAvailability_UsesTheCharacterHouseRule()
    {
        CharacterDocument character = LoadXml("<character />");

        Assert.False(character.AllowSkillDiceRollingEnabled);
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowSkillDiceRolling = true });
        Assert.True(character.AllowSkillDiceRollingEnabled);
    }

    [Fact]
    public void MoreLethalGameplay_AddsTwoToNumericWeaponDamageOnly()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Predator</name>"
            + "<category>Pistols</category><damage>8P</damage></weapon><weapon><name>Grenade</name>"
            + "<category>Thrown</category><damage>Grenade</damage></weapon></weapons></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MoreLethalGameplay = true });

        Assert.Equal("10P", character.Weapons[0].Damage);
        Assert.Equal("Grenade", character.Weapons[1].Damage);
        Assert.Equal("10P", character.WeaponTrees[0].WeaponDamage);

        CharacterDocument vehicleCharacter = LoadXml("<character><vehicles><vehicle><name>Van</name>"
            + "<category>Cars</category><weapons><weapon><name>LMG</name><category>Machine Guns</category>"
            + "<damage>6P</damage></weapon></weapons></vehicle></vehicles></character>");
        vehicleCharacter.SetCharacterOptionsForTesting(new CharacterOptions { MoreLethalGameplay = true });
        Assert.Equal("8P", vehicleCharacter.Vehicles.Single().Children.Single().WeaponDamage);
    }

    [Fact]
    public void VehicleSlots_ComputedFromBodyAndSummedAcrossInstalledMods()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>6</body><mods />"
            + "</vehicle></vehicles></character>");

        CharacterVehicleData vehicle = character.Vehicles.Single();
        // Body(6) > 4, so TotalSlots = Body, not the 4-slot floor.
        Assert.Equal(6, vehicle.TotalSlots);
        Assert.Equal(0, vehicle.SlotsUsed);
        Assert.Equal(6, vehicle.SlotsRemaining);

        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "Body * 200", "AR", "132"));
        vehicle = character.Vehicles.Single();
        Assert.Equal(2, vehicle.SlotsUsed);
        Assert.Equal(4, vehicle.SlotsRemaining);
    }

    [Fact]
    public void VehicleTotalCost_SumsBaseCostModsAndOnboardGear()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>6</body>"
            + "<cost>7000</cost><mods /></vehicle></vehicles></character>");

        Assert.Equal(7000, character.Vehicles.Single().TotalCost);

        // "Body * 200" at Body(6) -> 1200.
        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "Body * 200", "AR", "132"));
        Assert.Equal(8200, character.Vehicles.Single().TotalCost);

        Assert.True(character.AddVehicleGear(vehicleId, "Fake SIN", "Fake Identification", strCost: "500"));
        Assert.Equal(8700, character.Vehicles.Single().TotalCost);
    }

    [Fact]
    public void VehicleSlots_LowBodyVehicleFloorsAtFourSlots()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Dodge Scoot</name><category>Bikes</category><body>2</body><mods />"
            + "</vehicle></vehicles></character>");

        // Body(2) < 4, so TotalSlots floors at 4 (clsEquipment.cs's Vehicle.Slots).
        Assert.Equal(4, character.Vehicles.Single().TotalSlots);
    }

    private static CharacterDocument LoadVehicleWithSensorSuite(string strSavedSensor)
        => LoadXml("<character><vehicles><vehicle><guid>" + Guid.NewGuid() + "</guid><name>Americar</name>"
            + "<category>Cars</category><body>6</body><sensor>" + strSavedSensor + "</sensor><mods />"
            + "<gears><gear><name>Sensor Array</name><category>Sensors</category><signal>3</signal>"
            + "<children>"
            + "<gear><name>Camera</name><category>Sensor Functions</category><rating>4</rating></gear>"
            + "<gear><name>Radio Signal Scanner</name><category>Sensor Functions</category><rating>2</rating></gear>"
            + "</children></gear></gears></vehicle></vehicles></character>");

    [Fact]
    public void CalculatedSensor_AveragesSensorFunctionRatingsOfTheFirstOnboardGearItem()
    {
        CharacterDocument character = LoadVehicleWithSensorSuite("2");
        CharacterVehicleData vehicle = character.Vehicles.Single();

        // (4 + 2) / 2 = 3, rounded up (already whole).
        Assert.Equal(3, vehicle.CalculatedSensor);
    }

    [Fact]
    public void SensorDisplay_OnlyUsesTheCalculatedValueWhenTheHouseRuleIsOn()
    {
        CharacterDocument offCharacter = LoadVehicleWithSensorSuite("2");
        offCharacter.SetCharacterOptionsForTesting(new CharacterOptions { UseCalculatedVehicleSensorRatings = false });
        Assert.Equal("2", offCharacter.Vehicles.Single().SensorDisplay);

        CharacterDocument onCharacter = LoadVehicleWithSensorSuite("2");
        onCharacter.SetCharacterOptionsForTesting(new CharacterOptions { UseCalculatedVehicleSensorRatings = true });
        Assert.Equal("3", onCharacter.Vehicles.Single().SensorDisplay);
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
    public void AddVehicleMod_RejectsAModThatWouldExceedRemainingSlots()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>4</body><mods />"
            + "</vehicle></vehicles></character>");

        // TotalSlots = 4 (Body floor). A mod needing 5 slots doesn't fit.
        Assert.False(character.AddVehicleMod(vehicleId, "Oversized Mod", "Standard", "0", "5", "6R",
            "Body * 200", "AR", "132"));
        Assert.Empty(character.Vehicles.Single().Children);

        // But one that exactly fits does, and a second one that would push past the limit is rejected.
        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "4", "6R",
            "Body * 200", "AR", "132"));
        Assert.False(character.AddVehicleMod(vehicleId, "Another Mod", "Standard", "0", "1", "6R",
            "Body * 200", "AR", "132"));
        Assert.Single(character.Vehicles.Single().Children);
    }

    [Fact]
    public void VehicleMod_IncludedInVehicle_DoesNotCountTowardSlotsUsed()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>4</body><mods>"
            + "<mod><guid>" + Guid.NewGuid() + "</guid><name>Standard Chassis</name><category>Standard</category>"
            + "<slots>4</slots><rating>0</rating><included>True</included></mod>"
            + "</mods></vehicle></vehicles></character>");

        CharacterVehicleData vehicle = character.Vehicles.Single();
        Assert.Equal(0, vehicle.SlotsUsed);
        Assert.Equal(4, vehicle.SlotsRemaining);
    }

    [Fact]
    public void VehicleGear_CanBeAddedAndRemovedWithoutEnteringCharacterGearTree()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><gears />"
            + "</vehicle></vehicles></character>");

        Assert.True(character.AddVehicleGear(vehicleId, "Vehicle Toolkit", "Tools", "0", "2", "250", "4", "SR4", "320"));
        CharacterTreeItemData gear = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal("Vehicle Toolkit", gear.Name);
        Assert.Equal("4500", character.Nuyen);
        Assert.Empty(character.Gear);
        Assert.True(Guid.TryParse(gear.ItemGuid, out Guid gearId));
        Assert.True(character.RemoveVehicleGear(vehicleId, gearId));
        Assert.Empty(character.Vehicles.Single().Children);
    }

    [Fact]
    public void VehicleWeapons_CanBeAddedRemovedAndCharged()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");

        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        CharacterTreeItemData weapon = character.Vehicles.Single().Children.Single(c => c.Name == "Ares Alpha");
        Assert.Equal("6000", character.Nuyen); // 10000 - 1500(mount) - 2500(weapon)
        Assert.True(Guid.TryParse(weapon.ItemGuid, out Guid weaponId));
        Assert.True(character.RemoveVehicleWeapon(vehicleId, weaponId));
        Assert.Single(character.Vehicles.Single().Children); // the mount mod itself is still there
    }

    [Fact]
    public void AddVehicleWeapon_RejectedWithoutAnAvailableMount()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons /><mods />"
            + "</vehicle></vehicles></character>");

        // No Weapon Mount/Mechanical Arm mod installed at all.
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        Assert.Empty(character.Vehicles.Single().Children);

        // A regular (non-mount) mod doesn't count.
        character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "200", "AR", "132");
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
    }

    [Fact]
    public void AddVehicleWeapon_LimitsDirectWeaponsToTheInstalledMountCount()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>100000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons /><mods />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");

        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        // The single mount is already occupied - a second direct weapon is rejected.
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA",
            "0", "15", "350", "4R", "SR4", "313"));

        // Installing a second mount frees up room for a second weapon.
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA",
            "0", "15", "350", "4R", "SR4", "313"));
    }

    [Fact]
    public void AddVehicleWeapon_TracksWhichSpecificMountEachWeaponOccupies()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>100000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons /><mods />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");

        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA",
            "0", "15", "350", "4R", "SR4", "313"));

        var mounts = character.Vehicles.Single().Children.Where(c => c.Name.StartsWith("Weapon Mount")).ToList();
        var weapons = character.Vehicles.Single().Children.Where(c => !c.Name.StartsWith("Weapon Mount")).ToList();
        Assert.Equal(2, mounts.Count);
        Assert.Equal(2, weapons.Count);

        // Each weapon claimed a distinct mount, not just "there were enough mounts overall".
        Guid guiAlphaId = Guid.Parse(weapons.Single(w => w.Name == "Ares Alpha").ItemGuid);
        Assert.True(character.RemoveVehicleWeapon(vehicleId, guiAlphaId));

        // Removing one weapon frees exactly its own mount - a third weapon can now be added even
        // though the Predator IV still occupies the other mount.
        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312")); // Both mounts occupied again.
    }

    [Fact]
    public void VehicleLocations_CanBeAddedRemovedAndReloaded()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><guid>" + vehicleId
            + "</guid><name>Americar</name><category>Cars</category></vehicle></vehicles></character>");

        Assert.True(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.Contains("Kofferraum", character.Vehicles.Single().Locations);
        Assert.False(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.True(character.RemoveVehicleLocation(vehicleId, "Kofferraum"));
        Assert.Empty(character.Vehicles.Single().Locations);
    }

    [Fact]
    public void AssignVehicleGearLocation_MovesGearIntoAndOutOfANamedLocation()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><guid>" + vehicleId
            + "</guid><name>Americar</name><category>Cars</category></vehicle></vehicles></character>");
        Assert.True(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.True(character.AddVehicleGear(vehicleId, "Fake SIN", "Fake Identification"));

        CharacterTreeItemData gear = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal(string.Empty, gear.Location);
        Assert.True(Guid.TryParse(gear.ItemGuid, out Guid gearId));

        // Assigning to a location that doesn't exist on the vehicle is rejected.
        Assert.False(character.AssignVehicleGearLocation(vehicleId, gearId, "Kein Ort"));

        Assert.True(character.AssignVehicleGearLocation(vehicleId, gearId, "Kofferraum"));
        Assert.Equal("Kofferraum", character.Vehicles.Single().Children.Single().Location);

        // Removing the location clears the assignment from the gear that referenced it.
        Assert.True(character.RemoveVehicleLocation(vehicleId, "Kofferraum"));
        Assert.Equal(string.Empty, character.Vehicles.Single().Children.Single().Location);

        Assert.True(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.True(character.AssignVehicleGearLocation(vehicleId, gearId, "Kofferraum"));
        Assert.True(character.AssignVehicleGearLocation(vehicleId, gearId, string.Empty));
        Assert.Equal(string.Empty, character.Vehicles.Single().Children.Single().Location);
    }

    [Fact]
    public void ArmorSets_CanBeCreatedAssignedAndDissolved()
    {
        CharacterDocument character = LoadXml("<character><armors><armor><name>Armor Jacket</name><category>Armor</category><b>8</b><i>6</i></armor></armors></character>");

        Assert.True(character.AddArmorSet("Einsatzanzug"));
        Assert.Contains("Einsatzanzug", character.ArmorSets);
        Assert.True(character.SetArmorSet(character.Armor.Single(a => a.Name == "Armor Jacket").ArmorId, "Einsatzanzug"));
        CharacterTreeItemData set = Assert.Single(character.Armor);
        Assert.Equal("Einsatzanzug", set.Name);
        Assert.Equal("Armor set", set.Category);
        Assert.Single(set.Children);

        Assert.True(character.RemoveArmorSet("Einsatzanzug"));
        CharacterTreeItemData armor = Assert.Single(character.Armor);
        Assert.Equal("Armor Jacket", armor.Name);
        Assert.Empty(character.ArmorSets);
    }

    [Fact]
    public void ArmorMetadata_SeparatesLegacyCustomNameFromArmorSetAndMigratesOldPortAssignment()
    {
        CharacterDocument character = LoadXml("<character><armorbundles><armorbundle>Old Set</armorbundle><armorbundle>New Set</armorbundle></armorbundles><armors>"
            + "<armor><name>Armor Jacket</name><category>Armor</category><armorname>Old Set</armorname></armor>"
            + "<armor><name>Helmet</name><category>Armor</category><armorname>Personal label</armorname></armor>"
            + "</armors></character>");

        CharacterTreeItemData migrated = Assert.Single(character.Armor.Single(a => a.Name == "Old Set").Children);
        Assert.Equal(string.Empty, migrated.CustomName);
        CharacterTreeItemData labelled = character.Armor.Single(a => a.Name == "Helmet");
        Assert.Equal("Personal label", labelled.CustomName);

        Assert.True(character.SetArmorSet(migrated.ArmorId, "New Set"));
        Assert.True(character.SetArmorNotes(migrated.ArmorId, "Repair before next run."));
        Assert.True(character.SetArmorCustomName(migrated.ArmorId, "Covert jacket"));

        CharacterTreeItemData moved = Assert.Single(character.Armor.Single(a => a.Name == "New Set").Children);
        Assert.Equal("Covert jacket", moved.CustomName);
        Assert.Equal("Repair before next run.", moved.Notes);
        Assert.Equal("Armor Jacket", moved.Name);
    }

    [Fact]
    public void WeaponLocations_CanBeCreatedAssignedAndDissolved()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Predator</name><category>Pistols</category></weapon></weapons></character>");
        Assert.True(character.AddWeaponLocation("Concealed"));
        Assert.True(character.SetWeaponLocation("Ares Predator", "Pistols", "Concealed"));
        CharacterTreeItemData location = Assert.Single(character.WeaponTrees);
        Assert.Equal("Concealed", location.Name);
        Assert.Equal("Weapon location", location.Category);
        Assert.Single(location.Children);
        Assert.True(character.RemoveWeaponLocation("Concealed"));
        Assert.Equal("Ares Predator", Assert.Single(character.WeaponTrees).Name);
    }

    [Fact]
    public void GearLocations_CanBeCreatedAssignedAndDissolved()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Fake SIN", "Fake Identification", "0");
        int intGearId = character.Gear[0].GearId;

        Assert.True(character.AddGearLocation("Rucksack"));
        Assert.Contains("Rucksack", character.GearLocations);

        // Assigning to a location that doesn't exist is rejected.
        Assert.False(character.SetGearLocation(intGearId, "Nirgendwo"));

        Assert.True(character.SetGearLocation(intGearId, "Rucksack"));
        CharacterTreeItemData location = Assert.Single(character.Gear);
        Assert.Equal("Rucksack", location.Name);
        Assert.Equal("Gear location", location.Category);
        Assert.Single(location.Children);

        Assert.True(character.RemoveGearLocation("Rucksack"));
        CharacterTreeItemData remaining = Assert.Single(character.Gear);
        Assert.Equal("Fake SIN", remaining.Name);
        Assert.Equal(string.Empty, remaining.Location);
    }

    [Fact]
    public void GearLocations_CannotBeAssignedToNestedChildGear()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddGear("Commlink", "Commlink", "0");
        int intParentId = character.Gear[0].GearId;
        character.AddChildGear(intParentId, "Certified Credstick, Silver", "Commlink Accessory", "0");
        int intChildId = character.Gear[0].Children[0].GearId;
        character.AddGearLocation("Rucksack");

        Assert.False(character.SetGearLocation(intChildId, "Rucksack"));
    }

    private static string ImprovementXml(string strType, string strValue) =>
        "<improvement><improvementttype>" + strType + "</improvementttype><improvementsource>Quality</improvementsource>"
        + "<val>" + strValue + "</val><enabled>True</enabled></improvement>";

    private static string ImprovementAugXml(string strType, string strAug) =>
        "<improvement><improvementttype>" + strType + "</improvementttype><improvementsource>Quality</improvementsource>"
        + "<aug>" + strAug + "</aug><enabled>True</enabled></improvement>";

    [Fact]
    public void AdeptPowerPoints_PureAdept_UsesFullMagAttribute()
    {
        var character = LoadXml("<character><adept>True</adept><magician>False</magician><attributes>"
            + AttributeXml("MAG", "4") + "</attributes><powers>"
            + "<power><name>Astral Perception</name><rating>1</rating><pointsperlevel>1</pointsperlevel></power>"
            + "<power><name>Killing Hands</name><rating>1</rating><pointsperlevel>0.5</pointsperlevel></power>"
            + "</powers></character>");

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        // 4 MAG - (1 + 0.5) used = 2.5 remaining, truncated to 2.
        Assert.Equal(2, points.Value);
        Assert.Contains("Verbraucht: 1,5", points.Tooltip);
    }

    [Fact]
    public void AdeptPowerPoints_MysticAdept_UsesAdeptMagSplitNotFullMag()
    {
        var character = LoadXml("<character><adept>True</adept><magician>True</magician>"
            + "<magsplitadept>3</magsplitadept><magsplitmagician>3</magsplitmagician><attributes>"
            + AttributeXml("MAG", "6") + "</attributes><powers>"
            + "<power><name>Astral Perception</name><rating>1</rating><pointsperlevel>1</pointsperlevel></power>"
            + "</powers></character>");

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        // Only the 3-point Adept split applies, not the full MAG of 6.
        Assert.Equal(2, points.Value);
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
    public void AdeptPowerPoints_AddsAdeptPowerPointsImprovementBonus()
    {
        var character = LoadXml("<character><adept>True</adept><magician>False</magician><attributes>"
            + AttributeXml("MAG", "2") + "</attributes><powers></powers><improvements>"
            + ImprovementAugXml("AdeptPowerPoints", "2") + "</improvements></character>");

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        Assert.Equal(4, points.Value);
        Assert.Contains("Verfügbar: 4", points.Tooltip);
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

    [Fact]
    public void EssencePenalty_WholeEssenceLostFromCyberwareRoundsUp()
    {
        // 6 max ESS - 2.5 installed = 3.5 remaining -> Ceiling(6 - 3.5) = 3.
        var character = LoadXml("<character><attributes>" + AttributeXml("ESS", "6") + "</attributes><cyberwares>"
            + "<cyberware><name>Wired Reflexes</name><ess>2.5</ess><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");

        Assert.Equal(3, character.EssencePenalty);
    }

    [Fact]
    public void Attributes_MagAndRes_AreReducedByEssencePenaltyByDefault()
    {
        var character = LoadXml("<character><attributes>" + AttributeXml("ESS", "6") + AttributeXml("MAG", "6")
            + AttributeXml("RES", "6") + AttributeXml("BOD", "6") + "</attributes><cyberwares>"
            + "<cyberware><name>Wired Reflexes</name><ess>2.5</ess><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");

        var attributes = character.Attributes.ToDictionary(a => a.Code);
        Assert.Equal("3", attributes["MAG"].TotalValue); // 6 - EssencePenalty of 3.
        Assert.Equal("3", attributes["RES"].TotalValue);
        Assert.Equal("6", attributes["BOD"].TotalValue); // Unaffected - not MAG/RES.
    }

    [Fact]
    public void Attributes_MagAndRes_EssLossReducesMaximumOnly_OnlyClampsAgainstMetatypeMax()
    {
        var character = LoadXml("<character><attributes>" + AttributeXml("ESS", "6") + AttributeXml("MAG", "4")
            + "</attributes><cyberwares>"
            + "<cyberware><name>Wired Reflexes</name><ess>2.5</ess><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { EssLossReducesMaximumOnly = true });

        // 4 doesn't exceed the (essence-loss-unaffected) metatype max of 6, so it stays untouched.
        var attributes = character.Attributes.ToDictionary(a => a.Code);
        Assert.Equal("4", attributes["MAG"].TotalValue);
    }

    [Fact]
    public void AddGear_StickNShock_AllowedWhenHouseRuleOff()
    {
        var character = LoadXml("<character><nuyen>1000</nuyen></character>");
        Assert.True(character.AddGear("Ammo: Stick-n-Shock", "Ammunition"));
    }

    [Fact]
    public void AddGear_StickNShock_RejectedWithoutEligibleWeapon()
    {
        var character = LoadXml("<character><nuyen>1000</nuyen></character>");
        var objOptions = new CharacterOptions { RestrictStickNShock = true };
        objOptions.StickNShockExcludedWeaponCategories.Add("Heavy Pistols");
        character.SetCharacterOptionsForTesting(objOptions);

        // No weapons owned at all - nothing eligible.
        Assert.False(character.AddGear("Ammo: Stick-n-Shock", "Ammunition"));

        // Only an excluded-category weapon owned - still nothing eligible.
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Assert.False(character.AddGear("Ammo: Stick-n-Shock", "Ammunition"));
    }

    [Fact]
    public void AddGear_StickNShock_AllowedWithEligibleWeaponCategory()
    {
        var character = LoadXml("<character><nuyen>1000</nuyen></character>");
        var objOptions = new CharacterOptions { RestrictStickNShock = true };
        objOptions.StickNShockExcludedWeaponCategories.Add("Heavy Pistols");
        character.SetCharacterOptionsForTesting(objOptions);
        character.AddWeapon("Assault Rifle", "Assault Rifles", "8P", "-1", "SA", "0", "30", "1500", "6R", "SR4", "313");

        Assert.True(character.AddGear("Ammo: Stick-n-Shock", "Ammunition"));
    }

    [Fact]
    public void CyberlimbAveraging_OneOfSixArms_AveragesWithMeatValuePaddingToLimbCount()
    {
        // Base Cyberlimb Agility is 3; with only 1 of the default 6 limbs replaced, the other 5
        // "limbs" contribute the meat AGI of 4 each: floor((3 + 5*4) / 6) = floor(23/6) = 3.
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "4")
            + "</attributes><cyberwares><cyberware><name>Obvious Full Arm</name>"
            + "<category>Cyberlimb</category><improvementsource>Cyberware</improvementsource>"
            + "<children /></cyberware></cyberwares></character>");

        Assert.Equal("3", character.Attributes.Single(a => a.Code == "AGI").TotalValue);
    }

    [Fact]
    public void CyberlimbAveraging_CustomizedAgilityChild_OverridesLimbBase()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "4")
            + "</attributes><cyberwares><cyberware><name>Obvious Full Arm</name>"
            + "<category>Cyberlimb</category><improvementsource>Cyberware</improvementsource>"
            + "<children><cyberware><name>Customized Agility</name><rating>6</rating></cyberware></children>"
            + "</cyberware></cyberwares></character>");

        // floor((6 + 5*4) / 6) = floor(26/6) = 4.
        Assert.Equal("4", character.Attributes.Single(a => a.Code == "AGI").TotalValue);
    }

    [Fact]
    public void CyberlimbAveraging_ExcludedLimbSlot_LeavesMeatValueUnchanged()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("AGI", "4")
            + "</attributes><cyberwares><cyberware><name>Obvious Full Arm</name>"
            + "<category>Cyberlimb</category><improvementsource>Cyberware</improvementsource>"
            + "<children /></cyberware></cyberwares></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ExcludeLimbSlot = "arm" });

        Assert.Equal("4", character.Attributes.Single(a => a.Code == "AGI").TotalValue);
    }

    [Fact]
    public void CyberlimbAveraging_UnrelatedAttribute_IsUnaffected()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("LOG", "4")
            + "</attributes><cyberwares><cyberware><name>Obvious Full Arm</name>"
            + "<category>Cyberlimb</category><improvementsource>Cyberware</improvementsource>"
            + "<children /></cyberware></cyberwares></character>");

        Assert.Equal("4", character.Attributes.Single(a => a.Code == "LOG").TotalValue);
    }

    [Fact]
    public void AddGear_RestrictedAvail_MultipliesCostWhenHouseRuleOn()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MultiplyRestrictedCost = true, RestrictedCostMultiplier = 3 });

        character.AddGear("Contraband", "Misc", strQty: "1", strCost: "100", strAvail: "8R");

        Assert.Equal("9700", character.Nuyen);
    }

    [Fact]
    public void AddGear_RestrictedAvail_NotMultipliedWhenHouseRuleOff()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MultiplyRestrictedCost = false, RestrictedCostMultiplier = 3 });

        character.AddGear("Contraband", "Misc", strQty: "1", strCost: "100", strAvail: "8R");

        Assert.Equal("9900", character.Nuyen);
    }

    [Fact]
    public void AddGear_ForbiddenAvail_MultipliesCostWhenHouseRuleOn()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MultiplyForbiddenCost = true, ForbiddenCostMultiplier = 5 });

        character.AddGear("Illegal Item", "Misc", strQty: "1", strCost: "100", strAvail: "12F");

        Assert.Equal("9500", character.Nuyen);
    }

    [Fact]
    public void AddGear_UnrestrictedAvail_IsNeverMultiplied()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions
        {
            MultiplyRestrictedCost = true, RestrictedCostMultiplier = 3,
            MultiplyForbiddenCost = true, ForbiddenCostMultiplier = 5
        });

        character.AddGear("Ordinary Item", "Misc", strQty: "1", strCost: "100", strAvail: "8");

        Assert.Equal("9900", character.Nuyen);
    }

    [Fact]
    public void RaiseNuyenCreate_StopsAtTheSavedMaxByDefault()
    {
        CharacterDocument character = LoadXml(
            "<character><buildmethod>Bp</buildmethod><bp>10</bp><nuyenbp>2</nuyenbp><nuyenmaxbp>2</nuyenmaxbp></character>");
        Assert.False(character.RaiseNuyenCreate());
        Assert.Equal(2, character.NuyenPoints);
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

    [Fact]
    public void AddWeapon_DeductsItsOwnCostFromNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Assert.Equal("650", character.Nuyen);
    }

    [Fact]
    public void AddArmor_DeductsItsOwnCostFromNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        Assert.Equal("800", character.Nuyen);
    }

    [Fact]
    public void AddCyberware_DeductsItsOwnCostFromNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>2000</nuyen></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        Assert.Equal("1000", character.Nuyen);
    }

    [Fact]
    public void SellGear_RefundsPercentOfCostAndRemovesTheItem()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddGear("Medkit", "Biotech", strQty: "1", strCost: "200");
        int intGearId = character.Gear.Single().GearId;

        Assert.True(character.SellGear(intGearId, 0.5));

        Assert.Equal("900", character.Nuyen); // 1000 - 200 (bought) + 100 (50% refund)
        Assert.Empty(character.Gear);
        Assert.Contains(character.NuyenExpenses, exp => exp.Reason.Contains("Medkit"));
    }

    [Fact]
    public void SellWeapon_RefundsPercentOfCostAndRemovesTheItem()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");

        Assert.True(character.SellWeapon("Ares Predator IV", "Heavy Pistols", 0.5));

        Assert.Equal("825", character.Nuyen); // 1000 - 350 (bought) + 175 (50% refund)
        Assert.Empty(character.WeaponTrees);
    }

    [Fact]
    public void SellArmor_RefundsPercentOfCostAndRemovesTheItem()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");

        Assert.True(character.SellArmor("Leather Jacket", "Clothing", 0.25));

        Assert.Equal("850", character.Nuyen); // 1000 - 200 (bought) + 50 (25% refund)
        Assert.Empty(character.Armor);
    }

    [Fact]
    public void SellCyberware_RefundsPercentOfCostAndRemovesTheItem()
    {
        CharacterDocument character = LoadXml("<character><nuyen>2000</nuyen></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");

        Assert.True(character.SellCyberware("Cybereyes", "Cyberlimb", "0", 0.5));

        Assert.Equal("1500", character.Nuyen); // 2000 - 1000 (bought) + 500 (50% refund)
        Assert.Empty(character.Cyberware);
    }

    [Fact]
    public void SellGear_MissingItem_ReturnsFalseAndLeavesNuyenUnchanged()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        Assert.False(character.SellGear(999, 0.5));
        Assert.Equal("1000", character.Nuyen);
    }

    [Fact]
    public void AddNaturalWeapon_AssemblesDamageAndApFromTheChosenFields()
    {
        CharacterDocument character = LoadXml("<character></character>");

        Assert.True(character.AddNaturalWeapon("Claws", "Unarmed Combat", "(STR/2)", 1, "P", -1, 0));

        CharacterWeaponData weapon = character.Weapons.Single();
        Assert.Equal("Claws", weapon.Name);
        Assert.Equal("(STR/2)+1P", weapon.Damage);
        Assert.Equal("-1", weapon.Ap);
    }

    [Fact]
    public void AddNaturalWeapon_NoModifier_OmitsTheSignedSuffix()
    {
        CharacterDocument character = LoadXml("<character></character>");
        Assert.True(character.AddNaturalWeapon("Bite", "Unarmed Combat", "3", 0, "P", 0, 1));

        CharacterWeaponData weapon = character.Weapons.Single();
        Assert.Equal("3P", weapon.Damage);
        Assert.Equal("0", weapon.Ap);
    }

    [Fact]
    public void AddNaturalWeapon_DicePool_UsesTheChosenSkillInsteadOfTheCategoryMapping()
    {
        // "Natürliche Waffe" isn't in weapons.xml's Category->Skill map at all, so without the
        // UseSkill override the dice pool would resolve to nothing.
        CharacterDocument character = LoadXml("<character><skills>"
            + "<skill><name>Unarmed Combat</name><attribute>STR</attribute><rating>4</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills></character>");
        character.AddNaturalWeapon("Claws", "Unarmed Combat", "(STR/2)", 1, "P", -1, 0);

        CharacterWeaponData weapon = character.Weapons.Single();
        Assert.NotEmpty(weapon.DicePool);
        Assert.NotEqual("0", weapon.DicePool);
    }

    [Fact]
    public void GetCombatActiveSkillNames_IncludesUnarmedCombat()
    {
        CharacterDocument character = LoadXml("<character></character>");
        Assert.Contains("Unarmed Combat", character.GetCombatActiveSkillNames());
    }

    [Fact]
    public void ReloadWeapon_ConsumesAmmoAndUpdatesAmmoStatus()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15(c)", "350", "4R", "SR4", "313");
        character.AddGear("Ammo: Regular Ammo", "Ammunition", strQty: "30", strCost: "0");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        int intAmmoGearId = character.GetWeaponAmmoOptions(guiWeaponId).Single().GearId;

        Assert.True(character.ReloadWeapon(guiWeaponId, intAmmoGearId, 15));

        Assert.Equal(15, character.Gear.Single().Qty is var q && int.TryParse(q, out var i) ? i : -1);
        Assert.Equal("15 (Ammo: Regular Ammo)", character.WeaponTrees.Single().AmmoStatus);
    }

    [Fact]
    public void ReloadWeapon_ReturnsUnspentRoundsFromThePreviousLoad()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15(c)", "350", "4R", "SR4", "313");
        character.AddGear("Ammo: Regular Ammo", "Ammunition", strQty: "30", strCost: "0");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        int intAmmoGearId = character.GetWeaponAmmoOptions(guiWeaponId).Single().GearId;

        Assert.True(character.ReloadWeapon(guiWeaponId, intAmmoGearId, 15));
        // 15 rounds now sit in the weapon, 15 remain in the Gear stack. Reloading again with 10
        // more should first return the unspent 15 to the Gear stack (-> 30), then take 10 (-> 20).
        Assert.True(character.ReloadWeapon(guiWeaponId, intAmmoGearId, 10));

        Assert.Equal(20, character.Gear.Single().Qty is var q && int.TryParse(q, out var i) ? i : -1);
        Assert.Equal("10 (Ammo: Regular Ammo)", character.WeaponTrees.Single().AmmoStatus);
    }

    [Fact]
    public void ReloadWeapon_NotEnoughAmmo_ClampsToWhateverIsLeft()
    {
        // Matches legacy's own forgiving behavior ("use whatever is left") rather than failing.
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15(c)", "350", "4R", "SR4", "313");
        character.AddGear("Ammo: Regular Ammo", "Ammunition", strQty: "5", strCost: "0");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        int intAmmoGearId = character.GetWeaponAmmoOptions(guiWeaponId).Single().GearId;

        Assert.True(character.ReloadWeapon(guiWeaponId, intAmmoGearId, 15));
        Assert.Equal("0", character.Gear.Single().Qty);
        Assert.Equal("5 (Ammo: Regular Ammo)", character.WeaponTrees.Single().AmmoStatus);
    }

    [Fact]
    public void GetWeaponAmmoOptions_ExcludesIncompatibleAmmoTypes()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15(c)", "350", "4R", "SR4", "313");
        character.AddGear("Ammo: Regular Ammo", "Ammunition", strQty: "30", strCost: "0");
        character.AddGear("Arrow", "Ammunition", strQty: "10", strCost: "0");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        var lstOptions = character.GetWeaponAmmoOptions(guiWeaponId);

        Assert.Contains(lstOptions, o => o.Name == "Ammo: Regular Ammo");
        Assert.DoesNotContain(lstOptions, o => o.Name == "Arrow");
    }

    [Fact]
    public void GetWeaponAmmoOptions_RestrictStickNShock_ExcludesStickNShockForExcludedCategory()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15(c)", "350", "4R", "SR4", "313");
        character.AddGear("Ammo: Stick-n-Shock", "Ammunition", strQty: "10", strCost: "0");
        var objOptions = new CharacterOptions { RestrictStickNShock = true };
        objOptions.StickNShockExcludedWeaponCategories.Add("Heavy Pistols");
        character.SetCharacterOptionsForTesting(objOptions);
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        Assert.DoesNotContain(character.GetWeaponAmmoOptions(guiWeaponId), o => o.Name == "Ammo: Stick-n-Shock");
    }

    [Fact]
    public void GetWeaponAmmoCapacityChoices_ParsesRoundCountsFromTheAmmoString()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0",
            "15(c) or external source", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);

        Assert.Equal(new[] { 15 }, character.GetWeaponAmmoCapacityChoices(guiWeaponId));
    }

    [Fact]
    public void ReloadWeapon_LoadedAmmoDicePoolBonus_IsIncludedInTheWeaponPool()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>4</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid guiWeaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        string strBaseline = character.Weapons.Single().DicePool;

        character.AddGear("Ammo: Deathdealer", "Ammunition", strQty: "10", strCost: "0");
        int intAmmoGearId = character.GetWeaponAmmoOptions(guiWeaponId).Single(o => o.Name == "Ammo: Deathdealer").GearId;
        character.ReloadWeapon(guiWeaponId, intAmmoGearId, 10);

        string strWithAmmoBonus = character.Weapons.Single().DicePool;
        Assert.Equal(int.Parse(strBaseline) + 1, int.Parse(strWithAmmoBonus));
    }

    [Fact]
    public void ReloadWeapon_WorksForAVehicleMountedWeaponToo()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312");
        character.AddGear("Ammo: Regular Ammo", "Ammunition", strQty: "50", strCost: "0");

        CharacterTreeItemData weapon = character.Vehicles.Single().Children.Single(c => c.Name == "Ares Alpha");
        Guid guiWeaponId = Guid.Parse(weapon.ItemGuid);
        int intAmmoGearId = character.GetWeaponAmmoOptions(guiWeaponId).Single().GearId;

        Assert.True(character.ReloadWeapon(guiWeaponId, intAmmoGearId, 42));

        CharacterTreeItemData reloaded = character.Vehicles.Single().Children.Single(c => c.Name == "Ares Alpha");
        Assert.Equal("42 (Ammo: Regular Ammo)", reloaded.AmmoStatus);
        Assert.Equal("8", character.Gear.Single().Qty);
    }

    [Fact]
    public void AddWeapon_ForbiddenAvail_MultipliesCostWhenHouseRuleOn()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MultiplyForbiddenCost = true, ForbiddenCostMultiplier = 4 });
        character.AddWeapon("Illegal Gun", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "500", "12F", "SR4", "313");
        Assert.Equal("8000", character.Nuyen); // 10000 - 500*4
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

    [Fact]
    public void ConfirmKarmaExpenseEnabled_UsesCharacterSettingsProfile()
    {
        CharacterDocument character = LoadXml("<character />");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ConfirmKarmaExpense = false });
        Assert.False(character.ConfirmKarmaExpenseEnabled);

        character.SetCharacterOptionsForTesting(new CharacterOptions { ConfirmKarmaExpense = true });
        Assert.True(character.ConfirmKarmaExpenseEnabled);
    }

    [Fact]
    public void AddGear_AutomaticallyAddsUnwiredProgramOptions_WhenEnabled()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        var objOptions = new CharacterOptions { AutomaticCopyProtection = true, AutomaticRegistration = true };
        objOptions.Books.Clear();
        objOptions.Books.Add("UN");
        character.SetCharacterOptionsForTesting(objOptions);

        Assert.True(character.AddGear("Analyze", "Matrix Programs", strRating: "3", strCost: "100"));

        CharacterTreeItemData program = Assert.Single(character.Gear);
        Assert.Equal("900", character.Nuyen);
        Assert.Collection(program.Children,
            copy =>
            {
                Assert.Equal("Copy Protection", copy.Name);
                Assert.Equal("3", copy.Rating);
                Assert.Equal(0, copy.CalculatedCost);
                Assert.Equal("[0]", copy.Capacity);
            },
            registration =>
            {
                Assert.Equal("Registration", registration.Name);
                Assert.Equal("0", registration.Rating);
                Assert.Equal(0, registration.CalculatedCost);
                Assert.Equal("[0]", registration.Capacity);
            });

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(new[] { "Copy Protection", "Registration" }, Assert.Single(reloaded.Gear).Children.Select(c => c.Name));
    }

    [Fact]
    public void AddChildGear_AutomaticallyAddsOnlyEnabledUnwiredProgramOptions()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        var objOptions = new CharacterOptions { AutomaticCopyProtection = false, AutomaticRegistration = true };
        objOptions.Books.Clear();
        objOptions.Books.Add("UN");
        character.SetCharacterOptionsForTesting(objOptions);
        Assert.True(character.AddGear("Commlink", "Commlink"));

        int intCommlinkId = Assert.Single(character.Gear).GearId;
        Assert.True(character.AddChildGear(intCommlinkId, "Browse", "Matrix Programs", strRating: "0"));

        CharacterTreeItemData program = Assert.Single(Assert.Single(character.Gear).Children);
        CharacterTreeItemData registration = Assert.Single(program.Children);
        Assert.Equal("Registration", registration.Name);
        Assert.Equal("0", registration.Rating);
    }

    [Fact]
    public void AddGear_DoesNotAddAutomaticProgramOptions_WhenUnwiredIsDisabledOrProgramIsSuite()
    {
        CharacterDocument noUnwired = LoadXml("<character />");
        var noUnwiredOptions = new CharacterOptions { AutomaticCopyProtection = true, AutomaticRegistration = true };
        noUnwiredOptions.Books.Clear();
        noUnwired.SetCharacterOptionsForTesting(noUnwiredOptions);
        Assert.True(noUnwired.AddGear("Analyze", "Matrix Programs"));
        Assert.Empty(Assert.Single(noUnwired.Gear).Children);

        CharacterDocument suite = LoadXml("<character />");
        var suiteOptions = new CharacterOptions { AutomaticCopyProtection = true, AutomaticRegistration = true };
        suiteOptions.Books.Clear();
        suiteOptions.Books.Add("UN");
        suite.SetCharacterOptionsForTesting(suiteOptions);
        Assert.True(suite.AddGear("Suite: Common Use", "Matrix Programs"));
        Assert.Empty(Assert.Single(suite.Gear).Children);
    }

    private static Improvement BuildImprovement(string strUnique, int intValue, string strImprovedName = "")
    {
        var doc = new XmlDocument();
        XmlElement objNode = doc.CreateElement("improvement");
        void Add(string strTag, string strText)
        {
            XmlElement objChild = doc.CreateElement(strTag);
            objChild.InnerText = strText;
            objNode.AppendChild(objChild);
        }
        Add("improvementttype", "Smartlink");
        Add("improvedname", strImprovedName);
        Add("sourcename", "Test");
        Add("min", "0");
        Add("max", "0");
        Add("aug", "0");
        Add("augmax", "0");
        Add("val", intValue.ToString());
        Add("rating", "1");
        Add("unique", strUnique);
        Add("improvementsource", "Quality");
        Add("addtorating", "False");
        Add("enabled", "True");
        Add("custom", "False");
        return Improvement.Load(objNode);
    }

    [Fact]
    public void ImprovementManagerValueOf_Precedence1_SumsOnlyPrecedence1EntriesIgnoringEverythingElse()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("", 5),
            BuildImprovement("somegroup", 3),
            BuildImprovement("precedence1", 2),
            BuildImprovement("precedence1", 4),
        };

        Assert.Equal(6, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }

    [Fact]
    public void ImprovementManagerValueOf_Precedence0_KeepsOnlyTheHighestPrecedence0Entry()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("", 5),
            BuildImprovement("precedence0", 2),
            BuildImprovement("precedence0", 7),
        };

        Assert.Equal(7, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }

    [Fact]
    public void ImprovementManagerValueOf_Precedence1BeatsPrecedence0_WhenBothArePresent()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("precedence0", 100),
            BuildImprovement("precedence1", 1),
            BuildImprovement("precedence1", 2),
        };

        Assert.Equal(3, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }

    [Fact]
    public void ImprovementManagerValueOf_NoPrecedenceEntries_UsesTheNormalUniqueNameMaxDedupSum()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("", 5),
            BuildImprovement("somegroup", 3),
            BuildImprovement("somegroup", 7),
        };

        // 5 (no UniqueName, always summed) + 7 (max of the "somegroup" pair).
        Assert.Equal(12, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }
}
