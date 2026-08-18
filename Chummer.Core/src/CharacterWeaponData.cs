namespace Chummer.Core
{
    public sealed class CharacterWeaponData
    {
        internal CharacterWeaponData(string strName, string strCategory, string strDamage, string strAmmo,
            string strAp = "", string strRc = "", string strDicePool = "", string strDicePoolTooltip = "")
        {
            Name = strName;
            Category = strCategory;
            Damage = strDamage;
            Ammo = strAmmo;
            Ap = strAp;
            Rc = strRc;
            DicePool = strDicePool;
            DicePoolTooltip = strDicePoolTooltip;
        }

        public string Name { get; }
        public string Category { get; }
        public string Damage { get; }
        public string Ammo { get; private set; }
        public string Ap { get; }
        public string Rc { get; }

        /// <summary>Ported from clsEquipment.cs's Weapon.DicePool - the linked Active Skill's
        /// dice pool plus a Smartgun System bonus where applicable, formatted as "12" or
        /// "12 (14)" when a matching specialization applies. Empty if no matching Skill is found
        /// (e.g. a Skill the character never raised past a defaulting-disallowed 0).</summary>
        public string DicePool { get; }

        public string DicePoolTooltip { get; }

        public string DisplayName
        {
            get
            {
                var strDetails = string.IsNullOrEmpty(Damage) ? Category : Damage;
                return string.IsNullOrEmpty(strDetails) ? Name : Name + " (" + strDetails + ")";
            }
        }
    }

    /// <summary>Read-only vehicle record with its saved stats and installed mods, gear, and weapons.</summary>
}
