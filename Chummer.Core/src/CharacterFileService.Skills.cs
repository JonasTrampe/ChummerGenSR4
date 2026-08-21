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
        public bool AllowSkillDiceRollingEnabled => GetCharacterOptions().AllowSkillDiceRolling;

        /// <summary>Whether Skill Groups can be broken during creation - see <see cref="BreakSkillGroup"/>.</summary>
        public bool BreakSkillGroupsInCreateModeEnabled => GetCharacterOptions().BreakSkillGroupsInCreateMode;

        /// <summary>Whether the character's profile requests a pre-career backup.</summary>
        private int GetCreationActiveSkillCost()
        {
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            CharacterOptions objOptions = GetCharacterOptions();
            return (Document.SelectNodes("/character/skills/skill")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                .Where(s => GetValue(s, "knowledge", "False") != "True"
                    && GetValue(s, "grouped", "False") != "True")
                .Sum(s =>
                {
                    int intRating = ParseInteger(GetValue(s, "rating", "0"));
                    int intCost = ComputeCreationRatingCost(intRating, blnKarmaBuild, 4,
                        objOptions.KarmaNewActiveSkill, objOptions.KarmaImproveActiveSkill);
                    // BreakSkillGroupsInCreateMode: the Skill Group's own cost already covers every
                    // member Skill up to the Group's rating at the time it was broken (see
                    // BreakSkillGroup) - only the excess above that floor is charged individually
                    // here, ported from frmCreate.cs's "refund the cost of the first X points"
                    // comment.
                    int intFloor = GetSkillGroupBrokenFloor(GetValue(s, "skillgroup", string.Empty));
                    if (intFloor > 0)
                        intCost -= ComputeCreationRatingCost(intFloor, blnKarmaBuild, 4,
                            objOptions.KarmaNewActiveSkill, objOptions.KarmaImproveActiveSkill);
                    return Math.Max(0, intCost);
                });
        }

        /// <summary>The Skill Group rating a broken group's member Skills were locked to before
        /// being broken (0 if the group isn't broken or doesn't exist) - see <see
        /// cref="BreakSkillGroup"/>/<see cref="GetCreationActiveSkillCost"/>.</summary>
        private int GetSkillGroupBrokenFloor(string strGroupName)
        {
            if (string.IsNullOrEmpty(strGroupName))
                return 0;
            XmlNode? objGroup = GetSkillGroupNode(strGroupName);
            if (objGroup == null || GetValue(objGroup, "broken", "False") != "True")
                return 0;
            return ParseInteger(GetValue(objGroup, "rating", "0"));
        }

        /// <summary>Create mode: breaks a Skill Group (the BreakSkillGroupsInCreateMode house rule),
        /// unlocking its member Skills so they can be raised individually beyond the Group's own
        /// rating, while that rating's cost remains paid via the Group and only the excess is
        /// charged per Skill - ported from frmCreate.cs's chkBroken handling.</summary>
        public bool BreakSkillGroup(string strGroupName)
        {
            if (!GetCharacterOptions().BreakSkillGroupsInCreateMode || Created)
                return false;

            XmlNode? objGroup = GetSkillGroupNode(strGroupName);
            if (objGroup == null || GetValue(objGroup, "broken", "False") == "True")
                return false;
            if (ParseInteger(GetValue(objGroup, "rating", "0")) <= 0)
                return false;

            SetChildValue(objGroup, "broken", "True");
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes != null)
            {
                foreach (XmlNode objSkillNode in objNodes)
                {
                    if (GetValue(objSkillNode, "skillgroup", string.Empty) == strGroupName)
                        SetChildValue(objSkillNode, "grouped", "False");
                }
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Create mode: re-locks a broken Skill Group, provided its member Skills still
        /// agree with each other (or with <see cref="Options.AllowSkillRegrouping"/> - see <see
        /// cref="CanRaiseSkillGroupAsAWhole"/>), adopting their shared rating as the Group's own.</summary>
        public bool RegroupSkillGroup(string strGroupName)
        {
            XmlNode? objGroup = GetSkillGroupNode(strGroupName);
            if (objGroup == null || GetValue(objGroup, "broken", "False") != "True")
                return false;

            int intGroupRating = ParseInteger(GetValue(objGroup, "rating", "0"));
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intGroupRating, out int intCommonRating))
                return false;

            SetChildValue(objGroup, "broken", "False");
            SetChildValue(objGroup, "rating", intCommonRating.ToString(CultureInfo.InvariantCulture));
            SyncGroupedSkillRatings(strGroupName, intCommonRating);
            Changed?.Invoke();
            return true;
        }

        private int GetCreationSkillGroupCost()
        {
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            CharacterOptions objOptions = GetCharacterOptions();
            return (Document.SelectNodes("/character/skillgroups/skillgroup")?.Cast<XmlNode>()
                    ?? Enumerable.Empty<XmlNode>())
                .Sum(g => ComputeCreationRatingCost(ParseInteger(GetValue(g, "rating", "0")), blnKarmaBuild, 10,
                    objOptions.KarmaNewSkillGroup, objOptions.KarmaImproveSkillGroup));
        }

        private bool ApplyCreationSkillBudget(int intPreviousCost, int intCurrentCost)
        {
            if (Created || StartingBuildPoints <= 0)
                return true;

            int intDelta = intCurrentCost - intPreviousCost;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
            if (intDelta > intPool)
                return false;
            if (blnKarmaBuild)
                Karma = (intPool - intDelta).ToString(CultureInfo.InvariantCulture);
            else
                Bp = (intPool - intDelta).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        public enum CustomImprovementType
        {
            Attribute,
            Skill,
            ConditionMonitorPhysical,
            ConditionMonitorStun,
            ConditionMonitorThreshold,
            ConditionMonitorThresholdOffset,
            Initiative,
            MovementPercent,
            Concealability,
            UnarmedDv,
            UnarmedAp,
            Reach,
            LifestyleCost
        }

        /// <summary>Ported from frmCreateImprovement.cs's AcceptForm: synthesizes the same
        /// &lt;bonus&gt; XML shape a rules-data item's own &lt;bonus&gt; node would have (e.g.
        /// "Attribute" builds a &lt;specificattribute&gt; node) and runs it through the exact same
        /// <see cref="ApplyBonus"/>/<see cref="BonusApplier"/> pipeline every other bonus-granting
        /// item already uses, rather than hand-building an ImprovementSpec per type. <paramref
        /// name="strName"/> becomes both this Improvement's SourceName (its "Custom" identity for
        /// <see cref="RemoveCustomImprovement"/>) and its display label, since this port's
        /// Improvement model has no separate CustomName field the way legacy's does.</summary>
        public IReadOnlyList<string> GetActiveSkillNames()
        {
            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            var lstNames = new List<string>();
            XmlNodeList? objNodes = objSkillsDoc.SelectNodes("/chummer/skills/skill");
            if (objNodes != null)
                foreach (XmlNode objNode in objNodes)
                {
                    string strName = objNode["name"]?.InnerText ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(strName))
                        lstNames.Add(strName);
                }
            return lstNames;
        }

        public bool ComplexFormRequiresTextSelection(string strName) =>
            FindBonusChild("programs.xml", "programs", "program", strName, "selecttext") != null;

        /// <summary>Same as <see cref="GetQualitySkillSelectionOptions"/> but for a Critter
        /// Power's &lt;selectskill&gt; bonus.</summary>
        public IReadOnlyList<string> GetCritterPowerSkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("critterpowers.xml", "powers", "power", strName, "selectskill"));

        /// <summary>Same as <see cref="GetQualityAttributeSelectionOptions"/> but for a Critter
        /// Power's &lt;selectattribute&gt; bonus.</summary>
        public IReadOnlyList<string> GetCritterPowerAttributeSelectionOptions(string strName) =>
            ExtractAttributeSelectionOptions(FindBonusChild("critterpowers.xml", "powers", "power", strName, "selectattribute"));

        /// <summary>Same as <see cref="GetQualitySkillSelectionOptions"/> but for a Complex
        /// Form's &lt;selectskill&gt; bonus (e.g. Activesoft).</summary>
        public IReadOnlyList<string> GetComplexFormSkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("programs.xml", "programs", "program", strName, "selectskill"));

        /// <summary>Options to offer the player when this Quality's &lt;bonus&gt; is (or
        /// includes) a &lt;selectskill&gt; node - ported from clsImprovement.cs's selectskill
        /// handler's skillgroup/skillcategory/excludecategory filtering. Empty if not applicable.</summary>
        public IReadOnlyList<string> GetQualitySkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectskill"));

        /// <summary>Same as <see cref="GetQualitySkillSelectionOptions"/> but for an Adept
        /// Power's &lt;selectskill&gt; bonus (e.g. Improved Ability).</summary>
        public IReadOnlyList<string> GetAdeptPowerSkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("powers.xml", "powers", "power", strName, "selectskill"));

        /// <summary>Options to offer the player when this Quality's &lt;bonus&gt; is (or
        /// includes) a &lt;selectattribute&gt; node - ported from clsImprovement.cs's
        /// selectattribute handler's attribute/excludeattribute filtering (plus MAG/RES only
        /// being offered when the character actually has them). Empty if not applicable.</summary>
        private IReadOnlyList<string> ExtractSkillSelectionOptions(XmlNode? objNode)
        {
            if (objNode == null)
                return Array.Empty<string>();

            IEnumerable<CharacterSkillData> query = Skills.Where(s => !s.KnowledgeSkill);

            string? strSkillGroup = objNode.Attributes?["skillgroup"]?.InnerText;
            if (!string.IsNullOrEmpty(strSkillGroup))
                query = query.Where(s => s.SkillGroup == strSkillGroup);

            string? strCategory = objNode.Attributes?["skillcategory"]?.InnerText;
            if (!string.IsNullOrEmpty(strCategory))
                query = query.Where(s => s.Category == strCategory);

            string? strExcludeCategory = objNode.Attributes?["excludecategory"]?.InnerText;
            if (!string.IsNullOrEmpty(strExcludeCategory))
            {
                var setExcluded = new HashSet<string>(strExcludeCategory.Split(','), StringComparer.Ordinal);
                query = query.Where(s => !setExcluded.Contains(s.Category));
            }

            // Ported from clsImprovement.cs's selectskill handler: Exotic Skills (Exotic Melee/
            // Ranged Weapon, Pilot Exotic Vehicle) all share the same bare Name but are only
            // actually distinguished by their Specialization (e.g. "Exotic Ranged Weapon (Bow)"
            // vs. "...(Grenade Launcher)"), so a character can own several. Using the bare Name
            // as the selectable/stored value would silently collapse them into one ambiguous
            // option - offer/store the full "Name (Specialization)" form for those instead.
            return query.Select(s => s.Exotic ? s.Name + " (" + s.Specialization + ")" : s.Name)
                .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        private void AppendAutomaticProgramOptions(XmlElement objProgram)
        {
            string strCategory = GetValue(objProgram, "category", string.Empty);
            if (strCategory != "Matrix Programs" && strCategory != "Skillsofts"
                && strCategory != "Autosofts" && strCategory != "Autosofts, Agent"
                && strCategory != "Autosofts, Drone")
                return;

            string strName = GetValue(objProgram, "name", string.Empty);
            CharacterOptions objOptions = GetCharacterOptions();
            if (!objOptions.BookEnabled("UN") || strName.StartsWith("Suite:", StringComparison.Ordinal))
                return;

            XmlElement objChildren = objProgram.SelectSingleNode("children") as XmlElement
                ?? throw new InvalidOperationException("A newly created Gear item has no children collection.");
            string strParentRating = GetValue(objProgram, "rating", "0");
            string strCopyRating = int.TryParse(strParentRating, out int intRating) && intRating > 0
                ? intRating.ToString(CultureInfo.InvariantCulture)
                : "1";

            if (objOptions.AutomaticCopyProtection)
                AppendGearNode(objChildren, "Copy Protection", "Program Options", strCopyRating, "1", "0", "0",
                    "UN", "114", "[0]", "", "", "", "");
            if (objOptions.AutomaticRegistration)
                AppendGearNode(objChildren, "Registration", "Program Options", "0", "1", "0", "0", "UN",
                    "115", "[0]", "", "", "", "");
        }

        public bool PrintSkillsWithZeroRating => GetCharacterOptions().PrintSkillsWithZeroRating;
        public IReadOnlyList<string> GetCombatActiveSkillNames()
        {
            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            var lstNames = new List<string>();
            XmlNodeList? objNodes = objSkillsDoc.SelectNodes("/chummer/skills/skill[category = \"Combat Active\"]");
            if (objNodes != null)
                foreach (XmlNode objNode in objNodes)
                {
                    string strName = objNode["name"]?.InnerText ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(strName))
                        lstNames.Add(strName);
                }
            return lstNames;
        }

        private static Dictionary<string, bool>? _dicSkillDefaulting;

        // Ported from clsUnique.cs's Skill.Default: only used for the Rating-0 "defaulting" pool
        // (Attribute - 1), cross-referenced from skills.xml since the character file itself
        // doesn't save whether a Skill allows it.
        private static bool SkillAllowsDefaulting(string strName)
        {
            if (_dicSkillDefaulting == null)
                _dicSkillDefaulting = BuildSkillDefaultingLookup();
            return _dicSkillDefaulting.TryGetValue(strName, out bool blnDefault) && blnDefault;
        }

        private static Dictionary<string, bool> BuildSkillDefaultingLookup()
        {
            var dicResult = new Dictionary<string, bool>();
            XmlDocument objDocument = XmlManager.Instance.Load("skills.xml");
            XmlNodeList? objNodes = objDocument.SelectNodes("/chummer/skills/skill");
            if (objNodes == null) return dicResult;

            foreach (XmlNode objNode in objNodes)
            {
                string strName = objNode["name"]?.InnerText ?? string.Empty;
                if (strName.Length == 0) continue;
                dicResult[strName] = objNode["default"]?.InnerText == "Yes";
            }

            return dicResult;
        }

        public IReadOnlyList<CharacterSkillGroupData> SkillGroups => ReadSkillGroups();

        public IReadOnlyList<CharacterSkillData> Skills => ReadSkills();

        public IReadOnlyList<CharacterSkillData> KnowledgeSkills => ReadKnowledgeSkills();

        public bool AddKnowledgeSkill(string strName, string strCategory)
        {
            int intPreviousCreationCost = GetCreationKnowledgeSkillCost();
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSkills = objRoot.SelectSingleNode("skills");
            if (objSkills == null)
            {
                objSkills = Document.CreateElement("skills");
                objRoot.AppendChild(objSkills);
            }

            var objSkill = Document.CreateElement("skill");
            string strAttribute = AttributeForKnowledgeCategory(strCategory);
            AppendElement(objSkill, "name", strName.Trim());
            AppendElement(objSkill, "skillgroup", string.Empty);
            AppendElement(objSkill, "skillcategory", strCategory);
            AppendElement(objSkill, "grouped", "False");
            AppendElement(objSkill, "default", "False");
            AppendElement(objSkill, "rating", "1");
            AppendElement(objSkill, "ratingmax", "6");
            AppendElement(objSkill, "knowledge", "True");
            AppendElement(objSkill, "exotic", "False");
            AppendElement(objSkill, "spec", string.Empty);
            AppendElement(objSkill, "allowdelete", "True");
            AppendElement(objSkill, "attribute", strAttribute);
            AppendElement(objSkill, "totalvalue", "0");
            objSkills.AppendChild(objSkill);
            if (!ApplyCreationKnowledgeSkillBudget(intPreviousCreationCost))
            {
                objSkills.RemoveChild(objSkill);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public bool UpdateKnowledgeSkill(int intSkillId, string strName, string strRating, string strSpecialization,
            string strCategory)
        {
            XmlNode? objNode = GetKnowledgeSkillNode(intSkillId);
            if (objNode == null)
                return false;
            if (!int.TryParse(strRating, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intRating)
                || intRating < 0
                || intRating > ParseInteger(GetValue(objNode, "ratingmax", "6")))
                return false;

            int intPreviousCreationCost = GetCreationKnowledgeSkillCost();
            string strPreviousName = GetValue(objNode, "name", string.Empty);
            string strPreviousRating = GetValue(objNode, "rating", "0");
            string strPreviousSpecialization = GetValue(objNode, "spec", string.Empty);
            string strPreviousCategory = GetValue(objNode, "skillcategory", string.Empty);
            string strPreviousAttribute = GetValue(objNode, "attribute", string.Empty);

            SetChildValue(objNode, "name", strName);
            SetChildValue(objNode, "rating", strRating);
            SetChildValue(objNode, "spec", strSpecialization);
            SetChildValue(objNode, "skillcategory", strCategory);
            SetChildValue(objNode, "attribute", AttributeForKnowledgeCategory(strCategory));
            if (!ApplyCreationKnowledgeSkillBudget(intPreviousCreationCost))
            {
                SetChildValue(objNode, "name", strPreviousName);
                SetChildValue(objNode, "rating", strPreviousRating);
                SetChildValue(objNode, "spec", strPreviousSpecialization);
                SetChildValue(objNode, "skillcategory", strPreviousCategory);
                SetChildValue(objNode, "attribute", strPreviousAttribute);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Career mode: raises an active skill's rating by one, deducting Karma. False if
        /// not enough Karma, or the skill is currently grouped (raise the skill group instead).</summary>
        public bool RaiseActiveSkill(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            // A meta skill (e.g. "Perception (Visual)") always mirrors its base skill's rating and
            // can't be raised independently - ported from SkillControl.cs hiding the raise control
            // for IsMeta skills.
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True" || GetValue(objNode, "isMeta", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            int? intCostPreview = GetActiveSkillKarmaCostToIncrease(intSkillId);
            if (intCostPreview == null)
                return false;
            int intCost = intCostPreview.Value;

            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intCost > intKarma)
                return false;

            string strName = GetValue(objNode, "name", string.Empty);
            SetChildValue(objNode, "rating", (intRating + 1).ToString());
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveSkill, strName);
            AddExpense("Karma", -intCost, strName + " " + intRating + " -> " + (intRating + 1), null, objUndo);
            return true;
        }

        /// <summary>Returns the Karma needed for the next Career rating of an ungrouped Active
        /// Skill, or <see langword="null"/> when that skill cannot be raised independently.</summary>
        public int? GetActiveSkillKarmaCostToIncrease(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True" || GetValue(objNode, "isMeta", "False") == "True")
                return null;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            var objOptions = GetCharacterOptions();
            return intRating == 0
                ? objOptions.KarmaNewActiveSkill
                : (intRating + 1) * objOptions.KarmaImproveActiveSkill * (intRating >= 6 ? 2 : 1);
        }

        /// <summary>Create mode: sets an active skill's rating directly. False if the skill is
        /// currently grouped (set the group's rating instead).</summary>
        public bool SetActiveSkillRating(int intSkillId, int intRating)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True" || GetValue(objNode, "isMeta", "False") == "True")
                return false;

            int intRatingMax = ParseInteger(GetValue(objNode, "ratingmax", "6"));
            if (intRating < 0 || intRating > intRatingMax)
                return false;
            // A broken Skill Group's floor rating was paid via the Group itself - it can't be
            // refunded by dropping the member Skill below it (see BreakSkillGroup).
            if (intRating < GetSkillGroupBrokenFloor(GetValue(objNode, "skillgroup", string.Empty)))
                return false;

            int intPreviousCost = GetCreationActiveSkillCost();
            string strPreviousRating = GetValue(objNode, "rating", "0");
            SetChildValue(objNode, "rating", intRating.ToString());
            if (!ApplyCreationSkillBudget(intPreviousCost, GetCreationActiveSkillCost()))
            {
                SetChildValue(objNode, "rating", strPreviousRating);
                return false;
            }
            return true;
        }

        /// <summary>Create mode: raises an active skill's rating by one, deducting from the Karma
        /// or BP pool depending on <see cref="BuildMethod"/>. False if not enough points, the skill
        /// is grouped, or the skill is already at its ratingmax.</summary>
        public bool RaiseActiveSkillCreate(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True" || GetValue(objNode, "isMeta", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            int intRatingMax = int.TryParse(GetValue(objNode, "ratingmax", "6"), out var rm) ? rm : 6;
            if (intRating >= intRatingMax)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);

            if (blnKarmaBuild)
            {
                int intCost = intRating == 0
                    ? objOptions.KarmaNewActiveSkill
                    : (intRating + 1) * objOptions.KarmaImproveActiveSkill * (intRating >= 6 ? 2 : 1);
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intCost > intKarma)
                    return false;
                Karma = (intKarma - intCost).ToString();
            }
            else
            {
                int intCost = objOptions.BpActiveSkill;
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intCost > intBp)
                    return false;
                Bp = (intBp - intCost).ToString();
            }

            SetChildValue(objNode, "rating", (intRating + 1).ToString());
            return true;
        }

        /// <summary>Create mode: lowers an active skill's rating by one, refunding the Karma or BP
        /// that was spent to reach the current rank.</summary>
        public bool LowerActiveSkillCreate(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True" || GetValue(objNode, "isMeta", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (intRating <= 0)
                return false;
            // A broken Skill Group's floor rating was paid via the Group itself - it can't be
            // refunded by dropping the member Skill below it (see BreakSkillGroup).
            if (intRating <= GetSkillGroupBrokenFloor(GetValue(objNode, "skillgroup", string.Empty)))
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPreviousRating = intRating - 1;

            if (blnKarmaBuild)
            {
                int intRefund = intPreviousRating == 0
                    ? objOptions.KarmaNewActiveSkill
                    : intRating * objOptions.KarmaImproveActiveSkill * (intPreviousRating >= 6 ? 2 : 1);
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + intRefund).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + objOptions.BpActiveSkill).ToString();
            }

            SetChildValue(objNode, "rating", intPreviousRating.ToString());
            return true;
        }

        /// <summary>Whether a still-unlocked (grouped=False) skill group is safe to raise/set as a
        /// whole: true if every member skill already shares the same individual rating (including
        /// trivially true for a group with no member skills yet, or one already at its own stored
        /// rating). <paramref name="intCommonRating"/> is that shared rating when true.</summary>
        private bool CanRaiseSkillGroupAsAWhole(string strGroupName, int intGroupRating, out int intCommonRating)
        {
            intCommonRating = intGroupRating;
            bool blnFirst = true;
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null)
                return true;

            foreach (XmlNode objSkillNode in objNodes)
            {
                if (GetValue(objSkillNode, "knowledge", "False") == "True"
                    || GetValue(objSkillNode, "skillgroup", string.Empty) != strGroupName)
                    continue;

                int intRating = int.TryParse(GetValue(objSkillNode, "rating", "0"), out var r) ? r : 0;
                if (blnFirst)
                {
                    intCommonRating = intRating;
                    blnFirst = false;
                }
                else if (intRating != intCommonRating)
                {
                    return false;
                }
            }

            // A group whose member skills already agree with each other but not with the group's
            // own stored rating has been raised individually while ungrouped - only resumable as a
            // group (adopting that shared rating as the new baseline) with AllowSkillRegrouping.
            return intCommonRating == intGroupRating || GetCharacterOptions().AllowSkillRegrouping;
        }

        /// <summary>Career mode: raises a skill group's rating by one, deducting Karma, and keeps
        /// every grouped member skill's own rating in sync with the new group rating. False (in
        /// addition to insufficient Karma) if member skills have already diverged from each other,
        /// or from the group's own rating without <see cref="Options.AllowSkillRegrouping"/> - see
        /// <see cref="CanRaiseSkillGroupAsAWhole"/>.</summary>
        public bool RaiseSkillGroup(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null)
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intRating, out int intCommonRating))
                return false;
            intRating = intCommonRating;
            int? intCostPreview = GetSkillGroupKarmaCostToIncrease(strGroupName);
            if (intCostPreview == null)
                return false;
            int intCost = intCostPreview.Value;

            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intCost > intKarma)
                return false;

            int intNewRating = intRating + 1;
            SetChildValue(objNode, "rating", intNewRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intNewRating);
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveSkillGroup, strGroupName);
            AddExpense("Karma", -intCost, strGroupName + " " + intRating + " -> " + intNewRating, null, objUndo);
            return true;
        }

        /// <summary>Returns the Karma needed for the next Career rating of a skill group, or
        /// <see langword="null"/> if its member skills make it ineligible for a group raise.</summary>
        public int? GetSkillGroupKarmaCostToIncrease(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null)
                return null;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intRating, out int intCommonRating))
                return null;

            var objOptions = GetCharacterOptions();
            return intCommonRating == 0
                ? objOptions.KarmaNewSkillGroup
                : (intCommonRating + 1) * objOptions.KarmaImproveSkillGroup;
        }

        /// <summary>Create mode: sets a skill group's rating directly and syncs member skills. False
        /// if member skills have already diverged - see <see cref="CanRaiseSkillGroupAsAWhole"/>.</summary>
        public bool SetSkillGroupRating(string strGroupName, int intRating)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null || GetValue(objNode, "broken", "False") == "True")
                return false;
            if (intRating < 0 || intRating > 6)
                return false;

            int intCurrentRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intCurrentRating, out _))
                return false;

            int intPreviousCost = GetCreationSkillGroupCost();
            SetChildValue(objNode, "rating", intRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intRating);
            if (!ApplyCreationSkillBudget(intPreviousCost, GetCreationSkillGroupCost()))
            {
                SetChildValue(objNode, "rating", intCurrentRating.ToString());
                SyncGroupedSkillRatings(strGroupName, intCurrentRating);
                return false;
            }
            return true;
        }

        /// <summary>Create mode: raises a skill group's rating by one, deducting from the Karma or
        /// BP pool depending on <see cref="BuildMethod"/>, and syncs member skills. False if member
        /// skills have already diverged - see <see cref="CanRaiseSkillGroupAsAWhole"/>.</summary>
        public bool RaiseSkillGroupCreate(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null || GetValue(objNode, "broken", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            // Skill groups may only be raised to 4 during character creation - the 5/6 ranks
            // are career-mode-only (frmCreate.cs caps nudActiveSkillGroup at 4).
            if (intRating >= 4)
                return false;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intRating, out int intCommonRating))
                return false;
            intRating = intCommonRating;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);

            if (blnKarmaBuild)
            {
                int intCost = intRating == 0 ? objOptions.KarmaNewSkillGroup : (intRating + 1) * objOptions.KarmaImproveSkillGroup;
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intCost > intKarma)
                    return false;
                Karma = (intKarma - intCost).ToString();
            }
            else
            {
                int intCost = objOptions.BpSkillGroup;
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intCost > intBp)
                    return false;
                Bp = (intBp - intCost).ToString();
            }

            int intNewRating = intRating + 1;
            SetChildValue(objNode, "rating", intNewRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intNewRating);
            return true;
        }

        /// <summary>Create mode: lowers a skill group's rating by one, refunding the Karma or BP
        /// that was spent to reach the current rank, and syncs member skills.</summary>
        public bool LowerSkillGroupCreate(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null || GetValue(objNode, "broken", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (intRating <= 0)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPreviousRating = intRating - 1;

            if (blnKarmaBuild)
            {
                int intRefund = intPreviousRating == 0 ? objOptions.KarmaNewSkillGroup : intRating * objOptions.KarmaImproveSkillGroup;
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + intRefund).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + objOptions.BpSkillGroup).ToString();
            }

            SetChildValue(objNode, "rating", intPreviousRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intPreviousRating);
            return true;
        }

        /// <summary>Keeps every skill in <paramref name="strGroupName"/> locked to the group's own
        /// rating and cost. Once the group has a rating above 0, its member skills are marked
        /// "grouped" (locking them - see <see cref="RaiseActiveSkillCreate"/>/<see cref="RaiseActiveSkill"/>)
        /// and their own rating is overwritten to match, so the group's cost is what governs them
        /// and any individual skill cost no longer applies. Dropping the group back to 0 unlocks them.</summary>
        private void SyncGroupedSkillRatings(string strGroupName, int intRating)
        {
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return;
            foreach (XmlNode objSkillNode in objNodes)
            {
                if (GetValue(objSkillNode, "skillgroup", string.Empty) != strGroupName)
                    continue;

                SetChildValue(objSkillNode, "grouped", intRating > 0 ? "True" : "False");
                SetChildValue(objSkillNode, "rating", intRating.ToString());
            }
        }

        /// <summary>Create mode: sets an active skill's specialization directly, no Karma cost.</summary>
        public bool SetActiveSkillSpecialization(int intSkillId, string strSpecialization)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "spec", strSpecialization);
            return true;
        }

        /// <summary>Career mode: sets an active skill's specialization, deducting the flat
        /// KarmaSpecialization cost. False if not enough Karma.</summary>
        public bool AddActiveSkillSpecialization(int intSkillId, string strSpecialization)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null)
                return false;

            int intCost = GetActiveSkillSpecializationKarmaCost();
            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intCost > intKarma)
                return false;

            string strName = GetValue(objNode, "name", string.Empty);
            SetChildValue(objNode, "spec", strSpecialization);
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.SkillSpec, strName);
            AddExpense("Karma", -intCost, strName + " -> " + strSpecialization, null, objUndo);
            return true;
        }

        /// <summary>The flat Career Karma cost for an Active Skill specialization.</summary>
        public void AddExoticSkill(string strName, string strSpecialization, string strCategory, string strAttribute)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSkills = objRoot.SelectSingleNode("skills");
            if (objSkills == null)
            {
                objSkills = Document.CreateElement("skills");
                objRoot.AppendChild(objSkills);
            }

            var objSkill = Document.CreateElement("skill");
            AppendElement(objSkill, "name", strName.Trim());
            AppendElement(objSkill, "skillgroup", string.Empty);
            AppendElement(objSkill, "skillcategory", strCategory);
            AppendElement(objSkill, "grouped", "False");
            AppendElement(objSkill, "default", "False");
            AppendElement(objSkill, "rating", "0");
            AppendElement(objSkill, "ratingmax", "6");
            AppendElement(objSkill, "knowledge", "False");
            AppendElement(objSkill, "exotic", "True");
            AppendElement(objSkill, "spec", strSpecialization.Trim());
            AppendElement(objSkill, "allowdelete", "True");
            AppendElement(objSkill, "attribute", strAttribute);
            AppendElement(objSkill, "totalvalue", "0");
            objSkills.AppendChild(objSkill);
        }

        private XmlNode? GetActiveSkillNode(int intSkillId)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null || intSkillId < 0 || intSkillId >= objNodes.Count)
                return null;

            XmlNode objNode = objNodes[intSkillId]!;
            return GetValue(objNode, "knowledge", "False") == "False" ? objNode : null;
        }

        private XmlNode? GetSkillGroupNode(string strGroupName)
            => Document.SelectSingleNode("/character/skillgroups/skillgroup[name = '" + strGroupName + "']");

        public bool RemoveKnowledgeSkill(int intSkillId)
        {
            XmlNode? objNode = GetKnowledgeSkillNode(intSkillId);
            if (objNode?.ParentNode == null)
                return false;

            int intPreviousCreationCost = GetCreationKnowledgeSkillCost();
            XmlNode objParent = objNode.ParentNode;
            XmlNode? objNextSibling = objNode.NextSibling;
            objParent.RemoveChild(objNode);
            if (!ApplyCreationKnowledgeSkillBudget(intPreviousCreationCost))
            {
                if (objNextSibling == null)
                    objParent.AppendChild(objNode);
                else
                    objParent.InsertBefore(objNode, objNextSibling);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from frmCreate.cs's "Calculate Free Knowledge Skill Points" comment:
        /// (INT + LOG) * 3 free points, but only for BP-build characters or Karma-build characters
        /// with the FreeKarmaKnowledge house rule on - Karma-build otherwise gets none at all.</summary>
        private int GetFreeKnowledgeSkillPoints(bool blnKarmaBuild)
        {
            if (blnKarmaBuild && !GetCharacterOptions().FreeKarmaKnowledge)
                return 0;
            return (GetAttributeInt("INT") + GetAttributeInt("LOG")) * 3;
        }

        private int GetCreationKnowledgeSkillCost()
        {
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            CharacterOptions objOptions = GetCharacterOptions();
            int intFreePoints = GetFreeKnowledgeSkillPoints(blnKarmaBuild);

            if (!blnKarmaBuild)
            {
                int intTotalPoints = (Document.SelectNodes("/character/skills/skill")?.Cast<XmlNode>()
                    ?? Enumerable.Empty<XmlNode>())
                    .Where(s => GetValue(s, "knowledge", "False") == "True" && GetValue(s, "grouped", "False") != "True")
                    .Sum(s => ParseInteger(GetValue(s, "rating", "0")));
                return Math.Max(0, intTotalPoints - intFreePoints) * objOptions.BpKnowledgeSkill;
            }

            // Karma build: every rank of every Knowledge Skill costs its own per-step Karma value
            // (rating 1 = KarmaNewKnowledgeSkill, rating N>1 = N * KarmaImproveKnowledgeSkill). When
            // FreeKarmaKnowledge is on, the cheapest steps (rating 1 across every Knowledge Skill,
            // then rating 2, etc. - ported from frmCreate.cs's do/while loop) are waived first, up to
            // the free-point total.
            var lstStepCosts = new List<int>();
            foreach (XmlNode objSkill in Document.SelectNodes("/character/skills/skill")?.Cast<XmlNode>()
                ?? Enumerable.Empty<XmlNode>())
            {
                if (GetValue(objSkill, "knowledge", "False") != "True"
                    || GetValue(objSkill, "grouped", "False") == "True")
                    continue;
                int intRating = ParseInteger(GetValue(objSkill, "rating", "0"));
                for (int i = 1; i <= intRating; i++)
                    lstStepCosts.Add(i == 1 ? objOptions.KarmaNewKnowledgeSkill : i * objOptions.KarmaImproveKnowledgeSkill);
            }

            int intFreeStepsRemaining = intFreePoints;
            int intCost = 0;
            foreach (int intStepCost in lstStepCosts.OrderBy(i => i))
            {
                if (intFreeStepsRemaining > 0)
                    intFreeStepsRemaining--;
                else
                    intCost += intStepCost;
            }
            return intCost;
        }

        private bool ApplyCreationKnowledgeSkillBudget(int intPreviousCost)
        {
            if (Created || StartingBuildPoints <= 0)
                return true;

            int intDelta = GetCreationKnowledgeSkillCost() - intPreviousCost;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
            if (intDelta > intPool)
                return false;
            if (blnKarmaBuild)
                Karma = (intPool - intDelta).ToString(CultureInfo.InvariantCulture);
            else
                Bp = (intPool - intDelta).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        // Enemies are saved into the same <contacts> list as regular contacts and are only
        // distinguished by <type>Enemy</type> - split here so each gets its own display list.
        public IReadOnlyList<CharacterComplexFormData> ComplexForms => ReadComplexForms();

        /// <summary>Creation/career Karma cost for buying a Complex Form at a given rating.
        /// Uses the legacy Skillsoft exception and AlternateComplexFormCost spell-cost rule.</summary>
        public int ComputeComplexFormKarmaCost(string strCategory, int intRating)
        {
            if (intRating < 1) return 0;
            CharacterOptions objOptions = GetCharacterOptions();
            if (objOptions.AlternateComplexFormCost)
                return objOptions.KarmaSpell;
            if (string.Equals(strCategory, "Skillsofts", StringComparison.Ordinal))
                return intRating * objOptions.KarmaComplexFormSkillsoft;
            int intCost = objOptions.KarmaNewComplexForm;
            for (int i = 2; i <= intRating; i++) intCost += i * objOptions.KarmaImproveComplexForm;
            return intCost;
        }

        private int GetAttributeInt(string strCode)
            => int.TryParse(GetAttributeValue(strCode), out var intValue) ? intValue : 0;

        /// <summary>The natural (unaugmented) attribute Value, as opposed to GetAttributeInt's
        /// TotalValue - only used by the CapSkillRating house rule, which caps against the
        /// character's "real" attribute rather than its cyberware/magic-boosted total.</summary>
        private IReadOnlyList<CharacterSkillGroupData> ReadSkillGroups()
        {
            var lstGroups = new List<CharacterSkillGroupData>();
            var objNodes = Document.SelectNodes("/character/skillgroups/skillgroup");
            if (objNodes == null) return lstGroups;
            foreach (XmlNode objNode in objNodes)
                lstGroups.Add(new CharacterSkillGroupData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "rating", "0"), GetValue(objNode, "broken", "False") == "True"));
            return lstGroups;
        }

        private IReadOnlyList<CharacterSkillData> ReadSkills()
        {
            var lstSkills = new List<CharacterSkillData>();
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return lstSkills;

            // Meta skills (e.g. "Perception (Visual)") mirror their base skill's Rating/
            // Specialization - ported from SkillControl.cs's UpdateMetas(). Build a name lookup
            // first so BuildSkillData can resolve a meta skill's base regardless of node order.
            var dicBaseSkillInfo = new Dictionary<string, (int Rating, string Specialization)>(StringComparer.Ordinal);
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") == "True")
                    continue;
                dicBaseSkillInfo[GetValue(objNode, "name", string.Empty)] =
                    (ParseInteger(GetValue(objNode, "rating", "0")), GetValue(objNode, "spec", string.Empty));
            }

            int intSkillId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") != "True")
                {
                    lstSkills.Add(BuildSkillData(intSkillId, objNode, GetValue(objNode, "skillgroup", string.Empty),
                        GetValue(objNode, "grouped", "False") == "True", dicBaseSkillInfo));
                }

                intSkillId++;
            }

            return lstSkills;
        }

        private IReadOnlyList<CharacterSkillData> ReadKnowledgeSkills()
        {
            var lstSkills = new List<CharacterSkillData>();
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return lstSkills;
            int intSkillId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") == "True")
                {
                    lstSkills.Add(BuildSkillData(intSkillId, objNode, string.Empty, blnIsGroupLocked: false));
                }

                intSkillId++;
            }

            return lstSkills;
        }

        /// <summary>Ported from clsCharacter.cs's PrintToStream PrintLeadershipAlternates/
        /// PrintArcanaAlternates blocks: a synthetic print-only copy of <paramref name="skill"/>
        /// under a different name/linked Attribute (e.g. "Leadership, Command" uses LOG instead of
        /// Leadership's own CHA), sharing its Rating/Specialization/SkillGroup but with its own
        /// recomputed dice pool for the substitute Attribute.</summary>
        public CharacterSkillData BuildAlternateSkillForPrint(CharacterSkillData skill, string strSuffix, string strAttribute)
        {
            int intRating = int.TryParse(skill.BaseRating, out var r) ? r : 0;
            bool blnCanDefault = skill.KnowledgeSkill || (!skill.Exotic && SkillAllowsDefaulting(skill.Name));
            (string strRatingDisplay, int intPool, string strTooltip) = ComputeSkillDicePool(
                skill.Name, skill.SkillGroup, skill.Category, strAttribute, intRating, skill.Specialization, blnCanDefault);
            return new CharacterSkillData(skill.SkillId, skill.Name + ", " + strSuffix, strAttribute,
                skill.BaseRating, strRatingDisplay, intPool.ToString(CultureInfo.InvariantCulture), strTooltip,
                skill.Specialization, skill.Category, skill.IsGroupLocked, blnAllowDelete: false,
                blnKnowledgeSkill: false, skill.SkillGroup, skill.Exotic);
        }

        private CharacterSkillData BuildSkillData(int intSkillId, XmlNode objNode, string strSkillGroup, bool blnIsGroupLocked,
            IReadOnlyDictionary<string, (int Rating, string Specialization)>? dicBaseSkillInfo = null)
        {
            string strName = GetValue(objNode, "name", string.Empty);
            string strAttribute = GetValue(objNode, "attribute", string.Empty);
            string strCategory = GetValue(objNode, "skillcategory", string.Empty);
            string strSpecialization = GetValue(objNode, "spec", string.Empty);
            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            bool blnAllowDelete = GetValue(objNode, "allowdelete", "False") == "True";
            bool blnKnowledge = GetValue(objNode, "knowledge", "False") == "True";

            bool blnExotic = GetValue(objNode, "exotic", "False") == "True";

            bool blnIsMeta = GetValue(objNode, "isMeta", "False") == "True";
            string strMetaBase = GetValue(objNode, "metaBase", string.Empty);
            string strMetaSpec = GetValue(objNode, "metaSpec", string.Empty);
            int intMetaRatingModifier = 0;
            if (blnIsMeta && dicBaseSkillInfo != null && dicBaseSkillInfo.TryGetValue(strMetaBase, out var tupBase))
            {
                // Ported from SkillControl.cs's UpdateMetas(): a meta skill always mirrors its base
                // skill's Rating (never its own stored value), and gets a +2 dice pool bonus when
                // the base skill's current Specialization text matches this meta skill's MetaSpec.
                intRating = tupBase.Rating;
                if (strMetaSpec.Length > 0 && tupBase.Specialization == strMetaSpec)
                    intMetaRatingModifier = 2;
            }

            // Knowledge/Language Skills can always be used untrained (SR4 65); active Skills can
            // only default if skills.xml says so (exotic Active Skills never allow it).
            bool blnCanDefault = blnKnowledge || (!blnExotic && SkillAllowsDefaulting(strName));
            (string strRatingDisplay, int intPool, string strTooltip) = ComputeSkillDicePool(
                strName, strSkillGroup, strCategory, strAttribute, intRating, strSpecialization, blnCanDefault,
                blnIsMeta ? strMetaBase : string.Empty, intMetaRatingModifier);

            return new CharacterSkillData(intSkillId, strName, strAttribute, intRating.ToString(), strRatingDisplay,
                intPool.ToString(), strTooltip, strSpecialization, strCategory, blnIsGroupLocked, blnAllowDelete,
                blnKnowledge, strSkillGroup, blnExotic, blnIsMeta, strMetaBase);
        }

        /// <summary>Ported from clsUnique.cs's Skill.TotalRating (the dice pool) and
        /// Skill.RatingModifiers/DicePoolModifiers, which is where "skills have augmentations
        /// too" - Skillwire/Adept powers/gear can raise a skill's effective rating
        /// (RatingModifiers, added before the 1.5x-rating cap) separately from bonuses that only
        /// affect the pool without touching the displayed rating (DicePoolModifiers). Both are
        /// aggregated across Skill/SkillGroup/SkillCategory-targeted Improvements, same as the
        /// legacy version.
        ///
        /// Deliberately NOT ported (all narrow, all house-rule or edge-case paths): Skillsoft/
        /// Activesoft rating overrides, the Mystic Adept MAG-split, SwapSkillAttribute, Enhanced
        /// Articulation, and the metatype-talent MetaRatingModifier bonus.
        /// </summary>
        private (string RatingDisplay, int Pool, string Tooltip) ComputeSkillDicePool(string strName,
            string strSkillGroup, string strCategory, string strAttribute, int intRating, string strSpecialization,
            bool blnCanDefault, string strMetaBaseName = "", int intMetaRatingModifier = 0)
        {
            var objOptions = GetCharacterOptions();
            var lstRatingContributions = SkillImprovementContributions(strName, strSpecialization, strSkillGroup, strCategory, blnAddToRating: true, strMetaBaseName);
            var lstPoolContributions = SkillImprovementContributions(strName, strSpecialization, strSkillGroup, strCategory, blnAddToRating: false, strMetaBaseName);
            int intRatingMod = lstRatingContributions.Sum(c => c.Value);
            int intPoolMod = lstPoolContributions.Sum(c => c.Value);
            int intAttributeValue = GetAttributeInt(strAttribute);
            int intWound = WoundModifiers;
            int intAugmentedRating = intRating + intRatingMod;

            string strRatingDisplay = intRatingMod == 0
                ? intRating.ToString()
                : intRating + " (" + intAugmentedRating + ")";

            var sb = new StringBuilder();
            sb.Append("Fertigkeitswert: ").Append(intRating);
            int intPool;
            if (intRating == 0 && blnCanDefault)
            {
                // Ported from clsUnique.cs's Skill.TotalRating defaulting branch: a Rating-0 Skill
                // that allows defaulting rolls Attribute - 1, optionally including the Rating/Pool
                // Improvements if the house rule is on.
                intPool = intAttributeValue - 1;
                sb.Append(" (default)\n").Append("Attribut (").Append(strAttribute).Append(") - 1: ").Append(intPool);
                if (objOptions.SkillDefaultingIncludesModifiers)
                {
                    int intModSum = intRatingMod + intPoolMod;
                    intPool += intModSum;
                    AppendContributions(sb, lstRatingContributions);
                    AppendContributions(sb, lstPoolContributions);
                }

                if (objOptions.CapSkillRating)
                {
                    int intMax = Math.Max(20, (GetAttributeBaseInt(strAttribute) + intRating) * 2);
                    intPool = Math.Min(intMax, intPool);
                }

                intPool += intWound;
            }
            else
            {
                // House rule: the modified Rating (before DicePoolModifiers/Attribute) may not
                // exceed 1.5x the base Rating, rounded down.
                int intPoolRatingContribution = intAugmentedRating;
                if (objOptions.EnforceMaximumSkillRatingModifier)
                {
                    int intMaxModified = (int)Math.Floor(intRating * 1.5);
                    if (intPoolRatingContribution > intMaxModified)
                        intPoolRatingContribution = intMaxModified;
                }

                intPool = intRating == 0
                    ? 0
                    : intPoolRatingContribution + intPoolMod + intAttributeValue + intWound;

                // House rule: cap the total pool to the greater of 20 or 2x (natural, unaugmented
                // attribute + base Rating).
                if (objOptions.CapSkillRating)
                {
                    int intMax = Math.Max(20, (GetAttributeBaseInt(strAttribute) + intRating) * 2);
                    intPool = Math.Min(intMax, intPool);
                }

                AppendContributions(sb, lstRatingContributions);
                if (objOptions.EnforceMaximumSkillRatingModifier && intPoolRatingContribution != intAugmentedRating)
                    sb.Append('\n').Append("(Hausregel: max. 1,5x Fertigkeitswert -> ").Append(intPoolRatingContribution).Append(')');
                sb.Append('\n').Append("Attribut (").Append(strAttribute).Append("): ").Append(intAttributeValue);
                AppendContributions(sb, lstPoolContributions);
            }

            // Ported from clsUnique.cs's Skill.TotalRating: a meta skill's +2 bonus for matching
            // its base skill's current Specialization, added flat like the legacy return value.
            if (intMetaRatingModifier != 0)
            {
                intPool += intMetaRatingModifier;
                sb.Append('\n').Append("Meta-Fertigkeit (Spezialisierung stimmt überein): ").Append(FormatSigned(intMetaRatingModifier));
            }

            intPool = Math.Max(0, intPool);

            if (intWound != 0)
                sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
            if (!string.IsNullOrEmpty(strSpecialization))
                sb.Append('\n').Append("Spezialisierung \"").Append(strSpecialization).Append("\": +2 bei Anwendung");
            sb.Append('\n').Append("Würfelpool: ").Append(intPool);

            return (strRatingDisplay, intPool, sb.ToString());
        }

        private IReadOnlyList<(string SourceName, int Value)> SkillImprovementContributions(string strName,
            string strSpecialization, string strSkillGroup, string strCategory, bool blnAddToRating,
            string strMetaBaseName = "")
        {
            var lstContributions = new List<(string SourceName, int Value)>(
                ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Skill, strName, blnAddToRating));
            // Ported from clsUnique.cs's Skill pool calc, which always checks both the bare Name
            // and "Name (Specialization)" forms - the latter is how selectskill Improvements on
            // Exotic Skills (which all share the same bare Name, only distinguished by
            // Specialization - e.g. "Exotic Ranged Weapon (Bow)" vs. "...(Grenade Launcher)") get
            // stored, so a bonus picked for one specific Exotic Skill instance doesn't bleed onto
            // every other skill sharing its bare Name.
            if (!string.IsNullOrEmpty(strSpecialization))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Skill,
                    strName + " (" + strSpecialization + ")", blnAddToRating));
            if (!string.IsNullOrEmpty(strSkillGroup))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.SkillGroup, strSkillGroup, blnAddToRating));
            if (!string.IsNullOrEmpty(strCategory))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.SkillCategory, strCategory, blnAddToRating));
            // Ported from clsUnique.cs's Improvement-matching checks (e.g. RatingModifiers):
            // "objImprovement.ImprovedName == _strName || (_isMeta && objImprovement.ImprovedName == MetaBase)"
            // - a meta skill (e.g. "Perception (Visual)") also picks up bonuses targeted at its
            // base skill's bare name (e.g. cyberware boosting "Perception").
            if (!string.IsNullOrEmpty(strMetaBaseName))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Skill, strMetaBaseName, blnAddToRating));
            return lstContributions;
        }

        private XmlNode? GetKnowledgeSkillNode(int intSkillId)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null || intSkillId < 0 || intSkillId >= objNodes.Count)
                return null;

            XmlNode objNode = objNodes[intSkillId]!;
            return GetValue(objNode, "knowledge", "False") == "True" ? objNode : null;
        }

        private (int Pool, string Tooltip) ComputeSpellDicePool(string strCategory)
        {
            CharacterSkillData? objSpellcasting = Skills.FirstOrDefault(s => s.Name == "Spellcasting");
            int intSkillRating = objSpellcasting != null && int.TryParse(objSpellcasting.TotalValue, out var r) ? r : 0;
            bool blnSpecializationMatches = objSpellcasting != null && objSpellcasting.Specialization == strCategory;

            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.SpellCategory, strCategory);
            int intCategoryBonus = lstContributions.Sum(c => c.Value);
            int intTotal = intSkillRating + (blnSpecializationMatches ? 2 : 0) + intCategoryBonus;

            var sb = new StringBuilder();
            if (objSpellcasting != null)
            {
                sb.Append("Zaubern: ").Append(intSkillRating);
                if (blnSpecializationMatches)
                    sb.Append('\n').Append("Spezialisierung (").Append(strCategory).Append("): +2");
            }
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Würfelpool: ").Append(intTotal);
            return (intTotal, sb.ToString());
        }

    }
}
