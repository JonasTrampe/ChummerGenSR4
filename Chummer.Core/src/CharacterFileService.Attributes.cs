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
        public bool SetMysticAdeptMagicianMagSplit(int intMagicianPoints)
        {
            if (!MysticAdept)
                return false;

            int intMag = GetAttributeInt("MAG");
            int intMagician = Math.Clamp(intMagicianPoints, 0, intMag);
            int intAdept = Math.Max(0, intMag - intMagician - EssencePenalty);
            SetChildValue(Document.DocumentElement!, "magsplitmagician", intMagician.ToString());
            SetChildValue(Document.DocumentElement!, "magsplitadept", intAdept.ToString());
            Changed?.Invoke();
            return true;
        }

        private static string GetAttributeLabel(string strCode) => strCode switch
        {
            "BOD" => "Konstitution",
            "AGI" => "Geschicklichkeit",
            "REA" => "Reaktion",
            "STR" => "Stärke",
            "CHA" => "Charisma",
            "INT" => "Intuition",
            "LOG" => "Logik",
            "WIL" => "Willenskraft",
            "MAG" => "Magie",
            "RES" => "Resonanz",
            "EDG" => "Edge",
            _ => strCode,
        };

        public string FlyMovement => ComputeFlyMovement();

        /// <summary>Current and maximum Edge. Spent Edge is stored by the legacy application as
        /// an EdgeUse Attribute improvement with a negative augmented value.</summary>
        public CharacterEdgeData Edge
        {
            get
            {
                int intMaximum = GetAttributeInt("EDG");
                int intUsed = Improvements.Where(i => i.Enabled && i.Source == ImprovementSource.EdgeUse
                    && i.Type == ImprovementType.Attribute && i.ImprovedName == "EDG")
                    .Sum(i => i.Augmented * i.Rating);
                return new CharacterEdgeData(Math.Clamp(intMaximum + intUsed, 0, intMaximum), intMaximum);
            }
        }

        public bool BurnEdge()
        {
            int intCurrent = GetAttributeInt("EDG");
            if (intCurrent <= 0)
                return false;
            bool blnOk = SetAttributeValue("EDG", intCurrent - 1);
            if (blnOk)
                Changed?.Invoke();
            return blnOk;
        }

        private int ApplyEssencePenaltyToAttribute(string strCode, string strRawTotalValue)
        {
            int intRaw = int.TryParse(strRawTotalValue, out var intParsed) ? intParsed : 0;
            int intPenalty = EssencePenalty;
            if (intPenalty == 0)
                return intRaw;

            if (GetCharacterOptions().EssLossReducesMaximumOnly)
            {
                int intMax = int.TryParse(
                    GetValue($"/character/attributes/attribute[name = '{strCode}']/metatypemax", "0"), out var mx)
                    ? mx
                    : 0;
                return intRaw > intMax ? intMax : intRaw;
            }

            return Math.Max(0, intRaw - intPenalty);
        }

        /// <summary>Sums two attributes plus any Improvements of the given type against no specific
        /// ImprovedName (used by the "Special Attribute Tests": Composure, Judge Intentions, Lift and
        /// Carry, Memory). Each attribute is listed on its own tooltip line by its German name, then
        /// one line per contributing Improvement (source name + signed value) when several stack.</summary>
        private CharacterDerivedValueData SumAttributesWithImprovements(ImprovementType eType,
            params (string Code, string Label)[] attributes)
        {
            var sb = new StringBuilder();
            int intTotal = 0;
            for (int i = 0; i < attributes.Length; i++)
            {
                int intValue = GetAttributeInt(attributes[i].Code);
                intTotal += intValue;
                if (i > 0) sb.Append('\n');
                sb.Append(attributes[i].Label).Append(": ").Append(intValue);
            }

            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, eType);
            intTotal += lstContributions.Sum(c => c.Value);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);

            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        /// <summary>Appends one tooltip line per contribution as "SourceName: +N" (or "-N").</summary>
        private IReadOnlyList<CharacterAttributeData>? _cachedAttributes;
        public IReadOnlyList<CharacterAttributeData> Attributes
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedAttributes short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedAttributes ??= ReadAttributes();
            }
        }

        /// <summary>Raw bonus/modifier records - see Improvement.cs and ImprovementManager.cs for
        /// what these actually drive. Most callers want a derived value (like Condition above)
        /// rather than this list directly.</summary>
        public bool AddCustomImprovement(CustomImprovementType eType, string strName, int intVal,
            int intMin = 0, int intMax = 0, int intAug = 0, string strSelect = "", bool blnApplyToRating = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;
            if ((eType == CustomImprovementType.Attribute || eType == CustomImprovementType.Skill)
                && string.IsNullOrWhiteSpace(strSelect))
                return false;

            var objBonusDoc = new XmlDocument();
            XmlElement objBonus = objBonusDoc.CreateElement("bonus");
            objBonusDoc.AppendChild(objBonus);

            void AddChild(XmlElement objParent, string strTag, string strValue)
            {
                XmlElement objChild = objBonusDoc.CreateElement(strTag);
                objChild.InnerText = strValue;
                objParent.AppendChild(objChild);
            }

            switch (eType)
            {
                case CustomImprovementType.Attribute:
                    XmlElement objAttr = objBonusDoc.CreateElement("specificattribute");
                    AddChild(objAttr, "name", strSelect);
                    AddChild(objAttr, "val", intVal.ToString(CultureInfo.InvariantCulture));
                    AddChild(objAttr, "min", intMin.ToString(CultureInfo.InvariantCulture));
                    AddChild(objAttr, "max", intMax.ToString(CultureInfo.InvariantCulture));
                    AddChild(objAttr, "aug", intAug.ToString(CultureInfo.InvariantCulture));
                    objBonus.AppendChild(objAttr);
                    break;
                case CustomImprovementType.Skill:
                    XmlElement objSkill = objBonusDoc.CreateElement("specificskill");
                    AddChild(objSkill, "name", strSelect);
                    AddChild(objSkill, "bonus", intVal.ToString(CultureInfo.InvariantCulture));
                    if (blnApplyToRating)
                        AddChild(objSkill, "applytorating", "yes");
                    objBonus.AppendChild(objSkill);
                    break;
                case CustomImprovementType.ConditionMonitorPhysical:
                case CustomImprovementType.ConditionMonitorStun:
                case CustomImprovementType.ConditionMonitorThreshold:
                case CustomImprovementType.ConditionMonitorThresholdOffset:
                    XmlElement objCm = objBonusDoc.CreateElement("conditionmonitor");
                    string strCmTag = eType switch
                    {
                        CustomImprovementType.ConditionMonitorPhysical => "physical",
                        CustomImprovementType.ConditionMonitorStun => "stun",
                        CustomImprovementType.ConditionMonitorThreshold => "threshold",
                        _ => "thresholdoffset"
                    };
                    AddChild(objCm, strCmTag, intVal.ToString(CultureInfo.InvariantCulture));
                    objBonus.AppendChild(objCm);
                    break;
                case CustomImprovementType.Initiative:
                    AddChild(objBonus, "initiative", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case CustomImprovementType.MovementPercent:
                    AddChild(objBonus, "movementpercent", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case CustomImprovementType.Concealability:
                    AddChild(objBonus, "concealability", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case CustomImprovementType.UnarmedDv:
                    AddChild(objBonus, "unarmeddv", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case CustomImprovementType.UnarmedAp:
                    AddChild(objBonus, "unarmedap", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case CustomImprovementType.Reach:
                    AddChild(objBonus, "reach", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case CustomImprovementType.LifestyleCost:
                    AddChild(objBonus, "lifestylecost", intVal.ToString(CultureInfo.InvariantCulture));
                    break;
            }

            ApplyBonus(objBonus, ImprovementSource.Custom, strName.Trim());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Every Active Skill's rules-data name (any category), for the "Skill" Custom
        /// Improvement type's picker - unlike <see cref="GetCombatActiveSkillNames"/>, not
        /// restricted to Combat Active, since frmCreateImprovement.cs's own Select Skill dialog
        /// isn't either.</summary>
        private IReadOnlyList<string> ExtractAttributeSelectionOptions(XmlNode? objNode)
        {
            if (objNode == null)
                return Array.Empty<string>();

            var lstAll = new List<string> { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" };
            if (Magician) lstAll.Add("MAG");
            if (Technomancer) lstAll.Add("RES");

            var lstInclude = objNode.SelectNodes("attribute")?.Cast<XmlNode>().Select(n => n.InnerText).ToList();
            if (lstInclude is { Count: > 0 })
                lstAll = lstAll.Where(lstInclude.Contains).ToList();

            var setExclude = objNode.SelectNodes("excludeattribute")?.Cast<XmlNode>()
                .Select(n => n.InnerText).ToHashSet();
            if (setExclude is { Count: > 0 })
                lstAll = lstAll.Where(a => !setExclude.Contains(a)).ToList();

            return lstAll;
        }

        /// <summary>Ported from frmSelectQuality.cs's cmdOK_Click/clsQuality.Create: also applies
        /// the quality's own rules-data &lt;bonus&gt; block (see <see cref="ApplyBonus"/>), matching
        /// legacy's CreateImprovements call on add. When the bonus is (or includes) a
        /// &lt;selecttext&gt;/&lt;selectskill&gt;/&lt;selectattribute&gt; node (see <see
        /// cref="QualityRequiresTextSelection"/>/<see cref="GetQualitySkillSelectionOptions"/>/
        /// <see cref="GetQualityAttributeSelectionOptions"/>), <paramref name="strExtra"/> becomes
        /// the corresponding Improvement - ported from clsImprovement.cs's
        /// selecttext/selectskill/selectattribute handlers. No real qualities.xml Quality combines
        /// more than one of these, so a single strExtra value is unambiguous.</summary>
        public CharacterDerivedValueData Composure =>
            SumAttributesWithImprovements(ImprovementType.Composure, ("WIL", "Willenskraft"), ("CHA", "Charisma"));

        /// <summary>Judge Intentions (INT + CHA + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData JudgeIntentions =>
            SumAttributesWithImprovements(ImprovementType.JudgeIntentions, ("INT", "Intuition"), ("CHA", "Charisma"));

        /// <summary>Lifting and Carrying (STR + BOD + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData LiftAndCarry =>
            SumAttributesWithImprovements(ImprovementType.LiftAndCarry, ("STR", "Stärke"), ("BOD", "Konstitution"));

        /// <summary>Memory (LOG + WIL + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData Memory =>
            SumAttributesWithImprovements(ImprovementType.Memory, ("LOG", "Logik"), ("WIL", "Willenskraft"));

        /// <summary>Damage Resistance dice pool, ported from frmCareer.cs's condition-monitor
        /// refresh: BOD + DamageResistance Improvements.</summary>
        public CharacterDerivedValueData DamageResistance
        {
            get
            {
                int intBody = GetAttributeInt("BOD");
                var sb = new StringBuilder();
                sb.Append("Konstitution: ").Append(intBody);

                var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.DamageResistance);
                int intTotal = intBody + lstContributions.Sum(c => c.Value);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Gesamt: ").Append(intTotal);

                return new CharacterDerivedValueData(intTotal, sb.ToString());
            }
        }

        /// <summary>Dice-pool penalty from filled Physical/Stun condition-monitor boxes. SR4 applies
        /// -1 for every three filled boxes on each track; ConditionMonitor Improvements then adjust
        /// that result (for example, wound-penalty mitigation).</summary>
        public IReadOnlyList<SenseImprovementOption> GetSenseImprovementOptions(string strPowerName)
        {
            XmlNode? objXmlSelectSenseware = FindSelectSensewareNode(strPowerName);
            if (objXmlSelectSenseware == null)
                return Array.Empty<SenseImprovementOption>();

            bool blnRequireSenseImprovement = objXmlSelectSenseware.Attributes?["requiresenseimprovement"]?.InnerText == "yes";
            var lstOptions = new List<SenseImprovementOption>();
            var setSeenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (strAttribute, strFile, strBasePath) in new[]
                     {
                         ("cyberwarecategory", "cyberware.xml", "/chummer/cyberwares/cyberware"),
                         ("biowarecategory", "bioware.xml", "/chummer/biowares/bioware"),
                         ("gearcategory", "gear.xml", "/chummer/gears/gear"),
                     })
            {
                string? strCategories = objXmlSelectSenseware.Attributes?[strAttribute]?.InnerText;
                if (string.IsNullOrEmpty(strCategories))
                    continue;

                var setCategories = new HashSet<string>(strCategories.Split(','), StringComparer.Ordinal);
                XmlDocument objSourceDoc = XmlManager.Instance.Load(strFile);
                XmlNodeList? objNodes = objSourceDoc.SelectNodes(strBasePath);
                if (objNodes == null)
                    continue;

                foreach (XmlNode objXmlItem in objNodes)
                {
                    if (!setCategories.Contains(GetValue(objXmlItem, "category", string.Empty)))
                        continue;
                    if (blnRequireSenseImprovement && GetValue(objXmlItem, "senseimprovement", "no") != "yes")
                        continue;

                    string strName = GetValue(objXmlItem, "name", string.Empty);
                    if (string.IsNullOrEmpty(strName) || !setSeenNames.Add(strName))
                        continue;

                    lstOptions.Add(new SenseImprovementOption(strName,
                        GetValue(objXmlItem, "translate", strName), strFile));
                }
            }

            lstOptions.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return lstOptions;
        }

        /// <summary>Adds an Adept Power that grants a selectsenseware bonus (e.g. "Improved
        /// Sense"), then applies <paramref name="strSelectedSenseware"/>'s own rules-data
        /// &lt;bonus&gt; block - ported from clsImprovement.cs's selectsenseware handler.
        /// <paramref name="strSelectedSenseware"/> must be one of <see
        /// cref="GetSenseImprovementOptions"/>'s <see cref="SenseImprovementOption.Name"/> values.
        /// The applied Rating is 1, or the selected item's own &lt;rating&gt; under the
        /// ImprovedSenseFullRating house rule.</summary>
        private string GetAttributeValue(string strCode)
        {
            var objNode =
                Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']/totalvalue");
            return string.IsNullOrEmpty(objNode == null ? null : objNode.InnerText) ? "0" : objNode.InnerText;
        }

        private int GetAttributeBaseInt(string strCode)
        {
            var objNode = Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']/value");
            return int.TryParse(objNode?.InnerText, out var intValue) ? intValue : 0;
        }

        private int GetAttributeMinimum(string strCode)
        {
            var objNode = Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']/metatypemin");
            return int.TryParse(objNode?.InnerText, out var intValue) ? intValue : 0;
        }

        // Ported from clsCharacter.cs's PhysicalCM/StunCM properties. The A.I./technocritter/
        // protosapient special cases (no BOD -> half System instead, no Stun track at all)
        // aren't ported since Core doesn't read metatype category yet - flag if a save file
        // needs it.
        private CharacterDerivedValueData ComputePhysicalCm()
        {
            var dblBod = double.TryParse(GetAttributeValue("BOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            var intBase = (int)Math.Ceiling(dblBod / 2) + 8;
            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.PhysicalCm);
            var intTotal = intBase + lstContributions.Sum(c => c.Value);

            var sb = new StringBuilder();
            sb.Append("Basis (8 + Konstitution/2 aufgerundet): ").Append(intBase);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        private CharacterDerivedValueData ComputeStunCm()
        {
            var dblWil = double.TryParse(GetAttributeValue("WIL"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            var intBase = (int)Math.Ceiling(dblWil / 2) + 8;
            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.StunCm);
            var intTotal = intBase + lstContributions.Sum(c => c.Value);

            var sb = new StringBuilder();
            sb.Append("Basis (8 + Willenskraft/2 aufgerundet): ").Append(intBase);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        /// <summary>Ported from clsEquipment.cs's Armor.TotalBallistic/TotalImpact: an armor's own
        /// base rating plus every currently-equipped ArmorMod's flat contribution (armor.xml mod
        /// ratings are plain integers, never "+"-prefixed stacking values like base armor).</summary>
        private IReadOnlyList<CharacterAttributeData> ReadAttributes()
        {
            var lstAttributes = new List<CharacterAttributeData>();
            var objNodes = Document.SelectNodes("/character/attributes/attribute");
            if (objNodes == null) return lstAttributes;
            foreach (XmlNode objNode in objNodes)
            {
                var strCode = GetValue(objNode, "name", string.Empty);
                var strValue = GetValue(objNode, "value", "0");
                var strTotalValue = GetValue(objNode, "totalvalue", strValue);
                if (strCode == "MAG" || strCode == "RES")
                    strTotalValue = ApplyEssencePenaltyToAttribute(strCode, strTotalValue).ToString(CultureInfo.InvariantCulture);
                else if (strCode == "AGI" || strCode == "BOD" || strCode == "STR")
                {
                    int intMeatValue = int.TryParse(strTotalValue, out var intParsedMeat) ? intParsedMeat : 0;
                    strTotalValue = ApplyCyberlimbAveraging(strCode, intMeatValue).ToString(CultureInfo.InvariantCulture);
                }
                var strMinimum = GetValue(objNode, "metatypemin", "0");
                lstAttributes.Add(new CharacterAttributeData(
                    strCode, strValue, strTotalValue,
                    strMinimum, GetValue(objNode, "metatypemax", "0"),
                    GetValue(objNode, "metatypeaugmax", GetValue(objNode, "metatypemax", "0")),
                    ComputeAttributeAugmented(strCode, strTotalValue),
                    ComputeAttributeKarmaCostToIncrease(strCode, strValue, strMinimum)));
            }

            return lstAttributes;
        }

        /// <summary>
        /// Ported from clsUnique.cs's Attribute.TotalValue's Cyberlimb-averaging block: for AGI/
        /// BOD/STR, replaces the "meat" total with the average across all of the character's
        /// Cyberlimbs' own stats (any limb slots not replaced by a Cyberlimb still contribute the
        /// meat value, padded out to Options.LimbCount). Limbs whose &lt;limbslot&gt; matches
        /// Options.ExcludeLimbSlot (e.g. excluding the skull from a torso-cyberlimb-only build)
        /// are skipped entirely, same as legacy.
        /// </summary>
        private int GetAttributeValueModifiers(string strCode) =>
            ImprovementManager.AugmentedValueOf(Improvements, ImprovementType.Attribute, strCode + "Base");

        private CharacterDerivedValueData ComputeAttributeAugmented(string strCode, string strTotalValue)
        {
            var intBase = int.TryParse(strTotalValue, out var intParsed) ? intParsed : 0;
            var lstContributions = ImprovementManager.DescribeAugmentedValueOf(Improvements, ImprovementType.Attribute, strCode)
                .ToList();
            lstContributions.AddRange(ImprovementManager.DescribeAugmentedValueOf(Improvements, ImprovementType.Attribute, strCode + "Base"));

            // Ported from frmCareer.cs/frmCreate.cs: a negative ballistic/impact armor encumbrance
            // penalty is dynamically applied as an AGI and REA reduction (not just a skill dice
            // pool note), each source counted separately if both are negative.
            if (strCode == "AGI" || strCode == "REA")
            {
                CharacterEncumbranceData encumbrance = ArmorEncumbrance;
                if (encumbrance.BallisticPenalty.Value < 0)
                    lstContributions.Add(("Rüstungsbehinderung (ballistisch)", encumbrance.BallisticPenalty.Value));
                if (encumbrance.ImpactPenalty.Value < 0)
                    lstContributions.Add(("Rüstungsbehinderung (Stoß)", encumbrance.ImpactPenalty.Value));
            }

            var intTotal = intBase + lstContributions.Sum(c => c.Value);

            var sb = new StringBuilder();
            sb.Append("Basis: ").Append(intBase);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        // Ported from frmCareer.cs's cmdImprove<Attribute>_Click handlers: cost is based on the
        // "shown" value (base + AttributeValueModifiers), not just the raw base.
        private int ComputeAttributeKarmaCostToIncrease(string strCode, string strValue, string strMinimum)
        {
            var intValue = int.TryParse(strValue, out var intParsedValue) ? intParsedValue : 0;
            var intMinimum = int.TryParse(strMinimum, out var intParsedMinimum) ? intParsedMinimum : 0;
            var objOptions = GetCharacterOptions();

            var intCost = (intValue + GetAttributeValueModifiers(strCode) + 1) * objOptions.KarmaAttribute;
            if (objOptions.AlternateMetatypeAttributeKarma)
                intCost -= (intMinimum - 1) * objOptions.KarmaAttribute;
            return intCost;
        }

        /// <summary>Career mode: raises an attribute's base Value by one, deducting Karma and logging an expense+undo. False if not enough Karma.</summary>
        public bool RaiseAttribute(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;

            var strValue = GetValue(objNode, "value", "0");
            var strMinimum = GetValue(objNode, "metatypemin", "0");
            var intValue = int.TryParse(strValue, out var intParsedValue) ? intParsedValue : 0;

            // Ported from frmCareer.cs's cmdImproveMAG_Click/cmdImproveRES_Click: the
            // SpecialKarmaCostBasedOnShownValue house rule replaces the usual "shown value"
            // (base + AttributeValueModifiers) cost basis with base - EssencePenalty, for MAG/RES
            // only - EDG and the mundane attributes always use the usual formula.
            int intCost;
            if ((strCode == "MAG" || strCode == "RES") && GetCharacterOptions().SpecialKarmaCostBasedOnShownValue)
                intCost = (intValue - EssencePenalty + 1) * GetCharacterOptions().KarmaAttribute;
            else
                intCost = ComputeAttributeKarmaCostToIncrease(strCode, strValue, strMinimum);
            var intKarma = int.TryParse(Karma, out var intParsedKarma) ? intParsedKarma : 0;
            if (intCost > intKarma) return false;

            SetChildValue(objNode, "value", (intValue + 1).ToString());
            SetChildValue(objNode, "totalvalue", (intValue + 1).ToString());
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, strCode);
            AddExpense("Karma", -intCost, strCode + " " + intValue + " -> " + (intValue + 1), null, objUndo);
            return true;
        }

        /// <summary>Create mode: sets an attribute's base Value directly - cost is derived from the whole build's point pool, not charged per call.</summary>
        public bool SetAttributeValue(string strCode, int intValue)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;
            SetChildValue(objNode, "value", intValue.ToString());
            SetChildValue(objNode, "totalvalue", intValue.ToString());
            return true;
        }

        /// <summary>Create mode: raises an attribute's base Value by one, deducting from the Karma
        /// or BP pool depending on <see cref="BuildMethod"/>. Enforces the metatype maximum and the
        /// SR4 chargen rule that only one attribute may be raised to its natural maximum.</summary>
        public bool RaiseAttributeCreate(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;

            var strValue = GetValue(objNode, "value", "0");
            var strMinimum = GetValue(objNode, "metatypemin", "0");
            var strMaximum = GetValue(objNode, "metatypemax", "0");
            int intValue = int.TryParse(strValue, out var v) ? v : 0;
            int intMaximum = int.TryParse(strMaximum, out var mx) ? mx : 0;
            if (intValue >= intMaximum)
                return false;

            bool blnReachesMax = intValue + 1 == intMaximum;
            if (blnReachesMax && s_astrPrimaryAttributeCodes.Contains(strCode) && AnyOtherAttributeAtMax(strCode))
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);

            int intCost;
            if (blnKarmaBuild)
                intCost = ComputeAttributeKarmaCostToIncrease(strCode, strValue, strMinimum);
            else
                intCost = objOptions.BpAttribute + (blnReachesMax ? objOptions.BpAttributeMax : 0);

            bool blnCheckSpecialAttribute = blnKarmaBuild && !objOptions.SpecialAttributeKarmaLimit
                && s_astrSpecialAttributeCodes.Contains(strCode);
            if (!objOptions.AllowExceedAttributeBp
                && (s_astrPrimaryAttributeCodes.Contains(strCode) || blnCheckSpecialAttribute)
                && !ExceedAttributeBpAllowedForThisRaise(intCost, blnCheckSpecialAttribute))
                return false;

            if (blnKarmaBuild)
            {
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intCost > intKarma)
                    return false;
                Karma = (intKarma - intCost).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intCost > intBp)
                    return false;
                Bp = (intBp - intCost).ToString();
            }

            SetChildValue(objNode, "value", (intValue + 1).ToString());
            SetChildValue(objNode, "totalvalue", (intValue + 1).ToString());
            return true;
        }

        /// <summary>Ported from frmCreate.cs's nud&lt;Attribute&gt;_ValueChanged handlers' "no more
        /// than half your starting BP/Karma on primary attributes" check (gated by the
        /// AllowExceedAttributeBp house rule). Returns true (no restriction) if the character has
        /// no persisted starting total - older save files predating this field, or characters not
        /// created through <see cref="NewCharacterFactory"/> - since there's nothing to check
        /// against. <paramref name="intAdditionalCost"/> is the specific raise being attempted,
        /// added on top of everything already spent on the 8 primary attributes so far.
        /// <paramref name="blnIncludeSpecialAttributes"/> additionally folds EDG/MAG/RES spend into
        /// the same pool - ported from CalculatePrimaryAttributeBP()+CalculateSpecialAttributeBP():
        /// in Karma-build mode, Special Attributes count towards the limit by default, unless the
        /// SpecialAttributeKarmaLimit house rule excludes them (BP-build mode never counts them at
        /// all, matching legacy).</summary>
        private bool ExceedAttributeBpAllowedForThisRaise(int intAdditionalCost, bool blnIncludeSpecialAttributes = false)
        {
            int intStartingTotal = int.TryParse(GetValue("/character/startingbuildpoints", "0"), out var s) ? s : 0;
            if (intStartingTotal <= 0)
                return true;

            int intSpent = s_astrPrimaryAttributeCodes.Sum(ComputeAttributeCreatePointsSpent);
            if (blnIncludeSpecialAttributes)
                intSpent += s_astrSpecialAttributeCodes.Sum(ComputeAttributeCreatePointsSpent);
            return intSpent + intAdditionalCost <= intStartingTotal / 2;
        }

        /// <summary>Recomputes how many Karma/BP points have already been spent raising this
        /// attribute from its metatype minimum to its current value, using the exact same per-step
        /// cost formulas <see cref="RaiseAttributeCreate"/>/<see cref="LowerAttributeCreate"/>
        /// already charge/refund, so it can never drift from what was actually charged.</summary>
        private int ComputeAttributeCreatePointsSpent(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return 0;

            int intValue = int.TryParse(GetValue(objNode, "value", "0"), out var v) ? v : 0;
            int intMinimum = int.TryParse(GetValue(objNode, "metatypemin", "0"), out var mn) ? mn : 0;
            int intMaximum = int.TryParse(GetValue(objNode, "metatypemax", "0"), out var mx) ? mx : 0;

            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            if (blnKarmaBuild)
            {
                int intSpent = 0;
                for (int i = intMinimum; i < intValue; i++)
                    intSpent += ComputeAttributeKarmaCostToIncrease(strCode, i.ToString(), intMinimum.ToString());
                return intSpent;
            }

            var objOptions = GetCharacterOptions();
            int intBpSpent = objOptions.BpAttribute * (intValue - intMinimum);
            if (intValue == intMaximum && intMaximum > intMinimum)
                intBpSpent += objOptions.BpAttributeMax;
            return intBpSpent;
        }

        /// <summary>Create mode: lowers an attribute's base Value by one, refunding the Karma or BP
        /// that was spent to reach the current rank.</summary>
        public bool LowerAttributeCreate(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;

            var strValue = GetValue(objNode, "value", "0");
            var strMinimum = GetValue(objNode, "metatypemin", "0");
            int intValue = int.TryParse(strValue, out var v) ? v : 0;
            int intMinimum = int.TryParse(strMinimum, out var mn) ? mn : 0;
            if (intValue <= intMinimum)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intMaximum = int.TryParse(GetValue(objNode, "metatypemax", "0"), out var mx) ? mx : 0;
            bool blnWasAtMax = intValue == intMaximum;

            if (blnKarmaBuild)
            {
                int intRefund = ComputeAttributeKarmaCostToIncrease(strCode, (intValue - 1).ToString(), strMinimum);
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + intRefund).ToString();
            }
            else
            {
                int intRefund = objOptions.BpAttribute + (blnWasAtMax ? objOptions.BpAttributeMax : 0);
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + intRefund).ToString();
            }

            SetChildValue(objNode, "value", (intValue - 1).ToString());
            SetChildValue(objNode, "totalvalue", (intValue - 1).ToString());
            return true;
        }

        // The SR4 chargen rule "only one attribute may reach its natural maximum" applies only to
        // the 8 primary physical/mental attributes - Edge, Magic, and Resonance each have their
        // own separate cost/cap rules and neither trigger nor are blocked by this rule. Essence
        // isn't a chargen attribute at all (it only decreases from cyber/bioware).
        private static readonly string[] s_astrMetatypeAttributeCodes =
        {
            "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "INI", "EDG", "MAG", "RES", "ESS"
        };

        private static readonly string[] s_astrPrimaryAttributeCodes =
        {
            "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL"
        };

        private static readonly string[] s_astrSpecialAttributeCodes = { "EDG", "MAG", "RES" };

        private bool AnyOtherAttributeAtMax(string strExcludeCode)
        {
            var objNodes = Document.SelectNodes("/character/attributes/attribute");
            if (objNodes == null) return false;
            foreach (XmlNode objNode in objNodes)
            {
                string strCode = GetValue(objNode, "name", string.Empty);
                if (strCode == strExcludeCode || !s_astrPrimaryAttributeCodes.Contains(strCode))
                    continue;
                int intValue = int.TryParse(GetValue(objNode, "value", "0"), out var v) ? v : 0;
                int intMaximum = int.TryParse(GetValue(objNode, "metatypemax", "0"), out var mx) ? mx : 0;
                if (intMaximum > 0 && intValue >= intMaximum)
                    return true;
            }
            return false;
        }

        private XmlNode GetAttributeNode(string strCode)
            => Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']");

        private static string AttributeForKnowledgeCategory(string strCategory)
            => strCategory is "Street" or "Interest" or "Language" ? "INT" : "LOG";

    }
}
