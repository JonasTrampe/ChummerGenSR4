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
}
