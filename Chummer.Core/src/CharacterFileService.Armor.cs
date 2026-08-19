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
        public bool ArmorDegradationEnabled => GetCharacterOptions().ArmorDegradation;

        /// <summary>Remaining Build Points, only meaningful when <see cref="BuildMethod"/> is "BP".</summary>
        private void ApplyPacksArmor(XmlNode objXmlKit)
        {
            XmlNode? objXmlArmors = objXmlKit.SelectSingleNode("armors");
            if (objXmlArmors == null)
                return;

            XmlDocument objArmorDoc = XmlManager.Instance.Load("armor.xml");
            foreach (XmlNode objXmlArmor in objXmlArmors.SelectNodes("armor")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlArmor["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlArmorNode = objArmorDoc.SelectSingleNode($"/chummer/armors/armor[name = '{strName}']");
                if (objXmlArmorNode == null || !IsBookEnabled(objXmlArmorNode["source"]?.InnerText ?? string.Empty))
                    continue;

                AddArmor(strName, objXmlArmorNode["category"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["b"]?.InnerText ?? string.Empty, objXmlArmorNode["i"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["armorcapacity"]?.InnerText ?? string.Empty, objXmlArmorNode["cost"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["avail"]?.InnerText ?? string.Empty, objXmlArmorNode["source"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["page"]?.InnerText ?? string.Empty);
            }
        }

        public void AddArmor(string strName, string strCategory, string strB, string strI, string strCapacity,
            string strCost, string strAvail, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("An armor name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objArmors = objRoot.SelectSingleNode("armors");
            if (objArmors == null)
            {
                objArmors = Document.CreateElement("armors");
                objRoot.AppendChild(objArmors);
            }

            var objArmor = Document.CreateElement("armor");
            AppendElement(objArmor, "name", strName.Trim());
            AppendElement(objArmor, "category", strCategory);
            AppendElement(objArmor, "b", strB);
            AppendElement(objArmor, "i", strI);
            AppendElement(objArmor, "armorcapacity", strCapacity);
            AppendElement(objArmor, "cost", strCost);
            AppendElement(objArmor, "avail", strAvail);
            AppendElement(objArmor, "source", strSource);
            AppendElement(objArmor, "page", strPage);
            AppendElement(objArmor, "armorname", string.Empty);
            AppendElement(objArmor, "armorset", string.Empty);
            AppendElement(objArmor, "equipped", "True");
            AppendElement(objArmor, "ballisticdamage", "0");
            AppendElement(objArmor, "impactdamage", "0");
            objArmor.AppendChild(Document.CreateElement("armormods"));
            objArmor.AppendChild(Document.CreateElement("gears"));
            objArmors.AppendChild(objArmor);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
        }

        /// <summary>Creates a named armor bundle. A bundle is persisted independently so that an
        /// empty set remains visible and can receive armor later.</summary>
        public bool AddArmorSet(string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0 || ArmorSets.Contains(strName, StringComparer.Ordinal))
                return false;

            var objRoot = Document.DocumentElement;
            if (objRoot == null)
                return false;
            XmlElement? objSets = objRoot.SelectSingleNode("armorbundles") as XmlElement;
            if (objSets == null)
            {
                objSets = Document.CreateElement("armorbundles");
                objRoot.AppendChild(objSets);
            }
            AppendElement(objSets, "armorbundle", strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns a root-level armor item to a bundle, or to the ungrouped root when
        /// <paramref name="strSetName"/> is empty.</summary>
        public bool SetArmorSet(int intArmorId, string strSetName)
        {
            strSetName = strSetName.Trim();
            if (!string.IsNullOrEmpty(strSetName) && !ArmorSets.Contains(strSetName, StringComparer.Ordinal))
                return false;
            XmlNode? objArmor = GetArmorNodeById(intArmorId);
            if (objArmor == null || string.Equals(GetArmorSetName(objArmor), strSetName, StringComparison.Ordinal))
                return false;

            // Earlier Avalonia builds used armorname for grouping. Preserve such old saves on
            // read, but migrate the assignment out before writing any new set choice so the true
            // legacy armorname field can safely hold a player label again.
            string strLegacySet = GetValue(objArmor, "armorname", string.Empty);
            if (string.IsNullOrEmpty(GetValue(objArmor, "armorset", string.Empty))
                && ArmorSets.Contains(strLegacySet, StringComparer.Ordinal))
                SetChildValue(objArmor, "armorname", string.Empty);
            SetChildValue(objArmor, "armorset", strSetName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves a root armor item immediately before another one. The operation uses
        /// transient tree IDs rather than name/category, which keeps duplicate armor purchases
        /// independently addressable without changing legacy character-file shape.</summary>
        public bool MoveArmor(int intSourceArmorId, int intTargetArmorId)
        {
            XmlNode? objSource = GetArmorNodeById(intSourceArmorId);
            XmlNode? objTarget = GetArmorNodeById(intTargetArmorId);
            if (objSource == null || objTarget == null || objSource == objTarget
                || objSource.ParentNode == null || objSource.ParentNode != objTarget.ParentNode)
                return false;

            objSource.ParentNode.RemoveChild(objSource);
            objTarget.ParentNode.InsertBefore(objSource, objTarget);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Deletes a bundle and moves all of its armor back to the ungrouped root.</summary>
        public bool RemoveArmorSet(string strName)
        {
            strName = strName.Trim();
            XmlNode? objSet = Document.SelectNodes("/character/armorbundles/armorbundle")?
                .Cast<XmlNode>().FirstOrDefault(objNode => string.Equals(objNode.InnerText, strName, StringComparison.Ordinal));
            if (objSet?.ParentNode == null)
                return false;
            objSet.ParentNode.RemoveChild(objSet);
            XmlNodeList? objArmor = Document.SelectNodes("/character/armors/armor");
            if (objArmor != null)
                foreach (XmlNode objNode in objArmor)
                    if (string.Equals(GetValue(objNode, "armorset", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objNode, "armorset", string.Empty);
                    else if (string.IsNullOrEmpty(GetValue(objNode, "armorset", string.Empty))
                        && string.Equals(GetValue(objNode, "armorname", string.Empty), strName, StringComparison.Ordinal))
                        // Compatibility cleanup for port saves written before <armorset> existed.
                        SetChildValue(objNode, "armorname", string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first root-level saved armor matching its name/category.</summary>
        public bool RemoveArmor(string strName, string strCategory)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return false;

            foreach (XmlNode objArmor in objNodes)
            {
                if (!string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;

                objArmor.ParentNode?.RemoveChild(objArmor);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Adds Gear inside one root Armor item, preserving the legacy
        /// <c>armor/gears/gear</c> containment shape and normal purchase accounting.</summary>
        public bool AddArmorGear(int intArmorId, string strName, string strCategory, string strRating = "0",
            string strQty = "1", string strCost = "", string strAvail = "", string strSource = "",
            string strPage = "", string strCapacity = "")
        {
            if (string.IsNullOrWhiteSpace(strName) || !StickNShockAllowed(strName))
                return false;
            XmlNode? objArmor = GetArmorNodeById(intArmorId);
            XmlElement? objGears = objArmor?.SelectSingleNode("gears") as XmlElement;
            if (objGears == null)
                return false;
            XmlElement objGear = AppendGearNode(objGears, strName, strCategory, strRating, strQty, strCost,
                strAvail, strSource, strPage, strCapacity, string.Empty, string.Empty, string.Empty, string.Empty);
            AppendAutomaticProgramOptions(objGear);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct armor-contained Gear item by its persistent GUID.</summary>
        public bool RemoveArmorGear(int intArmorId, Guid guiGearId)
        {
            XmlNode? objGear = GetArmorNodeById(intArmorId)?.SelectSingleNode($"gears/gear[guid = '{guiGearId}']");
            if (objGear?.ParentNode == null)
                return false;
            objGear.ParentNode.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a Gear plugin below a direct armor-contained Gear item.</summary>
        public bool AddArmorGearPlugin(int intArmorId, Guid guiParentGearId, string strName, string strCategory,
            string strRating = "0", string strQty = "1", string strCost = "", string strAvail = "",
            string strSource = "", string strPage = "", string strCapacity = "")
        {
            XmlNode? objParent = GetArmorNodeById(intArmorId)?.SelectSingleNode($"gears/gear[guid = '{guiParentGearId}']");
            if (objParent == null || string.IsNullOrWhiteSpace(strName)
                || (GetCharacterOptions().EnforceCapacity && !GearCapacityAllowsChild(objParent, strCapacity, strQty)))
                return false;
            XmlElement objChildren = objParent.SelectSingleNode("children") as XmlElement
                ?? (XmlElement)objParent.AppendChild(Document.CreateElement("children"));
            XmlElement objGear = AppendGearNode(objChildren, strName, strCategory, strRating, strQty, strCost,
                strAvail, strSource, strPage, strCapacity, string.Empty, string.Empty, string.Empty, string.Empty);
            AppendAutomaticProgramOptions(objGear);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root Armor
        /// item (including its own installed mods/gear cost).</summary>
        public bool SellArmor(string strName, string strCategory, double dblSellPercent)
        {
            XmlNode? objNode = Document.SelectNodes("/character/armors/armor")?.Cast<XmlNode>()
                .FirstOrDefault(n => string.Equals(GetValue(n, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(n, "category", string.Empty), strCategory, StringComparison.Ordinal));
            if (objNode == null)
                return false;

            int intRefund = ComputeSellRefund(
                ReadTreeItem(objNode, "armormods/armormod", "gears/gear").CalculatedCost, dblSellPercent);
            if (!RemoveArmor(strName, strCategory))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        /// <summary>Equips or unequips the first root-level saved armor matching its name/category -
        /// only equipped armor counts toward <see cref="ArmorEncumbrance"/> and the worn ballistic/
        /// impact rating shown elsewhere.</summary>
        public bool SetArmorEquipped(string strName, string strCategory, bool blnEquipped)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return false;

            foreach (XmlNode objArmor in objNodes)
            {
                if (!string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;

                SetChildValue(objArmor, "equipped", blnEquipped ? "True" : "False");
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Applies or repairs AR 44 armor degradation. Positive deltas add damage and
        /// lower the displayed ballistic/impact rating; negative deltas repair it. The damage is
        /// clamped to the armor's current base rating and is unavailable unless the house rule is on.</summary>
        public bool AdjustArmorDegradation(string strName, string strCategory, int intBallisticDelta, int intImpactDelta)
        {
            if (!ArmorDegradationEnabled)
                return false;
            XmlNode? objArmor = FindArmorNode(strName, strCategory);
            if (objArmor == null)
                return false;
            int intBallisticMax = Math.Max(0, ParseInteger(GetValue(objArmor, "b", "0"))
                + (objArmor.SelectNodes("armormods/armormod")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                .Where(m => GetValue(m, "equipped", "True") == "True").Sum(m => ParseInteger(GetValue(m, "b", "0"))));
            int intImpactMax = Math.Max(0, ParseInteger(GetValue(objArmor, "i", "0"))
                + (objArmor.SelectNodes("armormods/armormod")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                .Where(m => GetValue(m, "equipped", "True") == "True").Sum(m => ParseInteger(GetValue(m, "i", "0"))));
            int intBallistic = Math.Clamp(ParseInteger(GetValue(objArmor, "ballisticdamage", "0")) + intBallisticDelta,
                0, intBallisticMax);
            int intImpact = Math.Clamp(ParseInteger(GetValue(objArmor, "impactdamage", "0")) + intImpactDelta,
                0, intImpactMax);
            SetChildValue(objArmor, "ballisticdamage", intBallistic.ToString(CultureInfo.InvariantCulture));
            SetChildValue(objArmor, "impactdamage", intImpact.ToString(CultureInfo.InvariantCulture));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds an Armor Modification to a root-level Armor item, matched by name+category
        /// (this port's Armor items have no guid, same lookup approach as <see
        /// cref="RemoveArmor"/>/<see cref="SetArmorEquipped"/>) - deducts cost and applies the
        /// mod's own rules-data &lt;bonus&gt; block at the given Rating. Armor Suit Capacity and
        /// Maximum Armor Modifications are enforced when their respective house rules are on.</summary>
        public bool AddArmorMod(string strArmorName, string strArmorCategory, string strName, string strRating,
            string strB, string strI, string strAvail, string strCost, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("An armor mod name is required.", nameof(strName));

            XmlNode? objArmor = FindArmorNode(strArmorName, strArmorCategory);
            XmlNode? objMods = objArmor?.SelectSingleNode("armormods");
            if (objArmor == null || objMods == null)
                return false;

            XmlDocument objArmorDoc = XmlManager.Instance.Load("armor.xml");
            XmlNode? objXmlMod = objArmorDoc.SelectSingleNode($"/chummer/mods/mod[name = '{strName.Trim()}']");
            string strArmorCapacity = objXmlMod == null ? string.Empty
                : GetValue(objXmlMod, "armorcapacity", string.Empty);
            if (!ArmorHasCapacityForMod(objArmor, strArmorCapacity, strRating))
                return false;

            var objMod = Document.CreateElement("armormod");
            AppendElement(objMod, "guid", Guid.NewGuid().ToString());
            AppendElement(objMod, "name", strName.Trim());
            AppendElement(objMod, "rating", strRating);
            AppendElement(objMod, "b", strB);
            AppendElement(objMod, "i", strI);
            AppendElement(objMod, "armorcapacity", strArmorCapacity);
            AppendElement(objMod, "avail", strAvail);
            AppendElement(objMod, "cost", strCost);
            AppendElement(objMod, "included", "False");
            AppendElement(objMod, "equipped", "True");
            AppendElement(objMod, "source", strSource);
            AppendElement(objMod, "page", strPage);
            objMods.AppendChild(objMod);

            DeductGearCost(strCost, strRating, "1", strAvail);

            ApplyBonus(objXmlMod?.SelectSingleNode("bonus"), ImprovementSource.ArmorMod, strName.Trim(), strRating);

            Changed?.Invoke();
            return true;
        }

        private bool ArmorHasCapacityForMod(XmlNode objArmor, string strModCapacity, string strRating)
        {
            CharacterArmorCapacityData objCapacity = ComputeArmorCapacity(objArmor);
            if (objCapacity.Total <= 0)
                return true;
            string strArmorCapacity = GetValue(objArmor, "armorcapacity", string.Empty);
            if (!string.IsNullOrWhiteSpace(strArmorCapacity) && strArmorCapacity != "0"
                && GetCharacterOptions().ArmorSuitCapacity)
                return objCapacity.Remaining >= EvaluateArmorCapacity(strModCapacity, ParseInteger(strRating));

            // Standard armor under Maximum Armor Modifications consumes a slot per mod rating,
            // with unrated modifications consuming one slot.
            int intRating = ParseInteger(strRating);
            return objCapacity.Remaining >= Math.Max(1, intRating);
        }

        private CharacterArmorCapacityData ComputeArmorCapacity(XmlNode objArmor)
        {
            string strRawCapacity = GetValue(objArmor, "armorcapacity", string.Empty);
            CharacterOptions objOptions = GetCharacterOptions();
            bool blnSuitCapacity = !string.IsNullOrWhiteSpace(strRawCapacity) && strRawCapacity != "0"
                && objOptions.ArmorSuitCapacity;
            bool blnMaximumMods = !blnSuitCapacity && (string.IsNullOrWhiteSpace(strRawCapacity) || strRawCapacity == "0")
                && objOptions.MaximumArmorModifications;
            int intTotal;
            if (blnSuitCapacity)
                intTotal = EvaluateArmorCapacity(strRawCapacity, 0);
            else if (blnMaximumMods)
            {
                int intBallistic = Math.Abs(ParseInteger(GetValue(objArmor, "b", "0")));
                int intImpact = Math.Abs(ParseInteger(GetValue(objArmor, "i", "0")));
                intTotal = Math.Max(6, (int)Math.Ceiling(Math.Max(intBallistic, intImpact) * 1.5));
            }
            else
                return new CharacterArmorCapacityData(0, 0);

            int intUsed = 0;
            foreach (XmlNode objMod in objArmor.SelectNodes("armormods/armormod")?.Cast<XmlNode>()
                ?? Enumerable.Empty<XmlNode>())
            {
                int intRating = ParseInteger(GetValue(objMod, "rating", "0"));
                intUsed += blnSuitCapacity
                    ? EvaluateArmorCapacity(GetValue(objMod, "armorcapacity", string.Empty), intRating)
                    : Math.Max(1, intRating);
            }
            foreach (XmlNode objGear in objArmor.SelectNodes("gears/gear")?.Cast<XmlNode>()
                ?? Enumerable.Empty<XmlNode>())
            {
                int intRating = ParseInteger(GetValue(objGear, "rating", "0"));
                intUsed += blnSuitCapacity
                    ? EvaluateArmorCapacity(GetValue(objGear, "armorcapacity", string.Empty), intRating)
                    : Math.Max(1, intRating);
            }
            return new CharacterArmorCapacityData(intTotal, intTotal - intUsed);
        }

        private static int EvaluateArmorCapacity(string strCapacity, int intRating)
        {
            if (string.IsNullOrWhiteSpace(strCapacity) || strCapacity == "*" || strCapacity == "[*]")
                return 0;
            int intSlash = strCapacity.IndexOf("/[", StringComparison.Ordinal);
            if (intSlash >= 0)
                strCapacity = strCapacity.Substring(intSlash + 1);
            if (strCapacity.StartsWith("FixedValues(", StringComparison.Ordinal) && strCapacity.EndsWith(")", StringComparison.Ordinal))
            {
                string[] astrValues = strCapacity.Substring("FixedValues(".Length,
                    strCapacity.Length - "FixedValues(".Length - 1).Split(',');
                if (astrValues.Length == 0) return 0;
                strCapacity = astrValues[Math.Clamp(Math.Max(1, intRating) - 1, 0, astrValues.Length - 1)];
            }
            strCapacity = strCapacity.Trim().Trim('[', ']');
            if (int.TryParse(strCapacity, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                return intValue;
            return (int)Math.Round(RatingExpression.Evaluate(strCapacity, intRating.ToString(CultureInfo.InvariantCulture)),
                MidpointRounding.AwayFromZero);
        }

        /// <summary>Removes the first saved Armor Modification matching its name, wherever it's
        /// nested (searches every root-level Armor's &lt;armormods&gt;), along with any
        /// Improvements its own &lt;bonus&gt; block granted on add.</summary>
        public bool RemoveArmorMod(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/armors/armor/armormods/armormod");
            if (objNodes == null)
                return false;

            foreach (XmlNode objMod in objNodes)
            {
                if (!string.Equals(GetValue(objMod, "name", string.Empty), strName.Trim(), StringComparison.Ordinal))
                    continue;

                objMod.ParentNode?.RemoveChild(objMod);
                RemoveBonusImprovements(ImprovementSource.ArmorMod, strName.Trim());
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        private XmlNode? FindArmorNode(string strName, string strCategory)
        {
            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return null;

            foreach (XmlNode objArmor in objNodes)
            {
                if (string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    return objArmor;
            }
            return null;
        }

        private XmlNode? GetArmorNodeById(int intArmorId)
        {
            if (intArmorId < 0)
                return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/armors/armor");
            return objNodes != null && intArmorId < objNodes.Count ? objNodes[intArmorId] : null;
        }

        private string GetArmorSetName(XmlNode objArmor)
        {
            string strSet = GetValue(objArmor, "armorset", string.Empty);
            if (!string.IsNullOrEmpty(strSet))
                return strSet;

            // Compatibility with the initial Avalonia grouping implementation, which wrote the
            // set into armorname. A real legacy custom name not present in armorbundles remains a
            // custom name and is not accidentally treated as a group.
            string strLegacyValue = GetValue(objArmor, "armorname", string.Empty);
            return ArmorSets.Contains(strLegacyValue, StringComparer.Ordinal) ? strLegacyValue : string.Empty;
        }

        public bool SetArmorNotes(int intArmorId, string strNotes)
        {
            XmlNode? objArmor = GetArmorNodeById(intArmorId);
            if (objArmor == null) return false;
            SetChildValue(objArmor, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Updates notes for an item installed in a root Armor item. The item GUID is
        /// scoped to its owning Armor so duplicate armor modifications and embedded Gear remain
        /// independently addressable.</summary>
        public bool SetArmorChildNotes(int intArmorId, Guid guiItemId, string strNotes)
        {
            XmlNode? objArmor = GetArmorNodeById(intArmorId);
            XmlNode? objItem = objArmor?.SelectSingleNode(
                $"armormods/armormod[guid = '{guiItemId}']")
                ?? objArmor?.SelectSingleNode($"gears/gear[guid = '{guiItemId}']");
            if (objItem == null)
                return false;

            SetChildValue(objItem, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        public bool SetArmorCustomName(int intArmorId, string strCustomName)
        {
            XmlNode? objArmor = GetArmorNodeById(intArmorId);
            if (objArmor == null) return false;
            SetChildValue(objArmor, "armorname", strCustomName?.Trim() ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first saved spell with the supplied name.</summary>
        public IReadOnlyList<CharacterTreeItemData> Armor => ReadArmorTree();

        /// <summary>Persisted armor-bundle names, including empty bundles.</summary>
        public IReadOnlyList<string> ArmorSets => (IReadOnlyList<string>?)Document.SelectNodes("/character/armorbundles/armorbundle")?
            .Cast<XmlNode>().Select(objNode => objNode.InnerText).Where(strName => !string.IsNullOrWhiteSpace(strName))
            .Distinct(StringComparer.Ordinal).ToList() ?? Array.Empty<string>();

        /// <summary>Armor encumbrance penalty (a negative dice-pool modifier, 0 if under threshold),
        /// ported from clsCharacter.cs's BallisticArmorEncumbrance/ImpactArmorEncumbrance. Covers the
        /// vanilla rule (BOD*2, or *3 if any worn armor is Military Grade, Form-Fitting counted at
        /// half rating), the SoftWeave Improvement's STR-based reduction, the ArmorEncumbrancePenalty
        /// Improvement, and all three optional house rules (IgnoreArmorEncumbrance,
        /// AlternateArmorEncumbrance's BOD+STR threshold, NoSingleArmorEncumbrance - Helmets and
        /// Shields/SecureTech PPP System don't count as a "piece" for that last rule, matching
        /// clsCharacter.cs), and equipped ArmorMod bonuses to ballistic/impact rating (see
        /// <see cref="GetArmorNodeTotalRating"/>, ported from clsEquipment.cs's Armor.TotalBallistic/
        /// TotalImpact).</summary>
        public CharacterEncumbranceData ArmorEncumbrance => ComputeArmorEncumbrance();

        /// <summary>Composure (WIL + CHA + Improvements), ported from clsCharacter.cs.</summary>
        private int GetArmorNodeTotalRating(XmlNode objArmorNode, string strElement)
        {
            int intTotal = ParseArmorRating(GetValue(objArmorNode, strElement, "0"));
            foreach (XmlNode objMod in objArmorNode.SelectNodes("armormods/armormod")?.Cast<XmlNode>()
                ?? Enumerable.Empty<XmlNode>())
            {
                if (GetValue(objMod, "equipped", "True") == "True")
                    intTotal += ParseInteger(GetValue(objMod, strElement, "0"));
            }
            return intTotal;
        }

        private CharacterEncumbranceData ComputeArmorEncumbrance()
        {
            var objOptions = GetCharacterOptions();
            var objNodes = Document.SelectNodes("/character/armors/armor[equipped = 'True']");
            var dblBod = double.TryParse(GetAttributeValue("BOD"), out var d) ? d : 0;
            var dblStr = double.TryParse(GetAttributeValue("STR"), out var dStr) ? dStr : 0;

            var intMultiplier = 2;
            var intTotalBallistic = 0;
            var intTotalImpact = 0;
            var intArmorCount = 0;
            var lstWorn = new List<string>();
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    var strCategory = GetValue(objNode, "category", string.Empty);
                    if (strCategory == "Military Grade Armor")
                        intMultiplier = 3;

                    // Helmets/Shields/SecureTech PPP System don't count as a "piece" for the
                    // NoSingleArmorEncumbrance house rule (you can wear one plus a base suit
                    // without it counting as "two pieces").
                    if (strCategory != "Helmets and Shields" && strCategory != "SecureTech PPP System")
                        intArmorCount++;

                    var strName = GetValue(objNode, "name", string.Empty);
                    var blnFormFitting = strName.StartsWith("Form-Fitting");
                    var intBallistic = GetArmorNodeTotalRating(objNode, "b");
                    var intImpact = GetArmorNodeTotalRating(objNode, "i");
                    var intCountedBallistic = blnFormFitting ? intBallistic / 2 : intBallistic;
                    var intCountedImpact = blnFormFitting ? intImpact / 2 : intImpact;
                    intTotalBallistic += intCountedBallistic;
                    intTotalImpact += intCountedImpact;
                    lstWorn.Add(strName + " (ballistisch " + FormatSigned(intCountedBallistic)
                        + ", Stoß " + FormatSigned(intCountedImpact) + (blnFormFitting ? ", Anschmiegsam: halbiert" : "") + ")");
                }
            }

            var intBallisticRating = ComputeArmorRating(objNodes, "b", ImprovementType.BallisticArmor);
            var intImpactRating = ComputeArmorRating(objNodes, "i", ImprovementType.ImpactArmor);

            // SoftWeave reduces the highest worn Ballistic/Impact rating (capped at STR) out of
            // the encumbrance total only - the displayed rating itself is unaffected.
            var blnSoftWeave = Improvements.Any(i => i.Type == ImprovementType.SoftWeave && i.Enabled);
            if (blnSoftWeave)
            {
                intTotalBallistic -= (int)Math.Min(intBallisticRating, dblStr);
                intTotalImpact -= (int)Math.Min(intImpactRating, dblStr);
            }

            // Alternate Armor Encumbrance house rule: threshold is BOD*(X-1) + STR instead of BOD*X.
            if (objOptions.AlternateArmorEncumbrance)
                intMultiplier--;
            var intThreshold = objOptions.AlternateArmorEncumbrance
                ? (int)(dblBod * intMultiplier + dblStr)
                : (int)(dblBod * intMultiplier);
            var strThresholdNote = objOptions.AlternateArmorEncumbrance
                ? "Schwelle: Konstitution " + dblBod + " x " + intMultiplier + " + Stärke " + dblStr + " = " + intThreshold
                : "Schwelle: Konstitution " + dblBod + " x " + intMultiplier + " = " + intThreshold
                    + (intMultiplier == 3 ? " (Militärgraderüstung getragen)" : "");

            int intArmorEncumbrancePenaltyBonus = ImprovementManager.ValueOf(Improvements, ImprovementType.ArmorEncumbrancePenalty);
            bool blnIgnoreEncumbrance = objOptions.IgnoreArmorEncumbrance;
            bool blnNoSinglePiecePenalty = objOptions.NoSingleArmorEncumbrance && intArmorCount == 1;

            return new CharacterEncumbranceData(
                BuildArmorRatingValue(intBallisticRating, "b", "ballistisch", objNodes, ImprovementType.BallisticArmor),
                BuildArmorRatingValue(intImpactRating, "i", "Stoß", objNodes, ImprovementType.ImpactArmor),
                BuildEncumbranceValue(intTotalBallistic, intThreshold, "ballistisch", strThresholdNote, lstWorn,
                    blnIgnoreEncumbrance, blnNoSinglePiecePenalty, intArmorEncumbrancePenaltyBonus),
                BuildEncumbranceValue(intTotalImpact, intThreshold, "Stoß", strThresholdNote, lstWorn,
                    blnIgnoreEncumbrance, blnNoSinglePiecePenalty, intArmorEncumbrancePenaltyBonus));
        }

        // Ported from clsCharacter.cs's BallisticArmorRating/ImpactArmorRating: a "+"-prefixed
        // rating (e.g. "+3") is stacking bonus armor, not a suit that supersedes others. Non-"+"
        // items only the single highest counts; "+" Clothing items sum among themselves and that
        // sum competes with the highest as an alternative base; "+" non-Clothing items always add
        // on top of whichever base wins.
        private int ComputeArmorRating(XmlNodeList? objNodes, string strElement, ImprovementType eImprovementType)
        {
            var intHighest = 0;
            var intStacking = 0;
            var intClothing = 0;
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    var strRating = GetValue(objNode, strElement, "0");
                    var intRating = GetArmorNodeTotalRating(objNode, strElement);
                    if (!strRating.StartsWith("+"))
                    {
                        intHighest = Math.Max(intHighest, intRating);
                        continue;
                    }

                    if (GetValue(objNode, "category", string.Empty) == "Clothing")
                        intClothing += intRating;
                    else
                        intStacking += intRating;
                }
            }

            var intArmor = Math.Max(intHighest, intClothing);
            return intArmor + intStacking + ImprovementManager.ValueOf(Improvements, eImprovementType);
        }

        private CharacterDerivedValueData BuildArmorRatingValue(int intTotal, string strElement, string strKind,
            XmlNodeList? objNodes, ImprovementType eImprovementType)
        {
            var sb = new StringBuilder();
            sb.Append("Höchste getragene Panzerung (").Append(strKind).Append("):");
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                    sb.Append('\n').Append("  ").Append(GetValue(objNode, "name", string.Empty)).Append(": ")
                        .Append(GetArmorNodeTotalRating(objNode, strElement));
            }

            foreach (var objContribution in ImprovementManager.DescribeValueOf(Improvements, eImprovementType))
                sb.Append('\n').Append("  ").Append(objContribution.SourceName).Append(": ")
                    .Append(FormatSigned(objContribution.Value));
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        private static int ParseArmorRating(string strRating)
        {
            var strLeading = new string(strRating.TakeWhile(c => char.IsDigit(c) || c == '-' || c == '+').ToArray());
            return int.TryParse(strLeading, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var intValue) ? intValue : 0;
        }

        private IReadOnlyList<CharacterTreeItemData> ReadArmorTree()
        {
            var lstArmor = new List<CharacterTreeItemData>();
            var dicSets = new Dictionary<string, CharacterTreeItemData>(StringComparer.Ordinal);
            foreach (string strSetName in ArmorSets)
            {
                var objSet = new CharacterTreeItemData(strSetName, "Armor set");
                dicSets.Add(strSetName, objSet);
                lstArmor.Add(objSet);
            }
            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null) return lstArmor;

            int intArmorId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                var objArmor = ReadTreeItem(objNode, "armormods/armormod", "gears/gear");
                objArmor.SetArmorId(intArmorId++);
                int intBallistic = ParseInteger(GetValue(objNode, "b", "0"))
                    - ParseInteger(GetValue(objNode, "ballisticdamage", "0"));
                int intImpact = ParseInteger(GetValue(objNode, "i", "0"))
                    - ParseInteger(GetValue(objNode, "impactdamage", "0"));
                objArmor.SetArmorRatings(intBallistic.ToString(CultureInfo.InvariantCulture),
                    intImpact.ToString(CultureInfo.InvariantCulture));
                CharacterArmorCapacityData objCapacity = ComputeArmorCapacity(objNode);
                if (objCapacity.Total > 0)
                    objArmor.SetCapacityDetails(objCapacity.Total.ToString(CultureInfo.InvariantCulture),
                        objCapacity.Remaining.ToString(CultureInfo.InvariantCulture));
                string strSetName = GetArmorSetName(objNode);
                string strCustomName = GetValue(objNode, "armorname", string.Empty);
                if (string.IsNullOrEmpty(GetValue(objNode, "armorset", string.Empty))
                    && ArmorSets.Contains(strCustomName, StringComparer.Ordinal))
                    strCustomName = string.Empty;
                objArmor.SetCustomName(strCustomName);
                objArmor.SetArmorSetName(strSetName);
                if (string.IsNullOrWhiteSpace(strSetName))
                {
                    lstArmor.Add(objArmor);
                    continue;
                }

                if (!dicSets.TryGetValue(strSetName, out var objSet))
                {
                    objSet = new CharacterTreeItemData(strSetName, "Armor set");
                    dicSets.Add(strSetName, objSet);
                    lstArmor.Add(objSet);
                }
                objSet.Children.Add(objArmor);
            }

            return lstArmor;
        }

    }
}
