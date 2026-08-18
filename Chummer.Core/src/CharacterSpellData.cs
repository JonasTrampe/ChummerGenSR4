namespace Chummer.Core
{
    public sealed class CharacterSpellData
    {
        internal CharacterSpellData(string strName, string strCategory, string strType, string strRange,
            string strDamage, string strDuration, string strDv, string strSource, string strPage,
            string strDicePool = "0", string strDicePoolTooltip = "", bool blnExtended = false,
            int intSpellId = -1, string strNotes = "")
        {
            Name = strName;
            Extended = blnExtended;
            DisplayName = blnExtended ? strName + ", Extended" : strName;
            Category = strCategory;
            Type = strType;
            Range = strRange;
            Damage = strDamage;
            Duration = strDuration;
            Dv = CharacterDocument.GetSpellDrainValue(strDv, blnExtended);
            Source = strSource;
            Page = strPage;
            DicePool = strDicePool;
            DicePoolTooltip = strDicePoolTooltip;
            SpellId = intSpellId;
            Notes = strNotes;
        }

        public string Name { get; }
        public string DisplayName { get; }
        public bool Extended { get; }
        public string Category { get; }
        public string Type { get; }
        public string Range { get; }
        public string Damage { get; }
        public string Duration { get; }
        public string Dv { get; }
        public string Source { get; }
        public string Page { get; }

        /// <summary>Ported from clsUnique.cs's Spell.DicePool: the Spellcasting skill's
        /// TotalRating, +2 if the skill's own Specialization matches this spell's Category, plus
        /// any SpellCategory Improvements. "0" if the character has no Spellcasting skill at all.</summary>
        public string DicePool { get; }
        public string DicePoolTooltip { get; }
        public int SpellId { get; }
        public string Notes { get; }
    }

}
