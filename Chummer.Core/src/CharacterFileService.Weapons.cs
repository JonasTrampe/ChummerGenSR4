using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Core
{
    public sealed partial class CharacterDocument
    {
        private bool StickNShockAllowed(string strName)
        {
            if (!string.Equals(strName, "Ammo: Stick-n-Shock", StringComparison.Ordinal))
                return true;

            var objOptions = GetCharacterOptions();
            if (!objOptions.RestrictStickNShock)
                return true;

            var setExcluded = objOptions.StickNShockExcludedWeaponCategories;
            return Weapons.Any(w => !setExcluded.Contains(w.Category));
        }

        /// <summary>Adds gear nested under an existing gear item (e.g. a Certified Credstick under
        /// a Commlink) - <paramref name="intParentGearId"/> is a <see cref="Gear"/> node's GearId,
        /// assigned in the same depth-first order the tree is displayed in. Also deducts its cost
        /// from Nuyen.</summary>
        private void ApplyPacksWeapons(XmlNode objXmlKit)
        {
            XmlNode? objXmlWeapons = objXmlKit.SelectSingleNode("weapons");
            if (objXmlWeapons == null)
                return;

            XmlDocument objWeaponDoc = XmlManager.Instance.Load("weapons.xml");
            foreach (XmlNode objXmlWeapon in objXmlWeapons.SelectNodes("weapon")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlWeapon["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlWeaponNode = objWeaponDoc.SelectSingleNode($"/chummer/weapons/weapon[name = '{strName}']");
                if (objXmlWeaponNode == null || !IsBookEnabled(objXmlWeaponNode["source"]?.InnerText ?? string.Empty))
                    continue;

                AddWeapon(strName, objXmlWeaponNode["category"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["damage"]?.InnerText ?? string.Empty, objXmlWeaponNode["ap"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["mode"]?.InnerText ?? string.Empty, objXmlWeaponNode["rc"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["ammo"]?.InnerText ?? string.Empty, objXmlWeaponNode["cost"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["avail"]?.InnerText ?? string.Empty, objXmlWeaponNode["source"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["page"]?.InnerText ?? string.Empty);
            }
        }

        public void AddWeapon(string strName, string strCategory, string strDamage, string strAp, string strMode,
            string strRc, string strAmmo, string strCost, string strAvail, string strSource, string strPage,
            string strUseSkill = "", string strReach = "0")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A weapon name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objWeapons = objRoot.SelectSingleNode("weapons");
            if (objWeapons == null)
            {
                objWeapons = Document.CreateElement("weapons");
                objRoot.AppendChild(objWeapons);
            }

            var objWeapon = Document.CreateElement("weapon");
            // Legacy characters (and this port's own weapons before this feature) don't necessarily
            // have a <guid> - name+category matching (RemoveWeapon/SetWeaponEquipped/
            // SetWeaponLocation) still works for those. Only accessory/mod add/remove needs a
            // stable per-instance identity, so it's the one operation that requires a guid.
            AppendElement(objWeapon, "guid", Guid.NewGuid().ToString());
            AppendElement(objWeapon, "name", strName.Trim());
            AppendElement(objWeapon, "category", strCategory);
            AppendElement(objWeapon, "reach", strReach);
            AppendElement(objWeapon, "damage", strDamage);
            AppendElement(objWeapon, "ap", strAp);
            AppendElement(objWeapon, "mode", strMode);
            AppendElement(objWeapon, "rc", strRc);
            AppendElement(objWeapon, "ammo", strAmmo);
            AppendElement(objWeapon, "cost", strCost);
            AppendElement(objWeapon, "avail", strAvail);
            AppendElement(objWeapon, "useskill", strUseSkill);
            AppendElement(objWeapon, "source", strSource);
            AppendElement(objWeapon, "page", strPage);
            AppendElement(objWeapon, "location", string.Empty);
            AppendElement(objWeapon, "equipped", "True");
            AppendElement(objWeapon, "ammoloaded", "-1");
            AppendElement(objWeapon, "ammoremaining", "0");
            objWeapon.AppendChild(Document.CreateElement("accessories"));
            objWeapon.AppendChild(Document.CreateElement("weaponmods"));
            objWeapon.AppendChild(Document.CreateElement("gears"));
            objWeapon.AppendChild(Document.CreateElement("ammos"));
            AddIncludedUnderbarrelWeapon(objWeapon);
            objWeapons.AppendChild(objWeapon);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
        }

        /// <summary>Removes one nested underbarrel weapon from its parent without affecting other
        /// root-level or vehicle weapons.</summary>
        public bool RemoveUnderbarrelWeapon(Guid guiWeaponId, Guid guiUnderbarrelId)
        {
            XmlNode? objParent = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objUnderbarrel = objParent?.SelectSingleNode(
                $"underbarrel/weapon[guid = '{guiUnderbarrelId}']");
            if (objUnderbarrel?.ParentNode == null)
                return false;
            objUnderbarrel.ParentNode.RemoveChild(objUnderbarrel);
            Changed?.Invoke();
            return true;
        }

        private void AddIncludedUnderbarrelWeapon(XmlElement objParentWeapon)
        {
            XmlNode? objRule = FindRuleItemByName(XmlManager.Instance.Load("weapons.xml"),
                "/chummer/weapons/weapon", GetValue(objParentWeapon, "name", string.Empty));
            if (objRule == null)
                return;
            string strUnderbarrelName = GetValue(objRule, "underbarrel", string.Empty);
            if (string.IsNullOrWhiteSpace(strUnderbarrelName))
                return;

            XmlNode? objUnderbarrelRule = FindRuleItemByName(XmlManager.Instance.Load("weapons.xml"),
                "/chummer/weapons/weapon", strUnderbarrelName);
            if (objUnderbarrelRule == null)
                return;

            var objContainer = Document.CreateElement("underbarrel");
            var objWeapon = Document.CreateElement("weapon");
            AppendElement(objWeapon, "guid", Guid.NewGuid().ToString());
            AppendElement(objWeapon, "name", strUnderbarrelName);
            AppendElement(objWeapon, "category", GetValue(objUnderbarrelRule, "category", string.Empty));
            AppendElement(objWeapon, "reach", GetValue(objUnderbarrelRule, "reach", "0"));
            AppendElement(objWeapon, "damage", GetValue(objUnderbarrelRule, "damage", string.Empty));
            AppendElement(objWeapon, "ap", GetValue(objUnderbarrelRule, "ap", string.Empty));
            AppendElement(objWeapon, "mode", GetValue(objUnderbarrelRule, "mode", string.Empty));
            AppendElement(objWeapon, "rc", GetValue(objUnderbarrelRule, "rc", "0"));
            AppendElement(objWeapon, "ammo", GetValue(objUnderbarrelRule, "ammo", string.Empty));
            AppendElement(objWeapon, "cost", GetValue(objUnderbarrelRule, "cost", "0"));
            AppendElement(objWeapon, "avail", GetValue(objUnderbarrelRule, "avail", string.Empty));
            AppendElement(objWeapon, "source", GetValue(objUnderbarrelRule, "source", string.Empty));
            AppendElement(objWeapon, "page", GetValue(objUnderbarrelRule, "page", string.Empty));
            AppendElement(objWeapon, "included", "True");
            AppendElement(objWeapon, "installed", "True");
            AppendElement(objWeapon, "equipped", "True");
            objWeapon.AppendChild(Document.CreateElement("accessories"));
            objWeapon.AppendChild(Document.CreateElement("weaponmods"));
            objWeapon.AppendChild(Document.CreateElement("gears"));
            objWeapon.AppendChild(Document.CreateElement("ammos"));
            objContainer.AppendChild(objWeapon);
            objParentWeapon.AppendChild(objContainer);
        }

        /// <summary>Ported from frmReload.cs/frmCareer.cs's "Buy Ammo"/reload flow: which Gear item
        /// (by <see cref="GearId"/>) is currently loaded into a Weapon, and how many rounds remain
        /// in it - the missing concept `RestrictStickNShock`'s own note previously called out as
        /// blocking loaded-ammo dice-pool bonuses. Unlike legacy's Extra-field weapon-category
        /// matching (which needs a whole separate "restrict this purchase to one category" gear-
        /// picker mode this port doesn't have), compatibility is checked purely by ammo name
        /// against the weapon's AmmoCategory - ported directly from frmCareer.cs's
        /// IsAmmunitionCompatible, which already works exactly this way for every non-generic ammo
        /// type (Arrows/Bolts/Grenades/Missiles/Mortars/etc.) and falls back to "any non-exotic
        /// category" for plain Ammo: Regular/Stick-n-Shock/etc.</summary>
        public sealed class WeaponAmmoOption
        {
            internal WeaponAmmoOption(int intGearId, string strName, int intQuantity)
            {
                GearId = intGearId;
                Name = strName;
                Quantity = intQuantity;
            }

            public int GearId { get; }
            public string Name { get; }
            public int Quantity { get; }
        }

        private static bool IsAmmunitionCompatible(string strAmmoName, string strAmmoCategory)
        {
            if (string.IsNullOrEmpty(strAmmoCategory))
                return false;
            if (strAmmoName.Contains("Arrow", StringComparison.Ordinal))
                return strAmmoCategory == "Bows";
            if (strAmmoName.Contains("Bolt", StringComparison.Ordinal))
                return strAmmoCategory == "Crossbows";
            if (strAmmoName.Contains("Assault Cannon", StringComparison.Ordinal))
                return strAmmoCategory == "Assault Cannons";
            if (strAmmoName.Contains("Taser Dart", StringComparison.Ordinal))
                return strAmmoCategory == "Tasers";
            if (strAmmoName.Contains("Gauss Rifle", StringComparison.Ordinal))
                return strAmmoCategory == "Gauss Rifles";
            if (strAmmoName.Contains("Grenade", StringComparison.Ordinal) || strAmmoName.Contains("Minigrenade", StringComparison.Ordinal))
                return strAmmoCategory == "Grenade Launchers";
            if (strAmmoName.Contains("Missile", StringComparison.Ordinal) || strAmmoName.Contains("Rocket", StringComparison.Ordinal))
                return strAmmoCategory == "Missile Launchers";
            if (strAmmoName.Contains("Mortar", StringComparison.Ordinal))
                return strAmmoCategory == "Mortar Launchers";
            return strAmmoCategory != "Bows" && strAmmoCategory != "Crossbows" && strAmmoCategory != "Grenade Launchers"
                && strAmmoCategory != "Missile Launchers" && strAmmoCategory != "Mortar Launchers";
        }

        /// <summary>The rules-data AmmoCategory (weapons.xml's &lt;ammocategory&gt; override,
        /// falling back to the weapon's own Category) a Weapon's ammo must be compatible with -
        /// ported from clsEquipment.cs's Weapon.AmmoCategory.</summary>
        private string GetWeaponAmmoCategory(string strWeaponName, string strFallbackCategory)
        {
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");
            XmlNode? objXmlWeapon = objWeaponsDoc.SelectSingleNode($"/chummer/weapons/weapon[name = '{strWeaponName}']");
            string strOverride = objXmlWeapon?["ammocategory"]?.InnerText ?? string.Empty;
            return string.IsNullOrEmpty(strOverride) ? strFallbackCategory : strOverride;
        }

        /// <summary>Every owned Ammunition Gear item (root-level or nested one level, e.g. inside a
        /// Spare Clip - same depth legacy's own search covers) compatible with this Weapon, for a
        /// reload picker. Excludes Stick-n-Shock when RestrictStickNShock excludes this weapon's
        /// AmmoCategory, matching the same house rule <see cref="AddGear"/> already enforces at
        /// purchase time.</summary>
        public IReadOnlyList<WeaponAmmoOption> GetWeaponAmmoOptions(Guid guiWeaponId)
        {
            XmlNode? objWeapon = FindWeaponNodeByGuid(guiWeaponId);
            if (objWeapon == null)
                return Array.Empty<WeaponAmmoOption>();

            string strAmmoCategory = GetWeaponAmmoCategory(
                GetValue(objWeapon, "name", string.Empty), GetValue(objWeapon, "category", string.Empty));

            CharacterOptions objOptions = GetCharacterOptions();
            bool blnExcludeStickNShock = objOptions.RestrictStickNShock
                && objOptions.StickNShockExcludedWeaponCategories.Contains(strAmmoCategory);

            var lstOptions = new List<WeaponAmmoOption>();
            int intGearId = 0;
            foreach (XmlNode objGearNode in EnumerateGearNodesDfs())
            {
                int intThisId = intGearId++;
                string strCategory = GetValue(objGearNode, "category", string.Empty);
                int intQty = int.TryParse(GetValue(objGearNode, "qty", "0"), out var q) ? q : 0;
                if (strCategory != "Ammunition" || intQty <= 0)
                    continue;

                string strName = GetValue(objGearNode, "name", string.Empty);
                if (blnExcludeStickNShock && string.Equals(strName, "Ammo: Stick-n-Shock", StringComparison.Ordinal))
                    continue;
                if (!IsAmmunitionCompatible(strName, strAmmoCategory))
                    continue;

                lstOptions.Add(new WeaponAmmoOption(intThisId, strName, intQty));
            }

            return lstOptions;
        }

        /// <summary>Parses a weapon's raw rules-data-derived Ammo string (e.g. "30(c)", "10(c) or
        /// external source", "6(cy)/2(belt)") into the whole-round-count choices it offers, same as
        /// frmCareer.cs's own cboType population - "external source" alternatives are dropped since
        /// this port has no External Source concept.</summary>
        public IReadOnlyList<int> GetWeaponAmmoCapacityChoices(Guid guiWeaponId)
        {
            XmlNode? objWeapon = FindWeaponNodeByGuid(guiWeaponId);
            var lstChoices = new List<int>();
            if (objWeapon == null)
                return lstChoices;

            string strAmmo = GetValue(objWeapon, "ammo", string.Empty);
            if (string.IsNullOrEmpty(strAmmo))
                return lstChoices;

            foreach (string strPart in strAmmo.Split(new[] { " or ", "/" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string strTrimmed = strPart.Trim();
                int intParenIndex = strTrimmed.IndexOf('(');
                if (intParenIndex >= 0)
                    strTrimmed = strTrimmed.Substring(0, intParenIndex);
                if (int.TryParse(strTrimmed, out int intCount) && intCount > 0)
                    lstChoices.Add(intCount);
            }

            return lstChoices;
        }

        /// <summary>Loads a Weapon with a chosen amount of a chosen Ammo Gear item - ported from
        /// frmCareer.cs's "Buy Ammo"/reload click handlers: any rounds still in the Weapon's
        /// previous load are returned to that Gear item's Quantity first (matching legacy's "return
        /// unspent rounds to the Ammo" step), then <paramref name="intCount"/> rounds are consumed
        /// from <paramref name="intAmmoGearId"/>'s Quantity - clamped to whatever's actually left if
        /// that's less than requested, same as legacy ("use whatever is left") rather than failing.</summary>
        public bool ReloadWeapon(Guid guiWeaponId, int intAmmoGearId, int intCount)
        {
            XmlNode? objWeapon = FindWeaponNodeByGuid(guiWeaponId);
            XmlNode? objAmmoGear = GetGearNodeById(intAmmoGearId);
            if (objWeapon == null || objAmmoGear == null || intCount <= 0)
                return false;

            // Return any rounds unspent from the weapon's current load first - if that's the same
            // Gear item being reloaded from, its just-restored Quantity is what the clamp below sees.
            ReturnUnspentAmmoToItsSource(objWeapon);

            int intAmmoQty = int.TryParse(GetValue(objAmmoGear, "qty", "0"), out var qAvail) ? qAvail : 0;
            int intLoaded = Math.Min(intCount, intAmmoQty);
            if (intLoaded <= 0)
                return false;

            SetChildValue(objAmmoGear, "qty", (intAmmoQty - intLoaded).ToString(CultureInfo.InvariantCulture));
            SetChildValue(objWeapon, "ammoloaded", intAmmoGearId.ToString(CultureInfo.InvariantCulture));
            SetChildValue(objWeapon, "ammoremaining", intLoaded.ToString(CultureInfo.InvariantCulture));
            Changed?.Invoke();
            return true;
        }

        private void ReturnUnspentAmmoToItsSource(XmlNode objWeapon)
        {
            int intPreviousGearId = int.TryParse(GetValue(objWeapon, "ammoloaded", "-1"), out var g) ? g : -1;
            int intUnspent = int.TryParse(GetValue(objWeapon, "ammoremaining", "0"), out var r) ? r : 0;
            if (intPreviousGearId < 0 || intUnspent <= 0)
                return;

            XmlNode? objPreviousGear = GetGearNodeById(intPreviousGearId);
            if (objPreviousGear == null)
                return;

            int intPreviousQty = int.TryParse(GetValue(objPreviousGear, "qty", "0"), out var q) ? q : 0;
            SetChildValue(objPreviousGear, "qty", (intPreviousQty + intUnspent).ToString(CultureInfo.InvariantCulture));
        }

        private string ComputeAmmoStatus(XmlNode objWeapon)
        {
            int intGearId = int.TryParse(GetValue(objWeapon, "ammoloaded", "-1"), out var g) ? g : -1;
            int intRemaining = int.TryParse(GetValue(objWeapon, "ammoremaining", "0"), out var r) ? r : 0;
            if (intGearId < 0)
                return string.Empty;

            XmlNode? objGear = GetGearNodeById(intGearId);
            string strName = objGear != null ? GetValue(objGear, "name", string.Empty) : string.Empty;
            return string.IsNullOrEmpty(strName) ? string.Empty : intRemaining + " (" + strName + ")";
        }

        /// <summary>Ported from frmNaturalWeapon.cs: manually defines a melee Weapon for an adept/
        /// critter power (e.g. Claws) instead of picking one from weapons.xml - assembles the
        /// Damage Value from a base (a fixed rating or "(STR/2)"), an optional +/- modifier, and a
        /// P/S type, an AP value, and links it to a player-chosen Combat Active Skill (persisted as
        /// the new UseSkill override <see cref="ComputeWeaponDicePool"/> now understands) instead
        /// of relying on the weapon's Category. Source/page are copied from critterpowers.xml's
        /// "Natural Weapon" power entry, matching legacy. Always Avail 0/Cost 0, like legacy.</summary>
        public bool AddNaturalWeapon(string strName, string strUseSkill, string strDvBase, int intDvMod,
            string strDvType, int intAp, int intReach)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            string strDamage = strDvBase;
            if (intDvMod > 0)
                strDamage += "+" + intDvMod;
            else if (intDvMod < 0)
                strDamage += intDvMod.ToString(CultureInfo.InvariantCulture);
            strDamage += strDvType;

            string strAp = intAp switch
            {
                0 => "0",
                > 0 => "+" + intAp.ToString(CultureInfo.InvariantCulture),
                _ => intAp.ToString(CultureInfo.InvariantCulture)
            };

            XmlDocument objPowersDoc = XmlManager.Instance.Load("critterpowers.xml");
            XmlNode? objPower = objPowersDoc.SelectSingleNode("/chummer/powers/power[name = \"Natural Weapon\"]");
            string strSource = objPower?["source"]?.InnerText ?? string.Empty;
            string strPage = objPower?["page"]?.InnerText ?? string.Empty;

            AddWeapon(strName, "Natürliche Waffe", strDamage, strAp, "0", "0", string.Empty, "0", "0",
                strSource, strPage, strUseSkill, intReach.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>Named weapon locations remain persisted even while empty.</summary>
        public IReadOnlyList<string> WeaponLocations => (IReadOnlyList<string>?)Document.SelectNodes("/character/weaponlocations/weaponlocation")?
            .Cast<XmlNode>().Select(objNode => objNode.InnerText).Where(strName => !string.IsNullOrWhiteSpace(strName))
            .Distinct(StringComparer.Ordinal).ToList() ?? Array.Empty<string>();

        public bool AddWeaponLocation(string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0 || WeaponLocations.Contains(strName, StringComparer.Ordinal)) return false;
            var objRoot = Document.DocumentElement;
            if (objRoot == null) return false;
            XmlElement? objLocations = objRoot.SelectSingleNode("weaponlocations") as XmlElement;
            if (objLocations == null)
            {
                objLocations = Document.CreateElement("weaponlocations");
                objRoot.AppendChild(objLocations);
            }
            AppendElement(objLocations, "weaponlocation", strName);
            Changed?.Invoke();
            return true;
        }

        public bool SetWeaponLocation(string strName, string strCategory, string strLocation)
        {
            strLocation = strLocation.Trim();
            if (!string.IsNullOrEmpty(strLocation) && !WeaponLocations.Contains(strLocation, StringComparer.Ordinal)) return false;
            XmlNodeList? objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return false;
            foreach (XmlNode objWeapon in objNodes)
            {
                if (!string.Equals(GetValue(objWeapon, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objWeapon, "category", string.Empty), strCategory, StringComparison.Ordinal)) continue;
                if (string.Equals(GetValue(objWeapon, "location", string.Empty), strLocation, StringComparison.Ordinal)) return false;
                SetChildValue(objWeapon, "location", strLocation);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Moves a root weapon immediately before another root weapon. Weapon locations
        /// are display-only groups, so both items remain assigned to their existing locations.</summary>
        public bool MoveWeapon(Guid guiSourceWeaponId, Guid guiTargetWeaponId)
        {
            XmlNode? objSource = GetWeaponNodeByGuid(guiSourceWeaponId);
            XmlNode? objTarget = GetWeaponNodeByGuid(guiTargetWeaponId);
            if (objSource == null || objTarget == null || objSource == objTarget
                || objSource.ParentNode == null || objSource.ParentNode != objTarget.ParentNode)
                return false;

            objSource.ParentNode.RemoveChild(objSource);
            objTarget.ParentNode.InsertBefore(objSource, objTarget);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveWeaponLocation(string strName)
        {
            strName = strName.Trim();
            XmlNode? objLocation = Document.SelectNodes("/character/weaponlocations/weaponlocation")?.Cast<XmlNode>()
                .FirstOrDefault(objNode => string.Equals(objNode.InnerText, strName, StringComparison.Ordinal));
            if (objLocation?.ParentNode == null) return false;
            objLocation.ParentNode.RemoveChild(objLocation);
            XmlNodeList? objWeapons = Document.SelectNodes("/character/weapons/weapon");
            if (objWeapons != null)
                foreach (XmlNode objWeapon in objWeapons)
                    if (string.Equals(GetValue(objWeapon, "location", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objWeapon, "location", string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first root-level saved weapon matching its name/category.</summary>
        public bool RemoveWeapon(string strName, string strCategory)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null)
                return false;

            foreach (XmlNode objWeapon in objNodes)
            {
                if (!string.Equals(GetValue(objWeapon, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objWeapon, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;

                objWeapon.ParentNode?.RemoveChild(objWeapon);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root Weapon
        /// (including its own installed accessories/mods/loaded gear cost).</summary>
        public bool SellWeapon(string strName, string strCategory, double dblSellPercent)
        {
            XmlNode? objNode = Document.SelectNodes("/character/weapons/weapon")?.Cast<XmlNode>()
                .FirstOrDefault(n => string.Equals(GetValue(n, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(n, "category", string.Empty), strCategory, StringComparison.Ordinal));
            if (objNode == null)
                return false;

            int intRefund = ComputeSellRefund(
                ReadTreeItem(objNode, "accessories/accessory", "weaponmods/weaponmod", "gears/gear", "ammos/ammo").CalculatedCost,
                dblSellPercent);
            if (!RemoveWeapon(strName, strCategory))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        /// <summary>Sets the equipped state of the first root-level weapon matching name and category.</summary>
        public bool SetWeaponEquipped(string strName, string strCategory, bool blnEquipped)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return false;
            foreach (XmlNode objWeapon in objNodes)
            {
                if (!string.Equals(GetValue(objWeapon, "name", string.Empty), strName, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objWeapon, "category", string.Empty), strCategory, StringComparison.Ordinal)) continue;
                SetChildValue(objWeapon, "equipped", blnEquipped ? "True" : "False");
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Updates a root weapon's free-form notes using the same transient root index
        /// as the tree reader. This preserves duplicate purchases that share name/category and
        /// does not require adding a GUID to old character files.</summary>
        public bool SetWeaponNotes(int intWeaponId, string strNotes)
        {
            XmlNode? objWeapon = GetWeaponNodeById(intWeaponId);
            if (objWeapon == null)
                return false;

            SetChildValue(objWeapon, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Updates notes for a saved item installed in a root weapon by its own GUID,
        /// scoped to that weapon. Accessories, modifications, attached gear, and stored ammo are
        /// all distinct purchases even when their names match.</summary>
        public bool SetWeaponChildNotes(Guid guiWeaponId, Guid guiItemId, string strNotes)
        {
            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objPart = objWeapon?.SelectSingleNode(
                $"accessories/accessory[guid = '{guiItemId}']")
                ?? objWeapon?.SelectSingleNode($"weaponmods/weaponmod[guid = '{guiItemId}']")
                ?? objWeapon?.SelectSingleNode($"gears/gear[guid = '{guiItemId}']")
                ?? objWeapon?.SelectSingleNode($"ammos/ammo[guid = '{guiItemId}']");
            if (objPart == null)
                return false;

            SetChildValue(objPart, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Backward-compatible accessory/modification-specific entry point.</summary>
        public bool SetWeaponPartNotes(Guid guiWeaponId, Guid guiPartId, string strNotes)
        {
            return SetWeaponChildNotes(guiWeaponId, guiPartId, strNotes);
        }

        /// <summary>Updates the legacy player-facing <c>weaponname</c> field, keeping the raw
        /// name used for data lookups and calculations unchanged.</summary>
        public bool SetWeaponCustomName(int intWeaponId, string strCustomName)
        {
            XmlNode? objWeapon = GetWeaponNodeById(intWeaponId);
            if (objWeapon == null)
                return false;

            SetChildValue(objWeapon, "weaponname", strCustomName?.Trim() ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetWeaponNodeById(int intWeaponId)
        {
            if (intWeaponId < 0)
                return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/weapons/weapon");
            return objNodes != null && intWeaponId < objNodes.Count ? objNodes[intWeaponId] : null;
        }

        private XmlNode? GetWeaponNodeByGuid(Guid guiWeaponId)
            => Document.SelectSingleNode($"/character/weapons/weapon[guid = '{guiWeaponId}']");

        /// <summary>Same as <see cref="GetWeaponNodeByGuid"/> but also matches vehicle-mounted
        /// Weapons - used by the ammo-tracking methods (ReloadWeapon/GetWeaponAmmoOptions/
        /// GetWeaponAmmoCapacityChoices), which work the same way for either.</summary>
        private XmlNode? FindWeaponNodeByGuid(Guid guiWeaponId)
            => GetWeaponNodeByGuid(guiWeaponId)
                ?? Document.SelectSingleNode($"/character/vehicles/vehicle/weapons/weapon[guid = '{guiWeaponId}']");

        /// <summary>Adds a Weapon Accessory (ported from clsEquipment.cs's WeaponAccessory.Save) to
        /// a root-level weapon, deducting its cost. Rejects if the weapon's mount-slot eligibility
        /// check (<see cref="WeaponAllowsAccessoryMount"/>) fails.</summary>
        public bool AddWeaponAccessory(Guid guiWeaponId, string strName, string strMount, string strRc,
            string strAvail, string strCost, string strSource, string strPage, string strRcGroup = "0")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A weapon accessory name is required.", nameof(strName));

            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objAccessories = objWeapon?.SelectSingleNode("accessories");
            if (objWeapon == null || objAccessories == null || !WeaponAllowsAccessoryMount(objWeapon, strMount))
                return false;

            var objAccessory = Document.CreateElement("accessory");
            AppendElement(objAccessory, "guid", Guid.NewGuid().ToString());
            AppendElement(objAccessory, "name", strName.Trim());
            AppendElement(objAccessory, "mount", strMount);
            AppendElement(objAccessory, "rc", strRc);
            AppendElement(objAccessory, "rcgroup", strRcGroup);
            AppendElement(objAccessory, "avail", strAvail);
            AppendElement(objAccessory, "cost", strCost);
            AppendElement(objAccessory, "included", "False");
            AppendElement(objAccessory, "installed", "True");
            AppendElement(objAccessory, "source", strSource);
            AppendElement(objAccessory, "page", strPage);
            objAccessory.AppendChild(Document.CreateElement("gears"));
            objAccessories.AppendChild(objAccessory);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds Gear below a weapon accessory, preserving legacy
        /// <c>accessory/gears/gear</c> containment.</summary>
        public bool AddWeaponAccessoryGear(Guid guiWeaponId, Guid guiAccessoryId, string strName,
            string strCategory, string strRating = "0", string strQty = "1", string strCost = "",
            string strAvail = "", string strSource = "", string strPage = "", string strCapacity = "")
        {
            XmlNode? objAccessory = GetWeaponNodeByGuid(guiWeaponId)?.SelectSingleNode(
                $"accessories/accessory[guid = '{guiAccessoryId}']");
            if (objAccessory == null || string.IsNullOrWhiteSpace(strName))
                return false;
            XmlElement objGears = objAccessory.SelectSingleNode("gears") as XmlElement
                ?? (XmlElement)objAccessory.AppendChild(Document.CreateElement("gears"));
            XmlElement objGear = AppendGearNode(objGears, strName, strCategory, strRating, strQty, strCost,
                strAvail, strSource, strPage, strCapacity, string.Empty, string.Empty, string.Empty, string.Empty);
            AppendAutomaticProgramOptions(objGear);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveWeaponAccessoryGear(Guid guiWeaponId, Guid guiAccessoryId, Guid guiGearId)
        {
            XmlNode? objGear = GetWeaponNodeByGuid(guiWeaponId)?.SelectSingleNode(
                $"accessories/accessory[guid = '{guiAccessoryId}']/gears/gear[guid = '{guiGearId}']");
            if (objGear?.ParentNode == null)
                return false;
            objGear.ParentNode.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveWeaponAccessory(Guid guiWeaponId, Guid guiAccessoryId)
        {
            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objAccessory = objWeapon?.SelectSingleNode($"accessories/accessory[guid = '{guiAccessoryId}']");
            if (objAccessory?.ParentNode == null)
                return false;
            objAccessory.ParentNode.RemoveChild(objAccessory);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Changes whether an accessory or weapon modification is included in its base
        /// weapon. Included mods do not consume the normal six modification slots, so moving one
        /// out of the base weapon also observes EnforceCapacity.</summary>
        public bool SetWeaponPartIncluded(Guid guiWeaponId, Guid guiPartId, bool blnIncluded)
        {
            if (!AllowEditPartOfBaseWeaponEnabled)
                return false;

            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            if (objWeapon == null)
                return false;
            XmlNode? objPart = objWeapon.SelectSingleNode($"accessories/accessory[guid = '{guiPartId}']")
                ?? objWeapon.SelectSingleNode($"weaponmods/weaponmod[guid = '{guiPartId}']");
            if (objPart == null)
                return false;

            bool blnIsMod = string.Equals(objPart.Name, "weaponmod", StringComparison.Ordinal);
            if (!blnIncluded && blnIsMod && GetCharacterOptions().EnforceCapacity
                && !WeaponHasModSlotsAvailable(objWeapon, GetValue(objPart, "slots", "0")))
                return false;

            SetChildValue(objPart, "included", blnIncluded.ToString());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from frmCareer.cs's tsWeaponAddAccessory_Click's mount-list check: looks
        /// up the weapon's own rules-data entry in weapons.xml for &lt;allowaccessory&gt; and its
        /// &lt;accessorymounts&gt; list, and requires the accessory's own (possibly "/"-separated)
        /// mount to intersect with it. An accessory with an empty mount is always allowed (matches
        /// legacy's "mount = ''" fallback in its own picker filter). Doesn't enforce exclusivity
        /// between multiple accessories sharing the same mount - legacy doesn't either.</summary>
        private static bool WeaponAllowsAccessoryMount(XmlNode objWeapon, string strAccessoryMount)
        {
            string strWeaponName = GetValue(objWeapon, "name", string.Empty);
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");
            XmlNode? objXmlWeapon = objWeaponsDoc.SelectSingleNode(
                $"/chummer/weapons/weapon[name = '{strWeaponName}']");
            if (objXmlWeapon == null)
                return true; // Unknown to rules data (e.g. a hand-entered weapon) - don't block it.

            if (string.Equals(GetValue(objXmlWeapon, "allowaccessory", "True"), "False",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(strAccessoryMount))
                return true;

            var setAllowedMounts = new HashSet<string>(StringComparer.Ordinal);
            XmlNodeList? objMountNodes = objXmlWeapon.SelectNodes("accessorymounts/mount");
            if (objMountNodes != null)
                foreach (XmlNode objMountNode in objMountNodes)
                    setAllowedMounts.Add(objMountNode.InnerText);

            return strAccessoryMount.Split('/').Any(setAllowedMounts.Contains);
        }

        /// <summary>Adds a Weapon Modification (ported from clsEquipment.cs's WeaponMod.Save) to a
        /// root-level weapon, deducting its cost - <paramref name="strCost"/> may reference "Weapon
        /// Cost" (substituted with the weapon's own saved cost, matching clsEquipment.cs's mod cost
        /// formulas) and/or "Rating" (substituted by <see cref="RatingExpression"/>). Ported from
        /// frmCareer.cs's tsWeaponAddModification_Click: rejects if the weapon's own rules-data
        /// entry sets &lt;allowmod&gt; to false, and - when EnforceCapacity is on - rejects if
        /// installed non-included mods' slots plus this one would exceed the fixed 6-slot cap every
        /// weapon has (clsEquipment.cs's Weapon.SlotsRemaining hardcodes this as a constant, not a
        /// per-weapon data field).</summary>
        public bool AddWeaponMod(Guid guiWeaponId, string strName, string strRating, string strSlots,
            string strAvail, string strCost, string strSource, string strPage, string strRc = "",
            string strRcGroup = "0")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A weapon mod name is required.", nameof(strName));

            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objMods = objWeapon?.SelectSingleNode("weaponmods");
            if (objWeapon == null || objMods == null || !WeaponAllowsMods(objWeapon))
                return false;
            if (GetCharacterOptions().EnforceCapacity && !WeaponHasModSlotsAvailable(objWeapon, strSlots))
                return false;

            var objMod = Document.CreateElement("weaponmod");
            AppendElement(objMod, "guid", Guid.NewGuid().ToString());
            AppendElement(objMod, "name", strName.Trim());
            AppendElement(objMod, "rating", strRating);
            AppendElement(objMod, "slots", strSlots);
            AppendElement(objMod, "avail", strAvail);
            AppendElement(objMod, "cost", strCost);
            AppendElement(objMod, "rc", strRc);
            AppendElement(objMod, "rcgroup", strRcGroup);
            AppendElement(objMod, "included", "False");
            AppendElement(objMod, "installed", "True");
            AppendElement(objMod, "source", strSource);
            AppendElement(objMod, "page", strPage);
            objMods.AppendChild(objMod);

            string strWeaponCost = GetValue(objWeapon, "cost", "0");
            DeductGearCost(strCost.Replace("Weapon Cost", strWeaponCost, StringComparison.OrdinalIgnoreCase),
                strRating, "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        private static bool WeaponAllowsMods(XmlNode objWeapon)
        {
            string strWeaponName = GetValue(objWeapon, "name", string.Empty);
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");
            XmlNode? objXmlWeapon = objWeaponsDoc.SelectSingleNode(
                $"/chummer/weapons/weapon[name = '{strWeaponName}']");
            return objXmlWeapon == null || !string.Equals(GetValue(objXmlWeapon, "allowmod", "True"), "False",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool WeaponHasModSlotsAvailable(XmlNode objWeapon, string strNewSlots)
        {
            const int intMaxSlots = 6;
            double dblUsed = 0;
            XmlNodeList? objModNodes = objWeapon.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    if (GetValue(objModNode, "included", "False") != "True"
                        && double.TryParse(GetValue(objModNode, "slots", "0"), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var dblSlots))
                        dblUsed += dblSlots;

            double dblNew = double.TryParse(strNewSlots, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var dblParsedNew) ? dblParsedNew : 0;
            return dblUsed + dblNew <= intMaxSlots;
        }

        public bool RemoveWeaponMod(Guid guiWeaponId, Guid guiModId)
        {
            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objMod = objWeapon?.SelectSingleNode($"weaponmods/weaponmod[guid = '{guiModId}']");
            if (objMod?.ParentNode == null)
                return false;
            objMod.ParentNode.RemoveChild(objMod);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a root-level Armor in the minimal saved-character tree shape used by
        /// <see cref="Armor"/> and the armor-encumbrance calc - <paramref name="strB"/>/
        /// <paramref name="strI"/> are the ballistic/impact ratings copied as-is from armor.xml
        /// (including a leading "+" for stacking bonus armor, same as the encumbrance calc already
        /// handles via ParseArmorRating).</summary>
        private IReadOnlyList<CharacterTreeItemData>? _cachedGear;
        public IReadOnlyList<CharacterTreeItemData> Gear
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedGear short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedGear ??= ReadGearTree();
            }
        }

        /// <summary>Named storage locations for root-level Gear (ported from frmCareer.cs's
        /// cmdAddLocation_Click) - existing while empty, same as WeaponLocations.</summary>
        private IReadOnlyList<CharacterWeaponData>? _cachedWeapons;
        public IReadOnlyList<CharacterWeaponData> Weapons
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedWeapons short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedWeapons ??= ReadWeapons();
            }
        }

        /// <summary>Weapons with their installed accessories, modifications, and mounted gear.</summary>
        private IReadOnlyList<CharacterTreeItemData>? _cachedWeaponTrees;
        public IReadOnlyList<CharacterTreeItemData> WeaponTrees
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedWeaponTrees short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedWeaponTrees ??= ReadWeaponTrees();
            }
        }

        public int GetActiveSkillSpecializationKarmaCost() => GetCharacterOptions().KarmaSpecialization;

        /// <summary>Adds a new Exotic Active Skill (e.g. "Exotic Ranged Weapon (Bow)" - the
        /// specialization holds the "(Bow)" sub-type). Starts at rating 0; the first point costs
        /// KarmaNewActiveSkill like any other new active skill when raised.</summary>
        private IReadOnlyList<CharacterTreeItemData> ReadWeaponTrees()
        {
            var lstWeapons = new List<CharacterTreeItemData>();
            var dicLocations = new Dictionary<string, CharacterTreeItemData>(StringComparer.Ordinal);
            foreach (string strLocation in WeaponLocations)
            {
                var objLocation = new CharacterTreeItemData(strLocation, "Weapon location");
                dicLocations.Add(strLocation, objLocation);
                lstWeapons.Add(objLocation);
            }
            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return lstWeapons;
            int intWeaponId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                var objWeapon = ReadTreeItem(objNode, "accessories/accessory", "weaponmods/weaponmod", "gears/gear", "ammos/ammo", "underbarrel/weapon");
                objWeapon.SetWeaponId(intWeaponId++);
                objWeapon.SetCustomName(GetValue(objNode, "weaponname", string.Empty));
                MarkWeaponAccessoriesAndMods(objWeapon, objNode);
                AttachWeaponAccessoryGear(objWeapon, objNode);
                MarkUnderbarrelWeapons(objWeapon, objNode);
                string strLocation = GetValue(objNode, "location", string.Empty);
                objWeapon.SetLocation(strLocation);
                (string strPoolDisplay, string strTooltip) = ComputeWeaponDicePool(
                    GetValue(objNode, "category", string.Empty), GetValue(objNode, "name", string.Empty),
                    WeaponNodeHasSmartgun(objNode), objNode);
                objWeapon.SetWeaponDicePool(strPoolDisplay, strTooltip);
                objWeapon.SetWeaponRc(ComputeWeaponTotalRc(objNode));
                objWeapon.SetWeaponDamage(ComputeWeaponDamage(objNode));
                objWeapon.SetAmmoStatus(ComputeAmmoStatus(objNode));
                if (!string.IsNullOrEmpty(strLocation) && dicLocations.TryGetValue(strLocation, out var objLocation))
                    objLocation.Children.Add(objWeapon);
                else
                    lstWeapons.Add(objWeapon);
            }
            return lstWeapons;
        }

        /// <summary>Flags <paramref name="objWeapon"/>'s direct children that came from
        /// &lt;accessories&gt;/&lt;weaponmods&gt; (matched by guid against the raw XML, since
        /// ReadTreeItem's Children collection loses which xpath each one was read from) so the UI
        /// can tell them apart from the weapon's Gear/Ammo children.</summary>
        private static void MarkWeaponAccessoriesAndMods(CharacterTreeItemData objWeapon, XmlNode objWeaponNode)
        {
            var setAccessoryGuids = new HashSet<string>(StringComparer.Ordinal);
            var objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                    setAccessoryGuids.Add(GetValue(objAccessoryNode, "guid", string.Empty));

            var setModGuids = new HashSet<string>(StringComparer.Ordinal);
            var objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    setModGuids.Add(GetValue(objModNode, "guid", string.Empty));

            foreach (CharacterTreeItemData objChild in objWeapon.Children)
            {
                if (!string.IsNullOrEmpty(objChild.ItemGuid) && setAccessoryGuids.Contains(objChild.ItemGuid))
                    objChild.IsWeaponAccessory = true;
                else if (!string.IsNullOrEmpty(objChild.ItemGuid) && setModGuids.Contains(objChild.ItemGuid))
                    objChild.IsWeaponMod = true;

                if (objChild.IsWeaponPart)
                {
                    XmlNode? objPart = objWeaponNode.SelectSingleNode($"accessories/accessory[guid = '{objChild.ItemGuid}']")
                        ?? objWeaponNode.SelectSingleNode($"weaponmods/weaponmod[guid = '{objChild.ItemGuid}']");
                    objChild.SetWeaponPartIncluded(objPart != null && GetValue(objPart, "included", "False") == "True");
                }
            }
        }

        private static void MarkUnderbarrelWeapons(CharacterTreeItemData objWeapon, XmlNode objWeaponNode)
        {
            var setUnderbarrelGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode objNode in objWeaponNode.SelectNodes("underbarrel/weapon")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
                setUnderbarrelGuids.Add(GetValue(objNode, "guid", string.Empty));
            foreach (CharacterTreeItemData objChild in objWeapon.Children)
                if (!string.IsNullOrEmpty(objChild.ItemGuid) && setUnderbarrelGuids.Contains(objChild.ItemGuid))
                    objChild.IsUnderbarrelWeapon = true;
        }

        private static void AttachWeaponAccessoryGear(CharacterTreeItemData objWeapon, XmlNode objWeaponNode)
        {
            foreach (XmlNode objAccessory in objWeaponNode.SelectNodes("accessories/accessory")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strGuid = GetValue(objAccessory, "guid", string.Empty);
                CharacterTreeItemData? objTreeAccessory = objWeapon.Children.FirstOrDefault(
                    objChild => objChild.IsWeaponAccessory && objChild.ItemGuid == strGuid);
                if (objTreeAccessory == null)
                    continue;
                foreach (XmlNode objGear in objAccessory.SelectNodes("gears/gear")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                    objTreeAccessory.Children.Add(ReadTreeItem(objGear, "children/gear"));
            }
        }

        private bool WeaponNodeHasSmartgun(XmlNode objWeaponNode)
        {
            var objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                    if (GetValue(objAccessoryNode, "name", string.Empty).StartsWith("Smartgun System", StringComparison.Ordinal)
                        && GetValue(objAccessoryNode, "installed", "True") == "True")
                        return true;
            var objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    if (GetValue(objModNode, "name", string.Empty).StartsWith("Smartgun System", StringComparison.Ordinal)
                        && GetValue(objModNode, "installed", "True") == "True")
                        return true;
            return false;
        }

        private IReadOnlyList<CharacterWeaponData> ReadWeapons()
        {
            var lstWeapons = new List<CharacterWeaponData>();
            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return lstWeapons;
            foreach (XmlNode objNode in objNodes)
            {
                string strName = GetValue(objNode, "name", string.Empty);
                string strCategory = GetValue(objNode, "category", string.Empty);
                (string strPoolDisplay, string strTooltip) = ComputeWeaponDicePool(strCategory, strName,
                    WeaponNodeHasSmartgun(objNode), objNode);

                lstWeapons.Add(new CharacterWeaponData(strName, strCategory, ComputeWeaponDamage(objNode),
                    GetValue(objNode, "ammo", string.Empty), GetValue(objNode, "ap", string.Empty),
                    ComputeWeaponTotalRc(objNode), strPoolDisplay, strTooltip));
            }
            return lstWeapons;
        }

        /// <summary>Applies More Lethal Gameplay's +2 DV to ordinary numeric damage codes.
        /// Special nonnumeric codes retain their saved text, matching the legacy fallback.</summary>
        private string ComputeWeaponDamage(XmlNode objWeaponNode)
        {
            string strDamage = GetValue(objWeaponNode, "damage", string.Empty);
            if (!GetCharacterOptions().MoreLethalGameplay)
                return strDamage;

            Match objMatch = Regex.Match(strDamage, @"^\s*(?<value>\d+)(?<suffix>.*)$");
            return objMatch.Success && int.TryParse(objMatch.Groups["value"].Value, out int intDamage)
                ? (intDamage + 2).ToString(CultureInfo.InvariantCulture) + objMatch.Groups["suffix"].Value
                : strDamage;
        }

        /// <summary>Ported from clsEquipment.cs's Weapon.TotalRC: the weapon's own base
        /// &lt;rc&gt; (which may be "x", "(x)" - entirely from removable parts - or "x(y)" - a
        /// fixed x plus removable y) plus installed Accessories'/Mods' own &lt;rc&gt; contributions,
        /// the Strength-affects-recoil house rule, and the RestrictRecoil house rule's
        /// per-mount-point-group ("RC Group" 1-5) cap - only the single highest &lt;rc&gt; value
        /// within each group counts when the house rule is on, instead of every item in that group
        /// stacking freely; a Foregrip+Sling combo in Group 1 is guaranteed at least 2 either way
        /// (SR4 83). "x(y)"-form items only ever contribute to the removable/full total, never the
        /// fixed base (legacy's own asymmetry, preserved here). Loaded ammo's own
        /// &lt;weaponbonus&gt;&lt;rc&gt; (e.g. Ammo: Gel Rounds) also adds to both totals, mirroring
        /// the dice-pool equivalent (see SumLoadedAmmoDicePoolBonus).</summary>
        private string ComputeWeaponTotalRc(XmlNode objWeaponNode)
        {
            string strRc = GetValue(objWeaponNode, "rc", "0");
            (int intRcBase, int intRcFull) = ParseRc(strRc);

            var dicGroupMax = new int[6]; // index 1-5 used, matching legacy's RC Group numbering.
            bool blnHasForegrip = false;
            bool blnHasSling = false;

            void ProcessItem(string strItemRc, int intRcGroup, string strItemName)
            {
                if (string.IsNullOrEmpty(strItemRc))
                    return;

                if (GetCharacterOptions().RestrictRecoil && intRcGroup != 0)
                {
                    int intItemRc = int.TryParse(strItemRc.Replace("(", string.Empty).Replace(")", string.Empty),
                        out var i) ? i : 0;
                    if (intRcGroup is >= 1 and <= 5 && dicGroupMax[intRcGroup] < intItemRc)
                        dicGroupMax[intRcGroup] = intItemRc;
                    if (intRcGroup == 1)
                    {
                        if (strItemName == "Foregrip") blnHasForegrip = true;
                        if (strItemName == "Sling") blnHasSling = true;
                    }
                }
                else
                {
                    (int intItemBase, int intItemFull) = ParseRc(strItemRc);
                    intRcBase += intItemBase;
                    intRcFull += intItemFull;
                }
            }

            XmlNodeList? objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                    if (GetValue(objAccessoryNode, "installed", "True") == "True")
                        ProcessItem(GetValue(objAccessoryNode, "rc", string.Empty),
                            int.TryParse(GetValue(objAccessoryNode, "rcgroup", "0"), out var g) ? g : 0,
                            GetValue(objAccessoryNode, "name", string.Empty));

            XmlNodeList? objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    if (GetValue(objModNode, "installed", "True") == "True")
                        ProcessItem(GetValue(objModNode, "rc", string.Empty),
                            int.TryParse(GetValue(objModNode, "rcgroup", "0"), out var g) ? g : 0,
                            GetValue(objModNode, "name", string.Empty));

            if (blnHasForegrip && blnHasSling && dicGroupMax[1] < 2)
                dicGroupMax[1] = 2;

            int intGroupTotal = dicGroupMax[1] + dicGroupMax[2] + dicGroupMax[3] + dicGroupMax[4] + dicGroupMax[5];
            intRcBase += intGroupTotal;
            intRcFull += intGroupTotal;

            if (GetCharacterOptions().StrengthAffectsRecoil)
            {
                int intStr = GetAttributeInt("STR");
                int intStrBonus = intStr switch
                {
                    >= 18 => 4,
                    >= 14 => 3,
                    >= 10 => 2,
                    >= 6 => 1,
                    _ => 0,
                };
                intRcBase += intStrBonus;
                intRcFull += intStrBonus;
            }

            int intAmmoRcBonus = SumLoadedAmmoRcBonus(objWeaponNode);
            intRcBase += intAmmoRcBonus;
            intRcFull += intAmmoRcBonus;

            return intRcFull.ToString();
        }

        /// <summary>Ported from clsEquipment.cs's Weapon.TotalRC: the currently loaded ammo Gear
        /// item's rules-data &lt;weaponbonus&gt;/&lt;rc&gt; value, if any.</summary>
        private int SumLoadedAmmoRcBonus(XmlNode objWeaponNode)
        {
            int intGearId = int.TryParse(GetValue(objWeaponNode, "ammoloaded", "-1"), out var g) ? g : -1;
            if (intGearId < 0)
                return 0;

            XmlNode? objAmmoGear = GetGearNodeById(intGearId);
            string strAmmoName = objAmmoGear != null ? GetValue(objAmmoGear, "name", string.Empty) : string.Empty;
            if (string.IsNullOrEmpty(strAmmoName))
                return 0;

            XmlDocument objGearDoc = XmlManager.Instance.Load("gear.xml");
            XmlNode? objXmlAmmo = objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strAmmoName}']");
            string strRc = objXmlAmmo?.SelectSingleNode("weaponbonus/rc")?.InnerText ?? string.Empty;
            return int.TryParse(strRc, out int intBonus) ? intBonus : 0;
        }

        /// <summary>Splits a weapon/accessory/mod RC string into its fixed ("base") and full
        /// (base + removable) components - "x" is both, "(x)" is entirely removable (base 0), and
        /// "x(y)" is a fixed x plus removable y. Ported from the parsing at the top of
        /// clsEquipment.cs's Weapon.TotalRC.</summary>
        private static readonly Dictionary<string, string> s_dicWeaponCategorySkills = new(StringComparer.Ordinal)
        {
            ["Bows"] = "Archery",
            ["Crossbows"] = "Archery",
            ["Assault Rifles"] = "Automatics",
            ["Machine Pistols"] = "Automatics",
            ["Submachine Guns"] = "Automatics",
            ["Battle Rifles"] = "Automatics",
            ["Blades"] = "Blades",
            ["Cyberware Blades"] = "Blades",
            ["Clubs"] = "Clubs",
            ["Assault Cannons"] = "Heavy Weapons",
            ["Grenade Launchers"] = "Heavy Weapons",
            ["Missile Launchers"] = "Heavy Weapons",
            ["Mortar Launchers"] = "Heavy Weapons",
            ["Light Machine Guns"] = "Heavy Weapons",
            ["Medium Machine Guns"] = "Heavy Weapons",
            ["Heavy Machine Guns"] = "Heavy Weapons",
            ["Shotguns"] = "Longarms",
            ["Sniper Rifles"] = "Longarms",
            ["Sports Rifles"] = "Longarms",
            ["Throwing Weapons"] = "Throwing Weapons",
            ["Cyberware Throwing Weapons"] = "Throwing Weapons",
            ["Unarmed"] = "Unarmed Combat",
            ["Cyberware Clubs"] = "Unarmed Combat",
            ["Cyberware"] = "Unarmed Combat"
        };

        // A Smartgun System only grants its bonus for skills SR4 actually pairs with smartguns.
        private static readonly HashSet<string> s_setSmartlinkEligibleSkills = new(StringComparer.Ordinal)
        {
            "Automatics", "Exotic Ranged Weapon", "Heavy Weapons", "Longarms", "Pistols"
        };

        private (string PoolDisplay, string Tooltip) ComputeWeaponDicePool(string strCategory, string strWeaponName,
            bool blnHasSmartgun, XmlNode? objWeaponNode = null)
        {
            // A per-weapon UseSkill override (e.g. a homebrew Natural Weapon linked to a chosen
            // Combat Active Skill, see AddNaturalWeapon) takes priority over the Category mapping.
            string strUseSkillOverride = objWeaponNode != null ? GetValue(objWeaponNode, "useskill", string.Empty) : string.Empty;

            // Ported from clsEquipment.cs's Weapon.DicePool: a "Special Weapons" Category (e.g.
            // flamethrowers, tasers) has no direct skill mapping of its own - it borrows its
            // Range field as a stand-in Category for the lookup below.
            if (strCategory == "Special Weapons" && objWeaponNode != null)
            {
                string strRange = GetValue(objWeaponNode, "range", string.Empty);
                if (!string.IsNullOrEmpty(strRange))
                    strCategory = strRange;
            }

            string strSkillName = !string.IsNullOrEmpty(strUseSkillOverride)
                ? strUseSkillOverride
                : s_dicWeaponCategorySkills.TryGetValue(strCategory, out var strMapped)
                    ? strMapped
                    : strCategory is "Exotic Melee Weapons" or "Exotic Ranged Weapons" or "Cyberware Exotic Melee Weapons"
                        or "Cyberware Exotic Ranged Weapons"
                        ? (strCategory.Contains("Melee") ? "Exotic Melee Weapon" : "Exotic Ranged Weapon")
                        : "Pistols";

            CharacterSkillData? objSkill = Skills.FirstOrDefault(s => s.Name == strSkillName
                && (!s.Exotic || s.Specialization == strWeaponName));
            if (objSkill == null)
                return (string.Empty, string.Empty);

            int intPool = int.TryParse(objSkill.TotalValue, out var intParsed) ? intParsed : 0;
            var sb = new StringBuilder();
            sb.Append("Fertigkeit: ").Append(objSkill.Name).Append(" (").Append(objSkill.TotalValue).Append(')');

            int intSmartlinkBonus = 0;
            if (blnHasSmartgun && s_setSmartlinkEligibleSkills.Contains(strSkillName))
            {
                intSmartlinkBonus = ImprovementManager.ValueOf(Improvements, ImprovementType.Smartlink);
                if (intSmartlinkBonus != 0)
                    sb.Append('\n').Append("Smartgun System: ").Append(FormatSigned(intSmartlinkBonus));
            }

            intPool += intSmartlinkBonus;

            if (objWeaponNode != null)
            {
                intPool += SumInstalledAccessoryAndModDicePool(objWeaponNode, sb);
                intPool += SumLoadedAmmoDicePoolBonus(objWeaponNode, sb);
            }

            string strDisplay = intPool.ToString();

            if (!string.IsNullOrEmpty(objSkill.Specialization)
                && (objSkill.Specialization == strWeaponName || objSkill.Specialization == strCategory))
            {
                strDisplay += " (" + (intPool + 2) + ")";
                sb.Append('\n').Append("Spezialisierung \"").Append(objSkill.Specialization).Append("\": +2");
            }

            sb.Append('\n').Append("Würfelpool: ").Append(strDisplay);
            return (strDisplay, sb.ToString());
        }

        /// <summary>Ported from clsEquipment.cs's Weapon.DicePool's Ammo-derived pool bonus: the
        /// currently loaded ammo Gear item's rules-data &lt;weaponbonus&gt;/&lt;pool&gt; value
        /// (e.g. Ammo: High-Power Rounds' -2, Ammo: Deathdealer's +1), if any.</summary>
        private int SumLoadedAmmoDicePoolBonus(XmlNode objWeaponNode, StringBuilder sb)
        {
            int intGearId = int.TryParse(GetValue(objWeaponNode, "ammoloaded", "-1"), out var g) ? g : -1;
            if (intGearId < 0)
                return 0;

            XmlNode? objAmmoGear = GetGearNodeById(intGearId);
            string strAmmoName = objAmmoGear != null ? GetValue(objAmmoGear, "name", string.Empty) : string.Empty;
            if (string.IsNullOrEmpty(strAmmoName))
                return 0;

            XmlDocument objGearDoc = XmlManager.Instance.Load("gear.xml");
            XmlNode? objXmlAmmo = objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strAmmoName}']");
            string strPool = objXmlAmmo?.SelectSingleNode("weaponbonus/pool")?.InnerText ?? string.Empty;
            if (!int.TryParse(strPool, out int intBonus) || intBonus == 0)
                return 0;

            sb.Append('\n').Append(strAmmoName).Append(": ").Append(FormatSigned(intBonus));
            return intBonus;
        }

        /// <summary>Ported from clsEquipment.cs's Weapon.DicePool: sums each installed Weapon
        /// Accessory's/Mod's own rules-data &lt;dicepool&gt; value (a plain integer, or "Rating"/
        /// "-Rating" for Mods whose bonus scales with their own Rating).</summary>
        private int SumInstalledAccessoryAndModDicePool(XmlNode objWeaponNode, StringBuilder sb)
        {
            int intTotal = 0;
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");

            XmlNodeList? objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                {
                    if (GetValue(objAccessoryNode, "installed", "True") != "True")
                        continue;
                    string strName = GetValue(objAccessoryNode, "name", string.Empty);
                    XmlNode? objXmlAccessory = objWeaponsDoc.SelectSingleNode(
                        $"/chummer/accessories/accessory[name = '{strName}']");
                    int intBonus = (int)RatingExpression.Evaluate(
                        objXmlAccessory?.SelectSingleNode("dicepool")?.InnerText ?? string.Empty, "0");
                    if (intBonus == 0)
                        continue;
                    intTotal += intBonus;
                    sb.Append('\n').Append(strName).Append(": ").Append(FormatSigned(intBonus));
                }

            XmlNodeList? objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                {
                    if (GetValue(objModNode, "installed", "True") != "True")
                        continue;
                    string strName = GetValue(objModNode, "name", string.Empty);
                    string strRating = GetValue(objModNode, "rating", "0");
                    XmlNode? objXmlMod = objWeaponsDoc.SelectSingleNode($"/chummer/mods/mod[name = '{strName}']");
                    int intBonus = (int)RatingExpression.Evaluate(
                        objXmlMod?.SelectSingleNode("dicepool")?.InnerText ?? string.Empty, strRating);
                    if (intBonus == 0)
                        continue;
                    intTotal += intBonus;
                    sb.Append('\n').Append(strName).Append(": ").Append(FormatSigned(intBonus));
                }

            return intTotal;
        }

    }
}
