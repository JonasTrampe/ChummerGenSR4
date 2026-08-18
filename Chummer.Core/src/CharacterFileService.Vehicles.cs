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
        public void AddVehicle(string strName, string strCategory, string strHandling, string strAcceleration,
            string strSpeed, string strPilot, string strBody, string strArmor, string strSensor,
            string strDeviceRating, string strAvail, string strCost, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objVehicles = objRoot.SelectSingleNode("vehicles");
            if (objVehicles == null)
            {
                objVehicles = Document.CreateElement("vehicles");
                objRoot.AppendChild(objVehicles);
            }

            var objVehicle = Document.CreateElement("vehicle");
            AppendElement(objVehicle, "guid", Guid.NewGuid().ToString());
            AppendElement(objVehicle, "name", strName.Trim());
            AppendElement(objVehicle, "category", strCategory);
            AppendElement(objVehicle, "handling", strHandling);
            AppendElement(objVehicle, "accel", strAcceleration);
            AppendElement(objVehicle, "speed", strSpeed);
            AppendElement(objVehicle, "pilot", strPilot);
            AppendElement(objVehicle, "body", strBody);
            AppendElement(objVehicle, "armor", strArmor);
            AppendElement(objVehicle, "sensor", strSensor);
            AppendElement(objVehicle, "devicerating", strDeviceRating);
            AppendElement(objVehicle, "avail", strAvail);
            AppendElement(objVehicle, "cost", strCost);
            AppendElement(objVehicle, "addslots", "0");
            AppendElement(objVehicle, "source", strSource);
            AppendElement(objVehicle, "page", strPage);
            AppendElement(objVehicle, "physicalcmfilled", "0");
            AppendElement(objVehicle, "vehiclename", string.Empty);
            AppendElement(objVehicle, "homenode", "False");
            objVehicle.AppendChild(Document.CreateElement("mods"));
            objVehicle.AppendChild(Document.CreateElement("gears"));
            objVehicle.AppendChild(Document.CreateElement("weapons"));
            AppendElement(objVehicle, "notes", string.Empty);
            AppendElement(objVehicle, "discountedcost", "False");
            objVehicles.AppendChild(objVehicle);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
        }

        /// <summary>Removes one root-level vehicle identified by its saved name and category.</summary>
        public bool RemoveVehicle(string strName, string strCategory)
        {
            XmlNode? objVehicle = null;
            XmlNodeList? objVehicles = Document.SelectNodes("/character/vehicles/vehicle");
            if (objVehicles != null)
            {
                foreach (XmlNode objCandidate in objVehicles)
                {
                    if (GetValue(objCandidate, "name", string.Empty) != strName
                        || GetValue(objCandidate, "category", string.Empty) != strCategory)
                        continue;
                    objVehicle = objCandidate;
                    break;
                }
            }
            if (objVehicle?.ParentNode == null)
                return false;

            objVehicle.ParentNode.RemoveChild(objVehicle);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes one root vehicle by its persisted GUID. New UI code should prefer this
        /// overload because a character may own more than one vehicle with the same name.</summary>
        public bool RemoveVehicle(Guid guiVehicleId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle?.ParentNode == null)
                return false;

            objVehicle.ParentNode.RemoveChild(objVehicle);
            Changed?.Invoke();
            return true;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root Vehicle
        /// (including its own installed mods' cost; onboard Weapons/Gear aren't costed here, same
        /// simplification <see cref="CalculatedCost"/>'s tree reading already makes elsewhere).</summary>
        public bool SellVehicle(Guid guiVehicleId, double dblSellPercent)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            int intRefund = ComputeSellRefund(ReadTreeItem(objVehicle, "mods/mod").CalculatedCost, dblSellPercent);
            string strName = GetValue(objVehicle, "name", string.Empty);
            if (!RemoveVehicle(guiVehicleId))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        /// <summary>Depth-first (parent before children, root order preserved) walk of the whole
        /// &lt;gears&gt; tree - the same order <see cref="Gear"/> assigns GearIds in, so an ID
        /// found in the UI tree always resolves back to the same node here.</summary>
        public bool AdjustVehicleDamage(string strName, string strCategory, int intDelta)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/vehicles/vehicle");
            if (objNodes == null) return false;
            foreach (XmlNode objVehicle in objNodes)
            {
                if (!string.Equals(GetValue(objVehicle, "name", string.Empty), strName, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objVehicle, "category", string.Empty), strCategory, StringComparison.Ordinal)) continue;
                int intCurrent = int.TryParse(GetValue(objVehicle, "physicalcmfilled", "0"), out var intValue) ? intValue : 0;
                int intNewValue = Math.Max(0, intCurrent + intDelta);
                if (intNewValue == intCurrent) return false;
                SetChildValue(objVehicle, "physicalcmfilled", intNewValue.ToString());
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Adjusts the filled physical condition-monitor boxes of a vehicle by GUID.</summary>
        public bool AdjustVehicleDamage(Guid guiVehicleId, int intDelta)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            int intCurrent = int.TryParse(GetValue(objVehicle, "physicalcmfilled", "0"), out var intParsed)
                ? intParsed : 0;
            SetChildValue(objVehicle, "physicalcmfilled", Math.Max(0, intCurrent + intDelta).ToString(CultureInfo.InvariantCulture));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a rules-data vehicle modification in the same persisted shape as
        /// <c>VehicleMod.Save</c>. The caller supplies the already selected rating; costs that
        /// reference Body are resolved against the owning vehicle before being deducted.</summary>
        /// <summary>Ported from clsEquipment.cs's Vehicle.Slots/SlotsUsed check that
        /// frmSelectVehicleMod.cs performs before letting a Mod be added. Returns false (and adds
        /// nothing) if the vehicle doesn't have enough free Slots left for it.</summary>
        public bool AddVehicleMod(Guid guiVehicleId, string strName, string strCategory, string strRating,
            string strSlots, string strAvail, string strCost, string strSource, string strPage, string strLimit = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle modification name is required.", nameof(strName));

            CharacterVehicleData? objVehicleData = Vehicles.FirstOrDefault(v => v.Guid == guiVehicleId.ToString());
            if (objVehicleData == null)
                return false;

            int intModSlots = (int)RatingExpression.Evaluate(strSlots, strRating);
            if (intModSlots > objVehicleData.SlotsRemaining)
                return false;

            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            XmlNode? objMods = objVehicle.SelectSingleNode("mods");
            if (objMods == null)
            {
                objMods = Document.CreateElement("mods");
                objVehicle.AppendChild(objMods);
            }

            var objMod = Document.CreateElement("mod");
            AppendElement(objMod, "guid", Guid.NewGuid().ToString());
            AppendElement(objMod, "name", strName.Trim());
            AppendElement(objMod, "category", strCategory);
            AppendElement(objMod, "limit", strLimit);
            AppendElement(objMod, "slots", strSlots);
            AppendElement(objMod, "rating", strRating);
            AppendElement(objMod, "maxrating", strRating);
            AppendElement(objMod, "response", "0");
            AppendElement(objMod, "system", "0");
            AppendElement(objMod, "firewall", "0");
            AppendElement(objMod, "signal", "0");
            AppendElement(objMod, "pilot", "0");
            AppendElement(objMod, "avail", strAvail);
            AppendElement(objMod, "cost", strCost);
            AppendElement(objMod, "extra", string.Empty);
            AppendElement(objMod, "source", strSource);
            AppendElement(objMod, "page", strPage);
            AppendElement(objMod, "included", "False");
            AppendElement(objMod, "installed", "True");
            AppendElement(objMod, "subsystems", string.Empty);
            objMod.AppendChild(Document.CreateElement("weapons"));
            AppendElement(objMod, "notes", string.Empty);
            AppendElement(objMod, "discountedcost", "False");
            objMods.AppendChild(objMod);

            var strBody = GetValue(objVehicle, "body", "0");
            DeductVehicleModCost(strCost, strRating, strBody, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds directly installed onboard gear to a vehicle using the legacy Gear.Save
        /// shape. This is separate from the character's root-level gear tree and is charged once
        /// at the selected rating and quantity.</summary>
        public bool AddVehicleGear(Guid guiVehicleId, string strName, string strCategory, string strRating = "0",
            string strQty = "1", string strCost = "", string strAvail = "", string strSource = "", string strPage = "",
            string strCapacity = "", string strResponse = "", string strSignal = "", string strSystemRating = "",
            string strFirewall = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle gear name is required.", nameof(strName));

            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;
            XmlNode? objGears = objVehicle.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objVehicle.AppendChild(objGears);
            }
            AppendGearNode(objGears, strName, strCategory, strRating, strQty, strCost, strAvail, strSource, strPage,
                strCapacity, strResponse, strSignal, strSystemRating, strFirewall);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a plugin below direct onboard vehicle gear.</summary>
        public bool AddVehicleGearPlugin(Guid guiVehicleId, Guid guiParentGearId, string strName, string strCategory,
            string strRating = "0", string strQty = "1", string strCost = "", string strAvail = "",
            string strSource = "", string strPage = "", string strCapacity = "")
        {
            XmlNode? objParent = GetVehicleNode(guiVehicleId)?.SelectSingleNode($"gears/gear[guid = '{guiParentGearId}']");
            if (objParent == null || string.IsNullOrWhiteSpace(strName)
                || (GetCharacterOptions().EnforceCapacity && !GearCapacityAllowsChild(objParent, strCapacity, strQty)))
                return false;
            XmlElement objChildren = objParent.SelectSingleNode("children") as XmlElement
                ?? (XmlElement)objParent.AppendChild(Document.CreateElement("children"));
            AppendGearNode(objChildren, strName, strCategory, strRating, strQty, strCost, strAvail, strSource,
                strPage, strCapacity, string.Empty, string.Empty, string.Empty, string.Empty);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct vehicle modification identified by its persisted GUID.</summary>
        public bool RemoveVehicleMod(Guid guiVehicleId, Guid guiModId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objMod = objVehicle?.SelectSingleNode($"mods/mod[guid = '{guiModId}']");
            if (objMod?.ParentNode == null)
                return false;

            objMod.ParentNode.RemoveChild(objMod);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Replaces an Obsolete modification (or an Obsolescent one when the matching
        /// house rule is enabled) with the zero-slot Retrofit modification. The retrofit costs a
        /// caller-selected percentage of the vehicle's base cost and is recorded as a Nuyen
        /// expense, matching frmCareer's removal flow.</summary>
        public bool RetrofitVehicleObsolescence(Guid guiVehicleId, Guid guiModId, int intPercentage)
        {
            if (intPercentage < 0 || intPercentage > 1000)
                return false;

            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objMod = objVehicle?.SelectSingleNode($"mods/mod[guid = '{guiModId}']");
            if (objVehicle == null || objMod?.ParentNode == null)
                return false;

            string strName = GetValue(objMod, "name", string.Empty);
            bool blnEligible = string.Equals(strName, "Obsolete", StringComparison.Ordinal)
                || (string.Equals(strName, "Obsolescent", StringComparison.Ordinal)
                    && GetCharacterOptions().AllowObsolescentUpgrade);
            if (!blnEligible)
                return false;

            double dblBaseCost = RatingExpression.Evaluate(GetValue(objVehicle, "cost", "0"), "0");
            int intCost = (int)Math.Round(dblBaseCost * intPercentage / 100d, MidpointRounding.ToEven);
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture,
                out double dblParsed) ? dblParsed : 0;
            if (intCost > dblNuyen)
                return false;

            XmlNode objMods = objMod.ParentNode;
            var objRetrofit = Document.CreateElement("mod");
            AppendElement(objRetrofit, "guid", Guid.NewGuid().ToString());
            AppendElement(objRetrofit, "name", "Retrofit");
            AppendElement(objRetrofit, "category", "Special");
            AppendElement(objRetrofit, "limit", string.Empty);
            AppendElement(objRetrofit, "slots", "0");
            AppendElement(objRetrofit, "rating", "0");
            AppendElement(objRetrofit, "maxrating", "0");
            AppendElement(objRetrofit, "response", "0");
            AppendElement(objRetrofit, "system", "0");
            AppendElement(objRetrofit, "firewall", "0");
            AppendElement(objRetrofit, "signal", "0");
            AppendElement(objRetrofit, "pilot", "0");
            AppendElement(objRetrofit, "avail", "0");
            AppendElement(objRetrofit, "cost", intCost.ToString(CultureInfo.InvariantCulture));
            AppendElement(objRetrofit, "extra", string.Empty);
            AppendElement(objRetrofit, "source", "UCL");
            AppendElement(objRetrofit, "page", "15");
            AppendElement(objRetrofit, "included", "False");
            AppendElement(objRetrofit, "installed", "True");
            AppendElement(objRetrofit, "subsystems", string.Empty);
            objRetrofit.AppendChild(Document.CreateElement("weapons"));
            AppendElement(objRetrofit, "notes", string.Empty);
            AppendElement(objRetrofit, "discountedcost", "False");

            objMods.ReplaceChild(objRetrofit, objMod);
            Nuyen = (dblNuyen - intCost).ToString(CultureInfo.InvariantCulture);
            AddExpense("Nuyen", -intCost, "Vehicle Retrofit: " + GetValue(objVehicle, "name", string.Empty));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct onboard-gear item by its persisted GUID.</summary>
        public bool RemoveVehicleGear(Guid guiVehicleId, Guid guiGearId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objGear = objVehicle?.SelectSingleNode($"gears/gear[guid = '{guiGearId}']");
            if (objGear?.ParentNode == null)
                return false;
            objGear.ParentNode.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns a direct onboard-gear item to one of the vehicle's own named storage
        /// locations (see <see cref="AddVehicleLocation"/>), or clears it back to unassigned with
        /// an empty <paramref name="strLocation"/>.</summary>
        public bool AssignVehicleGearLocation(Guid guiVehicleId, Guid guiGearId, string strLocation)
        {
            strLocation = strLocation?.Trim() ?? string.Empty;
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;
            if (strLocation.Length > 0 && objVehicle.SelectNodes("locations/location")?.Cast<XmlNode>()
                    .Any(node => string.Equals(node.InnerText, strLocation, StringComparison.Ordinal)) != true)
                return false;
            XmlNode? objGear = objVehicle.SelectSingleNode($"gears/gear[guid = '{guiGearId}']");
            if (objGear == null)
                return false;
            SetChildValue(objGear, "location", strLocation);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a direct vehicle weapon in the legacy Weapon.Save shape. Requires an
        /// available (not yet used by another direct weapon) installed "Weapon Mount"/"Mechanical
        /// Arm" mod - ported from frmCareer.cs's tsVehicleAddWeaponWeapon_Click, which refuses to
        /// add a weapon at all unless one of those is selected. The new weapon records which
        /// specific mount mod it occupies (&lt;vehiclemountguid&gt;, the first not already claimed
        /// by another direct weapon) rather than legacy's approach of nesting the weapon node
        /// physically under the mount VehicleMod - same one-weapon-per-mount-instance restriction,
        /// simplified to avoid restructuring this port's existing flat weapons/mods lists. Vehicle-
        /// class eligibility for the weapon itself isn't validated.</summary>
        public bool AddVehicleWeapon(Guid guiVehicleId, string strName, string strCategory, string strDamage,
            string strAp, string strMode, string strRc, string strAmmo, string strCost, string strAvail,
            string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle weapon name is required.", nameof(strName));
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null) return false;

            var setClaimedMountGuids = new HashSet<string>(
                objVehicle.SelectNodes("weapons/weapon")?.Cast<XmlNode>()
                    .Select(objWeaponNode => GetValue(objWeaponNode, "vehiclemountguid", string.Empty))
                    .Where(s => !string.IsNullOrEmpty(s)) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            string? strMountGuid = objVehicle.SelectNodes("mods/mod")?.Cast<XmlNode>()
                .FirstOrDefault(objMod =>
                {
                    string strModName = GetValue(objMod, "name", string.Empty);
                    bool blnIsMount = strModName.StartsWith("Weapon Mount", StringComparison.Ordinal)
                        || strModName.StartsWith("Mechanical Arm", StringComparison.Ordinal);
                    return blnIsMount && !setClaimedMountGuids.Contains(GetValue(objMod, "guid", string.Empty));
                })?.SelectSingleNode("guid")?.InnerText;
            if (strMountGuid == null)
                return false;

            XmlNode? objWeapons = objVehicle.SelectSingleNode("weapons");
            if (objWeapons == null)
            {
                objWeapons = Document.CreateElement("weapons");
                objVehicle.AppendChild(objWeapons);
            }

            var objWeapon = Document.CreateElement("weapon");
            AppendElement(objWeapon, "guid", Guid.NewGuid().ToString());
            AppendElement(objWeapon, "name", strName.Trim());
            AppendElement(objWeapon, "category", strCategory);
            AppendElement(objWeapon, "type", string.Empty);
            AppendElement(objWeapon, "spec", string.Empty);
            AppendElement(objWeapon, "spec2", string.Empty);
            AppendElement(objWeapon, "reach", "0");
            AppendElement(objWeapon, "vehiclemountguid", strMountGuid);
            AppendElement(objWeapon, "damage", strDamage);
            AppendElement(objWeapon, "ap", strAp);
            AppendElement(objWeapon, "mode", strMode);
            AppendElement(objWeapon, "rc", strRc);
            AppendElement(objWeapon, "ammo", strAmmo);
            AppendElement(objWeapon, "ammocategory", string.Empty);
            AppendElement(objWeapon, "ammoremaining", "0");
            AppendElement(objWeapon, "ammoremaining2", "0");
            AppendElement(objWeapon, "ammoremaining3", "0");
            AppendElement(objWeapon, "ammoremaining4", "0");
            AppendElement(objWeapon, "ammoloaded", Guid.Empty.ToString());
            AppendElement(objWeapon, "ammoloaded2", Guid.Empty.ToString());
            AppendElement(objWeapon, "ammoloaded3", Guid.Empty.ToString());
            AppendElement(objWeapon, "ammoloaded4", Guid.Empty.ToString());
            AppendElement(objWeapon, "conceal", "0");
            AppendElement(objWeapon, "avail", strAvail);
            AppendElement(objWeapon, "cost", strCost);
            AppendElement(objWeapon, "useskill", string.Empty);
            AppendElement(objWeapon, "range", string.Empty);
            AppendElement(objWeapon, "rangemultiply", "1");
            AppendElement(objWeapon, "fullburst", "0");
            AppendElement(objWeapon, "suppressive", "0");
            AppendElement(objWeapon, "source", strSource);
            AppendElement(objWeapon, "page", strPage);
            AppendElement(objWeapon, "weaponname", string.Empty);
            AppendElement(objWeapon, "included", "False");
            AppendElement(objWeapon, "installed", "True");
            AppendElement(objWeapon, "requireammo", "True");
            objWeapon.AppendChild(Document.CreateElement("accessories"));
            objWeapon.AppendChild(Document.CreateElement("weaponmods"));
            AppendElement(objWeapon, "location", string.Empty);
            AppendElement(objWeapon, "notes", string.Empty);
            AppendElement(objWeapon, "discountedcost", "False");
            objWeapons.AppendChild(objWeapon);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct vehicle weapon by its persisted GUID.</summary>
        public bool RemoveVehicleWeapon(Guid guiVehicleId, Guid guiWeaponId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objWeapon = objVehicle?.SelectSingleNode($"weapons/weapon[guid = '{guiWeaponId}']");
            if (objWeapon?.ParentNode == null) return false;
            objWeapon.ParentNode.RemoveChild(objWeapon);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Persists a named storage/location bucket on a vehicle. Existing onboard gear
        /// is intentionally not reassigned here; assignment is a separate UI workflow.</summary>
        public bool AddVehicleLocation(Guid guiVehicleId, string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0) return false;
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null || objVehicle.SelectNodes("locations/location")?.Cast<XmlNode>()
                    .Any(node => string.Equals(node.InnerText, strName, StringComparison.Ordinal)) == true)
                return false;
            XmlElement? objLocations = objVehicle.SelectSingleNode("locations") as XmlElement;
            if (objLocations == null)
            {
                objLocations = Document.CreateElement("locations");
                objVehicle.AppendChild(objLocations);
            }
            AppendElement(objLocations, "location", strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a named vehicle location, clearing it from any onboard gear that was
        /// assigned to it (mirrors <see cref="RemoveWeaponLocation"/>).</summary>
        public bool RemoveVehicleLocation(Guid guiVehicleId, string strName)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objLocation = objVehicle?.SelectNodes("locations/location")?.Cast<XmlNode>()
                .FirstOrDefault(node => string.Equals(node.InnerText, strName, StringComparison.Ordinal));
            if (objLocation?.ParentNode == null) return false;
            objLocation.ParentNode.RemoveChild(objLocation);
            XmlNodeList? objGears = objVehicle?.SelectNodes("gears/gear");
            if (objGears != null)
                foreach (XmlNode objGear in objGears)
                    if (string.Equals(GetValue(objGear, "location", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objGear, "location", string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetVehicleNode(Guid guiVehicleId)
            => Document.SelectSingleNode($"/character/vehicles/vehicle[guid = '{guiVehicleId}']");

        public bool SetVehicleNotes(Guid guiVehicleId, string strNotes)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            SetChildValue(objVehicle, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Updates a saved installed vehicle Mod, Gear, or Weapon by its own GUID.
        /// The GUID is stable across the vehicle tree's separate display projections.</summary>
        public bool SetVehicleItemNotes(Guid guiVehicleId, Guid guiItemId, string strNotes)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objItem = objVehicle?.SelectSingleNode($".//*[guid = '{guiItemId}']");
            if (objItem == null)
                return false;

            SetChildValue(objItem, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private void DeductVehicleModCost(string strCost, string strRating, string strBody, string strAvail = "")
        {
            string strExpression = strCost.Replace("Body", strBody, StringComparison.OrdinalIgnoreCase);
            double dblCost = RatingExpression.Evaluate(strExpression, strRating);
            dblCost = ApplyRestrictedForbiddenCostMultiplier(dblCost, strAvail);
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblParsed)
                ? dblParsed : 0;
            Nuyen = (dblNuyen - dblCost).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Adds a root-level Weapon in the minimal saved-character tree shape used by
        /// <see cref="Weapons"/>/<see cref="WeaponTrees"/> - <paramref name="strDamage"/>/
        /// <paramref name="strAp"/>/<paramref name="strMode"/>/<paramref name="strRc"/>/
        /// <paramref name="strAmmo"/> are copied as-is from weapons.xml (no STR-substitution or
        /// underbarrel/accessory bonus math is ported for the damage code).</summary>
        public IReadOnlyList<CharacterVehicleData> Vehicles => ReadVehicles();

        // Karma and Nuyen history entries are saved into the same <expenses> list and only
        // distinguished by <type> - split here to feed the two separate history lists/charts.
        private IReadOnlyList<CharacterVehicleData> ReadVehicles()
        {
            var lstVehicles = new List<CharacterVehicleData>();
            var objNodes = Document.SelectNodes("/character/vehicles/vehicle");
            if (objNodes == null) return lstVehicles;
            bool blnUseCalculatedSensor = GetCharacterOptions().UseCalculatedVehicleSensorRatings;
            foreach (XmlNode objNode in objNodes)
            {
                var objVehicle = new CharacterVehicleData(
                    GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "category", string.Empty),
                    GetValue(objNode, "handling", string.Empty), GetValue(objNode, "accel", string.Empty),
                    GetValue(objNode, "speed", string.Empty), GetValue(objNode, "pilot", string.Empty),
                    GetValue(objNode, "body", string.Empty), GetValue(objNode, "armor", string.Empty),
                    GetValue(objNode, "sensor", string.Empty), GetValue(objNode, "devicerating", string.Empty),
                    GetValue(objNode, "avail", string.Empty), GetValue(objNode, "cost", string.Empty),
                    GetValue(objNode, "addslots", string.Empty), GetValue(objNode, "source", string.Empty),
                    GetValue(objNode, "page", string.Empty), GetValue(objNode, "physicalcmfilled", "0"),
                    GetValue(objNode, "notes", string.Empty), IgnoreRules);
                AddVehicleChildren(objVehicle.Children, objNode.SelectNodes("mods/mod"), "Vehicle Mod");
                AddVehicleChildren(objVehicle.Children, objNode.SelectNodes("gears/gear"), "Gear");
                AddVehicleChildren(objVehicle.Children, objNode.SelectNodes("weapons/weapon"), "Weapon");
                XmlNodeList? objLocations = objNode.SelectNodes("locations/location");
                if (objLocations != null)
                    foreach (XmlNode objLocation in objLocations)
                        if (!string.IsNullOrWhiteSpace(objLocation.InnerText)) objVehicle.Locations.Add(objLocation.InnerText);
                objVehicle.SetSensorDisplay(blnUseCalculatedSensor
                    ? objVehicle.CalculatedSensor.ToString(CultureInfo.InvariantCulture)
                    : objVehicle.Sensor);
                lstVehicles.Add(objVehicle);
            }
            return lstVehicles;
        }

        private void AddVehicleChildren(List<CharacterTreeItemData> lstChildren, XmlNodeList? objNodes,
            string strFallbackCategory)
        {
            if (objNodes == null)
                return;

            foreach (XmlNode objNode in objNodes)
            {
                var objItem = new CharacterTreeItemData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "category", strFallbackCategory), GetValue(objNode, "rating", "0"),
                    GetValue(objNode, "installed", GetValue(objNode, "equipped", "False")) == "True",
                    GetValue(objNode, "cost", string.Empty), GetValue(objNode, "avail", string.Empty),
                    GetValue(objNode, "qty", "1"));
                objItem.SetItemGuid(GetValue(objNode, "guid", string.Empty));
                objItem.SetNotes(GetValue(objNode, "notes", string.Empty));
                if (strFallbackCategory == "Vehicle Mod")
                    objItem.SetModSlots(GetValue(objNode, "slots", "0"), GetValue(objNode, "included", "False") == "True");
                if (strFallbackCategory == "Weapon")
                {
                    objItem.IsVehicleWeapon = true;
                    objItem.SetWeaponDamage(ComputeWeaponDamage(objNode));
                    objItem.SetAmmoStatus(ComputeAmmoStatus(objNode));
                }
                if (strFallbackCategory == "Gear")
                {
                    objItem.SetLocation(GetValue(objNode, "location", string.Empty));
                    // Only Signal is actually used (CalculatedSensor) - Capacity/Response/System/
                    // Firewall aren't meaningful for vehicle-mounted gear the way they are for the
                    // root Gear tree's Commlinks, so they're left blank here.
                    objItem.SetGearDetails(string.Empty, string.Empty,
                        GetValue(objNode, "signal", string.Empty), string.Empty, string.Empty, blnActive: false);
                }
                AddVehicleChildren(objItem.Children, objNode.SelectNodes("children/gear"), "Gear");
                AddVehicleChildren(objItem.Children, objNode.SelectNodes("weapons/weapon"), "Weapon");
                lstChildren.Add(objItem);
            }
        }

    }
}
