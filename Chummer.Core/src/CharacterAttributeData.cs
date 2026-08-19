namespace Chummer.Core
{
    public sealed class CharacterAttributeData
    {
        internal CharacterAttributeData(string strCode, string strValue, string strTotalValue, string strMinimum,
            string strMaximum, string strAugmentedMaximum, CharacterDerivedValueData augmented, int intKarmaCostToIncrease)
        {
            Code = strCode;
            Value = strValue;
            TotalValue = strTotalValue;
            Minimum = strMinimum;
            Maximum = strMaximum;
            AugmentedMaximum = strAugmentedMaximum;
            Augmented = augmented;
            KarmaCostToIncrease = intKarmaCostToIncrease;
        }

        public string Code { get; private set; }
        public string Value { get; private set; }
        public string TotalValue { get; private set; }
        public string Minimum { get; private set; }
        public string Maximum { get; private set; }
        public string AugmentedMaximum { get; private set; }

        /// <summary>TotalValue plus any Attribute-type Improvement bonuses (e.g. Wired Reflexes'
        /// Reaction boost) that raise the augmented value without changing TotalValue itself.</summary>
        public CharacterDerivedValueData Augmented { get; private set; }

        /// <summary>Karma cost to raise this attribute's base Value by one point, ported from
        /// frmCareer.cs's cmdImprove&lt;Attribute&gt;_Click handlers.</summary>
        public int KarmaCostToIncrease { get; private set; }
    }

}
