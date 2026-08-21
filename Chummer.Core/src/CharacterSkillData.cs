namespace Chummer.Core
{
    public sealed class CharacterSkillData
    {
        internal CharacterSkillData(int intSkillId, string strName, string strAttribute, string strBaseRating,
            string strRating, string strTotalValue, string strPoolTooltip, string strSpecialization,
            string strCategory, bool blnIsGroupLocked, bool blnAllowDelete, bool blnKnowledgeSkill,
            string strSkillGroup = "", bool blnExotic = false, bool blnIsMeta = false, string strMetaBase = "")
        {
            SkillId = intSkillId;
            Name = strName;
            Attribute = strAttribute;
            BaseRating = strBaseRating;
            Rating = strRating;
            TotalValue = strTotalValue;
            PoolTooltip = strPoolTooltip;
            Specialization = strSpecialization;
            Category = strCategory;
            IsGroupLocked = blnIsGroupLocked;
            AllowDelete = blnAllowDelete;
            KnowledgeSkill = blnKnowledgeSkill;
            SkillGroup = strSkillGroup;
            Exotic = blnExotic;
            IsMeta = blnIsMeta;
            MetaBase = strMetaBase;
        }

        public int SkillId { get; }
        public string Name { get; private set; }
        public string Attribute { get; }

        /// <summary>The skill's own rating with no Improvements applied - <see cref="Rating"/> is
        /// what UI should actually display.</summary>
        public string BaseRating { get; }

        /// <summary>Rating for display, e.g. "3" or "3 (5)" when skill-rating-boosting
        /// Improvements (Skillwire, an Adept power, etc.) raise it above the base value - same
        /// "base (augmented)" convention as attributes.</summary>
        public string Rating { get; }

        /// <summary>The computed dice pool (skill rating + Improvements + linked attribute +
        /// wound modifiers) - see CharacterDocument's skill-reading code for the full formula and
        /// its documented simplifications.</summary>
        public string TotalValue { get; }

        public string PoolTooltip { get; }
        public string Specialization { get; }
        public string Category { get; private set; }
        public bool IsGroupLocked { get; private set; }
        public bool AllowDelete { get; private set; }
        public bool KnowledgeSkill { get; private set; }
        public string SkillGroup { get; private set; }
        public bool Exotic { get; private set; }

        /// <summary>True for a "meta skill" variant (e.g. "Perception (Visual)") - ported from
        /// clsUnique.cs's Skill.IsMeta. Always mirrors <see cref="MetaBase"/>'s Rating/Improvements
        /// and gets a +2 dice pool bonus when the base skill's own Specialization matches this
        /// skill's data-file metaSpec; costs 0 Karma/BP and can't be raised independently.</summary>
        public bool IsMeta { get; private set; }

        /// <summary>The base skill this meta skill mirrors (e.g. "Perception"), empty when
        /// <see cref="IsMeta"/> is false.</summary>
        public string MetaBase { get; private set; }
    }

}
