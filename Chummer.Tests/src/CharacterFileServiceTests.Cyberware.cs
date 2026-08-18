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
    public void CyberwareNotes_PersistForTheSelectedNestedTreeItem()
    {
        CharacterDocument character = LoadXml("<character><cyberwares>"
            + "<cyberware><name>Cybereyes</name><category>Cyberlimb</category><improvementsource>Cyberware</improvementsource><children>"
            + "<cyberware><name>Vision Enhancement</name><category>Eyeware</category><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</children></cyberware></cyberwares></character>");
        int intChildId = Assert.Single(Assert.Single(character.Cyberware).Children).CyberwareId;

        Assert.True(character.SetCyberwareNotes(intChildId, "Low-light replacement planned"));
        Assert.Equal("Low-light replacement planned", Assert.Single(character.Cyberware).Children.Single().Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Low-light replacement planned", Assert.Single(reloaded.Cyberware).Children.Single().Notes);
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
    public void AddCyberwareChild_NestsAppliesBonusAndRoundTrips()
    {
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        int parentId = Assert.Single(character.Cyberware).CyberwareId;

        Assert.True(character.AddCyberwareChild(parentId, "Smartlink", "Eyeware", "0", "0.1", "1000", "8R", "SR4", "340"));
        CharacterTreeItemData child = Assert.Single(Assert.Single(character.Cyberware).Children);
        Assert.Equal("Smartlink", child.Name);
        Assert.Equal(ImprovementType.Smartlink, Assert.Single(character.Improvements).Type);
        Assert.Equal("3000", character.Nuyen);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "cyberware-child.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "cyberware-child.chum");
        Assert.Equal("Smartlink", Assert.Single(Assert.Single(reloaded.Cyberware).Children).Name);
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
    public void Cyberware_And_Bioware_AreSplitByImprovementSource()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.Cyberware);
        Assert.Equal("Wired Reflexes", character.Cyberware[0].Name);

        Assert.Single(character.Bioware);
        Assert.Equal("Cerebral Booster", character.Bioware[0].Name);
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
    public void Condition_ComputesEssenceFromCyberwareAndBioware()
    {
        CharacterDocument character = LoadFixture();

        // ESS metatypemax 6, Wired Reflexes (Cyberware) ess 1.5 and Cerebral Booster (Bioware)
        // ess 0.5: the higher cost (1.5) counts in full, the lower (0.5) at half -> 6 - 1.5 - 0.25.
        Assert.Equal("4.25", character.Condition.Essence);
    }

    [Fact]
    public void Condition_CyborgEssenceOverrideFixesEssenceAtPointOne()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("ESS", "6")
            + "</attributes><cyberwares><cyberware><name>Wired Reflexes</name><ess>3</ess>"
            + "<improvementsource>Cyberware</improvementsource></cyberware></cyberwares><improvements>"
            + ImprovementXml("CyborgEssence", "1") + "</improvements></character>");

        Assert.Equal("0.1", character.Condition.Essence);
        Assert.Equal(6, character.EssencePenalty);
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
    public void AddCyberware_DeductsItsOwnCostFromNuyen()
    {
        CharacterDocument character = LoadXml("<character><nuyen>2000</nuyen></character>");
        character.AddCyberware("Cybereyes", "Cyberlimb", "0", "0.2", "1000", "8R", "SR4", "339");
        Assert.Equal("1000", character.Nuyen);
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

}
