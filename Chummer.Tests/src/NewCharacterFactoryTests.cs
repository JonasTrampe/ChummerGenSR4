using System.Linq;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public class NewCharacterFactoryTests
{
    private static NewCharacterMetatype LoadHuman()
        => NewCharacterFactory.LoadMetatypes().Single(m => m.Name == "Human");

    [Fact]
    public void CreateNewCharacter_KarmaBuild_SeedsStartingKarmaFromBuildPoints()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        Assert.Equal("750", character.Karma);
    }

    [Fact]
    public void CreateNewCharacter_BpBuild_StartsWithZeroKarma()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "BP", 400, 12, LoadHuman());

        Assert.Equal("0", character.Karma);
    }

    [Fact]
    public void CreateNewCharacter_StartsWithFullEssenceAndMetatypeAttributeRanges()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        Assert.Equal("6", character.Condition.Essence);

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal("1", bod.Minimum);
        Assert.Equal("6", bod.Maximum);
        Assert.Equal("1", bod.Value);
    }

    [Fact]
    public void CreateNewCharacter_SeedsFullSkillListAndSkillGroupsAtRatingZero()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        Assert.NotEmpty(character.SkillGroups);
        Assert.All(character.SkillGroups, g => Assert.Equal("0", g.Rating));

        Assert.NotEmpty(character.Skills);
        Assert.Contains(character.Skills, s => s.Name == "Pistols");
        Assert.All(character.Skills, s => Assert.Equal("0", s.Rating));

        Assert.DoesNotContain(character.Skills, s => s.Name == "Exotic Melee Weapon");
    }

    [Fact]
    public void RaiseAttributeCreate_KarmaBuild_DeductsKarmaAndRoundTripsWithLower()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        int intStartKarma = int.Parse(character.Karma);

        Assert.True(character.RaiseAttributeCreate("BOD"));
        Assert.True(int.Parse(character.Karma) < intStartKarma);

        Assert.True(character.LowerAttributeCreate("BOD"));
        Assert.Equal(intStartKarma, int.Parse(character.Karma));
    }

    [Fact]
    public void RaiseAttributeCreate_EssenceStartingAtItsOwnMaxDoesNotBlockOtherAttributes()
    {
        // Essence is seeded at value == metatypemax (6) for every fresh character. It must not
        // be treated as "an attribute already at max" by the one-attribute-at-max chargen rule,
        // or no real attribute could ever be raised to its own max.
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        for (int i = int.Parse(bod.Value); i < int.Parse(bod.Maximum); i++)
            Assert.True(character.RaiseAttributeCreate("BOD"));

        CharacterAttributeData bodAfter = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(bodAfter.Maximum, bodAfter.Value);
    }

    [Fact]
    public void RaiseAttributeCreate_KeepsAugmentedValueInSyncWithBaseValue()
    {
        // Augmented is derived from the "totalvalue" XML field, which used to stay stuck at the
        // seeded value (racial minimum) after a raise, making every raised attribute show a
        // bogus "(N)" augmented note even with no Improvements applied at all.
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        Assert.True(character.RaiseAttributeCreate("AGI"));
        CharacterAttributeData agi = character.Attributes.Single(a => a.Code == "AGI");
        Assert.Equal(int.Parse(agi.Value), agi.Augmented.Value);
    }

    [Fact]
    public void RaiseAttributeCreate_EdgeDoesNotParticipateInOnlyOneAttributeAtMaxRule()
    {
        // Edge, Magic, and Resonance have their own separate cost/cap rules in SR4 and are
        // exempt from the "only one primary attribute may reach its natural max" chargen rule.
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        CharacterAttributeData edg = character.Attributes.Single(a => a.Code == "EDG");
        Assert.True(character.SetAttributeValue("EDG", int.Parse(edg.Maximum)));

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        for (int i = int.Parse(bod.Value); i < int.Parse(bod.Maximum); i++)
            Assert.True(character.RaiseAttributeCreate("BOD"));

        CharacterAttributeData bodAfter = character.Attributes.Single(a => a.Code == "BOD");
        Assert.Equal(bodAfter.Maximum, bodAfter.Value);
    }

    [Fact]
    public void RaiseAttributeCreate_OnlyOneAttributeMayReachMetatypeMax()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        CharacterAttributeData bod = character.Attributes.Single(a => a.Code == "BOD");
        Assert.True(character.SetAttributeValue("BOD", int.Parse(bod.Maximum)));

        CharacterAttributeData agi = character.Attributes.Single(a => a.Code == "AGI");
        Assert.True(character.SetAttributeValue("AGI", int.Parse(agi.Maximum) - 1));

        Assert.False(character.RaiseAttributeCreate("AGI"));
    }

    [Fact]
    public void RaiseActiveSkillCreate_KarmaBuild_DeductsKarmaAndRoundTripsWithLower()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        int intStartKarma = int.Parse(character.Karma);
        int intSkillId = character.Skills.First(s => s.Name == "Pistols").SkillId;

        Assert.True(character.RaiseActiveSkillCreate(intSkillId));
        Assert.True(int.Parse(character.Karma) < intStartKarma);

        Assert.True(character.LowerActiveSkillCreate(intSkillId));
        Assert.Equal(intStartKarma, int.Parse(character.Karma));
    }

    [Fact]
    public void RaiseSkillGroupCreate_KarmaBuild_DeductsKarmaAndRoundTripsWithLower()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        int intStartKarma = int.Parse(character.Karma);
        string strGroup = character.SkillGroups[0].Name;

        Assert.True(character.RaiseSkillGroupCreate(strGroup));
        Assert.True(int.Parse(character.Karma) < intStartKarma);

        Assert.True(character.LowerSkillGroupCreate(strGroup));
        Assert.Equal(intStartKarma, int.Parse(character.Karma));
    }

    [Fact]
    public void RaiseSkillGroupCreate_LocksAndSyncsMemberSkills_ThenUnlocksWhenLoweredBackToZero()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        string strGroup = character.SkillGroups[0].Name;
        int intMemberSkillId = character.Skills.First(s => s.SkillGroup == strGroup).SkillId;

        Assert.True(character.RaiseSkillGroupCreate(strGroup));

        CharacterSkillData memberSkill = character.Skills.Single(s => s.SkillId == intMemberSkillId);
        Assert.True(memberSkill.IsGroupLocked);
        Assert.Equal("1", memberSkill.Rating);

        // While the group is active, the group's own cost governs it - the member skill can't be
        // raised (or lowered) independently.
        Assert.False(character.RaiseActiveSkillCreate(intMemberSkillId));
        Assert.False(character.LowerActiveSkillCreate(intMemberSkillId));

        Assert.True(character.LowerSkillGroupCreate(strGroup));
        CharacterSkillData memberSkillAfter = character.Skills.Single(s => s.SkillId == intMemberSkillId);
        Assert.False(memberSkillAfter.IsGroupLocked);
        Assert.Equal("0", memberSkillAfter.Rating);
    }

    [Fact]
    public void RaiseSkillGroupCreate_CannotExceedRatingFourDuringCreation()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        string strGroup = character.SkillGroups[0].Name;

        for (int i = 0; i < 4; i++)
            Assert.True(character.RaiseSkillGroupCreate(strGroup));

        Assert.False(character.RaiseSkillGroupCreate(strGroup));
    }

    [Fact]
    public void RaiseNuyenCreate_KarmaBuild_DeductsKarmaAndGrantsNuyenPerPoint_AndRoundTripsWithLower()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        int intStartKarma = int.Parse(character.Karma);
        int intStartNuyen = int.Parse(character.Nuyen);
        int intPerPoint = character.NuyenPerPoint;

        Assert.True(character.RaiseNuyenCreate());
        Assert.Equal(intStartKarma - 1, int.Parse(character.Karma));
        Assert.Equal(intStartNuyen + intPerPoint, int.Parse(character.Nuyen));
        Assert.Equal(1, character.NuyenPoints);

        Assert.True(character.LowerNuyenCreate());
        Assert.Equal(intStartKarma, int.Parse(character.Karma));
        Assert.Equal(intStartNuyen, int.Parse(character.Nuyen));
        Assert.Equal(0, character.NuyenPoints);
    }

    [Fact]
    public void RaiseNuyenCreate_CannotExceedNuyenPointsMax()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());

        for (int i = 0; i < character.NuyenPointsMax; i++)
            Assert.True(character.RaiseNuyenCreate());

        Assert.False(character.RaiseNuyenCreate());
    }

    [Fact]
    public void RaiseAttributeCreate_FiresChangedEvent_SoAHostUiCanRefreshItsKarmaBpDisplay()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "Karma", 750, 12, LoadHuman());
        int intChangedCount = 0;
        character.Changed += () => intChangedCount++;

        Assert.True(character.RaiseAttributeCreate("BOD"));
        Assert.True(intChangedCount > 0);
    }

    [Fact]
    public void RaiseAttributeCreate_BpBuild_DeductsBpAndRoundTripsWithLower()
    {
        CharacterDocument character = NewCharacterFactory.CreateNewCharacter(
            "Test", "default.xml", "BP", 400, 12, LoadHuman());
        int intStartBp = int.Parse(character.Bp);

        Assert.True(character.RaiseAttributeCreate("BOD"));
        Assert.True(int.Parse(character.Bp) < intStartBp);

        Assert.True(character.LowerAttributeCreate("BOD"));
        Assert.Equal(intStartBp, int.Parse(character.Bp));
    }
}
