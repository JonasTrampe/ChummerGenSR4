namespace Chummer.Core
{
    public sealed class CharacterLifestyleData
    {
        internal CharacterLifestyleData(string strName, string strCost, string strMonths, string strDice,
            string strMultiplier, int intLifestyleId, string strNotes)
        {
            Name = strName;
            Cost = strCost;
            Months = strMonths;
            Dice = strDice;
            Multiplier = strMultiplier;
            LifestyleId = intLifestyleId;
            Notes = strNotes;
        }

        public string Name { get; }
        public string Cost { get; }
        public string Months { get; }

        /// <summary>Empty for saves predating this field - see
        /// CharacterDocument.GetLifestyleNuyenRollInfo's by-name lifestyles.xml fallback.</summary>
        public string Dice { get; }

        public string Multiplier { get; }
        public int LifestyleId { get; }
        public string Notes { get; }
    }

}
