namespace Chummer.Core
{
    public sealed class CharacterConditionData
    {
        internal CharacterConditionData(string strEssence, string strPhysicalDamage, string strStunDamage,
            CharacterDerivedValueData physicalCm, CharacterDerivedValueData stunCm)
        {
            Essence = strEssence;
            PhysicalDamage = strPhysicalDamage;
            StunDamage = strStunDamage;
            PhysicalCm = physicalCm;
            StunCm = stunCm;
        }

        public string Essence { get; private set; }
        public string PhysicalDamage { get; private set; }
        public string StunDamage { get; private set; }

        /// <summary>Total Physical Condition Monitor boxes (8 + half BOD, rounded up, plus Improvements).</summary>
        public CharacterDerivedValueData PhysicalCm { get; }

        /// <summary>Total Stun Condition Monitor boxes (8 + half WIL, rounded up, plus Improvements).</summary>
        public CharacterDerivedValueData StunCm { get; }
    }

    /// <summary>Armor encumbrance dice-pool penalties - see CharacterDocument.ArmorEncumbrance.</summary>
}
