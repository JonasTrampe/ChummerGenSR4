using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chummer.Core
{
    public sealed class CharacterTreeItemData
    {
        internal CharacterTreeItemData(string strName, string strCategory = "", string strRating = "0",
            bool blnEquipped = false, string strCost = "", string strAvail = "", string strQty = "1")
        {
            Name = strName;
            TranslatedName = strName;
            Category = strCategory;
            Rating = strRating;
            Equipped = blnEquipped;
            Cost = strCost;
            Avail = strAvail;
            Qty = strQty;
            Children = new List<CharacterTreeItemData>();
        }

        public string Name { get; private set; }

        /// <summary>Name to display in the UI - the &lt;translate&gt; value from the data file if
        /// the current language pack provides one for this item, otherwise the same as Name. Only
        /// set (non-default) for Gear tree nodes so far.</summary>
        public string TranslatedName { get; private set; }

        internal void SetTranslatedName(string strTranslatedName) => TranslatedName = strTranslatedName;

        /// <summary>Empty for item types that don't save one (e.g. Quality nodes don't reuse this
        /// class). Gear/Cyberware/Armor/Weapon all use the same &lt;category&gt; element name.</summary>
        public string Category { get; }

        public string Rating { get; }

        /// <summary>Whether the item is currently worn/active/installed - only meaningful for
        /// item types that track it (Gear, Armor, Cyberware all do; not everything does).</summary>
        public bool Equipped { get; }

        /// <summary>Raw saved cost - a plain number or a "Rating"-formula string. Use CalculatedCost.</summary>
        public string Cost { get; }

        /// <summary>Raw saved availability, e.g. "6R". Use CalculatedAvail.</summary>
        public string Avail { get; }

        public string Qty { get; }

        /// <summary>Raw saved ballistic/impact armor rating (e.g. "+3") - only set for Armor tree
        /// nodes, empty otherwise. See CharacterDocument.ArmorEncumbrance for the aggregate rules.</summary>
        public string Ballistic { get; private set; } = string.Empty;

        public string Impact { get; private set; } = string.Empty;
        public string ArmorSetName { get; private set; } = string.Empty;
        /// <summary>Position in the root &lt;armors&gt; XML collection. It is recomputed when the
        /// tree is read and is used for reorder operations, including duplicate purchases.</summary>
        public int ArmorId { get; private set; } = -1;
        /// <summary>Position in the root &lt;weapons&gt; XML collection; only root Weapon nodes
        /// receive one. It makes duplicate weapon purchases independently editable without
        /// changing the legacy file format.</summary>
        public int WeaponId { get; private set; } = -1;
        public string Location { get; private set; } = string.Empty;
        /// <summary>Persisted GUID for items that have one (including vehicle modifications).</summary>
        public string ItemGuid { get; private set; } = string.Empty;

        /// <summary>Free-form user note saved beside this item. It is intentionally separate from
        /// the data-file name: notes must never affect rule lookup or calculated values.</summary>
        public string Notes { get; private set; } = string.Empty;

        /// <summary>Optional player-facing label (the legacy <c>gearname</c> field for Gear).
        /// The raw <see cref="Name"/> remains the stable rules-data identity.</summary>
        public string CustomName { get; private set; } = string.Empty;

        /// <summary>Raw saved slots ("Rating"-formula string) - only set for Vehicle Mod nodes.
        /// See CharacterVehicleData.SlotsUsed for the evaluated total.</summary>
        public string ModSlots { get; private set; } = string.Empty;

        /// <summary>Mods that come pre-installed with the vehicle don't consume purchased slots -
        /// only meaningful when <see cref="IsVehicleMod"/> is true.</summary>
        public bool IncludedInVehicle { get; private set; }

        /// <summary>True for nodes built from a vehicle's &lt;mods&gt;&lt;mod&gt; list - a saved
        /// Category isn't a reliable way to tell (mods keep their real rules category, e.g.
        /// "Standard", not a generic marker), so this is set explicitly instead.</summary>
        public bool IsVehicleMod { get; private set; }

        /// <summary>True for nodes built from a &lt;weapons&gt;&lt;weapon&gt; list nested under a
        /// vehicle or vehicle Mod - same rationale as IsVehicleMod (Category is the weapon's real
        /// rules category, not a marker).</summary>
        public bool IsVehicleWeapon { get; internal set; }

        /// <summary>True for a root weapon's &lt;accessories&gt;&lt;accessory&gt; children (used to
        /// tell them apart from the weapon's own WeaponMod/Gear/Ammo children for add/remove and
        /// display purposes) - accessory nodes have no Category of their own.</summary>
        public bool IsWeaponAccessory { get; internal set; }

        /// <summary>True for a root weapon's &lt;weaponmods&gt;&lt;weaponmod&gt; children - same
        /// rationale as IsWeaponAccessory.</summary>
        public bool IsWeaponMod { get; internal set; }
        /// <summary>True for a nested weapon supplied or mounted below a root weapon.</summary>
        public bool IsUnderbarrelWeapon { get; internal set; }

        /// <summary>True for a weapon accessory/mod child; its <see cref="IncludedInWeapon"/>
        /// flag means it is part of the base weapon rather than an added purchase.</summary>
        public bool IsWeaponPart => IsWeaponAccessory || IsWeaponMod;
        public bool IncludedInWeapon { get; private set; }

        /// <summary>True when a Bioware item has been converted to the Genetech: Transgenics
        /// category by the AllowCustomTransgenics rule.</summary>
        public bool IsTransgenic { get; private set; }

        internal void SetArmorRatings(string strBallistic, string strImpact)
        {
            Ballistic = strBallistic;
            Impact = strImpact;
        }

        internal void SetArmorSetName(string strSetName) => ArmorSetName = strSetName;
        internal void SetArmorId(int intArmorId) => ArmorId = intArmorId;
        internal void SetWeaponId(int intWeaponId) => WeaponId = intWeaponId;
        internal void SetLocation(string strLocation) => Location = strLocation;
        internal void SetItemGuid(string strItemGuid) => ItemGuid = strItemGuid;
        internal void SetNotes(string strNotes) => Notes = strNotes;
        internal void SetCustomName(string strCustomName) => CustomName = strCustomName;
        internal void SetModSlots(string strSlots, bool blnIncluded)
        {
            ModSlots = strSlots;
            IncludedInVehicle = blnIncluded;
            IsVehicleMod = true;
        }

        /// <summary>Only set (non-empty) for Weapon root nodes - see
        /// CharacterDocument.ComputeWeaponDicePool.</summary>
        public string WeaponDicePool { get; private set; } = string.Empty;

        public string WeaponDicePoolTooltip { get; private set; } = string.Empty;

        internal void SetWeaponDicePool(string strDicePool, string strTooltip)
        {
            WeaponDicePool = strDicePool;
            WeaponDicePoolTooltip = strTooltip;
        }

        /// <summary>Only set (non-empty) for Weapon root nodes - see
        /// CharacterDocument.ComputeWeaponTotalRc.</summary>
        public string WeaponRc { get; private set; } = string.Empty;

        public string WeaponDamage { get; private set; } = string.Empty;

        internal void SetWeaponRc(string strRc) => WeaponRc = strRc;
        internal void SetWeaponDamage(string strDamage) => WeaponDamage = strDamage;

        internal void SetWeaponPartIncluded(bool blnIncluded) => IncludedInWeapon = blnIncluded;
        internal void SetTransgenic(bool blnTransgenic) => IsTransgenic = blnTransgenic;

        /// <summary>"12/30 (Ammo: Regular Ammo)" - style summary of what's currently loaded, empty
        /// when nothing is loaded. Only set for root Weapon nodes. See
        /// CharacterDocument.ReloadWeapon/GetWeaponAmmoOptions.</summary>
        public string AmmoStatus { get; private set; } = string.Empty;

        internal void SetAmmoStatus(string strAmmoStatus) => AmmoStatus = strAmmoStatus;

        /// <summary>Depth-first position within the whole &lt;gears&gt; tree - only set for Gear
        /// tree nodes (-1 otherwise). Stable identity for AddChildGear/RemoveGear/SetGearQuantity,
        /// since name+category+rating isn't unique once gear can nest under other gear.</summary>
        public int GearId { get; private set; } = -1;

        internal void SetGearId(int intGearId) => GearId = intGearId;

        /// <summary>Depth-first position within the whole &lt;cyberwares&gt; tree - only set for
        /// Cyberware/Bioware tree nodes (-1 otherwise). Same purpose as <see cref="GearId"/>, for
        /// drag/drop reorder/reparent.</summary>
        public int CyberwareId { get; private set; } = -1;

        internal void SetCyberwareId(int intCyberwareId) => CyberwareId = intCyberwareId;

        /// <summary>Raw saved capacity (e.g. "8" or "[2]") - set for Gear and calculated Armor tree nodes.</summary>
        public string Capacity { get; private set; } = string.Empty;
        private string? _strCapacityRemainingOverride;

        /// <summary>Commlink stats - only set (non-empty) for Commlink-category Gear nodes.</summary>
        public string Response { get; private set; } = string.Empty;

        public string Signal { get; private set; } = string.Empty;
        public string System { get; private set; } = string.Empty;
        public string Firewall { get; private set; } = string.Empty;
        public bool Active { get; private set; }

        internal void SetGearDetails(string strCapacity, string strResponse, string strSignal, string strSystem,
            string strFirewall, bool blnActive)
        {
            Capacity = strCapacity;
            Response = strResponse;
            Signal = strSignal;
            System = strSystem;
            Firewall = strFirewall;
            Active = blnActive;
        }

        internal void SetCapacityDetails(string strCapacity, string strRemaining)
        {
            Capacity = strCapacity;
            _strCapacityRemainingOverride = strRemaining;
        }

        /// <summary>Own capacity minus the sum of children's own capacity (each child's Capacity is
        /// treated as how much of the parent's slots it consumes) - a simplified version of
        /// clsEquipment.cs's Gear.CapacityRemaining that doesn't handle bracketed "[x]" capacity
        /// styles or per-item capacity-consumption overrides.</summary>
        public string CapacityRemaining
        {
            get
            {
                if (_strCapacityRemainingOverride != null)
                    return _strCapacityRemainingOverride;
                double dblOwn = double.TryParse(Capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var d0) ? d0 : 0;
                double dblUsed = Children.Sum(c =>
                    double.TryParse(c.Capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0);
                return (dblOwn - dblUsed).ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>"capacity (remaining verbleibend)" - matches the legacy UI's always-shown
        /// capacity line, defaulting to 0 for items (e.g. Commlinks) that don't save a capacity.</summary>
        public string CapacityDisplay
        {
            get
            {
                double dblOwn = double.TryParse(Capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var d0) ? d0 : 0;
                return $"{dblOwn.ToString("0.##", CultureInfo.InvariantCulture)} ({CapacityRemaining} verbleibend)";
            }
        }

        /// <summary>The highest Response/Signal/System/Firewall value found across this node and
        /// its descendants - e.g. a Commlink's base hardware supplies Response/Signal, an installed
        /// Operating System gear supplies System/Firewall, and Commlink/OS Upgrade gear (which save
        /// a flat replacement rating, not a bonus) can raise any of the four further.</summary>
        // Only ever set (via SetResponsePenalty) on Commlink-category root items when the
        // CalculateCommlinkResponse house rule is on - see CharacterFileService.ApplyCommlinkResponsePenalties.
        private int _intResponsePenalty;

        internal void SetResponsePenalty(int intPenalty) => _intResponsePenalty = Math.Max(0, intPenalty);

        /// <summary>Ported from clsEquipment.cs's Commlink.TotalResponse: reduced by
        /// floor(running-programs / TotalSystem) under the CalculateCommlinkResponse house rule,
        /// clamped to never go below 0 - see <see cref="SetResponsePenalty"/>.</summary>
        public string EffectiveResponse
        {
            get
            {
                string strBase = EffectiveStat(g => g.Response);
                if (_intResponsePenalty == 0
                    || !double.TryParse(strBase, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblBase))
                    return strBase;
                return Math.Max(0, dblBase - _intResponsePenalty).ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        public string EffectiveSignal => EffectiveStat(g => g.Signal);
        public string EffectiveSystem => EffectiveStat(g => g.System);
        public string EffectiveFirewall => EffectiveStat(g => g.Firewall);

        // Legacy always saves a <response>/<signal>/<system>/<firewall> element on every Gear node
        // (defaulting to "0" for non-Commlink items), so a plain non-empty check here would treat
        // every piece of Gear as a Commlink - require a positive value instead.
        public bool HasCommlinkStats => IsPositive(EffectiveResponse) || IsPositive(EffectiveSignal)
            || IsPositive(EffectiveSystem) || IsPositive(EffectiveFirewall);

        private static bool IsPositive(string strValue)
            => double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0;

        private string EffectiveStat(Func<CharacterTreeItemData, string> funcSelector)
        {
            double? dblMax = null;
            CollectMaxStat(funcSelector, ref dblMax);
            return dblMax.HasValue ? dblMax.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        }

        private void CollectMaxStat(Func<CharacterTreeItemData, string> funcSelector, ref double? dblMax)
        {
            if (double.TryParse(funcSelector(this), NumberStyles.Float, CultureInfo.InvariantCulture, out var dblOwn))
                dblMax = dblMax.HasValue ? Math.Max(dblMax.Value, dblOwn) : dblOwn;
            foreach (CharacterTreeItemData objChild in Children)
                objChild.CollectMaxStat(funcSelector, ref dblMax);
        }

        public List<CharacterTreeItemData> Children { get; }

        /// <summary>Ported from clsEquipment.cs's Gear.TotalCost.</summary>
        public int CalculatedCost
        {
            get
            {
                var intQty = int.TryParse(Qty, out var intParsedQty) ? intParsedQty : 1;
                var intOwn = (int)Math.Ceiling(EvaluateRatingExpression(Cost, Rating)) * intQty;
                return intOwn + Children.Sum(objChild => objChild.CalculatedCost);
            }
        }

        /// <summary>Ported from clsEquipment.cs's Gear.TotalAvail.</summary>
        public string CalculatedAvail
        {
            get
            {
                if (string.IsNullOrEmpty(Avail)) return string.Empty;
                var strSuffix = string.Empty;
                var strExpression = Avail;
                var chLast = Avail[Avail.Length - 1];
                if (chLast == 'R' || chLast == 'F')
                {
                    strSuffix = chLast.ToString();
                    strExpression = Avail.Substring(0, Avail.Length - 1);
                }

                var intValue = (int)EvaluateRatingExpression(strExpression, Rating);
                return intValue + strSuffix;
            }
        }

        private static double EvaluateRatingExpression(string strExpression, string strRating)
            => RatingExpression.Evaluate(strExpression, strRating);
    }

}
