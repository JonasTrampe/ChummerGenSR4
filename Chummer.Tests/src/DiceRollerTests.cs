using System.Collections.Generic;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public class DiceRollerTests
{
    private static System.Func<int> FixedSequence(params int[] rolls)
    {
        int i = 0;
        return () => rolls[i++];
    }

    [Fact]
    public void Roll_Standard_CountsHitsOnFiveOrSix()
    {
        DiceRollResult result = DiceRoller.Roll(5, DiceRollMethod.Standard,
            FixedSequence(6, 5, 4, 3, 2));

        Assert.Equal(new[] { 6, 5, 4, 3, 2 }, result.Rolls);
        Assert.Equal(2, result.Hits);
        Assert.False(result.IsGlitch);
    }

    [Fact]
    public void Roll_Standard_CinematicGameplay_CountsHitsOnFourAndUp()
    {
        DiceRollResult result = DiceRoller.Roll(4, DiceRollMethod.Standard,
            FixedSequence(4, 4, 3, 2), blnCinematicGameplay: true);

        Assert.Equal(2, result.Hits);
    }

    [Fact]
    public void Roll_GlitchesWhenAtLeastHalfTheDiceRollOnes()
    {
        // 4 dice: glitch threshold = ceil(4/2) = 2. Two 1s triggers a glitch; one hit keeps it non-critical.
        DiceRollResult result = DiceRoller.Roll(4, DiceRollMethod.Standard,
            FixedSequence(1, 1, 5, 3));

        Assert.Equal(1, result.Hits);
        Assert.Equal(2, result.GlitchCount);
        Assert.True(result.IsGlitch);
        Assert.False(result.IsCriticalGlitch);
    }

    [Fact]
    public void Roll_CriticalGlitch_WhenGlitchedWithZeroHits()
    {
        DiceRollResult result = DiceRoller.Roll(2, DiceRollMethod.Standard, FixedSequence(1, 1));

        Assert.Equal(0, result.Hits);
        Assert.True(result.IsGlitch);
        Assert.True(result.IsCriticalGlitch);
    }

    [Fact]
    public void Roll_GremlinsRatingReducesGlitchThreshold()
    {
        // 4 dice: base threshold 2, Gremlins 1 -> threshold 1, so even one 1 glitches.
        DiceRollResult result = DiceRoller.Roll(4, DiceRollMethod.Standard,
            FixedSequence(1, 5, 5, 5), intGremlins: 1);

        Assert.True(result.IsGlitch);
    }

    [Fact]
    public void Roll_RushedJob_TreatsOnesAndTwosAsGlitchDice()
    {
        DiceRollResult result = DiceRoller.Roll(4, DiceRollMethod.Standard,
            FixedSequence(2, 2, 5, 5), blnRushedJob: true);

        Assert.Equal(2, result.GlitchCount);
        Assert.True(result.IsGlitch);
    }

    [Fact]
    public void Roll_RuleOf6_RerollsAndAddsExplodedSixes()
    {
        // First die explodes once (6 then 4), second die is a plain 5. Three rolls total from two dice.
        DiceRollResult result = DiceRoller.Roll(2, DiceRollMethod.Standard,
            FixedSequence(6, 4, 5), blnRuleOf6: true);

        Assert.Equal(new[] { 6, 4, 5 }, result.Rolls);
        // Hits on >= 5: the 6 and the 5 -> 2 hits (the exploded 4 doesn't hit on its own).
        Assert.Equal(2, result.Hits);
    }

    [Fact]
    public void Roll_ReallyLarge_SumsFaceValuesInsteadOfCountingHits()
    {
        DiceRollResult result = DiceRoller.Roll(3, DiceRollMethod.ReallyLarge,
            FixedSequence(6, 3, 1));

        Assert.Equal(10, result.Hits);
        Assert.False(result.IsGlitch);
    }

    [Fact]
    public void Roll_ThresholdSet_ReportsSuccessOrFailure()
    {
        DiceRollResult success = DiceRoller.Roll(3, DiceRollMethod.Standard,
            FixedSequence(5, 5, 3), intThreshold: 2);
        Assert.True(success.Success);

        DiceRollResult failure = DiceRoller.Roll(3, DiceRollMethod.Standard,
            FixedSequence(5, 3, 3), intThreshold: 2);
        Assert.False(failure.Success);
    }

    [Fact]
    public void Roll_NoThreshold_SuccessIsNull()
    {
        DiceRollResult result = DiceRoller.Roll(2, DiceRollMethod.Standard, FixedSequence(5, 5));
        Assert.Null(result.Success);
    }
}
