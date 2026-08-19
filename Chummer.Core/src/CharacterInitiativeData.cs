namespace Chummer.Core
{
    public sealed class CharacterInitiativeData
    {
        internal CharacterInitiativeData(int intBase, int intAugmented, string strTooltip)
        {
            Base = intBase;
            Augmented = intAugmented;
            Tooltip = strTooltip;
        }

        public int Base { get; }
        public int Augmented { get; }

        /// <summary>Mouseover breakdown of every attribute/Improvement that fed into Augmented.</summary>
        public string Tooltip { get; }

        /// <summary>"5" if Base == Augmented, otherwise "5 (7)".</summary>
        public string Display => Base == Augmented ? Base.ToString() : Base + " (" + Augmented + ")";
    }

    /// <summary>A computed number plus a mouseover explanation of how it was derived - the
    /// tooltip lists the base attribute(s) and each individual contributing Improvement's source
    /// name and value, so several stacking augmentations (cyberware + quality + spell, etc.) are
    /// each visible rather than collapsed into one opaque total.</summary>
}
