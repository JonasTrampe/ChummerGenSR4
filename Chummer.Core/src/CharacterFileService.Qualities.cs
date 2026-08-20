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
        private void RemoveMetatypeQualities()
        {
            foreach (XmlNode objQuality in Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>().ToList()
                     ?? new List<XmlNode>())
            {
                if (!Enum.TryParse(GetValue(objQuality, "qualitysource", "Selected"), true,
                        out QualitySource eSource) || (eSource != QualitySource.Metatype && eSource != QualitySource.MetatypeRemovable))
                    continue;
                RemoveBonusImprovements(ImprovementSource.Quality, GetValue(objQuality, "name", string.Empty));
                objQuality.ParentNode?.RemoveChild(objQuality);
            }
        }

        private void AddMetatypeQualities(XmlNode objRule)
        {
            foreach (string strType in new[] { "Positive", "Negative" })
            foreach (XmlNode objQuality in objRule.SelectNodes("qualities/" + strType.ToLowerInvariant() + "/quality")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
                AddQuality(objQuality.InnerText, strType, objQuality.Attributes?["select"]?.InnerText ?? string.Empty,
                    eQualitySource: objQuality.Attributes?["removable"]?.InnerText == "true"
                        ? QualitySource.MetatypeRemovable : QualitySource.Metatype, blnChargeCreation: false);
        }

        private bool HasSelectedQualityDependingOn(string strMetatype, string strMetavariant)
        {
            XmlDocument objQualities = XmlManager.Instance.Load("qualities.xml");
            foreach (XmlNode objQuality in Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                if (Enum.TryParse(GetValue(objQuality, "qualitysource", "Selected"), true,
                        out QualitySource eSource) && eSource != QualitySource.Selected)
                    continue;
                string strName = GetValue(objQuality, "name", string.Empty);
                XmlNode? objRule = objQualities.SelectSingleNode("/chummer/qualities/quality[name = '" + strName + "']");
                if (objRule?.SelectSingleNode("required/oneof/metatype[. = '" + strMetatype + "']") != null
                    || objRule?.SelectSingleNode("required/allof/metatype[. = '" + strMetatype + "']") != null
                    || (!string.IsNullOrEmpty(strMetavariant) && (objRule?.SelectSingleNode("required/oneof/metavariant[. = '" + strMetavariant + "']") != null
                        || objRule?.SelectSingleNode("required/allof/metavariant[. = '" + strMetavariant + "']") != null)))
                    return true;
            }
            return false;
        }

        /// <summary>Response rating of the character's equipped, active Commlink (0 if none),
        /// ported from clsCommonFunctions.FindCommlinks + Commlink.TotalResponse as used by
        /// MatrixInitiative. Searches every &lt;gear&gt; node anywhere in the document (so this
        /// does find Commlinks nested under Armor/Cyberware, same as the legacy scan), but doesn't
        /// separately check Vehicles' own onboard Commlinks the way FindCommlinks does. Uses the
        /// raw &lt;response&gt; value rather than TotalResponse (gear-mod bonuses to Response
        /// aren't modeled).</summary>
        public IReadOnlyList<CharacterQualityData> Qualities => ReadQualities();

        /// <summary>
        /// Adds a quality using the character-file representation used by the legacy application.
        /// The rules definition stays in <c>qualities.xml</c>; a character save only records the
        /// chosen name, optional selection detail, and positive/negative category. Because this
        /// mutates the backing document, <see cref="CharacterFileService.Save"/> persists it.
        /// </summary>
        /// <summary>Whether adding this Quality prompts for a free-text detail - ported from
        /// clsImprovement.cs's selecttext bonus node (e.g. Allergy's substance, Prejudiced's
        /// target). The UI should collect this from the player and pass it as <see
        /// cref="AddQuality"/>'s <paramref name="strName"/>-matching <c>strExtra</c>.</summary>
        public bool QualityRequiresTextSelection(string strName) =>
            FindBonusChild("qualities.xml", "qualities", "quality", strName, "selecttext") != null;

        /// <summary>Whether adding this Quality prompts for a Mentor Spirit (or Paragon, for
        /// Technomancers) pick - ported from clsImprovement.cs's selectmentorspirit/selectparagon
        /// bonus nodes (Quality "Mentor Spirit"/"The Beast's Way"). Returns the rules-data file to
        /// pick from ("mentors.xml"/"paragons.xml"), or null if this Quality doesn't need one.</summary>
        public string? QualityMentorSpiritDataFile(string strName)
        {
            XmlNode? objBonus = FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectmentorspirit");
            if (objBonus != null)
                return "mentors.xml";
            objBonus = FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectparagon");
            return objBonus != null ? "paragons.xml" : null;
        }

        /// <summary>Same as <see cref="QualityRequiresTextSelection"/> but for a Critter Power's
        /// &lt;selecttext&gt; bonus (e.g. Elemental Attack).</summary>
        public bool CritterPowerRequiresTextSelection(string strName) =>
            FindBonusChild("critterpowers.xml", "powers", "power", strName, "selecttext") != null;

        /// <summary>Same as <see cref="QualityRequiresTextSelection"/> but for a Complex Form's
        /// &lt;selecttext&gt; bonus (e.g. Knowsoft, Linguasoft).</summary>
        public IReadOnlyList<string> GetQualityAttributeSelectionOptions(string strName) =>
            ExtractAttributeSelectionOptions(FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectattribute"));

        /// <summary>Same as <see cref="GetQualityAttributeSelectionOptions"/> but for an Adept
        /// Power's &lt;selectattribute&gt; bonus (e.g. Improved Physical Attribute).</summary>
        public bool AddQuality(string strName, string strType, string strExtra = "",
            string strMentorSpirit = "", string strMentorChoice1 = "", string strMentorChoice2 = "",
            QualitySource eQualitySource = QualitySource.Selected, bool blnChargeCreation = true)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A quality name is required.", nameof(strName));
            if (strType != "Positive" && strType != "Negative")
                throw new ArgumentException("A quality must be Positive or Negative.", nameof(strType));

            XmlDocument objQualitiesDoc = XmlManager.Instance.Load("qualities.xml");
            XmlNode? objXmlQuality = objQualitiesDoc.SelectSingleNode(
                $"/chummer/qualities/quality[name = '{strName.Trim()}']");
            bool blnEnforceCreationBudget = blnChargeCreation && !Created && StartingBuildPoints > 0;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intCreationCost = GetQualityCreationCost(objXmlQuality, blnKarmaBuild);
            int intPool = int.TryParse(blnKarmaBuild ? Karma : Bp, out int intParsedPool) ? intParsedPool : 0;
            if (blnEnforceCreationBudget && intCreationCost > intPool)
                return false;
            if (blnEnforceCreationBudget && !IgnoreRules
                && ExceedsQualityLimit(objXmlQuality, strType, intCreationCost, blnKarmaBuild))
                return false;

            // Ported from frmCareer.cs's cmdAddQuality_Click: after character creation, buying a
            // Positive Quality costs BP*KarmaQuality Karma (the same formula GetQualityCreationCost
            // uses for a Karma-mode creation purchase); Negative Qualities are free to add in
            // career mode too (legacy still confirms via a separate Message_AddNegativeQuality
            // Yes/No, but grants no Karma back - only its UI-side confirmation is out of scope
            // here). blnChargeCreation doubles as "should this add be charged at all" so a
            // free/bundled grant (addqualities, mentor spirits) skips the career charge too.
            bool blnCareerPurchase = blnChargeCreation && Created;
            int intCareerKarmaCost = 0;
            if (blnCareerPurchase && strType == "Positive")
            {
                intCareerKarmaCost = GetQualityCreationCost(objXmlQuality, blnKarmaBuild: true);
                if (intCareerKarmaCost > ParseInteger(Karma))
                    return false;
            }

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objQualities = objRoot.SelectSingleNode("qualities");
            if (objQualities == null)
            {
                objQualities = Document.CreateElement("qualities");
                objRoot.AppendChild(objQualities);
            }

            var objQuality = Document.CreateElement("quality");
            AppendElement(objQuality, "name", strName.Trim());
            AppendElement(objQuality, "extra", strExtra.Trim());
            AppendElement(objQuality, "qualitytype", strType);
            // This is the legacy save field (Quality.Save writes qualitysource). Existing saves
            // that predate it are read as Selected below, preserving their historical behavior.
            AppendElement(objQuality, "qualitysource", eQualitySource.ToString());
            if (!string.IsNullOrWhiteSpace(strMentorSpirit))
            {
                AppendElement(objQuality, "mentorspirit", strMentorSpirit.Trim());
                AppendElement(objQuality, "mentorchoice1", strMentorChoice1.Trim());
                AppendElement(objQuality, "mentorchoice2", strMentorChoice2.Trim());
            }
            if (blnEnforceCreationBudget)
                AppendElement(objQuality, "creationcost", intCreationCost.ToString(CultureInfo.InvariantCulture));
            objQualities.AppendChild(objQuality);

            XmlNode? objXmlBonus = objXmlQuality?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.Quality, strName.Trim());
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.Quality, strName.Trim(), strExtra, "1");

            // Ported from frmCreate.cs's CalculateNuyen: a <nuyenamt> bonus (e.g. In Debt) is a
            // one-time credit added to the character's starting Nuyen pool at creation time only.
            if (blnEnforceCreationBudget)
            {
                string strNuyenAmt = objXmlBonus?["nuyenamt"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strNuyenAmt))
                {
                    int intNuyenBonus = (int)RatingExpression.Evaluate(strNuyenAmt, "1");
                    if (intNuyenBonus != 0)
                    {
                        int intCurrentNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
                        Nuyen = (intCurrentNuyen + intNuyenBonus).ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            string? strMentorDataFile = QualityMentorSpiritDataFile(strName);
            if (strMentorDataFile != null && !string.IsNullOrWhiteSpace(strMentorSpirit))
            {
                XmlDocument objMentorsDoc = XmlManager.Instance.Load(strMentorDataFile);
                XmlNode? objXmlMentor = objMentorsDoc.SelectSingleNode(
                    $"/chummer/mentors/mentor[name = '{strMentorSpirit.Trim()}']");
                ApplyBonus(objXmlMentor?.SelectSingleNode("bonus"), ImprovementSource.Quality, strName.Trim());

                if (!string.IsNullOrWhiteSpace(strMentorChoice1))
                    ApplyBonus(objXmlMentor?.SelectSingleNode(
                        $"choices/choice[name = '{strMentorChoice1.Trim()}']/bonus"),
                        ImprovementSource.Quality, strName.Trim());
                if (!string.IsNullOrWhiteSpace(strMentorChoice2))
                    ApplyBonus(objXmlMentor?.SelectSingleNode(
                        $"choices/choice[name = '{strMentorChoice2.Trim()}']/bonus"),
                        ImprovementSource.Quality, strName.Trim());
            }

            // Ported from frmCreate.cs's addqualities/addquality handling: some Qualities force
            // another Quality onto the character as a bundled side effect (e.g. Infected qualities
            // granting Distinctive Style) - granted for free, not charged separately, and skipped
            // if the character already has a matching Quality+Extra.
            XmlNodeList? objAddQualityNodes = objXmlQuality?.SelectNodes("addqualities/addquality");
            if (objAddQualityNodes != null)
                foreach (XmlNode objXmlAddQuality in objAddQualityNodes)
                {
                    string strAddQualityName = objXmlAddQuality.InnerText;
                    string strAddQualityExtra = objXmlAddQuality.Attributes?["select"]?.InnerText ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(strAddQualityName))
                        continue;

                    bool blnAlreadyHasQuality = Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                        .Any(q => GetValue(q, "name", string.Empty) == strAddQualityName
                            && GetValue(q, "extra", string.Empty) == strAddQualityExtra) ?? false;
                    if (blnAlreadyHasQuality)
                        continue;

                    XmlNode? objXmlAddedQuality = objQualitiesDoc.SelectSingleNode(
                        $"/chummer/qualities/quality[name = '{strAddQualityName}']");
                    string strAddedQualityType = GetValue(objXmlAddedQuality, "category", "Positive");
                    AddQuality(strAddQualityName, strAddedQualityType, strAddQualityExtra,
                        eQualitySource: QualitySource.Selected, blnChargeCreation: false);
                }

            if (blnEnforceCreationBudget)
            {
                if (blnKarmaBuild)
                    Karma = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
                else
                    Bp = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
            }
            else if (intCareerKarmaCost > 0)
            {
                Karma = (ParseInteger(Karma) - intCareerKarmaCost).ToString(CultureInfo.InvariantCulture);
                var objUndo = new ExpenseUndo();
                objUndo.CreateKarma(KarmaExpenseType.AddQuality, strName.Trim());
                AddExpense("Karma", -intCareerKarmaCost, "Quality hinzugefügt: " + strName.Trim(), null, objUndo);
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Shared selecttext/selectskill/selectattribute application, ported from
        /// clsImprovement.cs's respective handlers - used by both <see cref="AddQuality"/> and
        /// <see cref="AddAdeptPower"/>. <paramref name="strRating"/> resolves any "Rating"
        /// reference in the selected node's own val/max/aug formulas (e.g. Improved Ability's
        /// "Rating"-scaled skill bonus) - Qualities always pass "1" since they have no Rating.</summary>
        public bool RemoveQuality(string strName, string strType, string strExtra = "")
        {
            var objNodes = Document.SelectNodes("/character/qualities/quality");
            if (objNodes == null)
                return false;

            foreach (XmlNode objQuality in objNodes)
            {
                if (GetValue(objQuality, "name", string.Empty) != strName
                    || GetValue(objQuality, "qualitytype", string.Empty) != strType
                    || GetValue(objQuality, "extra", string.Empty) != strExtra)
                    continue;

                int intCreationRefund = ParseInteger(GetValue(objQuality, "creationcost", "0"));
                if (!Created && StartingBuildPoints > 0 && intCreationRefund != 0)
                {
                    bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                    int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
                    if (intPool + intCreationRefund < 0)
                        return false;
                    if (blnKarmaBuild)
                        Karma = (intPool + intCreationRefund).ToString(CultureInfo.InvariantCulture);
                    else
                        Bp = (intPool + intCreationRefund).ToString(CultureInfo.InvariantCulture);
                }

                // Ported from frmCareer.cs's cmdDeleteQuality_Click: after character creation,
                // "buying off" a Negative Quality costs BP*KarmaQuality Karma - removing a Positive
                // Quality, by contrast, is free (no refund), matching legacy's asymmetry.
                int intCareerBuyOffCost = 0;
                if (Created && strType == "Negative")
                {
                    XmlNode? objXmlNegativeQuality = XmlManager.Instance.Load("qualities.xml")
                        .SelectSingleNode($"/chummer/qualities/quality[name = '{strName.Trim()}']");
                    intCareerBuyOffCost = Math.Abs(GetQualityCreationCost(objXmlNegativeQuality, blnKarmaBuild: true));
                    if (intCareerBuyOffCost > ParseInteger(Karma))
                        return false;
                }

                if (!Created && StartingBuildPoints > 0)
                {
                    XmlNode? objXmlQuality = XmlManager.Instance.Load("qualities.xml")
                        .SelectSingleNode($"/chummer/qualities/quality[name = '{strName.Trim()}']");
                    string strNuyenAmt = objXmlQuality?.SelectSingleNode("bonus/nuyenamt")?.InnerText ?? string.Empty;
                    if (!string.IsNullOrEmpty(strNuyenAmt))
                    {
                        int intNuyenBonus = (int)RatingExpression.Evaluate(strNuyenAmt, "1");
                        if (intNuyenBonus != 0)
                        {
                            int intCurrentNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
                            Nuyen = (intCurrentNuyen - intNuyenBonus).ToString(CultureInfo.InvariantCulture);
                        }
                    }
                }
                objQuality.ParentNode?.RemoveChild(objQuality);
                RemoveBonusImprovements(ImprovementSource.Quality, strName);
                if (intCareerBuyOffCost > 0)
                {
                    Karma = (ParseInteger(Karma) - intCareerBuyOffCost).ToString(CultureInfo.InvariantCulture);
                    var objUndo = new ExpenseUndo();
                    objUndo.CreateKarma(KarmaExpenseType.RemoveQuality, strName.Trim());
                    AddExpense("Karma", -intCareerBuyOffCost, "Quality entfernt: " + strName.Trim(), null, objUndo);
                }
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Replaces one saved quality without temporarily requiring both qualities'
        /// creation costs. This keeps an equal-cost creation swap possible when no free pool
        /// remains, while still refusing a more expensive replacement that would overdraw it.</summary>
        public bool ReplaceQuality(string strOldName, string strOldType, string strOldExtra,
            string strNewName, string strNewType, string strNewExtra = "", string strMentorSpirit = "",
            string strMentorChoice1 = "", string strMentorChoice2 = "")
        {
            XmlNode? objOldQuality = Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                .FirstOrDefault(q => GetValue(q, "name", string.Empty) == strOldName
                    && GetValue(q, "qualitytype", string.Empty) == strOldType
                    && GetValue(q, "extra", string.Empty) == strOldExtra);
            if (objOldQuality == null)
                return false;

            if (!Created && StartingBuildPoints > 0)
            {
                bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
                int intOldCreationCost = ParseInteger(GetValue(objOldQuality, "creationcost", "0"));
                XmlNode? objNewQuality = XmlManager.Instance.Load("qualities.xml").SelectSingleNode(
                    $"/chummer/qualities/quality[name = '{strNewName.Trim()}']");
                int intNewCreationCost = GetQualityCreationCost(objNewQuality, blnKarmaBuild);
                if (intPool + intOldCreationCost - intNewCreationCost < 0)
                    return false;
            }

            return RemoveQuality(strOldName, strOldType, strOldExtra)
                && AddQuality(strNewName, strNewType, strNewExtra, strMentorSpirit, strMentorChoice1,
                    strMentorChoice2);
        }

        private int GetQualityCreationCost(XmlNode? objXmlQuality, bool blnKarmaBuild)
        {
            int intBp = ParseInteger(objXmlQuality?.SelectSingleNode("bp")?.InnerText ?? "0");
            return intBp * (blnKarmaBuild ? GetCharacterOptions().KarmaQuality : 1);
        }

        /// <summary>The Karma cost a career-mode AddQuality (Positive) or RemoveQuality (Negative
        /// buy-off) call for this Quality would charge - exposed so a host can preview the amount
        /// in a KarmaExpenseConfirmation prompt before committing, matching frmCareer.cs's
        /// pre-computed intKarmaCost shown in its own confirmation messages.</summary>
        public int GetQualityCareerKarmaCost(string strName)
        {
            XmlNode? objXmlQuality = XmlManager.Instance.Load("qualities.xml")
                .SelectSingleNode($"/chummer/qualities/quality[name = '{strName.Trim()}']");
            return Math.Abs(GetQualityCreationCost(objXmlQuality, blnKarmaBuild: true));
        }

        private bool ExceedsQualityLimit(XmlNode? objNewQuality, string strType, int intNewCost, bool blnKarmaBuild)
        {
            if (string.Equals(GetValue(objNewQuality, "contributetolimit", "yes"), "no",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            CharacterOptions objOptions = GetCharacterOptions();
            if (strType == "Positive" && objOptions.ExceedPositiveQualities)
                return false;
            if (strType == "Negative" && objOptions.ExceedNegativeQualities)
                return false;

            XmlDocument objQualities = XmlManager.Instance.Load("qualities.xml");
            int intExistingCost = (Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                ?? Enumerable.Empty<XmlNode>())
                .Where(q => GetValue(q, "qualitytype", string.Empty) == strType)
                .Sum(q =>
                {
                    XmlNode? objRule = objQualities.SelectSingleNode(
                        $"/chummer/qualities/quality[name = '{GetValue(q, "name", string.Empty)}']");
                    return string.Equals(GetValue(objRule, "contributetolimit", "yes"), "no",
                        StringComparison.OrdinalIgnoreCase) ? 0 : GetQualityCreationCost(objRule, blnKarmaBuild);
                });
            if (strType == "Positive")
            {
                int intFree = ImprovementManager.ValueOf(Improvements, ImprovementType.FreePositiveQualities);
                if (blnKarmaBuild) intFree *= objOptions.KarmaQuality;
                int intMartialArts = MartialArts.Sum(a => ParseInteger(a.Rating)
                    * (blnKarmaBuild ? 5 * objOptions.KarmaQuality : objOptions.BpMartialArt));
                return intExistingCost - intFree + intMartialArts + intNewCost > (blnKarmaBuild ? 70 : 35);
            }

            int intFreeNegative = ImprovementManager.ValueOf(Improvements, ImprovementType.FreeNegativeQualities);
            if (blnKarmaBuild) intFreeNegative *= objOptions.KarmaQuality;
            int intEnemyCost = Enemies.Where(c => !c.Free).Sum(c =>
                (ParseInteger(c.Connection) + c.GroupRating + ParseInteger(c.Loyalty))
                * (blnKarmaBuild ? objOptions.KarmaContact : objOptions.BpContact));
            return intExistingCost - intEnemyCost - intFreeNegative + intNewCost < (blnKarmaBuild ? -70 : -35);
        }

        /// <summary>Updates a Quality's free-form notes. The root-list index is recomputed when
        /// the character is read, which keeps older files without a quality GUID editable and
        /// distinguishes repeated name/type/detail combinations.</summary>
        public bool SetQualityNotes(int intQualityId, string strNotes)
        {
            XmlNode? objQuality = GetQualityNodeById(intQualityId);
            if (objQuality == null)
                return false;

            SetChildValue(objQuality, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetQualityNodeById(int intQualityId)
        {
            if (intQualityId < 0)
                return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/qualities/quality");
            return objNodes != null && intQualityId < objNodes.Count ? objNodes[intQualityId] : null;
        }

        /// <summary>
        /// Adds a spell using the saved-character fields consumed by <see cref="Spells"/>.
        /// Rules metadata is selected from spells.xml by the UI and copied here so the character
        /// remains self-contained when saved and reopened.
        /// </summary>
        public int ReapplyKnownRuleImprovements()
        {
            int intRefreshed = 0;

            XmlDocument objQualities = XmlManager.Instance.Load("qualities.xml");
            foreach (XmlNode objItem in Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = GetValue(objItem, "name", string.Empty);
                XmlNode? objRule = FindRuleItemByName(objQualities, "/chummer/qualities/quality", strName);
                if (objRule == null)
                    continue;

                RemoveBonusImprovements(ImprovementSource.Quality, strName);
                XmlNode? objBonus = objRule.SelectSingleNode("bonus");
                ApplyBonus(objBonus, ImprovementSource.Quality, strName);
                ApplySelectedImprovement(objBonus, ImprovementSource.Quality, strName,
                    GetValue(objItem, "extra", string.Empty), "1");
                ReapplyMentorSpiritBonuses(objItem, strName);
                intRefreshed++;
            }

            XmlDocument objPowers = XmlManager.Instance.Load("powers.xml");
            foreach (XmlNode objItem in Document.SelectNodes("/character/powers/power")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = GetValue(objItem, "name", string.Empty);
                XmlNode? objRule = FindRuleItemByName(objPowers, "/chummer/powers/power", strName);
                XmlNode? objBonus = objRule?.SelectSingleNode("bonus");
                if (objRule == null || objBonus?.SelectSingleNode("selectsenseware") != null)
                    continue;

                string strRating = GetValue(objItem, "rating", "1");
                RemoveBonusImprovements(ImprovementSource.Power, strName);
                ApplyBonus(objBonus, ImprovementSource.Power, strName, strRating);
                ApplySelectedImprovement(objBonus, ImprovementSource.Power, strName,
                    GetValue(objItem, "extra", string.Empty), strRating);
                intRefreshed++;
            }

            XmlDocument objPrograms = XmlManager.Instance.Load("programs.xml");
            foreach (XmlNode objItem in Document.SelectNodes("/character/techprograms/techprogram")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = GetValue(objItem, "name", string.Empty);
                XmlNode? objRule = FindRuleItemByName(objPrograms, "/chummer/programs/program", strName);
                if (objRule == null)
                    continue;

                string strRating = GetValue(objItem, "rating", "1");
                XmlNode? objBonus = objRule.SelectSingleNode("bonus");
                RemoveBonusImprovements(ImprovementSource.ComplexForm, strName);
                ApplyBonus(objBonus, ImprovementSource.ComplexForm, strName, strRating);
                ApplySelectedImprovement(objBonus, ImprovementSource.ComplexForm, strName,
                    GetValue(objItem, "extra", string.Empty), strRating);
                intRefreshed++;
            }

            XmlDocument objCritterPowers = XmlManager.Instance.Load("critterpowers.xml");
            foreach (XmlNode objItem in Document.SelectNodes("/character/critterpowers/critterpower")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = GetValue(objItem, "name", string.Empty);
                XmlNode? objRule = FindRuleItemByName(objCritterPowers, "/chummer/powers/power", strName);
                if (objRule == null)
                    continue;

                bool blnHasRating = GetValue(objRule, "rating", "no") == "yes";
                string strRating = blnHasRating ? GetValue(objItem, "rating", "1") : "1";
                XmlNode? objBonus = objRule.SelectSingleNode("bonus");
                RemoveBonusImprovements(ImprovementSource.CritterPower, strName);
                ApplyBonus(objBonus, ImprovementSource.CritterPower, strName, strRating);
                ApplySelectedImprovement(objBonus, ImprovementSource.CritterPower, strName,
                    GetValue(objItem, "extra", string.Empty), strRating);
                intRefreshed++;
            }

            foreach (XmlNode objItem in Document.SelectNodes("/character/metamagics/metamagic")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = GetValue(objItem, "name", string.Empty);
                bool blnEcho = GetValue(objItem, "improvementsource", "Metamagic") == ImprovementSource.Echo.ToString();
                XmlDocument objRules = XmlManager.Instance.Load(blnEcho ? "echoes.xml" : "metamagic.xml");
                XmlNode? objRule = FindRuleItemByName(objRules,
                    blnEcho ? "/chummer/echoes/echo" : "/chummer/metamagics/metamagic", strName);
                if (objRule == null)
                    continue;

                ImprovementSource eSource = blnEcho ? ImprovementSource.Echo : ImprovementSource.Metamagic;
                XmlNode? objBonus = objRule.SelectSingleNode("bonus");
                RemoveBonusImprovements(eSource, strName);
                ApplyBonus(objBonus, eSource, strName);
                ApplySelectedImprovement(objBonus, eSource, strName, GetValue(objItem, "extra", string.Empty), "1");
                intRefreshed++;
            }

            if (intRefreshed > 0)
                Changed?.Invoke();
            return intRefreshed;
        }

        private void ReapplyMentorSpiritBonuses(XmlNode objQuality, string strQualityName)
        {
            string? strDataFile = QualityMentorSpiritDataFile(strQualityName);
            string strMentor = GetValue(objQuality, "mentorspirit", string.Empty);
            if (strDataFile == null || string.IsNullOrWhiteSpace(strMentor))
                return;

            XmlNode? objMentor = FindRuleItemByName(XmlManager.Instance.Load(strDataFile),
                "/chummer/mentors/mentor", strMentor);
            ApplyBonus(objMentor?.SelectSingleNode("bonus"), ImprovementSource.Quality, strQualityName);
            foreach (string strChoice in new[]
                     {
                         GetValue(objQuality, "mentorchoice1", string.Empty),
                         GetValue(objQuality, "mentorchoice2", string.Empty)
                     })
            {
                if (string.IsNullOrWhiteSpace(strChoice))
                    continue;
                XmlNode? objChoice = objMentor?.SelectNodes("choices/choice")?.Cast<XmlNode>().FirstOrDefault(
                    objNode => string.Equals(GetValue(objNode, "name", string.Empty), strChoice,
                        StringComparison.Ordinal));
                ApplyBonus(objChoice?.SelectSingleNode("bonus"), ImprovementSource.Quality, strQualityName);
            }
        }

        /// <summary>Fires whenever a root-level value (Karma, Bp, Nuyen, ...) changes, so a host
        /// UI showing those totals (e.g. the main window's status bar) can refresh without needing
        /// to know about every individual mutator that might have spent points.</summary>
        private bool ExceedsPositiveQualityLimit(int intNewMartialArtCost, bool blnKarmaBuild)
        {
            CharacterOptions objOptions = GetCharacterOptions();
            if (objOptions.ExceedPositiveQualities)
                return false;

            XmlDocument objQualities = XmlManager.Instance.Load("qualities.xml");
            int intQualityCost = (Document.SelectNodes("/character/qualities/quality")?.Cast<XmlNode>()
                ?? Enumerable.Empty<XmlNode>())
                .Where(q => GetValue(q, "qualitytype", string.Empty) == "Positive")
                .Sum(q =>
                {
                    XmlNode? objRule = objQualities.SelectSingleNode(
                        $"/chummer/qualities/quality[name = '{GetValue(q, "name", string.Empty)}']");
                    return string.Equals(GetValue(objRule, "contributetolimit", "yes"), "no",
                        StringComparison.OrdinalIgnoreCase) ? 0 : GetQualityCreationCost(objRule, blnKarmaBuild);
                });
            int intFree = ImprovementManager.ValueOf(Improvements, ImprovementType.FreePositiveQualities);
            if (blnKarmaBuild)
                intFree *= objOptions.KarmaQuality;
            int intMartialArts = MartialArts.Sum(a => ParseInteger(a.Rating)
                * (blnKarmaBuild ? 5 * objOptions.KarmaQuality : objOptions.BpMartialArt));
            int intLimit = blnKarmaBuild ? 70 : 35;
            return intQualityCost - intFree + intMartialArts + intNewMartialArtCost > intLimit;
        }

        public sealed record LifestyleQualityOption(string Name, string Category, int Lp, string Source, string Page);

        public IReadOnlyList<LifestyleQualityOption> GetLifestyleQualityOptions()
        {
            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            var lstOptions = new List<LifestyleQualityOption>();
            foreach (XmlNode objQuality in objLifestylesDoc.SelectNodes("/chummer/qualities/quality")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strAllowed = objQuality["allowed"]?.InnerText ?? string.Empty;
                if (!strAllowed.Split(',').Contains("Advanced"))
                    continue;

                string strName = objQuality["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(strName))
                    continue;

                int intLp = int.TryParse(objQuality["lp"]?.InnerText, out var lp) ? lp : 0;
                lstOptions.Add(new LifestyleQualityOption(strName, objQuality["category"]?.InnerText ?? string.Empty,
                    intLp, objQuality["source"]?.InnerText ?? string.Empty, objQuality["page"]?.InnerText ?? string.Empty));
            }

            return lstOptions;
        }

        /// <summary>Adds an Advanced Lifestyle - ported from frmSelectAdvancedLifestyle.cs's
        /// AcceptForm/CalculateValues. Total LP is the five aspects' own &lt;lp&gt; plus each
        /// chosen Quality's &lt;lp&gt; (Negative Qualities carry negative values, matching real
        /// data); Nuyen cost comes from lifestyles.xml's &lt;costs&gt; table (linear extrapolation
        /// past LP 30, exactly like legacy), then scaled by Roommates% and the overall Percentage
        /// slider. Dice/Multiplier for the starting-Nuyen roll are looked up from whichever
        /// standard Lifestyle (Street..Luxury) the total LP maps onto - not ported: legacy's
        /// separate "effective LP" (aspects-only, no Qualities) used only to pick Dice/Multiplier
        /// independently of the Nuyen-cost LP; this port uses the same total LP for both, which
        /// only differs from legacy when Qualities push the character across a LP tier boundary.</summary>
        public void AddAdvancedLifestyle(string strName, string strComforts, string strEntertainment,
            string strNecessities, string strNeighborhood, string strSecurity, int intRoommates, int intPercentage,
            IReadOnlyList<string> lstPositiveQualities, IReadOnlyList<string> lstNegativeQualities)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A lifestyle name is required.", nameof(strName));

            AdvancedLifestylePreview objPreview = PreviewAdvancedLifestyle(strComforts, strEntertainment,
                strNecessities, strNeighborhood, strSecurity, intRoommates, intPercentage, lstPositiveQualities,
                lstNegativeQualities);

            AddLifestyle(strName, objPreview.Cost.ToString(CultureInfo.InvariantCulture), "1",
                objPreview.Dice.ToString(CultureInfo.InvariantCulture),
                objPreview.Multiplier.ToString(CultureInfo.InvariantCulture));

            var objNodes = Document.SelectNodes("/character/lifestyles/lifestyle");
            if (objNodes?[objNodes.Count - 1] is XmlElement objLastLifestyle)
            {
                AppendElement(objLastLifestyle, "comforts", strComforts);
                AppendElement(objLastLifestyle, "entertainment", strEntertainment);
                AppendElement(objLastLifestyle, "necessities", strNecessities);
                AppendElement(objLastLifestyle, "neighborhood", strNeighborhood);
                AppendElement(objLastLifestyle, "security", strSecurity);
                AppendElement(objLastLifestyle, "roommates", intRoommates.ToString(CultureInfo.InvariantCulture));
                AppendElement(objLastLifestyle, "percentage", intPercentage.ToString(CultureInfo.InvariantCulture));
                AppendElement(objLastLifestyle, "type", "Advanced");
                var objQualities = Document.CreateElement("qualities");
                foreach (string strQuality in lstPositiveQualities.Concat(lstNegativeQualities))
                    AppendElement(objQualities, "quality", strQuality);
                objLastLifestyle.AppendChild(objQualities);
            }
        }

        public AdvancedLifestylePreview PreviewAdvancedLifestyle(string strComforts, string strEntertainment,
            string strNecessities, string strNeighborhood, string strSecurity, int intRoommates, int intPercentage,
            IReadOnlyList<string> lstPositiveQualities, IReadOnlyList<string> lstNegativeQualities)
        {
            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            int intLp = 0;
            intLp += AspectLp(objLifestylesDoc, "comfort", strComforts);
            intLp += AspectLp(objLifestylesDoc, "entertainment", strEntertainment);
            intLp += AspectLp(objLifestylesDoc, "necessity", strNecessities);
            intLp += AspectLp(objLifestylesDoc, "neighborhood", strNeighborhood);
            intLp += AspectLp(objLifestylesDoc, "security", strSecurity);

            foreach (string strQuality in lstPositiveQualities.Concat(lstNegativeQualities))
            {
                XmlNode? objXmlQuality = objLifestylesDoc.SelectSingleNode($"/chummer/qualities/quality[name = '{strQuality}']");
                if (objXmlQuality != null && int.TryParse(objXmlQuality["lp"]?.InnerText, out var intQualityLp))
                    intLp += intQualityLp;
            }

            intLp = Math.Max(0, intLp);

            int intNuyen;
            if (intLp < 29)
            {
                intNuyen = int.TryParse(objLifestylesDoc.SelectSingleNode(
                    $"/chummer/costs/cost[lp = '{intLp}']")?["cost"]?.InnerText, out var c) ? c : 0;
            }
            else
            {
                int intBase = int.TryParse(objLifestylesDoc.SelectSingleNode(
                    "/chummer/costs/cost[lp = '30']")?["cost"]?.InnerText, out var b) ? b : 0;
                int intPerLp = int.TryParse(objLifestylesDoc.SelectSingleNode(
                    "/chummer/costs/cost[lp = '31+']")?["cost"]?.InnerText, out var p) ? p : 0;
                intNuyen = intBase + (intLp - 30) * intPerLp;
            }

            intNuyen = (int)(intNuyen * (1.0 + intRoommates / 10.0));
            intNuyen = (int)(intNuyen * (intPercentage / 100.0));

            string strTierName = intLp switch
            {
                >= 21 => "Luxury",
                >= 16 => "High",
                >= 11 => "Middle",
                >= 6 => "Low",
                >= 1 => "Squatter",
                _ => "Street",
            };
            XmlNode? objXmlTier = objLifestylesDoc.SelectSingleNode($"/chummer/lifestyles/lifestyle[name = '{strTierName}']");
            int intDice = int.TryParse(objXmlTier?["dice"]?.InnerText, out var d) ? d : 1;
            int intMultiplier = int.TryParse(objXmlTier?["multiplier"]?.InnerText, out var m) ? m : 0;

            return new AdvancedLifestylePreview(intLp, intNuyen, intDice, intMultiplier);
        }

        private IReadOnlyList<CharacterQualityData> ReadQualities()
        {
            var lstQualities = new List<CharacterQualityData>();
            var objNodes = Document.SelectNodes("/character/qualities/quality");
            if (objNodes == null) return lstQualities;
            for (int intQualityId = 0; intQualityId < objNodes.Count; intQualityId++)
            {
                XmlNode objNode = objNodes[intQualityId]!;
                QualitySource eSource = Enum.TryParse(GetValue(objNode, "qualitysource", "Selected"),
                    ignoreCase: true, out QualitySource eParsedSource)
                    ? eParsedSource
                    : QualitySource.Selected;
                lstQualities.Add(new CharacterQualityData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "qualitytype", string.Empty),
                    intQualityId, GetValue(objNode, "notes", string.Empty), eSource));
            }
            return lstQualities;
        }

    }
}
