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
        public bool AllowEditPartOfBaseWeaponEnabled => GetCharacterOptions().AllowEditPartOfBaseWeapon;

        /// <summary>Whether this character may convert an acquired piece of Bioware into
        /// Genetech: Transgenics under the Augmentation house rule.</summary>
        public bool AllowCustomTransgenicsEnabled => GetCharacterOptions().AllowCustomTransgenics;

        /// <summary>Whether Bioware suite acquisition is enabled for this character. Cyberware
        /// suites are always available; legacy exposes the Bioware counterpart only under this
        /// house rule.</summary>
        public bool AllowBiowareSuitesEnabled => GetCharacterOptions().AllowBiowareSuites;

        /// <summary>Whether an Obsolescent vehicle modification may be upgraded in the same
        /// manner as an Obsolete modification.</summary>
        public bool AdjustStunDamage(int intDelta) => AdjustConditionDamage("stuncmfilled", ComputeStunCm().Value, intDelta);

        /// <summary>Sum of installed Cyberware's own Essence cost (excludes Bioware and Essence Holes).</summary>
        public double CyberwareEssence => SumCyberwareEssence().Cyberware;

        /// <summary>Sum of installed Bioware's own Essence cost (excludes Cyberware and Essence Holes).</summary>
        public double BiowareEssence => SumCyberwareEssence().Bioware;

        private (double Cyberware, double Bioware, double Holes) SumCyberwareEssence()
        {
            double dblCyberware = 0, dblBioware = 0, dblHoles = 0;
            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    double dblEss = double.TryParse(GetValue(objNode, "ess", "0"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var dblParsedEss) ? dblParsedEss : 0;
                    if (GetValue(objNode, "name", string.Empty) == "Essence Hole")
                        dblHoles += dblEss;
                    else if (GetValue(objNode, "improvementsource", string.Empty) == "Bioware")
                        dblBioware += dblEss;
                    else
                        dblCyberware += dblEss;
                }
            }

            return (dblCyberware, dblBioware, dblHoles);
        }

        private XmlNode? GetGearNodeById(int intGearId)
        {
            int intCurrentId = 0;
            foreach (XmlNode objNode in EnumerateGearNodesDfs())
            {
                if (intCurrentId == intGearId)
                    return objNode;
                intCurrentId++;
            }

            return null;
        }

        /// <summary>Whether the AllowCyberwareEssDiscounts house rule is on - gates the Essence
        /// discount input in the Cyberware/Bioware picker (the discount itself is applied entirely
        /// client-side into the already-resolved Essence value passed to <see cref="AddCyberware"/>,
        /// same as the grade multiplier, so there's nothing else to enforce here).</summary>
        public bool AllowCyberwareEssenceDiscounts => GetCharacterOptions().AllowCyberwareEssDiscounts;

        /// <summary>Print-output house rules (frmOptions.cs's "House Rules" tab) - read by
        /// <see cref="CharacterSheetExporter"/> so sheet output honors them the same way legacy's
        /// clsCharacter.cs's PrintToStream does.</summary>
        public bool PrintNotesEnabled => GetCharacterOptions().PrintNotes;

        /// <summary>Adds a root-level Cyberware or Bioware item in the minimal saved-character tree
        /// shape used by <see cref="Cyberware"/>/<see cref="Bioware"/> and <see cref="ComputeEssence"/>
        /// - <paramref name="strEss"/>/<paramref name="strCost"/>/<paramref name="strAvail"/> are the
        /// already grade-and-rating-resolved values (the picker applies the Standard/Alphaware/
        /// Betaware/Deltaware multipliers from cyberware.xml/bioware.xml's &lt;grades&gt; before
        /// calling this - Cyberware.CalculatedESS's own further discount formulas aren't ported).</summary>
        /// <summary>Whether adding this Cyberware/Bioware prompts for a Left/Right side - ported
        /// from clsImprovement.cs's selectside bonus handler (frmSelectSide.cs; used by paired
        /// items like Single Cybereye). Legacy stores the pick directly on the item's own
        /// Location field rather than as an Improvement - see <see cref="AddCyberware"/>'s
        /// <paramref name="strSide"/>.</summary>
        public bool CyberwareRequiresSideSelection(string strName, bool blnBioware = false) =>
            FindBonusChild(blnBioware ? "bioware.xml" : "cyberware.xml", blnBioware ? "biowares" : "cyberwares",
                blnBioware ? "bioware" : "cyberware", strName, "selectside") != null;

        /// <summary>Whether adding this Cyberware/Bioware prompts for a Skill Group pick (e.g.
        /// Reflex Recorder (Skill Group)) - ported from clsImprovement.cs's selectskillgroup
        /// bonus handler (frmSelectSkillGroup.cs). The actual Improvement is created via <see
        /// cref="ApplySelectedImprovement"/> once <see cref="AddCyberware"/> is given the chosen
        /// group name.</summary>
        public bool CyberwareRequiresSkillGroupSelection(string strName, bool blnBioware = false) =>
            FindBonusChild(blnBioware ? "bioware.xml" : "cyberware.xml", blnBioware ? "biowares" : "cyberwares",
                blnBioware ? "bioware" : "cyberware", strName, "selectskillgroup") != null;

        /// <summary>Skill Group names to offer for this Cyberware/Bioware's selectskillgroup
        /// bonus, filtered by its excludecategory attribute exactly like frmSelectSkillGroup.cs's
        /// Load handler: a group is offered if skills.xml has at least one skill in that group
        /// whose category is not in the exclude list (or if there's no excludecategory at all).</summary>
        public IReadOnlyList<string> GetCyberwareSkillGroupOptions(string strName, bool blnBioware = false)
        {
            XmlNode? objSelectSkillGroup = FindBonusChild(blnBioware ? "bioware.xml" : "cyberware.xml",
                blnBioware ? "biowares" : "cyberwares", blnBioware ? "bioware" : "cyberware", strName, "selectskillgroup");

            string strExcludeCategory = objSelectSkillGroup?.Attributes?["excludecategory"]?.InnerText ?? string.Empty;
            HashSet<string>? setExclude = string.IsNullOrEmpty(strExcludeCategory)
                ? null
                : new HashSet<string>(strExcludeCategory.Split(',').Select(s => s.Trim()), StringComparer.Ordinal);

            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            var lstGroups = new List<string>();
            foreach (XmlNode objGroup in objSkillsDoc.SelectNodes("/chummer/skillgroups/name")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strGroup = objGroup.InnerText;
                if (setExclude == null)
                {
                    lstGroups.Add(strGroup);
                    continue;
                }

                bool blnHasIncludedSkill = (objSkillsDoc.SelectNodes(
                        $"/chummer/skills/skill[skillgroup = '{strGroup}']")?.Cast<XmlNode>()
                        ?? Enumerable.Empty<XmlNode>())
                    .Any(objSkill => !setExclude.Contains(objSkill["category"]?.InnerText ?? string.Empty));
                if (blnHasIncludedSkill)
                    lstGroups.Add(strGroup);
            }

            return lstGroups;
        }

        public void AddCyberware(string strName, string strCategory, string strRating, string strEss,
            string strCost, string strAvail, string strSource, string strPage, string strGrade = "Standard",
            bool blnBioware = false, string strSide = "", string strSelectedSkillGroup = "",
            bool blnTransgenic = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A cyberware name is required.", nameof(strName));

            // frmCreate/frmCareer force this category and Standard grade when the Augmentation
            // house rule's "Add as Transgenic" box is used. Keep the invariant in Core rather
            // than trusting each picker, as both values affect later rule calculations.
            if (blnTransgenic)
            {
                if (!blnBioware || !AllowCustomTransgenicsEnabled)
                    throw new InvalidOperationException("Only enabled custom Bioware may be added as Transgenic.");
                strCategory = "Genetech: Transgenics";
                strGrade = "Standard";
            }

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCyberwares = objRoot.SelectSingleNode("cyberwares");
            if (objCyberwares == null)
            {
                objCyberwares = Document.CreateElement("cyberwares");
                objRoot.AppendChild(objCyberwares);
            }

            var objCyberware = Document.CreateElement("cyberware");
            AppendElement(objCyberware, "name", strName.Trim());
            AppendElement(objCyberware, "category", strCategory);
            AppendElement(objCyberware, "rating", strRating);
            AppendElement(objCyberware, "ess", strEss);
            AppendElement(objCyberware, "cost", strCost);
            AppendElement(objCyberware, "avail", strAvail);
            AppendElement(objCyberware, "source", strSource);
            AppendElement(objCyberware, "page", strPage);
            AppendElement(objCyberware, "grade", strGrade);
            AppendElement(objCyberware, "improvementsource", blnBioware ? "Bioware" : "Cyberware");
            AppendElement(objCyberware, "equipped", "True");
            AppendElement(objCyberware, "location", strSide.Trim());
            objCyberware.AppendChild(Document.CreateElement("children"));
            objCyberwares.AppendChild(objCyberware);
            DeductGearCost(strCost, strRating, "1", strAvail);

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            XmlNode? objXmlWare = objWareDoc.SelectSingleNode(
                $"/chummer/{(blnBioware ? "biowares/bioware" : "cyberwares/cyberware")}[name = '{strName.Trim()}']");
            ApplyBonus(objXmlWare?.SelectSingleNode("bonus"),
                blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim(), strRating);
            ApplySelectedImprovement(objXmlWare?.SelectSingleNode("bonus"),
                blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim(),
                strSelectedSkillGroup, strRating);

            Changed?.Invoke();
        }

        /// <summary>Adds a cyberware/bioware plugin below an existing item identified by its
        /// depth-first Core tree ID. The saved shape matches legacy <c>children/cyberware</c>.</summary>
        public bool AddCyberwareChild(int intParentCyberwareId, string strName, string strCategory, string strRating,
            string strEss, string strCost, string strAvail, string strSource, string strPage,
            string strGrade = "Standard", bool blnBioware = false)
        {
            XmlNode? objParent = GetCyberwareNodeById(intParentCyberwareId);
            if (objParent == null || string.IsNullOrWhiteSpace(strName))
                return false;
            XmlElement objChildren = objParent.SelectSingleNode("children") as XmlElement
                ?? (XmlElement)objParent.AppendChild(Document.CreateElement("children"));
            var objChild = Document.CreateElement("cyberware");
            AppendElement(objChild, "name", strName.Trim());
            AppendElement(objChild, "category", strCategory);
            AppendElement(objChild, "rating", strRating);
            AppendElement(objChild, "ess", strEss);
            AppendElement(objChild, "cost", strCost);
            AppendElement(objChild, "avail", strAvail);
            AppendElement(objChild, "source", strSource);
            AppendElement(objChild, "page", strPage);
            AppendElement(objChild, "grade", strGrade);
            AppendElement(objChild, "improvementsource", blnBioware ? "Bioware" : "Cyberware");
            AppendElement(objChild, "equipped", "True");
            AppendElement(objChild, "location", string.Empty);
            objChild.AppendChild(Document.CreateElement("children"));
            objChildren.AppendChild(objChild);
            XmlDocument objRules = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            string strType = blnBioware ? "bioware" : "cyberware";
            XmlNode? objRule = FindRuleItemByName(objRules, "/chummer/" + strType + "s/" + strType, strName);
            ApplyBonus(objRule?.SelectSingleNode("bonus"), blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware,
                strName.Trim(), strRating);
            DeductGearCost(strCost, strRating, "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Cyberware/Bioware Suite names offered by <see cref="AddCyberwareSuite"/> -
        /// ported from frmSelectCyberwareSuite.cs's Load handler (its flat list, no category
        /// filter unlike frmSelectPACKSKit).</summary>
        public IReadOnlyList<string> GetCyberwareSuiteNames(bool blnBioware = false)
        {
            if (blnBioware && !AllowBiowareSuitesEnabled)
                return Array.Empty<string>();

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            var lstNames = new List<string>();
            foreach (XmlNode objSuite in objWareDoc.SelectNodes("/chummer/suites/suite")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objSuite["name"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strName))
                    lstNames.Add(strName);
            }

            return lstNames;
        }

        /// <summary>Adds a pre-bundled Cyberware/Bioware Suite (e.g. "Aztechnology Topo") - ported
        /// from frmSelectCyberwareSuite.cs's ParseNode. A Suite fixes a single Grade for all of its
        /// parts and can nest cyberware within cyberware (plugins), so this builds the XML tree
        /// directly (mirroring <see cref="AddCyberware"/>'s own node shape and per-item bonus
        /// application) instead of reusing that single-item method, appending nested parts under
        /// their parent's own &lt;children&gt; exactly like <see cref="RemoveCyberware"/>'s tree
        /// already expects. Unlike <see cref="AddCyberware"/>, legacy's own Suite.TotalCost is
        /// informational only (frmCareer.cs deducts it separately) - not ported, matching
        /// AddCyberware's existing no-nuyen-deduction behavior for Cyberware/Bioware.</summary>
        public bool AddCyberwareSuite(string strSuiteName, bool blnBioware = false)
        {
            if (string.IsNullOrWhiteSpace(strSuiteName))
                return false;
            if (blnBioware && !AllowBiowareSuitesEnabled)
                return false;

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            XmlNode? objXmlSuite = objWareDoc.SelectSingleNode(
                $"/chummer/suites/suite[name = '{strSuiteName.Trim()}']");
            string strItemTag = blnBioware ? "bioware" : "cyberware";
            XmlNode? objXmlItems = objXmlSuite?.SelectSingleNode(strItemTag + "s");
            if (objXmlItems == null)
                return false;

            string strGrade = objXmlSuite!["grade"]?.InnerText ?? "Standard";
            XmlNode? objXmlGrade = objWareDoc.SelectSingleNode($"/chummer/grades/grade[name = '{strGrade}']");
            double dblGradeEss = double.TryParse(objXmlGrade?["ess"]?.InnerText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dEss) ? dEss : 1.0;
            double dblGradeCost = double.TryParse(objXmlGrade?["cost"]?.InnerText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dCost) ? dCost : 1.0;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCyberwares = objRoot.SelectSingleNode("cyberwares");
            if (objCyberwares == null)
            {
                objCyberwares = Document.CreateElement("cyberwares");
                objRoot.AppendChild(objCyberwares);
            }

            foreach (XmlNode objXmlItem in objXmlItems.SelectNodes(strItemTag)?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendCyberwareSuiteItem(objCyberwares, objXmlItem, objWareDoc, strGrade, dblGradeEss, dblGradeCost,
                    blnBioware);

            Changed?.Invoke();
            return true;
        }

        private void AppendCyberwareSuiteItem(XmlNode objParentList, XmlNode objXmlItem, XmlDocument objWareDoc,
            string strGrade, double dblGradeEss, double dblGradeCost, bool blnBioware)
        {
            string strName = objXmlItem["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(strName))
                return;

            string strItemTag = blnBioware ? "bioware" : "cyberware";
            XmlNode? objXmlWare = objWareDoc.SelectSingleNode(
                $"/chummer/{strItemTag}s/{strItemTag}[name = '{strName.Trim()}']");
            // Ported from a normal picker's own filtering (e.g. CyberwareDialogViewModel): a Suite
            // or PACKS kit is just a pre-selected shortcut for items a normal picker would offer,
            // so it should skip anything from a sourcebook the character has disabled too - legacy
            // never did this (frmSelectCyberwareSuite.cs has no such check), but this port applies
            // the same filter everywhere for consistency.
            if (objXmlWare == null || !IsBookEnabled(objXmlWare["source"]?.InnerText ?? string.Empty))
                return;

            string strRating = objXmlItem["rating"]?.InnerText ?? "0";
            double dblBaseEss = RatingExpression.Evaluate(objXmlWare["ess"]?.InnerText ?? "0", strRating);
            double dblBaseCost = RatingExpression.Evaluate(objXmlWare["cost"]?.InnerText ?? "0", strRating);
            string strEss = Math.Round(dblBaseEss * dblGradeEss, 2).ToString(CultureInfo.InvariantCulture);
            string strCost = ((int)(dblBaseCost * dblGradeCost)).ToString(CultureInfo.InvariantCulture);

            var objElement = Document.CreateElement(strItemTag);
            AppendElement(objElement, "name", strName.Trim());
            AppendElement(objElement, "category", objXmlWare["category"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "rating", strRating);
            AppendElement(objElement, "ess", strEss);
            AppendElement(objElement, "cost", strCost);
            AppendElement(objElement, "avail", objXmlWare["avail"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "source", objXmlWare["source"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "page", objXmlWare["page"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "grade", strGrade);
            AppendElement(objElement, "improvementsource", blnBioware ? "Bioware" : "Cyberware");
            AppendElement(objElement, "equipped", "True");
            AppendElement(objElement, "location", string.Empty);
            var objChildren = Document.CreateElement("children");
            objElement.AppendChild(objChildren);
            objParentList.AppendChild(objElement);

            ApplyBonus(objXmlWare.SelectSingleNode("bonus"),
                blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim(), strRating);

            foreach (XmlNode objXmlChild in objXmlItem.SelectNodes(strItemTag + "s/" + strItemTag)?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendCyberwareSuiteItem(objChildren, objXmlChild, objWareDoc, strGrade, dblGradeEss, dblGradeCost,
                    blnBioware);
        }

        /// <summary>Categories offered by <see cref="GetPacksKitNames"/> - ported from
        /// frmSelectPACKSKit.cs's category dropdown (real packs.xml data uses "Attribute Kits",
        /// "Skill Kits", "Adept Kits", "Complex Form Kits", "Spell Kits", "Gear Kits"; "Custom" is
        /// listed but has no real kits and is included here anyway for completeness).</summary>
        private void ApplyPacksCyberwareOrBioware(XmlNode objXmlKit, bool blnBioware)
        {
            XmlNode? objXmlItems = objXmlKit.SelectSingleNode(blnBioware ? "biowares" : "cyberwares");
            if (objXmlItems == null)
                return;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCyberwares = objRoot.SelectSingleNode("cyberwares");
            if (objCyberwares == null)
            {
                objCyberwares = Document.CreateElement("cyberwares");
                objRoot.AppendChild(objCyberwares);
            }

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            string strItemTag = blnBioware ? "bioware" : "cyberware";
            foreach (XmlNode objXmlItem in objXmlItems.SelectNodes(strItemTag)?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strGrade = objXmlItem["grade"]?.InnerText ?? "Standard";
                XmlNode? objXmlGrade = objWareDoc.SelectSingleNode($"/chummer/grades/grade[name = '{strGrade}']");
                double dblGradeEss = double.TryParse(objXmlGrade?["ess"]?.InnerText, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dEss) ? dEss : 1.0;
                double dblGradeCost = double.TryParse(objXmlGrade?["cost"]?.InnerText, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dCost) ? dCost : 1.0;

                AppendCyberwareSuiteItem(objCyberwares, objXmlItem, objWareDoc, strGrade, dblGradeEss, dblGradeCost,
                    blnBioware);
            }
        }

        public bool RemoveCyberware(string strName, string strCategory, string strRating, bool blnBioware = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes == null)
                return false;

            string strExpectedSource = blnBioware ? "Bioware" : "Cyberware";
            foreach (XmlNode objCyberware in objNodes)
            {
                if (!string.Equals(GetValue(objCyberware, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objCyberware, "category", string.Empty), strCategory, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objCyberware, "rating", "0"), strRating, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objCyberware, "improvementsource", string.Empty), strExpectedSource, StringComparison.Ordinal))
                    continue;

                objCyberware.ParentNode?.RemoveChild(objCyberware);
                RemoveBonusImprovements(blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim());
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root
        /// Cyberware/Bioware item (including its own installed plugins' cost).</summary>
        public bool SellCyberware(string strName, string strCategory, string strRating, double dblSellPercent, bool blnBioware = false)
        {
            XmlNode? objNode = FindCyberwareNode(strName, strCategory, strRating, blnBioware);
            if (objNode == null)
                return false;

            int intRefund = ComputeSellRefund(ReadTreeItem(objNode, "children/cyberware").CalculatedCost, dblSellPercent);
            if (!RemoveCyberware(strName, strCategory, strRating, blnBioware))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        private XmlNode? FindCyberwareNode(string strName, string strCategory, string strRating, bool blnBioware)
        {
            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes == null)
                return null;

            string strExpectedSource = blnBioware ? "Bioware" : "Cyberware";
            foreach (XmlNode objCyberware in objNodes)
            {
                if (string.Equals(GetValue(objCyberware, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(objCyberware, "category", string.Empty), strCategory, StringComparison.Ordinal)
                    && string.Equals(GetValue(objCyberware, "rating", "0"), strRating, StringComparison.Ordinal)
                    && string.Equals(GetValue(objCyberware, "improvementsource", string.Empty), strExpectedSource, StringComparison.Ordinal))
                    return objCyberware;
            }

            return null;
        }

        /// <summary>Adjusts the filled physical condition-monitor boxes of a root vehicle.</summary>
        public IReadOnlyList<CharacterTreeItemData> Cyberware => ReadCyberwareOrBiowareTree(blnBioware: false);

        public IReadOnlyList<CharacterTreeItemData> Bioware => ReadCyberwareOrBiowareTree(blnBioware: true);

        private IReadOnlyList<CharacterTreeItemData> ReadCyberwareOrBiowareTree(bool blnBioware)
        {
            var lstItems = new List<CharacterTreeItemData>();
            var objAllNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objAllNodes == null) return lstItems;

            int intNextId = 0;
            foreach (XmlNode objNode in objAllNodes)
            {
                bool blnIsBioware = GetValue(objNode, "improvementsource", string.Empty) == "Bioware";
                CharacterTreeItemData objItem = ReadCyberwareTreeItem(objNode, ref intNextId);
                if (blnIsBioware == blnBioware)
                    lstItems.Add(objItem);
            }
            return lstItems;
        }

        private CharacterTreeItemData ReadCyberwareTreeItem(XmlNode objNode, ref int intNextId)
        {
            var objItem = ReadTreeItem(objNode);
            objItem.SetTransgenic(GetValue(objNode, "category", string.Empty) == "Genetech: Transgenics"
                && GetValue(objNode, "improvementsource", string.Empty) == "Bioware");
            objItem.SetCyberwareId(intNextId);
            intNextId++;

            var objChildren = objNode.SelectNodes("children/cyberware");
            if (objChildren == null) return objItem;
            foreach (XmlNode objChild in objChildren)
                objItem.Children.Add(ReadCyberwareTreeItem(objChild, ref intNextId));
            return objItem;
        }

        private XmlNode? GetCyberwareNodeById(int intCyberwareId)
        {
            var objTopNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objTopNodes == null) return null;

            int intCurrentId = 0;
            foreach (XmlNode objNode in objTopNodes)
            {
                XmlNode? objFound = FindCyberwareNodeById(objNode, intCyberwareId, ref intCurrentId);
                if (objFound != null) return objFound;
            }
            return null;
        }

        /// <summary>Updates notes for a Cyberware/Bioware tree item. The depth-first ID is shared
        /// by the two filtered trees, so nested components remain addressable after save/reload.</summary>
        public bool SetCyberwareNotes(int intCyberwareId, string strNotes)
        {
            XmlNode? objNode = GetCyberwareNodeById(intCyberwareId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? FindCyberwareNodeById(XmlNode objNode, int intTargetId, ref int intCurrentId)
        {
            if (intCurrentId == intTargetId) return objNode;
            intCurrentId++;

            var objChildren = objNode.SelectNodes("children/cyberware");
            if (objChildren == null) return null;
            foreach (XmlNode objChild in objChildren)
            {
                XmlNode? objFound = FindCyberwareNodeById(objChild, intTargetId, ref intCurrentId);
                if (objFound != null) return objFound;
            }
            return null;
        }

        /// <summary>Moves a Cyberware/Bioware item within the &lt;cyberwares&gt; tree - either
        /// reordering it among its current siblings (inserted immediately before <paramref
        /// name="intTargetCyberwareId"/>) or, with <paramref name="blnReparent"/>, making it a
        /// child of the target instead. Same shape as <see cref="MoveGear"/> for the Gear tree -
        /// CyberwareIds are depth-first positions recomputed on every read, so callers must reload
        /// the tree after a successful move before issuing another one.</summary>
        public bool MoveCyberware(int intSourceCyberwareId, int intTargetCyberwareId, bool blnReparent)
        {
            XmlNode? objSource = GetCyberwareNodeById(intSourceCyberwareId);
            XmlNode? objTarget = GetCyberwareNodeById(intTargetCyberwareId);
            if (objSource == null || objTarget == null || objSource == objTarget || objSource.ParentNode == null)
                return false;

            // Refuse to move an item into its own subtree - see MoveGear's identical guard.
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

        /// <summary>Armor is represented as a tree so installed armor modifications and optional
        /// saved armor sets remain visible instead of being flattened into a list.</summary>
        public sealed record SenseImprovementOption(string Name, string DisplayName, string SourceFile);

        /// <summary>Ported from clsImprovement.cs's AddSensewareSource/the selectsenseware picker
        /// setup: given an Adept Power whose rules-data &lt;bonus&gt; is a &lt;selectsenseware&gt;
        /// node (e.g. "Improved Sense"), lists every Cyberware/Bioware/Gear item in the mount's
        /// requested categories, filtered to &lt;senseimprovement&gt;yes&lt;/senseimprovement&gt;
        /// items when the node's requiresenseimprovement="yes". Empty if the power has no such
        /// bonus.</summary>
        private string GetCyberwareLimbSlot(string strName, bool blnBioware)
        {
            XmlDocument objDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            XmlNode? objXmlWare = objDoc.SelectSingleNode(
                $"/chummer/{(blnBioware ? "biowares/bioware" : "cyberwares/cyberware")}[name = '{strName}']");
            return objXmlWare?["limbslot"]?.InnerText ?? string.Empty;
        }

        /// <summary>Ported from clsEquipment.cs's Cyberware.TotalBody/TotalStrength/TotalAgility -
        /// a Cyberlimb's own physical stats start at a base of 3 and can be overridden/boosted by
        /// its "Customized X"/"Enhanced X" child plugins.</summary>
        private (int Body, int Strength, int Agility) ComputeCyberlimbStats(XmlNode objCyberwareNode)
        {
            int intBody = 3, intStrength = 3, intAgility = 3;
            int intBodyBonus = 0, intStrengthBonus = 0, intAgilityBonus = 0;

            XmlNodeList? objChildren = objCyberwareNode.SelectNodes("children/cyberware");
            if (objChildren != null)
            {
                foreach (XmlNode objChild in objChildren)
                {
                    string strName = GetValue(objChild, "name", string.Empty);
                    int intRating = int.TryParse(GetValue(objChild, "rating", "0"), out var intParsed) ? intParsed : 0;
                    switch (strName)
                    {
                        case "Customized Body": intBody = intRating; break;
                        case "Enhanced Body": intBodyBonus = intRating; break;
                        case "Customized Strength": intStrength = intRating; break;
                        case "Enhanced Strength": intStrengthBonus = intRating; break;
                        case "Customized Agility": intAgility = intRating; break;
                        case "Enhanced Agility": intAgilityBonus = intRating; break;
                    }
                }
            }

            return (intBody + intBodyBonus, intStrength + intStrengthBonus, intAgility + intAgilityBonus);
        }

        // Ported from clsUnique.cs's Attribute.AttributeModifiers/TotalValue.
        /// <summary>Ported from clsUnique.cs's Attribute.AttributeValueModifiers: the total of
        /// every enabled Attribute Improvement whose ImprovedName is the code+"Base" (e.g.
        /// "BODBase") rather than the plain code - a distinct producer marked by a bonus's
        /// &lt;affectbase&gt; child (see BonusApplier's specificattribute handler and
        /// <see cref="ApplySelectedImprovement"/>'s selectattribute handler), used by things like
        /// the Improved Physical Attribute power. Unlike a plain Attribute bonus, this also raises
        /// the Karma cost to increase the attribute further, not just its shown/augmented value.</summary>
    }
}
