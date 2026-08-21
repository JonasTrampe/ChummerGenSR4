using System.Linq;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public partial class CharacterFileServiceTests
{
    private static string PerceptionSkillXml(string strRating, string strSpec) =>
        "<skill><name>Perception</name><attribute>INT</attribute><skillcategory>Physical Active</skillcategory>"
        + "<spec>" + strSpec + "</spec><rating>" + strRating + "</rating><knowledge>False</knowledge><grouped>False</grouped></skill>";

    private static string PerceptionVisualMetaSkillXml() =>
        "<skill><name>Perception (Visual)</name><attribute>INT</attribute><skillcategory>Physical Active</skillcategory>"
        + "<spec></spec><rating>0</rating><knowledge>False</knowledge><grouped>False</grouped>"
        + "<isMeta>True</isMeta><metaBase>Perception</metaBase><metaSpec>Visual</metaSpec></skill>";

    [Fact]
    public void MetaSkill_MirrorsItsBaseSkillsRatingInsteadOfItsOwnStoredValue()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><attributes>"
            + AttributeXml("INT", "4") + "</attributes><skills>"
            + PerceptionSkillXml("3", string.Empty) + PerceptionVisualMetaSkillXml() + "</skills></character>");

        CharacterSkillData metaSkill = character.Skills.Single(s => s.Name == "Perception (Visual)");
        Assert.True(metaSkill.IsMeta);
        Assert.Equal("Perception", metaSkill.MetaBase);
        Assert.Equal("3", metaSkill.BaseRating); // mirrors Perception's rating, not its own stored "0"
    }

    [Fact]
    public void MetaSkill_GetsPlusTwoDicePoolWhenBaseSkillsSpecializationMatchesMetaSpec()
    {
        CharacterDocument matching = LoadXml("<character><created>True</created><attributes>"
            + AttributeXml("INT", "4") + "</attributes><skills>"
            + PerceptionSkillXml("3", "Visual") + PerceptionVisualMetaSkillXml() + "</skills></character>");
        CharacterDocument notMatching = LoadXml("<character><created>True</created><attributes>"
            + AttributeXml("INT", "4") + "</attributes><skills>"
            + PerceptionSkillXml("3", "Astral") + PerceptionVisualMetaSkillXml() + "</skills></character>");

        int intPoolMatching = int.Parse(matching.Skills.Single(s => s.Name == "Perception (Visual)").TotalValue);
        int intPoolNotMatching = int.Parse(notMatching.Skills.Single(s => s.Name == "Perception (Visual)").TotalValue);

        Assert.Equal(intPoolNotMatching + 2, intPoolMatching);
    }

    [Fact]
    public void MetaSkill_CannotBeRaisedIndependentlyAndCostsNoKarma()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><karma>100</karma><attributes>"
            + AttributeXml("INT", "4") + "</attributes><skills>"
            + PerceptionSkillXml("3", string.Empty) + PerceptionVisualMetaSkillXml() + "</skills></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions());

        int intMetaSkillId = character.Skills.Single(s => s.Name == "Perception (Visual)").SkillId;

        Assert.Null(character.GetActiveSkillKarmaCostToIncrease(intMetaSkillId));
        Assert.False(character.RaiseActiveSkill(intMetaSkillId));
        Assert.Equal("100", character.Karma); // unchanged - nothing was spent
    }

    [Fact]
    public void MetaSkill_InheritsImprovementsTargetedAtItsBaseSkillsName()
    {
        CharacterDocument character = LoadXml("<character><created>True</created><attributes>"
            + AttributeXml("INT", "4") + "</attributes><skills>"
            + PerceptionSkillXml("3", string.Empty) + PerceptionVisualMetaSkillXml() + "</skills>"
            + "<improvements><improvement><improvedname>Perception</improvedname><improvementttype>Skill</improvementttype>"
            + "<improvementsource>Gear</improvementsource><val>2</val><addtorating>True</addtorating><enabled>True</enabled></improvement></improvements></character>");

        CharacterSkillData baseSkill = character.Skills.Single(s => s.Name == "Perception");
        CharacterSkillData metaSkill = character.Skills.Single(s => s.Name == "Perception (Visual)");

        // Both pick up the same +2 rating bonus from an Improvement that names the base skill -
        // ported from clsUnique.cs's "_isMeta && objImprovement.ImprovedName == MetaBase" fallback.
        Assert.Contains("(5)", baseSkill.Rating);
        Assert.Contains("(5)", metaSkill.Rating);
    }

    [Fact]
    public void NewCharacterFactory_SeedsPerceptionMetaSkillsFromSkillsXml()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "BP", 400, 12,
            NewCharacterFactory.LoadMetatypes().Single(m => m.Name == "Human"));

        CharacterSkillData metaSkill = character.Skills.Single(s => s.Name == "Perception (Visual)");
        Assert.True(metaSkill.IsMeta);
        Assert.Equal("Perception", metaSkill.MetaBase);
    }
}
