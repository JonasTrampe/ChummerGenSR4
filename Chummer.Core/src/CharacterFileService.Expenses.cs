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
        public bool ConfirmDeleteEnabled => GetCharacterOptions().ConfirmDelete;

        /// <summary>Whether Career Karma-spending UI actions should ask for confirmation,
        /// matching the per-character settings profile's ConfirmKarmaExpense option.</summary>
        public string Karma
        {
            get => GetValue("/character/karma", "0");
            set => SetRootValue("karma", value);
        }

        /// <summary>False during character creation, true once the character has entered career mode.</summary>
        public bool FinalizeCreation()
        {
            if (Created)
                return false;

            SetRootValue("created", "True");
            return true;
        }

        /// <summary>"Karma" or "BP" - which build method this character was created with.</summary>
        public string BuildMethod => GetValue("/character/buildmethod", "Karma");

        /// <summary>Whether the AR 44 armor-degradation controls are enabled for this character.</summary>
        public bool ConfigureCreationBudget(string strBuildMethod, int intBuildPoints, int intMaximumAvailability,
            bool blnIgnoreRules)
        {
            if (Created || intBuildPoints < 0 || intMaximumAvailability < 0
                || (strBuildMethod != "BP" && strBuildMethod != "Karma"))
                return false;
            bool blnKarma = strBuildMethod == "Karma";
            SetRootValue("buildmethod", strBuildMethod);
            SetRootValue("bp", blnKarma ? "0" : intBuildPoints.ToString(CultureInfo.InvariantCulture));
            SetRootValue("buildkarma", blnKarma ? intBuildPoints.ToString(CultureInfo.InvariantCulture) : "0");
            SetRootValue("karma", blnKarma ? intBuildPoints.ToString(CultureInfo.InvariantCulture) : "0");
            SetRootValue("startingbuildpoints", intBuildPoints.ToString(CultureInfo.InvariantCulture));
            SetRootValue("nuyenmaxbp", blnKarma ? "100" : "50");
            SetRootValue("maxavail", intMaximumAvailability.ToString(CultureInfo.InvariantCulture));
            SetRootValue("ignorerules", blnIgnoreRules ? "True" : "False");
            Changed?.Invoke();
            return true;
        }

        /// <summary>The starting BP/Karma total the character was created with (unlike <see
        /// cref="Bp"/>/<see cref="Karma"/>, which shrink as points are spent) - only set for
        /// characters created through <see cref="NewCharacterFactory"/>; "0" for older save files.
        /// Used by <see cref="RaiseAttributeCreate"/>'s AllowExceedAttributeBp gate.</summary>
        public CharacterCreationBudgetData CreationBudget
        {
            get
            {
                bool blnKarma = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                int intRemaining = int.TryParse(blnKarma ? Karma : Bp, out int intRemainingValue) ? intRemainingValue : 0;
                int intStarting = StartingBuildPoints;
                int intSpent = intStarting - intRemaining;
                var lstCategories = new List<CharacterCreationBudgetCategoryData>();
                CharacterOptions objOptions = GetCharacterOptions();

                void AddCategory(string strName, int intCost)
                {
                    if (intCost != 0)
                        lstCategories.Add(new CharacterCreationBudgetCategoryData(strName, intCost));
                }

                int intMetatypeBp = ParseInteger(GetValue("/character/metatypebp", "0"));
                AddCategory("Metatype", blnKarma
                    ? (objOptions.MetatypeCostsKarma ? intMetatypeBp * objOptions.MetatypeCostsKarmaMultiplier : 0)
                    : intMetatypeBp);

                int intPrimaryAttributes = 0;
                int intSpecialAttributes = 0;
                foreach (XmlNode objAttribute in Document.SelectNodes("/character/attributes/attribute")?.Cast<XmlNode>()
                    ?? Enumerable.Empty<XmlNode>())
                {
                    string strCode = GetValue(objAttribute, "name", string.Empty);
                    if (strCode == "ESS")
                        continue;
                    int intValue = ParseInteger(GetValue(objAttribute, "value", "0"));
                    int intMinimum = ParseInteger(GetValue(objAttribute, "metatypemin", "0"));
                    int intMaximum = ParseInteger(GetValue(objAttribute, "metatypemax", "0"));
                    if (intValue == 0 && intMaximum == 0)
                        continue;
                    int intCost;
                    if (blnKarma)
                    {
                        int intStart = objOptions.AlternateMetatypeAttributeKarma ? 1 : intMinimum + 1;
                        intCost = 0;
                        for (int i = intStart; i <= intValue; i++)
                            intCost += i * objOptions.KarmaAttribute;
                    }
                    else
                    {
                        intCost = Math.Max(0, intValue - intMinimum) * 10;
                        if (intValue == intMaximum && intMaximum > intMinimum)
                            intCost += 15;
                    }
                    if (strCode == "EDG" || strCode == "MAG" || strCode == "RES") intSpecialAttributes += intCost;
                    else intPrimaryAttributes += intCost;
                }
                AddCategory("Primary attributes", intPrimaryAttributes);
                AddCategory("Special attributes", intSpecialAttributes);

                // This includes the dynamic FreeContacts allowances and enemy refunds, which
                // are applied to the aggregate pool rather than a stable individual contact.
                AddCategory("Contacts", ContactPointsUsed);

                XmlDocument objQualities = XmlManager.Instance.Load("qualities.xml");
                int intQualities = 0;
                foreach (XmlNode objQuality in Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                    ?? Enumerable.Empty<XmlNode>())
                {
                    string strName = GetValue(objQuality, "name", string.Empty);
                    XmlNode? objRule = objQualities.SelectSingleNode($"/chummer/qualities/quality[name = '{strName}']");
                    int intBp = ParseInteger(GetValue(objRule, "bp", "0"));
                    intQualities += blnKarma ? intBp * objOptions.KarmaQuality : intBp;
                }
                AddCategory("Qualities", intQualities);

                AddCategory("Skill groups", GetCreationSkillGroupCost());
                AddCategory("Active skills", GetCreationActiveSkillCost());
                AddCategory("Knowledge skills", GetCreationKnowledgeSkillCost());

                AddCategory("Spells", (Document.SelectNodes("/character/spells/spell")?.Count ?? 0)
                    * (blnKarma ? objOptions.KarmaSpell : 3));
                int intComplexForms = ComplexForms.Sum(f => blnKarma
                    ? ComputeComplexFormKarmaCost(f.Category, ParseInteger(f.Rating))
                    : (objOptions.AlternateComplexFormCost ? 3 : ParseInteger(f.Rating)));
                AddCategory("Complex Forms", intComplexForms);
                int intMartialArts = MartialArts.Sum(a => ParseInteger(a.Rating)
                    * (blnKarma ? 5 * objOptions.KarmaQuality : objOptions.BpMartialArt));
                AddCategory("Martial arts", intMartialArts);
                AddCategory("Martial art maneuvers", MartialArtManeuvers.Count
                    * (blnKarma ? objOptions.KarmaManeuver : objOptions.BpMartialArtManeuver));
                int intSpiritCost = (Document.SelectNodes("/character/spirits/spirit")?.Cast<XmlNode>()
                    ?? Enumerable.Empty<XmlNode>()).Sum(s => ParseInteger(GetValue(s, "services", "0"))
                    * (blnKarma ? objOptions.KarmaSpirit : objOptions.BpSpirit));
                AddCategory("Spirits and sprites", intSpiritCost);
                AddCategory("Starting Nuyen", NuyenPoints);

                int intCategorized = lstCategories.Sum(c => c.Cost);
                int intUncategorized = intSpent - intCategorized;
                if (intUncategorized != 0)
                    lstCategories.Add(new CharacterCreationBudgetCategoryData("Other / not yet categorized", intUncategorized));
                return new CharacterCreationBudgetData(BuildMethod, intStarting, intRemaining, intSpent, lstCategories);
            }
        }

        private static int ComputeCreationRatingCost(int intRating, bool blnKarma, int intBpPerRating,
            int intKarmaNew, int intKarmaImprove)
        {
            if (intRating <= 0) return 0;
            if (!blnKarma) return intRating * intBpPerRating;
            int intCost = intKarmaNew;
            for (int i = 2; i <= intRating; i++) intCost += i * intKarmaImprove;
            return intCost;
        }

        public string Nuyen
        {
            get => GetValue("/character/nuyen", "0");
            set => SetRootValue("nuyen", value);
        }

        /// <summary>Number of points sunk into starting Nuyen during creation (each costs 1 Karma
        /// or 1 BP, depending on <see cref="BuildMethod"/>, and grants a build-specific Nuyen payout).</summary>
        public int NuyenPerPoint
        {
            get
            {
                var objOptions = GetCharacterOptions();
                return string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase)
                    ? objOptions.KarmaNuyenPer
                    : objOptions.NuyenPerBp;
            }
        }

        /// <summary>Create mode: spends one point (1 Karma or 1 BP) on starting Nuyen.</summary>
        public bool RaiseNuyenCreate()
        {
            if (NuyenPoints >= NuyenPointsMax)
                return false;

            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            if (blnKarmaBuild)
            {
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intKarma < 1)
                    return false;
                Karma = (intKarma - 1).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intBp < 1)
                    return false;
                Bp = (intBp - 1).ToString();
            }

            SetRootValue("nuyenbp", (NuyenPoints + 1).ToString());
            int intNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
            Nuyen = (intNuyen + NuyenPerPoint).ToString();
            return true;
        }

        /// <summary>Create mode: refunds one point (1 Karma or 1 BP) previously spent on starting Nuyen.</summary>
        public bool LowerNuyenCreate()
        {
            if (NuyenPoints <= 0)
                return false;

            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            if (blnKarmaBuild)
            {
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + 1).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + 1).ToString();
            }

            SetRootValue("nuyenbp", (NuyenPoints - 1).ToString());
            int intNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
            Nuyen = (intNuyen - NuyenPerPoint).ToString();
            return true;
        }

        /// <summary>Calculated walking/running movement, including MovementPercent Improvements.</summary>
        public int CareerKarma => SumEarnedExpenses(KarmaExpenses);

        /// <summary>Total Nuyen earned over the character's career (sum of positive, non-refund
        /// Nuyen expense entries), ported from clsCharacter.cs's CareerNuyen.</summary>
        public int CareerNuyen => SumEarnedExpenses(NuyenExpenses);

        private static int SumEarnedExpenses(IReadOnlyList<CharacterExpenseData> lstExpenses)
        {
            int intTotal = 0;
            foreach (CharacterExpenseData expense in lstExpenses)
            {
                if (!expense.Refund && int.TryParse(expense.Amount, out var intAmount) && intAmount > 0)
                    intTotal += intAmount;
            }

            return intTotal;
        }

        private void ApplySellRefund(int intRefund, string strItemName)
        {
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
            Nuyen = (dblNuyen + intRefund).ToString(CultureInfo.InvariantCulture);
            if (intRefund != 0)
                AddExpense("Nuyen", intRefund, "Verkauft: " + strItemName);
        }

        /// <summary>Equips or unequips a gear item - matches the legacy tree's "angelegt" checkbox.</summary>
        public bool PrintExpenses => GetCharacterOptions().PrintExpenses;
        private void RefundCreationCost(XmlNode objNode)
        {
            if (Created || StartingBuildPoints <= 0)
                return;
            int intRefund = ParseInteger(GetValue(objNode, "creationcost", "0"));
            if (intRefund == 0)
                return;
            if (string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase))
                Karma = (ParseInteger(Karma) + intRefund).ToString(CultureInfo.InvariantCulture);
            else
                Bp = (ParseInteger(Bp) + intRefund).ToString(CultureInfo.InvariantCulture);
        }

        public IReadOnlyList<CharacterExpenseData> KarmaExpenses => ReadExpenses(strType: "Karma");

        public IReadOnlyList<CharacterExpenseData> NuyenExpenses => ReadExpenses(strType: "Nuyen");

        /// <summary>
        /// Appends a Karma or Nuyen history entry in the same XML shape as the legacy career mode.
        /// Positive amounts are earnings; negative amounts are expenditures. The caller supplies
        /// the signed amount so refunds can be represented without a second write API.
        /// </summary>
        public void AddExpense(string strType, decimal decAmount, string strReason, DateTime? datDate = null,
            ExpenseUndo objUndo = null)
        {
            if (strType != "Karma" && strType != "Nuyen")
                throw new ArgumentException("An expense must be Karma or Nuyen.", nameof(strType));
            if (decAmount == 0)
                throw new ArgumentOutOfRangeException(nameof(decAmount), "An expense amount cannot be zero.");
            if (string.IsNullOrWhiteSpace(strReason))
                throw new ArgumentException("An expense reason is required.", nameof(strReason));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objExpenses = objRoot.SelectSingleNode("expenses");
            if (objExpenses == null)
            {
                objExpenses = Document.CreateElement("expenses");
                objRoot.AppendChild(objExpenses);
            }

            var objExpense = Document.CreateElement("expense");
            AppendElement(objExpense, "guid", Guid.NewGuid().ToString());
            AppendElement(objExpense, "date", (datDate ?? DateTime.Now).ToString("O"));
            AppendElement(objExpense, "amount", decAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendElement(objExpense, "reason", strReason.Trim());
            AppendElement(objExpense, "type", strType);
            AppendElement(objExpense, "refund", "False");
            objExpenses.AppendChild(objExpense);

            if (objUndo != null)
            {
                var objUndoElement = Document.CreateElement("undo");
                AppendElement(objUndoElement, "karmatype", objUndo.KarmaType.ToString());
                AppendElement(objUndoElement, "nuyentype", objUndo.NuyenType.ToString());
                AppendElement(objUndoElement, "objectid", objUndo.ObjectId);
                AppendElement(objUndoElement, "qty", objUndo.Qty.ToString());
                AppendElement(objUndoElement, "extra", objUndo.Extra);
                objExpense.AppendChild(objUndoElement);
            }
        }

        /// <summary>Ported from frmCareer.cs's Karma/Nuyen expense edit dialog: Reason and Date are
        /// always editable; Amount is included here too (legacy locks it for non-manual entries via
        /// Undo.KarmaType/NuyenType, which this port's AddExpense never attaches, so every entry the
        /// Avalonia UI creates is effectively "manual" and editable).</summary>
        public bool UpdateExpense(string strGuid, string strReason, decimal decAmount, DateTime datDate)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            XmlNode? objExpense = FindExpenseNode(strGuid);
            if (objExpense == null)
                return false;

            SetChildValue(objExpense, "reason", strReason.Trim());
            SetChildValue(objExpense, "amount", decAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetChildValue(objExpense, "date", datDate.ToString("O"));
            Changed?.Invoke();
            return true;
        }

        public bool RemoveExpense(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            XmlNode? objExpense = FindExpenseNode(strGuid);
            if (objExpense == null)
                return false;

            objExpense.ParentNode?.RemoveChild(objExpense);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? FindExpenseNode(string strGuid)
        {
            var objNodes = Document.SelectNodes("/character/expenses/expense");
            if (objNodes == null) return null;
            foreach (XmlNode objExpense in objNodes)
            {
                if (string.Equals(GetValue(objExpense, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    return objExpense;
            }

            return null;
        }

        private IReadOnlyList<CharacterExpenseData> ReadExpenses(string strType)
        {
            var lstExpenses = new List<CharacterExpenseData>();
            var objNodes = Document.SelectNodes("/character/expenses/expense");
            if (objNodes == null) return lstExpenses;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "type", string.Empty) != strType) continue;
                lstExpenses.Add(new CharacterExpenseData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "date", string.Empty),
                    GetValue(objNode, "amount", "0"), GetValue(objNode, "reason", string.Empty),
                    GetValue(objNode, "refund", "False") == "True"));
            }

            return lstExpenses;
        }

    }
}
