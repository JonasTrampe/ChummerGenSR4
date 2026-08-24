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
        private IReadOnlyList<CharacterCommlinkData>? _cachedCommlinks;
        public IReadOnlyList<CharacterCommlinkData> Commlinks
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedCommlinks short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedCommlinks ??= ReadCommlinks();
            }
        }

        /// <summary>Matrix "System" stat, only meaningful for A.I./technocritter/protosapient
        /// characters (drone/sprite-style characters whose Matrix Initiative uses this instead of
        /// a Commlink's Response) - see MatrixInitiative.</summary>
        private int ActiveCommlinkResponse()
        {
            var objNodes = Document.SelectNodes(
                "//gear[category = 'Commlink' and equipped = 'True' and active = 'True']/response");
            return objNodes is { Count: > 0 } && int.TryParse(objNodes[0]!.InnerText, out var intResponse)
                ? intResponse
                : 0;
        }

        public void SetActiveCommlink(string strGuid)
        {
            XmlNodeList? objNodes = Document.SelectNodes("//gear[category = 'Commlink']");
            if (objNodes == null)
                return;

            for (int intFormId = 0; intFormId < objNodes.Count; intFormId++)
            {
                XmlNode objNode = objNodes[intFormId]!;
                string strNodeGuid = GetValue(objNode, "guid", string.Empty);
                SetChildValue(objNode, "active", strNodeGuid == strGuid ? "True" : "False");
            }

            Changed?.Invoke();
        }

        public bool AddGear(string strName, string strCategory, string strRating = "0", string strQty = "1",
            string strCost = "", string strAvail = "", string strSource = "", string strPage = "",
            string strCapacity = "", string strResponse = "", string strSignal = "", string strSystemRating = "",
            string strFirewall = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A gear name is required.", nameof(strName));
            if (!StickNShockAllowed(strName))
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objGears = objRoot.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objRoot.AppendChild(objGears);
            }

            XmlElement objGear = AppendGearNode(objGears, strName, strCategory, strRating, strQty, strCost, strAvail,
                strSource, strPage, strCapacity, strResponse, strSignal, strSystemRating, strFirewall);
            AppendAutomaticProgramOptions(objGear);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Builds a custom Nexus (a build-your-own Matrix node, UN p.50) and adds it as a
        /// single root-level Gear item - ported from frmSelectNexus.cs's CalculateNexus. This
        /// port's Gear already carries direct Response/Signal/System/Firewall fields (unlike
        /// legacy, which assembles five separate child Gear items per attribute), so those four
        /// values are written straight onto the one Nexus Gear node; the Persona Limit has no
        /// equivalent field and is folded into the item's own name instead, matching how legacy's
        /// own top-level Nexus Gear name embeds the Processor rating. Cost/Avail formulas are
        /// copied verbatim per-tier, including legacy's Response-cost bug: Response 7-10 always
        /// costs 0 nuyen because CalculateNexus multiplies its own not-yet-assigned (still 0)
        /// running total instead of the rating - faithfully reproduced rather than fixed, since a
        /// real character built to match a legacy save must land on the exact same numbers.
        /// <paramref name="blnFree"/> matches the "Free!" checkbox (career mode only in legacy;
        /// here it's just an optional override).</summary>
        public bool AddNexus(int intProcessor, int intResponse, int intSystem, int intFirewall, int intSignal,
            int intPersona, bool blnFree = false)
        {
            // Legacy also computes a per-attribute Availability string for display (e.g.
            // Response's is (Response*4), +"F" past tier 2) - purely informational there (the
            // assembled Nexus Gear's own Avail is always "0"), so not reproduced here.
            int intResponseCost;
            if (intResponse <= 3)
                intResponseCost = intResponse * intProcessor * 50;
            else if (intResponse <= 6)
                intResponseCost = intResponse * intProcessor * 100;
            else
                intResponseCost = 0; // legacy bug: multiplies its own still-zero running total.

            int intSystemCost;
            if (intSystem <= 3)
                intSystemCost = intSystem * intPersona * 25;
            else if (intSystem <= 6)
                intSystemCost = intSystem * intPersona * 50;
            else
                intSystemCost = intSystem * intPersona * 300;

            int intFirewallCost;
            if (intFirewall <= 3)
                intFirewallCost = intFirewall * intProcessor * 25;
            else if (intFirewall <= 6)
                intFirewallCost = intFirewall * intProcessor * 50;
            else
                intFirewallCost = intFirewall * intProcessor * 250;

            int intSignalCost = intSignal switch
            {
                2 => 50,
                3 => 150,
                4 => 500,
                5 => 1000,
                6 => 3000,
                7 => 6500,
                8 => 8750,
                9 => 12250,
                10 => 17250,
                _ => 10,
            };

            int intCost = blnFree ? 0 : intResponseCost + intSystemCost + intFirewallCost + intSignalCost;

            string strName = $"Nexus (Processor {intProcessor})";
            return AddGear(strName, "Nexus", strCost: intCost.ToString(CultureInfo.InvariantCulture), strAvail: "0",
                strSource: "UN", strPage: "50", strResponse: intResponse.ToString(CultureInfo.InvariantCulture),
                strSignal: intSignal.ToString(CultureInfo.InvariantCulture),
                strSystemRating: intSystem.ToString(CultureInfo.InvariantCulture),
                strFirewall: intFirewall.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Ported from frmCareer.cs/frmCreate.cs's Stick-n-Shock weapon-category
        /// restriction checks (e.g. frmCareer.cs:24378), simplified at purchase time: legacy blocks
        /// loading Stick-n-Shock ammo into a specific excluded-category weapon; this instead blocks
        /// acquiring the ammo at all when the RestrictStickNShock house rule is on and the
        /// character owns no weapon outside the excluded categories to use it with. The actual
        /// loaded-into-a-specific-weapon concept exists now too (see ReloadWeapon/
        /// GetWeaponAmmoOptions, which does exclude Stick-n-Shock per-weapon there), but this
        /// broader "can they own it at all" gate is still checked at AddGear time.</summary>
        public bool AddChildGear(int intParentGearId, string strName, string strCategory, string strRating = "0",
            string strQty = "1", string strCost = "", string strAvail = "", string strSource = "", string strPage = "",
            string strCapacity = "", string strResponse = "", string strSignal = "", string strSystemRating = "",
            string strFirewall = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A gear name is required.", nameof(strName));

            XmlNode? objParent = GetGearNodeById(intParentGearId);
            if (objParent == null)
                return false;

            if (GetCharacterOptions().EnforceCapacity
                && !GearCapacityAllowsChild(objParent, strCapacity, strQty))
                return false;

            var objChildren = objParent.SelectSingleNode("children") as XmlElement;
            if (objChildren == null)
            {
                objChildren = Document.CreateElement("children");
                objParent.AppendChild(objChildren);
            }

            XmlElement objGear = AppendGearNode(objChildren, strName, strCategory, strRating, strQty, strCost, strAvail,
                strSource, strPage, strCapacity, strResponse, strSignal, strSystemRating, strFirewall);
            AppendAutomaticProgramOptions(objGear);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from clsEquipment.cs's Gear.CapacityRemaining, honoring the
        /// EnforceCapacity house rule setting - same simplified capacity model as
        /// CharacterTreeItemData.CapacityRemaining (own capacity minus the sum of children's own
        /// capacity, no bracketed "[x]" capacity handling). Existing children with a non-numeric
        /// (bracketed) Capacity are treated as consuming 0, matching that same simplification.</summary>
        private static bool GearCapacityAllowsChild(XmlNode objParent, string strChildCapacity, string strChildQty)
        {
            double dblOwn = double.TryParse(GetValue(objParent, "capacity", string.Empty), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dOwn) ? dOwn : 0;
            var objExistingChildren = objParent.SelectNodes("children/gear");
            double dblUsed = 0;
            if (objExistingChildren != null)
                foreach (XmlNode objChild in objExistingChildren)
                    if (double.TryParse(GetValue(objChild, "capacity", string.Empty), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var dUsed))
                        dblUsed += dUsed;

            double dblNewCapacity = double.TryParse(strChildCapacity, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dNew) ? dNew : 0;
            int intQty = int.TryParse(strChildQty, out var q) ? q : 1;

            return dblUsed + dblNewCapacity * intQty <= dblOwn;
        }

        private void DeductGearCost(string strCost, string strRating, string strQty, string strAvail = "")
        {
            int intQty = int.TryParse(strQty, out var q) ? q : 1;
            double dblCost = RatingExpression.Evaluate(strCost, strRating) * intQty;
            dblCost = ApplyRestrictedForbiddenCostMultiplier(dblCost, strAvail);
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
            Nuyen = (dblNuyen - dblCost).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Ported from frmCareer.cs's per-item purchase handlers (e.g. tsGearAdd_Click):
        /// when the MultiplyRestrictedCost/MultiplyForbiddenCost house rules are on, an item whose
        /// raw Availability string ends in "R"/"F" has its cost multiplied by the matching
        /// RestrictedCostMultiplier/ForbiddenCostMultiplier.</summary>
        private XmlElement AppendGearNode(XmlNode objParentList, string strName, string strCategory, string strRating,
            string strQty, string strCost, string strAvail, string strSource, string strPage, string strCapacity,
            string strResponse, string strSignal, string strSystemRating, string strFirewall)
        {
            var objGear = Document.CreateElement("gear");
            AppendElement(objGear, "name", strName.Trim());
            AppendElement(objGear, "category", strCategory);
            AppendElement(objGear, "rating", strRating);
            AppendElement(objGear, "qty", strQty);
            AppendElement(objGear, "cost", strCost);
            AppendElement(objGear, "avail", strAvail);
            AppendElement(objGear, "capacity", strCapacity);
            AppendElement(objGear, "source", strSource);
            AppendElement(objGear, "page", strPage);
            AppendElement(objGear, "equipped", "False");
            // <guid>/<active> are what CharacterDocument.Commlinks matches Commlink-category gear
            // by - always written (harmless for non-Commlink gear) so a newly bought Commlink is
            // immediately recognized by the existing Commlink dropdown/MatrixInitiative calc.
            AppendElement(objGear, "guid", Guid.NewGuid().ToString());
            AppendElement(objGear, "active", "False");
            // <location> is only meaningful for direct onboard vehicle gear (see
            // AssignVehicleGearLocation) - always written (harmless elsewhere) to match the
            // guid/active always-write pattern above.
            AppendElement(objGear, "location", string.Empty);
            if (!string.IsNullOrEmpty(strResponse)) AppendElement(objGear, "response", strResponse);
            if (!string.IsNullOrEmpty(strSignal)) AppendElement(objGear, "signal", strSignal);
            if (!string.IsNullOrEmpty(strSystemRating)) AppendElement(objGear, "system", strSystemRating);
            if (!string.IsNullOrEmpty(strFirewall)) AppendElement(objGear, "firewall", strFirewall);
            objGear.AppendChild(Document.CreateElement("children"));
            objParentList.AppendChild(objGear);
            return objGear;
        }

        /// <summary>Removes the gear matching this <see cref="Gear"/> tree's GearId, wherever it
        /// is nested (root-level or as another item's child).</summary>
        public bool RemoveGear(int intGearId)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear?.ParentNode == null)
                return false;

            objGear.ParentNode.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from frmSellItem.cs/frmCareer.cs's per-item "tsXxxSell_Click" handlers:
        /// refunds <paramref name="dblSellPercent"/> (0.0-1.0) of the item's current total cost
        /// back to Nuyen (logged as a Nuyen expense entry) before removing it. Refunds round to the
        /// nearest whole Nuyen, same as legacy's Convert.ToInt32.</summary>
        public bool SellGear(int intGearId, double dblSellPercent)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            int intRefund = ComputeSellRefund(ReadTreeItem(objGear).CalculatedCost, dblSellPercent);
            string strName = GetValue(objGear, "name", string.Empty);
            if (!RemoveGear(intGearId))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        public bool SetGearEquipped(int intGearId, bool blnEquipped)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            bool blnWasEquipped = GetValue(objGear, "equipped", "False") == "True";
            if (blnWasEquipped == blnEquipped)
                return true;

            SetChildValue(objGear, "equipped", blnEquipped ? "True" : "False");
            if (Guid.TryParse(GetValue(objGear, "guid", string.Empty), out Guid guiGearId))
            {
                CharacterFocusData? objFocus = Foci.FirstOrDefault(focus =>
                    string.Equals(focus.GearId, guiGearId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (objFocus != null)
                {
                    if (blnEquipped)
                        ApplyFocusGearBonus(objGear, guiGearId, GetValue(objGear, "rating", "0"));
                    else
                        RemoveBonusImprovements(ImprovementSource.Gear, guiGearId.ToString());
                }
                CharacterStackedFocusData? objStack = StackedFoci.FirstOrDefault(focus =>
                    string.Equals(focus.GearId, guiGearId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (objStack?.Bonded == true && Guid.TryParse(objStack.Guid, out Guid guiStackId))
                {
                    if (blnEquipped)
                    {
                        XmlNode? objStackNode = FindStackedFocusNode(guiStackId);
                        if (objStackNode != null)
                            ApplyStackedFocusBonuses(objStackNode, guiStackId);
                    }
                    else
                        RemoveBonusImprovements(ImprovementSource.StackedFocus, guiStackId.ToString());
                }
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sets a gear item's quantity directly - matches the legacy tree's editable
        /// quantity spinner.</summary>
        public bool SetGearQuantity(int intGearId, string strQty)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            SetChildValue(objGear, "qty", strQty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Updates the free-form note attached to a Gear item. The depth-first ID is
        /// deliberately shared with quantity/delete/reorder so this also addresses Gear nested
        /// under another Gear item, without relying on a name that may be duplicated.</summary>
        public bool SetGearNotes(int intGearId, string strNotes)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            SetChildValue(objGear, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sets Gear's player-facing label without changing its rules-data
        /// <c>&lt;name&gt;</c>. Legacy saves use <c>&lt;gearname&gt;</c> for this distinction,
        /// so lookups, costs, and category-based rules remain tied to the original item.</summary>
        public bool SetGearCustomName(int intGearId, string strCustomName)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            SetChildValue(objGear, "gearname", strCustomName?.Trim() ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves a gear item within the &lt;gears&gt; tree - either reordering it among its
        /// current siblings (inserted immediately before <paramref name="intTargetGearId"/>) or, with
        /// <paramref name="blnReparent"/>, making it a child of the target instead. GearIds are
        /// depth-first positions recomputed on every read (see <see cref="GetGearNodeById"/>), so
        /// callers must reload the tree after a successful move before issuing another one.</summary>
        public bool MoveGear(int intSourceGearId, int intTargetGearId, bool blnReparent)
        {
            XmlNode? objSource = GetGearNodeById(intSourceGearId);
            XmlNode? objTarget = GetGearNodeById(intTargetGearId);
            if (objSource == null || objTarget == null || objSource == objTarget || objSource.ParentNode == null)
                return false;

            // Refuse to move an item into its own subtree - that would either orphan the branch or
            // (for reparent) create a cycle.
            for (XmlNode? objCursor = objTarget; objCursor != null; objCursor = objCursor.ParentNode)
                if (objCursor == objSource)
                    return false;

            objSource.ParentNode.RemoveChild(objSource);

            if (blnReparent)
            {
                XmlNode? objChildren = objTarget.SelectSingleNode("children");
                if (objChildren == null)
                {
                    objChildren = Document.CreateElement("children");
                    objTarget.AppendChild(objChildren);
                }
                objChildren.AppendChild(objSource);
            }
            else
            {
                if (objTarget.ParentNode == null)
                    return false;
                objTarget.ParentNode.InsertBefore(objSource, objTarget);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a vehicle in the same persisted shape as the legacy Vehicle.Save method.
        /// The purchase price is deducted from Nuyen just like root-level gear.</summary>
        private IEnumerable<XmlNode> EnumerateGearNodesDfs()
        {
            var objNodes = Document.SelectNodes("/character/gears/gear");
            if (objNodes == null) yield break;

            foreach (XmlNode objNode in objNodes)
            {
                foreach (XmlNode objDescendant in EnumerateGearNodeAndChildren(objNode))
                    yield return objDescendant;
            }
        }

        private IEnumerable<XmlNode> EnumerateGearNodeAndChildren(XmlNode objNode)
        {
            yield return objNode;
            var objChildren = objNode.SelectNodes("children/gear");
            if (objChildren == null) yield break;

            foreach (XmlNode objChild in objChildren)
            {
                foreach (XmlNode objDescendant in EnumerateGearNodeAndChildren(objChild))
                    yield return objDescendant;
            }
        }

        public IReadOnlyList<string> GetPacksKitCategories()
        {
            XmlDocument objPacksDoc = XmlManager.Instance.Load("packs.xml");
            var lstCategories = new List<string>();
            foreach (XmlNode objCategory in objPacksDoc.SelectNodes("/chummer/categories/category")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strCategory = objCategory.InnerText;
                if (!string.IsNullOrEmpty(strCategory))
                    lstCategories.Add(strCategory);
            }

            return lstCategories;
        }

        public IReadOnlyList<string> GetPacksKitNames(string strCategory)
        {
            XmlDocument objPacksDoc = XmlManager.Instance.Load("packs.xml");
            var lstNames = new List<string>();
            foreach (XmlNode objPack in objPacksDoc.SelectNodes(
                         $"/chummer/packs/pack[category = '{strCategory}']")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objPack["name"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strName))
                    lstNames.Add(strName);
            }

            return lstNames;
        }

        /// <summary>Applies a PACKS Kit (a bundled starting-gear preset) - ported from
        /// frmCreate.cs's AddPACKSKit. Reuses the same higher-level Add* methods this port already
        /// has for each item type wherever possible (Qualities, Spells, Adept Powers, Complex
        /// Forms, Armor, Weapons), matching their existing bonus-application behavior for free.
        /// Not ported (all rare in real packs.xml, documented rather than silently wrong):
        /// Vehicles (7 real kits), Martial Arts via &lt;selectmartialart&gt; (2), Spirits (1),
        /// Lifestyles (0 real kits use it) - and, within the sections that ARE ported, Armor
        /// Mods/nested Gear, Weapon Accessories/Mods, and Exotic Skills are all skipped the same
        /// way <see cref="AddArmor"/>/<see cref="AddWeapon"/> callers elsewhere in this port treat
        /// those as separate follow-up adds rather than kit-bundled content.</summary>
        public bool AddPacksKit(string strKitName, string strCategory)
        {
            if (string.IsNullOrWhiteSpace(strKitName))
                return false;

            XmlDocument objPacksDoc = XmlManager.Instance.Load("packs.xml");
            XmlNode? objXmlKit = objPacksDoc.SelectSingleNode(
                $"/chummer/packs/pack[name = '{strKitName.Trim()}' and category = '{strCategory}']");
            if (objXmlKit == null)
                return false;

            ApplyPacksAttributes(objXmlKit);
            ApplyPacksQualities(objXmlKit);
            ApplyPacksSkills(objXmlKit);
            ApplyPacksKnowledgeSkills(objXmlKit);
            ApplyPacksSpells(objXmlKit);
            ApplyPacksPowers(objXmlKit);
            ApplyPacksComplexForms(objXmlKit);
            ApplyPacksCyberwareOrBioware(objXmlKit, blnBioware: false);
            ApplyPacksCyberwareOrBioware(objXmlKit, blnBioware: true);
            ApplyPacksArmor(objXmlKit);
            ApplyPacksWeapons(objXmlKit);
            ApplyPacksGear(objXmlKit);
            ApplyPacksNuyen(objXmlKit);

            Changed?.Invoke();
            return true;
        }

        private void ApplyPacksAttributes(XmlNode objXmlKit)
        {
            XmlNode? objXmlAttributes = objXmlKit.SelectSingleNode("attributes");
            if (objXmlAttributes == null)
                return;

            // Legacy resets every Attribute to its Metatype minimum first, then applies each
            // given value adjusted by "- (6 - MetatypeMaximum)" to translate a human-scale (max 6)
            // value onto the current Metatype's own scale. SetAttributeValue takes the absolute
            // target value directly, so that translation isn't needed here - the given values are
            // applied as-is.
            foreach (XmlNode objXmlAttribute in objXmlAttributes.ChildNodes)
            {
                if (!int.TryParse(objXmlAttribute.InnerText, out int intValue))
                    continue;

                SetAttributeValue(objXmlAttribute.Name.ToUpperInvariant(), intValue);
            }
        }

        private void ApplyPacksQualities(XmlNode objXmlKit)
        {
            XmlNode? objXmlQualities = objXmlKit.SelectSingleNode("qualities");
            if (objXmlQualities == null)
                return;

            XmlDocument objQualitiesDoc = XmlManager.Instance.Load("qualities.xml");

            foreach (XmlNode objXmlQuality in objXmlQualities.SelectNodes("positive/quality")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                if (!IsPacksItemBookEnabled(objQualitiesDoc, "qualities/quality", objXmlQuality.InnerText))
                    continue;
                AddQuality(objXmlQuality.InnerText, "Positive", objXmlQuality.Attributes?["select"]?.InnerText ?? string.Empty);
            }

            foreach (XmlNode objXmlQuality in objXmlQualities.SelectNodes("negative/quality")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                if (!IsPacksItemBookEnabled(objQualitiesDoc, "qualities/quality", objXmlQuality.InnerText))
                    continue;
                AddQuality(objXmlQuality.InnerText, "Negative", objXmlQuality.Attributes?["select"]?.InnerText ?? string.Empty);
            }
        }

        /// <summary>Shared PACKS-kit/Suite sourcebook filter: resolves the named rules-data item
        /// under /chummer/{strXPath} and checks its &lt;source&gt; against the character's enabled
        /// sourcebooks - the same check a normal picker dialog runs, applied here since a kit/Suite
        /// is just a pre-selected shortcut for items a picker would otherwise offer.</summary>
        private bool IsPacksItemBookEnabled(XmlDocument objDataDoc, string strXPath, string strName)
        {
            if (string.IsNullOrEmpty(strName))
                return false;
            XmlNode? objXmlNode = objDataDoc.SelectSingleNode($"/chummer/{strXPath}[name = '{strName.Trim()}']");
            return objXmlNode != null && IsBookEnabled(objXmlNode["source"]?.InnerText ?? string.Empty);
        }

        private void ApplyPacksSkills(XmlNode objXmlKit)
        {
            XmlNode? objXmlSkills = objXmlKit.SelectSingleNode("skills");
            if (objXmlSkills == null)
                return;

            foreach (XmlNode objXmlSkill in objXmlSkills.SelectNodes("skill")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlSkill["name"]?.InnerText ?? string.Empty;
                XmlNode? objNode = Document.SelectSingleNode(
                    $"/character/skills/skill[name = '{strName}' and knowledge = 'False']");
                if (objNode == null || !int.TryParse(objXmlSkill["rating"]?.InnerText, out int intRating))
                    continue;

                int intMax = int.TryParse(GetValue(objNode, "ratingmax", "6"), out var m) ? m : 6;
                SetChildValue(objNode, "rating", Math.Min(intRating, intMax).ToString(CultureInfo.InvariantCulture));
                if (objXmlSkill["spec"] != null)
                    SetChildValue(objNode, "spec", objXmlSkill["spec"]!.InnerText);
            }

            foreach (XmlNode objXmlGroup in objXmlSkills.SelectNodes("skillgroup")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlGroup["name"]?.InnerText ?? string.Empty;
                if (int.TryParse(objXmlGroup["rating"]?.InnerText, out int intRating))
                    SetSkillGroupRating(strName, intRating);
            }
        }

        private void ApplyPacksKnowledgeSkills(XmlNode objXmlKit)
        {
            XmlNode? objXmlSkills = objXmlKit.SelectSingleNode("knowledgeskills");
            if (objXmlSkills == null)
                return;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSkillsRoot = objRoot.SelectSingleNode("skills");
            if (objSkillsRoot == null)
            {
                objSkillsRoot = Document.CreateElement("skills");
                objRoot.AppendChild(objSkillsRoot);
            }

            foreach (XmlNode objXmlSkill in objXmlSkills.SelectNodes("skill")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlSkill["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(strName))
                    continue;

                string strCategory = objXmlSkill["category"]?.InnerText ?? string.Empty;
                var objSkill = Document.CreateElement("skill");
                AppendElement(objSkill, "name", strName.Trim());
                AppendElement(objSkill, "skillgroup", string.Empty);
                AppendElement(objSkill, "skillcategory", strCategory);
                AppendElement(objSkill, "grouped", "False");
                AppendElement(objSkill, "default", "False");
                AppendElement(objSkill, "rating", objXmlSkill["rating"]?.InnerText ?? "1");
                AppendElement(objSkill, "ratingmax", "6");
                AppendElement(objSkill, "knowledge", "True");
                AppendElement(objSkill, "exotic", "False");
                AppendElement(objSkill, "spec", objXmlSkill["spec"]?.InnerText ?? string.Empty);
                AppendElement(objSkill, "allowdelete", "True");
                AppendElement(objSkill, "attribute", AttributeForKnowledgeCategory(strCategory));
                AppendElement(objSkill, "totalvalue", "0");
                objSkillsRoot.AppendChild(objSkill);
            }
        }

        private void ApplyPacksSpells(XmlNode objXmlKit)
        {
            XmlNode? objXmlSpells = objXmlKit.SelectSingleNode("spells");
            if (objXmlSpells == null)
                return;

            XmlDocument objSpellDoc = XmlManager.Instance.Load("spells.xml");
            var setExisting = new HashSet<string>(Spells.Select(s => s.Name), StringComparer.Ordinal);
            foreach (XmlNode objXmlSpell in objXmlSpells.SelectNodes("spell")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlSpell.InnerText;
                if (string.IsNullOrEmpty(strName) || setExisting.Contains(strName))
                    continue;

                XmlNode? objXmlSpellNode = objSpellDoc.SelectSingleNode($"/chummer/spells/spell[name = '{strName}']");
                if (objXmlSpellNode == null || !IsBookEnabled(objXmlSpellNode["source"]?.InnerText ?? string.Empty))
                    continue;

                AddSpell(strName, objXmlSpellNode["category"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["type"]?.InnerText ?? string.Empty, objXmlSpellNode["range"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["damage"]?.InnerText ?? string.Empty, objXmlSpellNode["duration"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["dv"]?.InnerText ?? string.Empty, objXmlSpellNode["source"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["page"]?.InnerText ?? string.Empty);
                setExisting.Add(strName);
            }
        }

        private void ApplyPacksPowers(XmlNode objXmlKit)
        {
            XmlNode? objXmlPowers = objXmlKit.SelectSingleNode("powers");
            if (objXmlPowers == null)
                return;

            XmlDocument objPowerDoc = XmlManager.Instance.Load("powers.xml");
            foreach (XmlNode objXmlPower in objXmlPowers.SelectNodes("power")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlPower["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlPowerNode = objPowerDoc.SelectSingleNode($"/chummer/powers/power[name = '{strName}']");
                if (objXmlPowerNode == null || !IsBookEnabled(objXmlPowerNode["source"]?.InnerText ?? string.Empty))
                    continue;

                string strRating = objXmlPower["rating"]?.InnerText ?? "1";
                string strSelected = objXmlPower["name"]?.Attributes?["select"]?.InnerText ?? string.Empty;
                AddAdeptPower(strName, strRating, objXmlPowerNode["points"]?.InnerText ?? "0", strSelected);
            }
        }

        private void ApplyPacksComplexForms(XmlNode objXmlKit)
        {
            XmlNode? objXmlPrograms = objXmlKit.SelectSingleNode("programs");
            if (objXmlPrograms == null)
                return;

            XmlDocument objProgramDoc = XmlManager.Instance.Load("programs.xml");
            foreach (XmlNode objXmlProgram in objXmlPrograms.SelectNodes("program")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlProgram["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlProgramNode = objProgramDoc.SelectSingleNode($"/chummer/programs/program[name = '{strName}']");
                if (objXmlProgramNode == null || !IsBookEnabled(objXmlProgramNode["source"]?.InnerText ?? string.Empty))
                    continue;

                string strSelected = objXmlProgram.Attributes?["select"]?.InnerText ?? string.Empty;
                AddComplexForm(strName, objXmlProgramNode["category"]?.InnerText ?? string.Empty,
                    objXmlProgramNode["source"]?.InnerText ?? string.Empty, objXmlProgramNode["page"]?.InnerText ?? string.Empty,
                    strSelected);

                string strGuid = ComplexForms.LastOrDefault(f => f.Name == strName)?.Guid ?? string.Empty;
                if (strGuid.Length == 0)
                    continue;

                foreach (XmlNode objXmlOption in objXmlProgram.SelectNodes("options/option")?.Cast<XmlNode>()
                             ?? Enumerable.Empty<XmlNode>())
                {
                    string strOptionName = objXmlOption["name"]?.InnerText ?? string.Empty;
                    if (strOptionName.Length > 0)
                        AddComplexFormOption(strGuid, strOptionName);
                }
            }
        }

        private void ApplyPacksGear(XmlNode objXmlKit)
        {
            XmlNode? objXmlGears = objXmlKit.SelectSingleNode("gears");
            if (objXmlGears == null)
                return;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objGears = objRoot.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objRoot.AppendChild(objGears);
            }

            XmlDocument objGearDoc = XmlManager.Instance.Load("gear.xml");
            foreach (XmlNode objXmlItem in objXmlGears.SelectNodes("gear")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendPacksGearItem(objGears, objXmlItem, objGearDoc);
        }

        /// <summary>Recursively builds a Gear node (and any nested &lt;gears&gt;/&lt;gear&gt;
        /// plugins) directly, mirroring <see cref="AppendGearNode"/>'s own shape - ported from
        /// frmCreate.cs's AddPACKSGear, simplified to plain Gear (its Commlink/OperatingSystem
        /// special-casing has no effect on this port's flat Response/Signal/System/Firewall
        /// fields, which are read straight from the base gear.xml node either way).</summary>
        private void AppendPacksGearItem(XmlNode objParentList, XmlNode objXmlItem, XmlDocument objGearDoc)
        {
            string strName = objXmlItem["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(strName))
                return;

            string? strCategory = objXmlItem["category"]?.InnerText;
            XmlNode? objXmlGear = string.IsNullOrEmpty(strCategory)
                ? objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strName.Trim()}']")
                : objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strName.Trim()}' and category = '{strCategory}']");
            if (objXmlGear == null || !IsBookEnabled(objXmlGear["source"]?.InnerText ?? string.Empty))
                return;

            string strRating = objXmlItem["rating"]?.InnerText ?? "0";
            string strQty = objXmlItem["qty"]?.InnerText ?? "1";

            var objElement = Document.CreateElement("gear");
            AppendElement(objElement, "name", strName.Trim());
            AppendElement(objElement, "category", objXmlGear["category"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "rating", strRating);
            AppendElement(objElement, "qty", strQty);
            AppendElement(objElement, "cost", objXmlGear["cost"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "avail", objXmlGear["avail"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "capacity", objXmlGear["capacity"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "source", objXmlGear["source"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "page", objXmlGear["page"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "equipped", "False");
            AppendElement(objElement, "guid", Guid.NewGuid().ToString());
            AppendElement(objElement, "active", "False");
            AppendElement(objElement, "location", string.Empty);
            string? strResponse = objXmlGear["response"]?.InnerText;
            if (!string.IsNullOrEmpty(strResponse)) AppendElement(objElement, "response", strResponse);
            string? strSignal = objXmlGear["signal"]?.InnerText;
            if (!string.IsNullOrEmpty(strSignal)) AppendElement(objElement, "signal", strSignal);
            string? strSystem = objXmlGear["system"]?.InnerText;
            if (!string.IsNullOrEmpty(strSystem)) AppendElement(objElement, "system", strSystem);
            string? strFirewall = objXmlGear["firewall"]?.InnerText;
            if (!string.IsNullOrEmpty(strFirewall)) AppendElement(objElement, "firewall", strFirewall);
            var objChildren = Document.CreateElement("children");
            objElement.AppendChild(objChildren);
            objParentList.AppendChild(objElement);

            foreach (XmlNode objXmlChild in objXmlItem.SelectNodes("gears/gear")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendPacksGearItem(objChildren, objXmlChild, objGearDoc);
        }

        private void ApplyPacksNuyen(XmlNode objXmlKit)
        {
            XmlNode? objXmlNuyenBp = objXmlKit.SelectSingleNode("nuyenbp");
            if (objXmlNuyenBp == null || !int.TryParse(objXmlNuyenBp.InnerText, out int intAmount))
                return;

            if (string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase))
                intAmount *= 2;

            int intCurrent = int.TryParse(Nuyen, out var n) ? n : 0;
            Nuyen = (intCurrent + intAmount).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Removes the first root-level saved Cyberware/Bioware item matching its
        /// name/category/rating and Cyberware-vs-Bioware source, along with any Improvements its
        /// own &lt;bonus&gt; block granted on add.</summary>
        public IReadOnlyList<string> GearLocations => (IReadOnlyList<string>?)Document.SelectNodes("/character/gearlocations/gearlocation")?
            .Cast<XmlNode>().Select(objNode => objNode.InnerText).Where(strName => !string.IsNullOrWhiteSpace(strName))
            .Distinct(StringComparer.Ordinal).ToList() ?? Array.Empty<string>();

        public bool AddGearLocation(string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0 || GearLocations.Contains(strName, StringComparer.Ordinal)) return false;
            var objRoot = Document.DocumentElement;
            if (objRoot == null) return false;
            XmlElement? objLocations = objRoot.SelectSingleNode("gearlocations") as XmlElement;
            if (objLocations == null)
            {
                objLocations = Document.CreateElement("gearlocations");
                objRoot.AppendChild(objLocations);
            }
            AppendElement(objLocations, "gearlocation", strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a named Gear location, clearing it from any root-level Gear that was
        /// assigned to it - mirrors RemoveWeaponLocation's behavior for weapons.</summary>
        public bool RemoveGearLocation(string strName)
        {
            strName = strName.Trim();
            XmlNode? objLocation = Document.SelectNodes("/character/gearlocations/gearlocation")?.Cast<XmlNode>()
                .FirstOrDefault(objNode => string.Equals(objNode.InnerText, strName, StringComparison.Ordinal));
            if (objLocation?.ParentNode == null) return false;
            objLocation.ParentNode.RemoveChild(objLocation);
            XmlNodeList? objGearNodes = Document.SelectNodes("/character/gears/gear");
            if (objGearNodes != null)
                foreach (XmlNode objGear in objGearNodes)
                    if (string.Equals(GetValue(objGear, "location", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objGear, "location", string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns a root-level Gear item (by its depth-first GearId) to one of the
        /// character's own named locations, or clears it back to unassigned with an empty
        /// <paramref name="strLocation"/>.</summary>
        public bool SetGearLocation(int intGearId, string strLocation)
        {
            strLocation = strLocation.Trim();
            if (strLocation.Length > 0 && !GearLocations.Contains(strLocation, StringComparer.Ordinal))
                return false;
            XmlNode? objGear = GetGearNodeById(intGearId);
            // Only root-level Gear (a direct child of <gears>, not nested under another item) can
            // have a location - matches the legacy tree, which only ever lets you drag a top-level
            // item into a location.
            if (objGear == null || objGear.ParentNode?.Name != "gears")
                return false;
            SetChildValue(objGear, "location", strLocation);
            Changed?.Invoke();
            return true;
        }

        private IReadOnlyList<CharacterTreeItemData> ReadGearTree()
        {
            var objNodes = Document.SelectNodes("/character/gears/gear");
            var lstItems = new List<CharacterTreeItemData>();
            if (objNodes == null) return lstItems;

            var dicLocations = new Dictionary<string, CharacterTreeItemData>(StringComparer.Ordinal);
            foreach (string strLocation in GearLocations)
            {
                var objLocationNode = new CharacterTreeItemData(strLocation, "Gear location");
                dicLocations.Add(strLocation, objLocationNode);
                lstItems.Add(objLocationNode);
            }

            int intNextId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                var objItem = ReadGearTreeItem(objNode, ref intNextId);
                string strLocation = GetValue(objNode, "location", string.Empty);
                objItem.SetLocation(strLocation);
                if (!string.IsNullOrEmpty(strLocation) && dicLocations.TryGetValue(strLocation, out var objLocationNode2))
                    objLocationNode2.Children.Add(objItem);
                else
                    lstItems.Add(objItem);
            }

            if (GetCharacterOptions().CalculateCommlinkResponse)
                ApplyCommlinkResponsePenalties(lstItems);

            return lstItems;
        }

        /// <summary>Ported from clsEquipment.cs's Commlink.TotalResponse: under the
        /// CalculateCommlinkResponse house rule, a Commlink's Response drops by
        /// floor(running programs / TotalSystem) - "programs" are direct child Gear items whose
        /// category is one of IsProgram's real gear.xml categories, currently Equipped, and not
        /// exempted by the ErgonomicProgramLimit house rule. By default (the house rule off) an
        /// "Ergonomic" child plugin grants no exemption - the program counts like any other; only
        /// when the house rule is explicitly turned on are Ergonomic programs excluded from the
        /// count (legacy's own inverted-sounding but exact naming/logic, preserved as-is). Walks
        /// the whole tree recursively (not just root items) since a Commlink can be nested under
        /// Armor/Cyberware, matching how EffectiveResponse itself already works regardless of
        /// depth.</summary>
        private void ApplyCommlinkResponsePenalties(IEnumerable<CharacterTreeItemData> lstItems)
        {
            bool blnErgonomicProgramLimit = GetCharacterOptions().ErgonomicProgramLimit;
            foreach (CharacterTreeItemData objItem in lstItems)
            {
                if (objItem.Category == "Commlink"
                    && double.TryParse(objItem.EffectiveSystem, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblSystem)
                    && dblSystem > 0)
                {
                    int intRunningPrograms = objItem.Children.Count(objChild =>
                        IsProgramCategory(objChild.Category) && objChild.Equipped
                        && (!IsErgonomic(objChild) || !blnErgonomicProgramLimit));
                    objItem.SetResponsePenalty((int)Math.Floor(intRunningPrograms / dblSystem));
                }

                ApplyCommlinkResponsePenalties(objItem.Children);
            }
        }

        private static bool IsErgonomic(CharacterTreeItemData objGear) =>
            objGear.Children.Any(objChild => objChild.Name == "Ergonomic");

        // Assigns GearId in the same depth-first order EnumerateGearNodesDfs walks the raw XML in,
        // so an ID handed back from the UI always resolves to the same node via GetGearNodeById.
        private static Dictionary<string, string>? _dicGearNameTranslations;
        private static string? _strGearTranslationsLanguage;

        // Cached per-language, same "<translate> child element" mechanism the Avalonia gear
        // picker (GearDialogViewModel) uses - keeps the Ausrüstung tree's names in sync with
        // whatever language pack the user has selected, instead of always showing the raw
        // (English) name that's actually saved in the character file.
        private static string GetGearTranslatedName(string strName)
        {
            string strLanguage = GlobalOptions.Instance.Language;
            if (_dicGearNameTranslations == null || _strGearTranslationsLanguage != strLanguage)
            {
                _dicGearNameTranslations = BuildGearNameTranslations();
                _strGearTranslationsLanguage = strLanguage;
            }
            return _dicGearNameTranslations.TryGetValue(strName, out string? strTranslate) ? strTranslate : strName;
        }

        private static Dictionary<string, string> BuildGearNameTranslations()
        {
            var dicResult = new Dictionary<string, string>();
            XmlDocument objDocument = XmlManager.Instance.Load("gear.xml");
            XmlNodeList? objNodes = objDocument.SelectNodes("/chummer/gears/gear");
            if (objNodes == null) return dicResult;

            foreach (XmlNode objNode in objNodes)
            {
                string strName = objNode["name"]?.InnerText ?? string.Empty;
                string? strTranslate = objNode["translate"]?.InnerText;
                if (!string.IsNullOrEmpty(strName) && !string.IsNullOrEmpty(strTranslate))
                    dicResult[strName] = strTranslate!;
            }
            return dicResult;
        }

        private CharacterTreeItemData ReadGearTreeItem(XmlNode objNode, ref int intNextId)
        {
            var objItem = ReadTreeItem(objNode);
            objItem.SetTranslatedName(GetGearTranslatedName(objItem.Name));
            objItem.SetGearId(intNextId);
            objItem.SetGearDetails(GetValue(objNode, "capacity", string.Empty), GetValue(objNode, "response", string.Empty),
                GetValue(objNode, "signal", string.Empty), GetValue(objNode, "system", string.Empty),
                GetValue(objNode, "firewall", string.Empty), GetValue(objNode, "active", "False") == "True");
            intNextId++;

            var objChildren = objNode.SelectNodes("children/gear");
            if (objChildren == null) return objItem;

            foreach (XmlNode objChild in objChildren)
                objItem.Children.Add(ReadGearTreeItem(objChild, ref intNextId));
            return objItem;
        }

        // Cyberware and bioware are saved to the same <cyberwares> list and only distinguished by
        // <improvementsource> ("Cyberware" vs "Bioware") - split here so each gets its own tree.
        // Both read through the same ID-assigning walk (see ReadCyberwareOrBiowareTree) so a
        // CyberwareId handed back from either tree's UI always resolves to the same node via
        // GetCyberwareNodeById, matching the Gear tree's GearId/MoveGear pattern.
        private IReadOnlyList<CharacterSpiritData>? _cachedSpirits;
        public IReadOnlyList<CharacterSpiritData> Spirits
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedSpirits short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedSpirits ??= ReadSpirits();
            }
        }

        /// <summary>Saved bonded Foci, joined to their linked Gear by GUID. Focus records are
        /// separate from Gear in the legacy file, so a broken GearId is preserved and surfaced
        /// instead of silently dropping the player's bonded record.</summary>
        private IReadOnlyList<CharacterFocusData>? _cachedFoci;
        public IReadOnlyList<CharacterFocusData> Foci
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedFoci short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedFoci ??= ReadFoci();
            }
        }

        /// <summary>Saved Stacked Foci, retaining their composite Gear identity, bonded state,
        /// and the component-Gear snapshots stored by legacy character files.</summary>
        private IReadOnlyList<CharacterStackedFocusData>? _cachedStackedFoci;
        public IReadOnlyList<CharacterStackedFocusData> StackedFoci
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedStackedFoci short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedStackedFoci ??= ReadStackedFoci();
            }
        }

        /// <summary>Reports whether a Focus Gear can be bonded under the legacy MAG count and
        /// total-Force limits. Cost and bonus application are deliberately handled by the binding
        /// transaction, not by this pure validation method.</summary>
        public bool CanBondFocus(Guid guiGearId)
        {
            XmlNode? objGear = FindGearNodeByGuid(guiGearId);
            if (objGear == null || (GetValue(objGear, "category", string.Empty) != "Foci"
                && GetValue(objGear, "category", string.Empty) != "Metamagic Foci"))
                return false;
            if (Foci.Any(focus => string.Equals(focus.GearId, guiGearId.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;

            int intMag = GetAttributeInt("MAG");
            int intRating = ParseInteger(GetValue(objGear, "rating", "0"));
            return intMag > 0 && Foci.Count < intMag
                && Foci.Sum(focus => ParseInteger(focus.Rating)) + intRating <= intMag * 5;
        }

        /// <summary>Returns the Karma needed to bind a normal Focus Gear, using the active
        /// character profile's legacy per-focus multiplier. A non-Focus Gear has no binding cost.</summary>
        public int? GetFocusBindingKarmaCost(Guid guiGearId)
        {
            XmlNode? objGear = FindGearNodeByGuid(guiGearId);
            if (objGear == null || (GetValue(objGear, "category", string.Empty) != "Foci"
                && GetValue(objGear, "category", string.Empty) != "Metamagic Foci"))
                return null;

            int intRating = ParseInteger(GetValue(objGear, "rating", "0"));
            if (intRating < 1)
                return null;
            return intRating * GetFocusKarmaMultiplier(GetValue(objGear, "name", string.Empty));
        }

        /// <summary>Ports frmCareer's normal-Focus binding transaction: validates the MAG limits
        /// and available Karma, writes the legacy Focus record and bonded Gear flag, records the
        /// Karma expense with undo information, and applies the Gear bonus when equipped.</summary>
        public bool BindFocus(Guid guiGearId)
        {
            if (!CanBondFocus(guiGearId))
                return false;

            XmlNode? objGear = FindGearNodeByGuid(guiGearId);
            int? intCost = GetFocusBindingKarmaCost(guiGearId);
            if (objGear == null || intCost is not > 0
                || !int.TryParse(Karma, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intKarma)
                || intKarma < intCost.Value)
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objFoci = objRoot.SelectSingleNode("foci") as XmlElement;
            if (objFoci == null)
            {
                objFoci = Document.CreateElement("foci");
                objRoot.AppendChild(objFoci);
            }

            string strRating = GetValue(objGear, "rating", "0");
            string strName = GetFocusDisplayName(objGear, strRating);
            string strFocusId = Guid.NewGuid().ToString();
            var objFocus = Document.CreateElement("focus");
            AppendElement(objFocus, "guid", strFocusId);
            AppendElement(objFocus, "name", strName);
            AppendElement(objFocus, "gearid", guiGearId.ToString());
            AppendElement(objFocus, "rating", strRating);
            objFoci.AppendChild(objFocus);
            SetChildValue(objGear, "bonded", "True");

            if (GetValue(objGear, "equipped", "False") == "True")
                ApplyFocusGearBonus(objGear, guiGearId, strRating);

            Karma = (intKarma - intCost.Value).ToString(CultureInfo.InvariantCulture);
            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.BindFocus, guiGearId.ToString());
            AddExpense("Karma", -intCost.Value, "Bound " + strName, null, objUndo);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Unbinds a normal Focus by its Focus-record GUID. Karma is not refunded,
        /// matching the legacy tree's unchecked path.</summary>
        public bool UnbindFocus(Guid guiFocusId)
        {
            XmlNode? objFocus = Document.SelectNodes("/character/foci/focus")?.Cast<XmlNode>().FirstOrDefault(node =>
                string.Equals(GetValue(node, "guid", string.Empty), guiFocusId.ToString(), StringComparison.OrdinalIgnoreCase));
            if (objFocus == null)
                return false;

            string strGearId = GetValue(objFocus, "gearid", string.Empty);
            if (!Guid.TryParse(strGearId, out Guid guiGearId))
                return false;

            XmlNode? objGear = FindGearNodeByGuid(guiGearId);
            if (objGear == null)
                return false;

            SetChildValue(objGear, "bonded", "False");
            RemoveBonusImprovements(ImprovementSource.Gear, guiGearId.ToString());
            objFocus.ParentNode?.RemoveChild(objFocus);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Focus Gear eligible to go into a new Stacked Focus - unbonded, matching <see
        /// cref="CreateStackedFocus"/>'s own eligibility checks (a stack's component Gear is
        /// removed from the main tree once stacked, so it can't reappear here). Used to populate
        /// the Avalonia "Create Stacked Focus" picker.</summary>
        public IReadOnlyList<(Guid GearId, string DisplayName)> GetStackableFocusGear() =>
            EnumerateGearNodesDfs()
                .Where(objGear => (GetValue(objGear, "category", string.Empty) == "Foci"
                        || GetValue(objGear, "category", string.Empty) == "Metamagic Foci")
                    && GetValue(objGear, "bonded", "False") != "True")
                .Select(objGear => (Guid.Parse(GetValue(objGear, "guid", string.Empty)),
                    GetFocusDisplayName(objGear, GetValue(objGear, "rating", "0"))))
                .ToList();

        /// <summary>Replaces two or more unbonded Focus Gear items with the legacy composite
        /// Stacked Focus representation. Component Gear is cloned verbatim into the stacked-focus
        /// record so later binding/unstacking retains all saved item data.</summary>
        public bool CreateStackedFocus(IReadOnlyCollection<Guid> lstGearIds)
        {
            if (lstGearIds == null || lstGearIds.Count < 2 || lstGearIds.Distinct().Count() != lstGearIds.Count)
                return false;

            var lstGears = lstGearIds.Select(FindGearNodeByGuid).ToList();
            if (lstGears.Any(objGear => objGear == null) || lstGears.Any(objGear =>
                    GetValue(objGear!, "category", string.Empty) != "Foci"
                    && GetValue(objGear!, "category", string.Empty) != "Metamagic Foci")
                || lstGears.Any(objGear => GetValue(objGear!, "bonded", "False") == "True")
                || lstGears.Any(objGear => Foci.Any(focus => string.Equals(focus.GearId,
                    GetValue(objGear!, "guid", string.Empty), StringComparison.OrdinalIgnoreCase))))
                return false;

            int intTotalForce = lstGears.Sum(objGear => ParseInteger(GetValue(objGear!, "rating", "0")));
            if (intTotalForce < 1 || (!GetCharacterOptions().AllowHigherStackedFoci && intTotalForce > 6))
                return false;

            XmlElement objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            XmlElement objGears = objRoot.SelectSingleNode("gears") as XmlElement
                ?? throw new InvalidOperationException("A character with Focus Gear has no gears collection.");
            int intCost = lstGears.Sum(objGear => ReadTreeItem(objGear!).CalculatedCost);
            string strComponentNames = string.Join(", ", lstGears.Select(objGear => GetValue(objGear!, "name", string.Empty)));
            XmlElement objComposite = AppendGearNode(objGears, "Stacked Focus: " + strComponentNames, "Stacked Focus",
                "0", "1", intCost.ToString(CultureInfo.InvariantCulture), "0", "SM", "84", "", "", "", "", "");
            string strCompositeId = GetValue(objComposite, "guid", string.Empty);

            XmlElement objStacks = objRoot.SelectSingleNode("stackedfoci") as XmlElement;
            if (objStacks == null)
            {
                objStacks = Document.CreateElement("stackedfoci");
                objRoot.AppendChild(objStacks);
            }
            XmlElement objStack = Document.CreateElement("stackedfocus");
            AppendElement(objStack, "guid", Guid.NewGuid().ToString());
            AppendElement(objStack, "gearid", strCompositeId);
            AppendElement(objStack, "bonded", "False");
            XmlElement objComponents = Document.CreateElement("gears");
            foreach (XmlNode objGear in lstGears!)
                objComponents.AppendChild(objGear.CloneNode(deep: true));
            objStack.AppendChild(objComponents);
            objStacks.AppendChild(objStack);
            foreach (XmlNode objGear in lstGears!)
                objGear.ParentNode?.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Career-mode cost preview for <see cref="BindStackedFocus"/> - sums each
        /// component Gear's Rating times its own per-focus-type Karma multiplier, the same formula
        /// the bind transaction itself uses. Null if the stack doesn't exist or has no components.</summary>
        public int? GetStackedFocusBindingKarmaCost(Guid guiStackId)
        {
            XmlNode? objStack = FindStackedFocusNode(guiStackId);
            var lstComponents = objStack?.SelectNodes("gears/gear")?.Cast<XmlNode>().ToList();
            if (lstComponents == null || lstComponents.Count == 0)
                return null;
            return lstComponents.Sum(component => ParseInteger(GetValue(component, "rating", "0"))
                * GetFocusKarmaMultiplier(GetValue(component, "name", string.Empty)));
        }

        /// <summary>Restores an unbonded Stacked Focus' saved component Gear snapshots and
        /// removes its generated composite Gear. A bonded stack must be unbound first.</summary>
        public bool BindStackedFocus(Guid guiStackId)
        {
            XmlNode? objStack = FindStackedFocusNode(guiStackId);
            if (objStack == null || GetValue(objStack, "bonded", "False") == "True"
                || !Guid.TryParse(GetValue(objStack, "gearid", string.Empty), out Guid guiGearId))
                return false;
            XmlNode? objComposite = FindGearNodeByGuid(guiGearId);
            var lstComponents = objStack.SelectNodes("gears/gear")?.Cast<XmlNode>().ToList() ?? new List<XmlNode>();
            int intForce = lstComponents.Sum(component => ParseInteger(GetValue(component, "rating", "0")));
            int intMag = GetAttributeInt("MAG");
            if (objComposite == null || lstComponents.Count == 0 || intMag <= 0
                || Foci.Count + StackedFoci.Count(focus => focus.Bonded) + 1 > intMag
                || Foci.Sum(focus => ParseInteger(focus.Rating)) + StackedFoci.Where(focus => focus.Bonded)
                    .Sum(focus => focus.TotalForce) + intForce > intMag * 5)
                return false;
            int intCost = GetStackedFocusBindingKarmaCost(guiStackId) ?? 0;
            if (!int.TryParse(Karma, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intKarma)
                || intCost < 1 || intKarma < intCost)
                return false;

            SetChildValue(objStack, "bonded", "True");
            if (GetValue(objComposite, "equipped", "False") == "True")
                ApplyStackedFocusBonuses(objStack, guiStackId);
            Karma = (intKarma - intCost).ToString(CultureInfo.InvariantCulture);
            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.BindFocus, guiStackId.ToString());
            AddExpense("Karma", -intCost, "Bound Stacked Focus", null, objUndo);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? FindGearNodeByGuid(Guid guiGearId) => EnumerateGearNodesDfs().FirstOrDefault(node =>
            string.Equals(GetValue(node, "guid", string.Empty), guiGearId.ToString(), StringComparison.OrdinalIgnoreCase));

        private void ApplyFocusGearBonus(XmlNode objGear, Guid guiGearId, string strRating)
        {
            string strName = GetValue(objGear, "name", string.Empty);
            string strCategory = GetValue(objGear, "category", string.Empty);
            XmlNode? objRulesGear = XmlManager.Instance.Load("gear.xml").SelectNodes("/chummer/gears/gear")?
                .Cast<XmlNode>().FirstOrDefault(node => GetValue(node, "name", string.Empty) == strName
                    && GetValue(node, "category", string.Empty) == strCategory);
            ApplyBonus(objRulesGear?.SelectSingleNode("bonus"), ImprovementSource.Gear, guiGearId.ToString(), strRating);
        }

        private string GetFocusDisplayName(XmlNode objGear, string strRating)
        {
            string strName = GetValue(objGear, "name", string.Empty);
            string strExtra = GetValue(objGear, "extra", string.Empty);
            if (!string.IsNullOrWhiteSpace(strExtra) && !strName.Contains("("))
                strName += " (" + strExtra + ")";
            return strName + " (Force " + strRating + ")";
        }

        private IReadOnlyList<CharacterFocusData> ReadFoci()
        {
            var lstFoci = new List<CharacterFocusData>();
            var objNodes = Document.SelectNodes("/character/foci/focus");
            if (objNodes == null) return lstFoci;
            foreach (XmlNode objNode in objNodes)
            {
                string strGearId = GetValue(objNode, "gearid", string.Empty);
                XmlNode? objGear = EnumerateGearNodesDfs().FirstOrDefault(node =>
                    string.Equals(GetValue(node, "guid", string.Empty), strGearId, StringComparison.OrdinalIgnoreCase));
                lstFoci.Add(new CharacterFocusData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), strGearId, GetValue(objNode, "rating", "0"),
                    objGear != null, objGear == null ? string.Empty : GetValue(objGear, "category", string.Empty)));
            }
            return lstFoci;
        }

        private IReadOnlyList<CharacterStackedFocusData> ReadStackedFoci()
        {
            var lstFoci = new List<CharacterStackedFocusData>();
            XmlNodeList? objNodes = Document.SelectNodes("/character/stackedfoci/stackedfocus");
            if (objNodes == null) return lstFoci;
            foreach (XmlNode objNode in objNodes)
            {
                string strGearId = GetValue(objNode, "gearid", string.Empty);
                XmlNode? objCompositeGear = Guid.TryParse(strGearId, out Guid guiGearId)
                    ? FindGearNodeByGuid(guiGearId) : null;
                var lstComponents = new List<CharacterStackedFocusGearData>();
                XmlNodeList? objGears = objNode.SelectNodes("gears/gear");
                if (objGears != null)
                    foreach (XmlNode objGear in objGears)
                        lstComponents.Add(new CharacterStackedFocusGearData(GetValue(objGear, "guid", string.Empty),
                            GetValue(objGear, "name", string.Empty), GetValue(objGear, "category", string.Empty),
                            GetValue(objGear, "rating", "0")));

                lstFoci.Add(new CharacterStackedFocusData(GetValue(objNode, "guid", string.Empty), strGearId,
                    GetValue(objNode, "bonded", "False") == "True", objCompositeGear != null,
                    objCompositeGear == null ? string.Empty : GetValue(objCompositeGear, "name", string.Empty),
                    lstComponents));
            }
            return lstFoci;
        }

        private IReadOnlyList<CharacterCommlinkData> ReadCommlinks()
        {
            var lstCommlinks = new List<CharacterCommlinkData>();
            XmlNodeList? objNodes = Document.SelectNodes("//gear[category = 'Commlink']");
            if (objNodes == null)
                return lstCommlinks;

            foreach (XmlNode objNode in objNodes)
            {
                string strGuid = GetValue(objNode, "guid", string.Empty);
                if (string.IsNullOrEmpty(strGuid))
                    continue;

                lstCommlinks.Add(new CharacterCommlinkData(
                    strGuid,
                    GetValue(objNode, "name", string.Empty),
                    int.TryParse(GetValue(objNode, "response", "0"), out var intResponse) ? intResponse : 0,
                    GetValue(objNode, "equipped", "False") == "True",
                    GetValue(objNode, "active", "False") == "True"));
            }

            return lstCommlinks;
        }

    }
}
