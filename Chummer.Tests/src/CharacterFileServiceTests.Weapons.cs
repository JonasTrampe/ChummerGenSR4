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
    public void SpellNotes_PersistForTheSelectedDuplicateAndSurviveReload()
    {
        CharacterDocument character = LoadXml("<character><spells>"
            + "<spell><name>Heal</name><category>Health</category></spell>"
            + "<spell><name>Heal</name><category>Health</category></spell>"
            + "</spells></character>");

        Assert.True(character.SetSpellNotes(character.Spells[1].SpellId, "Second entry only"));
        Assert.Equal(string.Empty, character.Spells[0].Notes);
        Assert.Equal("Second entry only", character.Spells[1].Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(string.Empty, reloaded.Spells[0].Notes);
        Assert.Equal("Second entry only", reloaded.Spells[1].Notes);
    }

    [Fact]
    public void QualityNotes_PersistForTheSelectedDuplicateAndSurviveReload()
    {
        CharacterDocument character = LoadXml("<character><qualities>"
            + "<quality><name>Allergy</name><qualitytype>Negative</qualitytype><extra>Silver</extra></quality>"
            + "<quality><name>Allergy</name><qualitytype>Negative</qualitytype><extra>Silver</extra></quality>"
            + "</qualities></character>");

        Assert.True(character.SetQualityNotes(character.Qualities[1].QualityId, "Second entry only"));
        Assert.Equal(string.Empty, character.Qualities[0].Notes);
        Assert.Equal("Second entry only", character.Qualities[1].Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal(string.Empty, reloaded.Qualities[0].Notes);
        Assert.Equal("Second entry only", reloaded.Qualities[1].Notes);
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
    public void AddWeapon_RulesDataUnderbarrelIsNestedIncludedAndRoundTrips()
    {
        CharacterDocument character = LoadXml("<character><nuyen>2000</nuyen></character>");
        character.AddWeapon("AK-98", "Assault Rifles", "6P", "-1", "SA/BF/FA", "0", "38(c)",
            "1000", "8F", "AR", "26");

        CharacterTreeItemData parent = Assert.Single(character.WeaponTrees);
        CharacterTreeItemData underbarrel = Assert.Single(parent.Children);
        Assert.Equal("AK-98 Grenade Launcher", underbarrel.Name);
        Assert.True(underbarrel.IsUnderbarrelWeapon);
        Assert.Equal("True", character.Document.SelectSingleNode("/character/weapons/weapon/underbarrel/weapon/included")!.InnerText);
        Assert.Equal("1000", character.Nuyen); // The included launcher is not purchased separately.

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "underbarrel.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "underbarrel.chum");
        Assert.True(Assert.Single(Assert.Single(reloaded.WeaponTrees).Children).IsUnderbarrelWeapon);

        Assert.True(Guid.TryParse(parent.ItemGuid, out Guid parentId));
        Assert.True(Guid.TryParse(underbarrel.ItemGuid, out Guid underbarrelId));
        Assert.True(character.RemoveUnderbarrelWeapon(parentId, underbarrelId));
        Assert.Empty(character.WeaponTrees.Single().Children);
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
    public void AddWeaponAccessoryGear_NestsUnderAccessoryAndCanBeRemoved()
    {
        CharacterDocument character = LoadXml("<character><nuyen>2000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Guid weaponId = Guid.Parse(character.WeaponTrees.Single().ItemGuid);
        Assert.True(character.AddWeaponAccessory(weaponId, "Laser Sight", "Top", "", "4", "200", "SR4", "321"));
        Guid accessoryId = Guid.Parse(character.WeaponTrees.Single().Children.Single().ItemGuid);

        Assert.True(character.AddWeaponAccessoryGear(weaponId, accessoryId, "Battery", "Electronics", "0", "1", "50", "0", "SR4", "1"));
        CharacterTreeItemData gear = Assert.Single(Assert.Single(character.WeaponTrees).Children.Single().Children);
        Assert.Equal("Battery", gear.Name);
        Assert.True(Guid.TryParse(gear.ItemGuid, out Guid gearId));
        Assert.True(character.RemoveWeaponAccessoryGear(weaponId, accessoryId, gearId));
        Assert.Empty(Assert.Single(character.WeaponTrees).Children.Single().Children);
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
    public void WeaponChildNotes_PersistForEveryInstalledWeaponItem()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><guid>00000000-0000-0000-0000-000000000001</guid><name>Pistol</name>"
            + "<accessories><accessory><guid>00000000-0000-0000-0000-000000000002</guid><name>Laser Sight</name></accessory></accessories>"
            + "<weaponmods><weaponmod><guid>00000000-0000-0000-0000-000000000003</guid><name>Smartgun System</name></weaponmod></weaponmods>"
            + "<gears><gear><guid>00000000-0000-0000-0000-000000000004</guid><name>Concealed Holster</name></gear></gears>"
            + "<ammos><ammo><guid>00000000-0000-0000-0000-000000000005</guid><name>APDS</name></ammo></ammos>"
            + "</weapon></weapons></character>");
        Assert.True(character.SetWeaponChildNotes(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"), "Accessory note"));
        Assert.True(character.SetWeaponChildNotes(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"), "Mod note"));
        Assert.True(character.SetWeaponChildNotes(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000004"), "Gear note"));
        Assert.True(character.SetWeaponChildNotes(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000005"), "Ammo note"));

        Assert.Equal("Accessory note", character.WeaponTrees[0].Children[0].Notes);
        Assert.Equal("Mod note", character.WeaponTrees[0].Children[1].Notes);
        Assert.Equal("Gear note", character.WeaponTrees[0].Children[2].Notes);
        Assert.Equal("Ammo note", character.WeaponTrees[0].Children[3].Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Accessory note", reloaded.WeaponTrees[0].Children[0].Notes);
        Assert.Equal("Mod note", reloaded.WeaponTrees[0].Children[1].Notes);
        Assert.Equal("Gear note", reloaded.WeaponTrees[0].Children[2].Notes);
        Assert.Equal("Ammo note", reloaded.WeaponTrees[0].Children[3].Notes);
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
    public void StackedFoci_ProjectLegacyComponentSnapshotsAndBrokenCompositeGearLinks()
    {
        CharacterDocument character = LoadXml("<character><gears><gear><guid>00000000-0000-0000-0000-000000000001</guid>"
            + "<name>Stacked Focus: Power Focus, Weapon Focus</name><category>Stacked Focus</category></gear></gears>"
            + "<stackedfoci><stackedfocus><guid>00000000-0000-0000-0000-000000000010</guid>"
            + "<gearid>00000000-0000-0000-0000-000000000001</gearid><bonded>True</bonded><gears>"
            + "<gear><guid>00000000-0000-0000-0000-000000000020</guid><name>Power Focus</name><category>Foci</category><rating>2</rating></gear>"
            + "<gear><guid>00000000-0000-0000-0000-000000000021</guid><name>Weapon Focus</name><category>Foci</category><rating>3</rating></gear>"
            + "</gears></stackedfocus><stackedfocus><guid>00000000-0000-0000-0000-000000000011</guid>"
            + "<gearid>00000000-0000-0000-0000-000000000099</gearid><bonded>False</bonded><gears /></stackedfocus></stackedfoci></character>");

        Assert.Equal(2, character.StackedFoci.Count);
        CharacterStackedFocusData stacked = character.StackedFoci[0];
        Assert.True(stacked.CompositeGearExists);
        Assert.True(stacked.Bonded);
        Assert.Equal(5, stacked.TotalForce);
        Assert.Equal("Power Focus, Weapon Focus", stacked.DisplayName);
        Assert.Equal("Weapon Focus", stacked.Components[1].Name);
        Assert.False(character.StackedFoci[1].CompositeGearExists);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "stacked-focus.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "stacked-focus.chum");
        Assert.Equal(5, reloaded.StackedFoci[0].TotalForce);
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
    public void WeaponEquippedState_CanBeChanged()
    {
        CharacterDocument character = LoadXml("<character><weapons><weapon><name>Ares Predator</name><category>Pistols</category><equipped>True</equipped></weapon></weapons></character>");
        Assert.True(character.SetWeaponEquipped("Ares Predator", "Pistols", false));
        Assert.False(character.WeaponTrees.Single().Equipped);
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
    public void AddWeapon_DeductsItsOwnCostFromNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        character.AddWeapon("Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "350", "4R", "SR4", "313");
        Assert.Equal("650", character.Nuyen);
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
    public void DicePool_SpecialWeaponsCategoryUsesRangeAsTheSkillLookupCategory()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen><skills>"
            + "<skill><name>Automatics</name><attribute>AGI</attribute><rating>5</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills>"
            + "<weapons><weapon><guid>" + Guid.NewGuid() + "</guid><name>Test Special Weapon</name>"
            + "<category>Special Weapons</category><range>Assault Rifles</range><reach>0</reach>"
            + "<damage>6P</damage><ap>-1</ap><mode>SA</mode><rc>0</rc><ammo>20(c)</ammo>"
            + "<cost>0</cost><avail>0</avail><useskill></useskill><source>SR4</source><page>0</page>"
            + "<location></location><equipped>True</equipped><ammoloaded>-1</ammoloaded>"
            + "<ammoremaining>0</ammoremaining><accessories /><weaponmods /><gears /><ammos />"
            + "</weapon></weapons></character>");

        Assert.Equal("5", character.Weapons.Single().DicePool);
    }

    [Fact]
    public void DicePool_SpecialWeaponsCategoryWithoutRangeFallsBackToTheDefault()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen><skills>"
            + "<skill><name>Pistols</name><attribute>AGI</attribute><rating>3</rating>"
            + "<knowledge>False</knowledge><allowdelete>True</allowdelete></skill></skills>"
            + "<weapons><weapon><guid>" + Guid.NewGuid() + "</guid><name>Test Special Weapon</name>"
            + "<category>Special Weapons</category><range></range><reach>0</reach>"
            + "<damage>6P</damage><ap>-1</ap><mode>SA</mode><rc>0</rc><ammo>20(c)</ammo>"
            + "<cost>0</cost><avail>0</avail><useskill></useskill><source>SR4</source><page>0</page>"
            + "<location></location><equipped>True</equipped><ammoloaded>-1</ammoloaded>"
            + "<ammoremaining>0</ammoremaining><accessories /><weaponmods /><gears /><ammos />"
            + "</weapon></weapons></character>");

        Assert.Equal("3", character.Weapons.Single().DicePool);
    }

    [Fact]
    public void AddWeapon_ForbiddenAvail_MultipliesCostWhenHouseRuleOn()
    {
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { MultiplyForbiddenCost = true, ForbiddenCostMultiplier = 4 });
        character.AddWeapon("Illegal Gun", "Heavy Pistols", "6P", "-1", "SA", "0", "15", "500", "12F", "SR4", "313");
        Assert.Equal("8000", character.Nuyen); // 10000 - 500*4
    }

}
