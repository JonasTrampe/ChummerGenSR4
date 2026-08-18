namespace Chummer.Core
{
    public sealed class CharacterEncumbranceData
    {
        internal CharacterEncumbranceData(CharacterDerivedValueData ballisticRating, CharacterDerivedValueData impactRating,
            CharacterDerivedValueData ballisticPenalty, CharacterDerivedValueData impactPenalty)
        {
            BallisticRating = ballisticRating;
            ImpactRating = impactRating;
            BallisticPenalty = ballisticPenalty;
            ImpactPenalty = impactPenalty;
        }

        public CharacterDerivedValueData BallisticRating { get; }
        public CharacterDerivedValueData ImpactRating { get; }
        public CharacterDerivedValueData BallisticPenalty { get; }
        public CharacterDerivedValueData ImpactPenalty { get; }
    }

    /// <summary>Base vs. augmented value for a derived stat that's normally shown as
    /// "base (augmented)" when they differ, e.g. Initiative or Initiative Passes.</summary>
}
