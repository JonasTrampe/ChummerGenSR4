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
    public void ClipboardConvenienceMethods_CopyAndPasteEachCollectionAcrossCharacters()
    {
        // Ported from frmCreate.cs's per-collection Copy/Paste menu commands: each collection's
        // own By-Id lookup (GetGearNodeById etc.) is used to resolve the node, so hosts never need
        // to build XPaths or a <guid> themselves - CharacterClipboard.Instance is the single,
        // process-wide clipboard shared by every open character, matching legacy's
        // GlobalOptions.Instance.Clipboard.
        CharacterClipboard.Instance.Clear();

        CharacterDocument source = LoadXml("<character><nuyen>100000</nuyen></character>");
        CharacterDocument target = LoadXml("<character><nuyen>0</nuyen></character>");

        source.AddGear("Toolkit", "Tools", strQty: "1", strCost: "0");
        Assert.True(source.CopyGear(source.Gear[0].GearId));
        Assert.True(target.HasClipboardItemOfType(ClipboardContentType.Gear));
        Assert.True(target.PasteGear());
        Assert.Equal("Toolkit", target.Gear[0].Name);

        source.AddWeapon("Katana", "Blades", "5P", "-2", "-", "0", "-", "0", "4", "SR4", "313");
        Assert.True(source.CopyWeapon(0));
        Assert.True(target.PasteWeapon());
        Assert.Contains(target.Weapons, w => w.Name == "Katana");

        source.AddArmor("Armor Jacket", "Armor", "6", "0", "0", "600", "2", "SR4", "319");
        Assert.True(source.CopyArmor(0));
        Assert.True(target.PasteArmor());
        Assert.Contains(target.Armor, a => a.Name == "Armor Jacket");

        source.AddCyberware("Cyberear", "Headware", "1", "0.1", "500", "4", "SR4", "319");
        Assert.True(source.CopyCyberware(0, blnBioware: false));
        Assert.True(target.PasteCyberware(blnBioware: false));
        Assert.Contains(target.Cyberware, c => c.Name == "Cyberear");

        source.AddLifestyle("Squatter", "0", "1");
        Assert.True(source.CopyLifestyle(0));
        Assert.True(target.PasteLifestyle());
        Assert.Contains(target.Lifestyles, l => l.Name == "Squatter");

        source.AddVehicle("Hyundai Shin-Hyung", "Cars", "3", "15", "180", "2", "10", "6", "2", "3",
            "4", "16000", "SR4", "351");
        Guid guiVehicleId = Guid.Parse(source.Vehicles.Single().Guid);
        Assert.True(source.CopyVehicle(guiVehicleId));
        Assert.True(target.PasteVehicle());
        Assert.Contains(target.Vehicles, v => v.Name == "Hyundai Shin-Hyung");
    }

    [Fact]
    public void ClipboardConvenienceMethods_PasteRejectsAMismatchedCollectionType()
    {
        CharacterClipboard.Instance.Clear();

        CharacterDocument source = LoadXml("<character><nuyen>100000</nuyen></character>");
        CharacterDocument target = LoadXml("<character><nuyen>0</nuyen></character>");
        source.AddGear("Toolkit", "Tools", strQty: "1", strCost: "0");

        Assert.True(source.CopyGear(source.Gear[0].GearId));
        Assert.False(target.PasteArmor());
        Assert.Empty(target.Armor);
    }

    [Fact]
    public void CharacterClipboard_Copy_RejectsAnXPathThatDoesNotResolveToTheExpectedType_sRootElement()
    {
        // Ported from frmCreate.cs's mnuEditPaste_Click's second, independent check beyond the
        // ClipboardContentType tag - here caught even earlier, at Copy time: a caller passing the
        // wrong XPath/type pairing (e.g. tagging a <weapon> node as Gear) must not succeed.
        CharacterDocument source = LoadXml("<character><weapons><weapon><name>Katana</name>"
            + "<category>Blades</category></weapon></weapons></character>");
        var clipboard = new CharacterClipboard();

        Assert.False(clipboard.Copy(source, "/character/weapons/weapon", ClipboardContentType.Gear));
        Assert.Null(clipboard.Item);
    }

    [Fact]
    public void CharacterClipboard_CommlinkAndOperatingSystem_ShareGearsRootElementWithPlainGear()
    {
        CharacterDocument source = LoadXml("<character><gears><gear><name>Fairlight Caliban</name>"
            + "<category>Commlink</category></gear></gears></character>");
        CharacterDocument target = LoadXml("<character><gears /></character>");
        var clipboard = new CharacterClipboard();

        Assert.True(clipboard.Copy(source, "/character/gears/gear", ClipboardContentType.Commlink));
        Assert.True(clipboard.Paste(target, "/character/gears", ClipboardContentType.Commlink));
        Assert.Equal("Fairlight Caliban", target.Document.SelectSingleNode("/character/gears/gear/name")!.InnerText);
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

    private static string SmartlinkImprovementXml() =>
        "<improvement><improvementttype>Smartlink</improvementttype><improvementsource>Gear</improvementsource>"
        + "<val>2</val><enabled>True</enabled></improvement>";

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

    [Fact]
    public void Foci_ProjectLegacyRecordsAndRetainBrokenGearLinks()
    {
        CharacterDocument character = LoadXml("<character><gears><gear><guid>00000000-0000-0000-0000-000000000001</guid><name>Power Focus</name><category>Foci</category></gear></gears><foci>"
            + "<focus><guid>00000000-0000-0000-0000-000000000010</guid><name>Power Focus (Force 3)</name><gearid>00000000-0000-0000-0000-000000000001</gearid><rating>3</rating></focus>"
            + "<focus><guid>00000000-0000-0000-0000-000000000011</guid><name>Lost Focus</name><gearid>00000000-0000-0000-0000-000000000099</gearid><rating>2</rating></focus>"
            + "</foci></character>");

        Assert.Equal(2, character.Foci.Count);
        Assert.True(character.Foci[0].LinkedGearExists);
        Assert.Equal("Foci", character.Foci[0].GearCategory);
        Assert.False(character.Foci[1].LinkedGearExists);
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
    public void SellGear_MissingItem_ReturnsFalseAndLeavesNuyenUnchanged()
    {
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen></character>");
        Assert.False(character.SellGear(999, 0.5));
        Assert.Equal("1000", character.Nuyen);
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

}
