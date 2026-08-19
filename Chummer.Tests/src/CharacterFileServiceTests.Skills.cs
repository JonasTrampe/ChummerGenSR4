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
    public void KnowledgeSkillCreationBudget_ChargesAddEditAndRefundsRemoval()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>10</startingbuildpoints><bp>4</bp></character>");

        Assert.True(creation.AddKnowledgeSkill("Shadowing", "Street"));
        int intSkillId = Assert.Single(creation.KnowledgeSkills).SkillId;
        Assert.Equal("2", creation.Bp);
        Assert.True(creation.UpdateKnowledgeSkill(intSkillId, "Shadowing", "2", string.Empty, "Street"));
        Assert.Equal("0", creation.Bp);
        Assert.False(creation.UpdateKnowledgeSkill(intSkillId, "Shadowing", "3", string.Empty, "Street"));
        Assert.Equal("2", Assert.Single(creation.KnowledgeSkills).BaseRating);
        Assert.True(creation.RemoveKnowledgeSkill(intSkillId));
        Assert.Equal("4", creation.Bp);
    }

    [Fact]
    public void KnowledgeSkillCreationBudget_BpBuildGetsIntPlusLogTimesThreeFreePoints()
    {
        // INT 0 + LOG 1 = 1 attribute point -> 3 free Knowledge Skill points at 3/point.
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod>"
            + "<startingbuildpoints>20</startingbuildpoints><bp>20</bp><attributes>"
            + AttributeXml("LOG", "1") + "</attributes></character>");

        Assert.True(creation.AddKnowledgeSkill("Shadowing", "Street"));
        Assert.True(creation.UpdateKnowledgeSkill(Assert.Single(creation.KnowledgeSkills).SkillId,
            "Shadowing", "3", string.Empty, "Street"));
        Assert.Equal("20", creation.Bp); // fully covered by the 3 free points

        Assert.True(creation.UpdateKnowledgeSkill(Assert.Single(creation.KnowledgeSkills).SkillId,
            "Shadowing", "4", string.Empty, "Street"));
        Assert.Equal("18", creation.Bp); // 1 point over free allowance, at BpKnowledgeSkill (2) each
    }

    [Fact]
    public void KnowledgeSkillCreationBudget_KarmaBuildGetsNoFreePointsByDefault()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>Karma</buildmethod>"
            + "<startingbuildpoints>50</startingbuildpoints><karma>50</karma><attributes>"
            + AttributeXml("INT", "5") + AttributeXml("LOG", "5") + "</attributes></character>");

        Assert.True(creation.AddKnowledgeSkill("Shadowing", "Street")); // rating 1, costs KarmaNewKnowledgeSkill (2)
        Assert.Equal("48", creation.Karma);
    }

    [Fact]
    public void KnowledgeSkillCreationBudget_FreeKarmaKnowledgeHouseRuleGrantsFreePointsInKarmaBuild()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>Karma</buildmethod>"
            + "<startingbuildpoints>50</startingbuildpoints><karma>50</karma><attributes>"
            + AttributeXml("INT", "1") + AttributeXml("LOG", "1") + "</attributes></character>");
        creation.SetCharacterOptionsForTesting(new CharacterOptions { FreeKarmaKnowledge = true });

        // INT 1 + LOG 1 = 2 -> 6 free points, more than covers a single rating-1 Knowledge Skill.
        Assert.True(creation.AddKnowledgeSkill("Shadowing", "Street"));
        Assert.Equal("50", creation.Karma);
    }

    [Fact]
    public void DirectCreationSkillSetters_RespectTheSharedBudgetAndRollBack()
    {
        CharacterDocument active = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>20</startingbuildpoints><bp>4</bp><skills><skill><name>Pistols</name><rating>0</rating><ratingmax>6</ratingmax><knowledge>False</knowledge><grouped>False</grouped></skill></skills></character>");
        Assert.True(active.SetActiveSkillRating(0, 1));
        Assert.Equal("0", active.Bp);
        Assert.False(active.SetActiveSkillRating(0, 2));
        Assert.Equal("1", Assert.Single(active.Skills).BaseRating);
        Assert.True(active.SetActiveSkillRating(0, 0));
        Assert.Equal("4", active.Bp);

        CharacterDocument group = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>20</startingbuildpoints><bp>10</bp><skillgroups><skillgroup><name>Firearms</name><rating>0</rating></skillgroup></skillgroups><skills><skill><name>Pistols</name><skillgroup>Firearms</skillgroup><rating>0</rating><knowledge>False</knowledge><grouped>False</grouped></skill></skills></character>");
        Assert.True(group.SetSkillGroupRating("Firearms", 1));
        Assert.Equal("0", group.Bp);
        Assert.False(group.SetSkillGroupRating("Firearms", 2));
        Assert.Equal("1", Assert.Single(group.SkillGroups).Rating);
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
    public void Spells_DicePool_IsZeroWithoutASpellcastingSkill()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddSpell("Acid Stream", "Combat", "P", "LOS", "P", "I", "(F/2)+3", "SR4", "204");

        Assert.Equal("0", Assert.Single(character.Spells).DicePool);
    }

    [Fact]
    public void ReapplyKnownRuleImprovements_RefreshesKnownItemsAndPreservesRetiredRulesData()
    {
        CharacterDocument character = LoadXml("<character><qualities><quality><name>Retired Quality</name>"
            + "</quality></qualities><improvements><improvement><improvementttype>Skill</improvementttype>"
            + "<improvementsource>Quality</improvementsource><sourcename>Retired Quality</sourcename>"
            + "<improvedname>Retired Skill</improvedname><val>7</val><enabled>True</enabled>"
            + "</improvement></improvements></character>");
        character.AddQuality("Analytical Mind", "Positive");
        Assert.True(character.AddComplexForm("Empathy Software", "Sensor Software", "AR", "60"));
        character.AddCritterPower("Armor (Ballistic)", "1", "RW", "204", strRating: "4");

        foreach (XmlNode improvement in character.Document.SelectNodes("/character/improvements/improvement")!
                     .Cast<XmlNode>().Where(node => node["sourcename"]?.InnerText != "Retired Quality"))
            improvement["val"]!.InnerText = "99";

        Assert.Equal(3, character.ReapplyKnownRuleImprovements());
        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Data Search"));
        Assert.Equal(2, ImprovementManager.ValueOf(character.Improvements, ImprovementType.Skill, "Software"));
        Assert.Equal(1, ImprovementManager.ValueOf(character.Improvements, ImprovementType.SkillCategory, "Social Active"));
        Assert.Equal(4, ImprovementManager.ValueOf(character.Improvements, ImprovementType.BallisticArmor));

        XmlNode retired = Assert.Single(character.Document.SelectNodes(
            "/character/improvements/improvement[sourcename = 'Retired Quality']")!.Cast<XmlNode>());
        Assert.Equal("7", retired["val"]!.InnerText);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "reapplied.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "reapplied.chum");
        Assert.Equal(4, ImprovementManager.ValueOf(reloaded.Improvements, ImprovementType.BallisticArmor));
        Assert.Contains(reloaded.Improvements, improvement => improvement.SourceName == "Retired Quality"
            && improvement.Value == 7);
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
    public void GetActiveSkillNames_IncludesSkillsOutsideCombatActive()
    {
        CharacterDocument character = LoadXml("<character></character>");
        Assert.Contains("Pistols", character.GetActiveSkillNames());
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
    public void BreakSkillGroup_RequiresTheHouseRuleOn()
    {
        CharacterDocument character = LoadCreationFirearmsGroupAtRating2();
        Assert.False(character.BreakSkillGroup("Firearms"));
        Assert.False(character.SkillGroups.Single(g => g.Name == "Firearms").Broken);
    }

    [Fact]
    public void BreakSkillGroup_UnlocksMemberSkillsForIndividualRaises()
    {
        CharacterDocument character = LoadCreationFirearmsGroupAtRating2();
        character.SetCharacterOptionsForTesting(new CharacterOptions { BreakSkillGroupsInCreateMode = true });

        Assert.True(character.BreakSkillGroup("Firearms"));
        Assert.True(character.SkillGroups.Single(g => g.Name == "Firearms").Broken);
        Assert.All(character.Skills, s => Assert.False(s.IsGroupLocked));

        // Raising Firearms as a whole is blocked once broken.
        Assert.False(character.RaiseSkillGroupCreate("Firearms"));

        int intPistolenId = character.Skills.Single(s => s.Name == "Pistolen").SkillId;
        Assert.True(character.RaiseActiveSkillCreate(intPistolenId));
        Assert.Equal("3", character.Skills.Single(s => s.Name == "Pistolen").BaseRating);
        Assert.Equal("2", character.Skills.Single(s => s.Name == "Automatik").BaseRating); // untouched
        Assert.Equal("94", character.Karma); // (2+1)*KarmaImproveActiveSkill(2) = 6
    }

    [Fact]
    public void BreakSkillGroup_LoweringBelowTheFloorIsRejected()
    {
        CharacterDocument character = LoadCreationFirearmsGroupAtRating2();
        character.SetCharacterOptionsForTesting(new CharacterOptions { BreakSkillGroupsInCreateMode = true });
        Assert.True(character.BreakSkillGroup("Firearms"));

        int intPistolenId = character.Skills.Single(s => s.Name == "Pistolen").SkillId;
        // Floor is 2 (the group's rating when broken) - can't refund Karma never spent individually.
        Assert.False(character.LowerActiveSkillCreate(intPistolenId));
        Assert.Equal("2", character.Skills.Single(s => s.Name == "Pistolen").BaseRating);
        Assert.Equal("100", character.Karma);
    }

    [Fact]
    public void BreakSkillGroup_CreationBudgetOnlyChargesTheExcessAboveTheFloor()
    {
        CharacterDocument character = LoadCreationFirearmsGroupAtRating2();
        character.SetCharacterOptionsForTesting(new CharacterOptions { BreakSkillGroupsInCreateMode = true });
        Assert.True(character.BreakSkillGroup("Firearms"));

        int intPistolenId = character.Skills.Single(s => s.Name == "Pistolen").SkillId;
        Assert.True(character.RaiseActiveSkillCreate(intPistolenId)); // Pistolen: 2 -> 3

        CharacterCreationBudgetData budget = character.CreationBudget;
        // Firearms group itself: KarmaNewSkillGroup(10) + 2*KarmaImproveSkillGroup(5) = 20.
        Assert.Contains(budget.Categories, c => c.Name == "Skill groups" && c.Cost == 20);
        // Active skills: only Pistolen's excess above the floor (rating 3 minus floor 2) = 6, not
        // the full cost of a rating-3 Pistolen (14) which would double-charge the group's floor.
        Assert.Contains(budget.Categories, c => c.Name == "Active skills" && c.Cost == 6);
    }

    [Fact]
    public void RegroupSkillGroup_RelocksWhenMemberSkillsStillAgree()
    {
        CharacterDocument character = LoadCreationFirearmsGroupAtRating2();
        character.SetCharacterOptionsForTesting(new CharacterOptions { BreakSkillGroupsInCreateMode = true });
        Assert.True(character.BreakSkillGroup("Firearms"));

        Assert.True(character.RegroupSkillGroup("Firearms"));
        Assert.False(character.SkillGroups.Single(g => g.Name == "Firearms").Broken);
        Assert.All(character.Skills, s => Assert.True(s.IsGroupLocked));
        Assert.Equal("2", character.SkillGroups.Single(g => g.Name == "Firearms").Rating);
    }

    [Fact]
    public void RegroupSkillGroup_RejectsWhenMemberSkillsHaveDiverged()
    {
        CharacterDocument character = LoadCreationFirearmsGroupAtRating2();
        character.SetCharacterOptionsForTesting(new CharacterOptions { BreakSkillGroupsInCreateMode = true });
        Assert.True(character.BreakSkillGroup("Firearms"));

        int intPistolenId = character.Skills.Single(s => s.Name == "Pistolen").SkillId;
        Assert.True(character.RaiseActiveSkillCreate(intPistolenId)); // Pistolen now 3, Automatik still 2.

        Assert.False(character.RegroupSkillGroup("Firearms"));
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
    public void SkillDiceRollingAvailability_UsesTheCharacterHouseRule()
    {
        CharacterDocument character = LoadXml("<character />");

        Assert.False(character.AllowSkillDiceRollingEnabled);
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowSkillDiceRolling = true });
        Assert.True(character.AllowSkillDiceRollingEnabled);
    }

    [Fact]
    public void GetCombatActiveSkillNames_IncludesUnarmedCombat()
    {
        CharacterDocument character = LoadXml("<character></character>");
        Assert.Contains("Unarmed Combat", character.GetCombatActiveSkillNames());
    }

}
