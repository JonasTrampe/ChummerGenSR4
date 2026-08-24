using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

/// <summary>
/// Improvements (CharacterFileService.Improvements.cs) now caches its parsed list per
/// CharacterDocument instance instead of re-walking /character/improvements/improvement and
/// rebuilding every Improvement object on every access (it's read once per skill/attribute/item
/// bonus calculation, so a full reload could hit this 100+ times). This test proves the cache
/// still picks up a real mutation (Changed invalidates it) and isn't just frozen at first read.
/// </summary>
public sealed class ImprovementsCacheTests
{
    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    [Fact]
    public void Improvements_ReturnsTheSameCachedInstanceUntilSomethingChanges()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");

        var first = character.Improvements;
        var second = character.Improvements;

        Assert.Same(first, second);
    }

    [Fact]
    public void Improvements_PicksUpANewlyAddedImprovementAfterAMutation()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><attributes>"
            + "<attribute><name>BOD</name><value>3</value><totalvalue>3</totalvalue></attribute>"
            + "</attributes></character>");
        Assert.Empty(character.Improvements);

        Assert.True(character.AddCustomImprovement(
            CharacterDocument.CustomImprovementType.Attribute, "GM Bonus", intVal: 1, strSelect: "BOD"));

        Assert.Single(character.Improvements);
    }
}
