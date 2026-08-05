using System;
using System.Collections.Generic;
using System.Linq;

namespace Chummer.Core
{
    /// <summary>SR4 dice-pool rolling method, ported from frmDiceRoller.cs's cmdRollDice_Click.</summary>
    public enum DiceRollMethod
    {
        /// <summary>Hits on 5-6 (4-6 with Cinematic Gameplay), 1s (or 1-2 with Rushed Job) glitch.</summary>
        Standard,

        /// <summary>Hits on 3-6 - for pools too large to roll individually, rolled in batches of 3.</summary>
        Large,

        /// <summary>Sums every die's face value directly - for even larger pools.</summary>
        ReallyLarge
    }

    public sealed class DiceRollResult
    {
        public IReadOnlyList<int> Rolls { get; }
        public int Hits { get; }
        public int GlitchCount { get; }
        public bool IsGlitch { get; }
        public bool IsCriticalGlitch { get; }

        /// <summary>Null when no Threshold was set (nothing to succeed/fail against).</summary>
        public bool? Success { get; }

        internal DiceRollResult(IReadOnlyList<int> lstRolls, int intHits, int intGlitchCount, bool blnGlitch,
            bool blnCriticalGlitch, bool? blnSuccess)
        {
            Rolls = lstRolls;
            Hits = intHits;
            GlitchCount = intGlitchCount;
            IsGlitch = blnGlitch;
            IsCriticalGlitch = blnCriticalGlitch;
            Success = blnSuccess;
        }
    }

    public static class DiceRoller
    {
        /// <summary>Rolls a Shadowrun 4 dice pool. <paramref name="funcRandom"/> returns a random
        /// int in [1,6] - injectable so tests can drive it deterministically.</summary>
        public static DiceRollResult Roll(int intDice, DiceRollMethod eMethod, Func<int> funcRandom,
            int intGremlins = 0, bool blnRuleOf6 = false, bool blnCinematicGameplay = false,
            bool blnRushedJob = false, int intThreshold = 0)
        {
            if (intDice < 1)
                throw new ArgumentOutOfRangeException(nameof(intDice), "At least one die is required.");

            var lstRolls = new List<int>();
            for (int i = 0; i < intDice; i++)
            {
                int intResult;
                do
                {
                    intResult = funcRandom();
                    lstRolls.Add(intResult);
                } while (blnRuleOf6 && intResult == 6);
            }

            int intGlitchMin = blnRushedJob ? 2 : 1;
            int intGlitchThreshold = Math.Max(1, (int)Math.Ceiling(intDice / 2.0) - intGremlins);

            int intHits = 0;
            int intGlitchCount = lstRolls.Count(r => r <= intGlitchMin);

            switch (eMethod)
            {
                case DiceRollMethod.Standard:
                    int intTarget = blnCinematicGameplay ? 4 : 5;
                    intHits = lstRolls.Count(r => r >= intTarget);
                    break;
                case DiceRollMethod.Large:
                    intHits = lstRolls.Count(r => r >= 3);
                    break;
                case DiceRollMethod.ReallyLarge:
                    intHits = lstRolls.Sum();
                    // "Really Large" pools aren't rolled as individual glitch-eligible dice.
                    intGlitchCount = 0;
                    break;
            }

            bool blnGlitch = intGlitchCount >= intGlitchThreshold;
            bool blnCriticalGlitch = blnGlitch && intHits == 0;
            bool? blnSuccess = intThreshold > 0 ? intHits >= intThreshold : null;

            return new DiceRollResult(lstRolls, intHits, intGlitchCount, blnGlitch, blnCriticalGlitch, blnSuccess);
        }
    }
}
