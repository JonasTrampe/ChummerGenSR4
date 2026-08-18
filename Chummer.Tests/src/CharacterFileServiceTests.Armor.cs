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
    public void ArmorChildNotes_PersistForTheSelectedArmorModification()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        Assert.True(character.AddArmorMod("Leather Jacket", "Clothing", "Chemical Protection", "3",
            "0", "0", "9", "Rating * 250", "SR4", "327"));

        CharacterTreeItemData armor = Assert.Single(character.Armor);
        CharacterTreeItemData mod = Assert.Single(armor.Children);
        Assert.True(Guid.TryParse(mod.ItemGuid, out Guid modGuid));
        Assert.True(character.SetArmorChildNotes(armor.ArmorId, modGuid, "Replace filters after next run."));
        Assert.Equal("Replace filters after next run.", Assert.Single(character.Armor).Children.Single().Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Replace filters after next run.", Assert.Single(reloaded.Armor).Children.Single().Notes);
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
    public void AddArmorGear_NestsDeductsAndRoundTrips()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        int armorId = Assert.Single(character.Armor).ArmorId;

        Assert.True(character.AddArmorGear(armorId, "Gecko Tape Gloves", "Tools", "0", "1", "250", "4", "SR4", "322"));
        CharacterTreeItemData gear = Assert.Single(Assert.Single(character.Armor).Children);
        Assert.Equal("Gecko Tape Gloves", gear.Name);
        Assert.Equal("550", character.Nuyen);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "armor-gear.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "armor-gear.chum");
        Assert.Equal("Gecko Tape Gloves", Assert.Single(Assert.Single(reloaded.Armor).Children).Name);

        Assert.True(Guid.TryParse(gear.ItemGuid, out Guid gearId));
        Assert.True(character.AddArmorGearPlugin(armorId, gearId, "Registration", "Program Options", "0", "1",
            "0", "0", "UN", "115", "[0]"));
        Assert.Equal("Registration", Assert.Single(Assert.Single(character.Armor).Children).Children.Single().Name);
        Assert.True(character.RemoveArmorGear(armorId, gearId));
        Assert.Empty(Assert.Single(character.Armor).Children);
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
    public void ArmorEncumbrance_EquippedArmorModRatingBonusesCountTowardsArmorRating()
    {
        CharacterDocument character = LoadXml(
            "<character><attributes><attribute><name>BOD</name><value>4</value><totalvalue>4</totalvalue></attribute><attribute><name>STR</name><value>0</value><totalvalue>0</totalvalue></attribute></attributes></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "4", "200", "0", "SR4", "326");

        Assert.Equal(2, character.ArmorEncumbrance.BallisticRating.Value);
        Assert.Equal(2, character.ArmorEncumbrance.ImpactRating.Value);

        Assert.True(character.AddArmorMod("Leather Jacket", "Clothing", "Fire Resistance", "1", "1", "1",
            "0", "50", "SR4", "326"));

        Assert.Equal(3, character.ArmorEncumbrance.BallisticRating.Value);
        Assert.Equal(3, character.ArmorEncumbrance.ImpactRating.Value);
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
    public void ComplexFormAndCritterPowerNotes_PersistByGuid()
    {
        CharacterDocument character = LoadXml("<character><techprograms>"
            + "<techprogram><guid>form-id</guid><name>Armor</name></techprogram></techprograms>"
            + "<critterpowers><critterpower><guid>power-id</guid><name>Armor</name></critterpower>"
            + "</critterpowers></character>");

        Assert.True(character.SetComplexFormNotes("form-id", "Form note"));
        Assert.True(character.SetCritterPowerNotes("power-id", "Power note"));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Form note", Assert.Single(reloaded.ComplexForms).Notes);
        Assert.Equal("Power note", Assert.Single(reloaded.CritterPowers).Notes);
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
    public void AddArmor_DeductsItsOwnCostFromNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddArmor("Leather Jacket", "Clothing", "2", "2", "0", "200", "0", "SR4", "326");
        Assert.Equal("800", character.Nuyen);
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

}
