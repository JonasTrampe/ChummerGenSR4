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
        public IReadOnlyList<string> GetAdeptPowerAttributeSelectionOptions(string strName) =>
            ExtractAttributeSelectionOptions(FindBonusChild("powers.xml", "powers", "power", strName, "selectattribute"));

        public IReadOnlyList<CharacterPowerData> AdeptPowers => ReadAdeptPowers();

        /// <summary>Ported from frmSelectPower.cs's cmdOK_Click. Also applies the power's own
        /// rules-data &lt;bonus&gt; block (see <see cref="ApplyBonus"/>) at the power's Rating,
        /// matching legacy's CreateImprovements call on add - including &lt;selectskill&gt;/
        /// &lt;selectattribute&gt; (see <see cref="GetAdeptPowerSkillSelectionOptions"/>/<see
        /// cref="GetAdeptPowerAttributeSelectionOptions"/>), whose player-picked
        /// <paramref name="strSelected"/> becomes the corresponding Improvement. Note:
        /// &lt;selectsenseware&gt; (Improved Sense) isn't a <see cref="BonusApplier"/>-covered
        /// node - see <see cref="AddImprovedSensePower"/> for that dedicated flow.</summary>
        public void AddAdeptPower(string strName, string strRating, string strPointsPerLevel, string strSelected = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A power name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objPowers = objRoot.SelectSingleNode("powers");
            if (objPowers == null)
            {
                objPowers = Document.CreateElement("powers");
                objRoot.AppendChild(objPowers);
            }

            var objPower = Document.CreateElement("power");
            AppendElement(objPower, "name", strName.Trim());
            AppendElement(objPower, "extra", strSelected.Trim());
            AppendElement(objPower, "rating", strRating);
            AppendElement(objPower, "pointsperlevel", strPointsPerLevel);
            AppendElement(objPower, "discounted", "False");
            AppendElement(objPower, "discountedgeas", "False");
            objPowers.AppendChild(objPower);

            XmlDocument objPowersDoc = XmlManager.Instance.Load("powers.xml");
            XmlNode? objXmlPower = objPowersDoc.SelectSingleNode($"/chummer/powers/power[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlPower?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.Power, strName.Trim(), strRating);
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.Power, strName.Trim(), strSelected, strRating);

            Changed?.Invoke();
        }

        /// <summary>One senseware item ("Improved Sense" Adept Power etc.) the player can pick
        /// from - <see cref="SourceFile"/> lets the caller re-resolve the item's own rules-data
        /// node (cyberware.xml/bioware.xml/gear.xml) when the choice is applied.</summary>
        public bool AddImprovedSensePower(string strName, string strRating, string strPointsPerLevel,
            string strSelectedSenseware)
        {
            XmlNode? objXmlSelectSenseware = FindSelectSensewareNode(strName);
            if (objXmlSelectSenseware == null || string.IsNullOrWhiteSpace(strSelectedSenseware))
                return false;

            XmlNode? objXmlSelected = GetSenseImprovementOptions(strName)
                .Where(o => o.Name == strSelectedSenseware)
                .Select(o => XmlManager.Instance.Load(o.SourceFile)
                    .SelectSingleNode($"/chummer/*/*[name = '{strSelectedSenseware}']"))
                .FirstOrDefault(n => n != null);
            if (objXmlSelected == null)
                return false;

            AddAdeptPower(strName, strRating, strPointsPerLevel);

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objImprovements = objRoot.SelectSingleNode("improvements") as XmlElement
                ?? (XmlElement)objRoot.AppendChild(Document.CreateElement("improvements"));
            var objSelection = Document.CreateElement("improvement");
            AppendElement(objSelection, "improvementttype", ImprovementType.SelectSenseware.ToString());
            AppendElement(objSelection, "improvedname", strSelectedSenseware);
            AppendElement(objSelection, "sourcename", strName.Trim());
            AppendElement(objSelection, "val", "0");
            AppendElement(objSelection, "rating", "1");
            AppendElement(objSelection, "min", "0");
            AppendElement(objSelection, "max", "0");
            AppendElement(objSelection, "aug", "0");
            AppendElement(objSelection, "augmax", "0");
            AppendElement(objSelection, "unique", string.Empty);
            AppendElement(objSelection, "improvementsource", ImprovementSource.Power.ToString());
            AppendElement(objSelection, "addtorating", "False");
            AppendElement(objSelection, "enabled", "True");
            AppendElement(objSelection, "custom", "False");
            objImprovements.AppendChild(objSelection);

            string strSensewareRating = GetCharacterOptions().ImprovedSenseFullRating
                ? GetValue(objXmlSelected, "rating", "1")
                : "1";
            ApplyBonus(objXmlSelected.SelectSingleNode("bonus"), ImprovementSource.Power, strName.Trim(), strSensewareRating);

            Changed?.Invoke();
            return true;
        }

        public bool RemoveAdeptPower(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/powers/power");
            if (objNodes == null)
                return false;

            foreach (XmlNode objPower in objNodes)
            {
                if (!string.Equals(GetValue(objPower, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                objPower.ParentNode?.RemoveChild(objPower);
                RemoveBonusImprovements(ImprovementSource.Power, strName.Trim());
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Removes the power at the displayed root-list position, preserving duplicate
        /// powers that have the same name but different selected details.</summary>
        public bool RemoveAdeptPower(int intPowerId)
        {
            XmlNode? objPower = GetAdeptPowerNodeById(intPowerId);
            if (objPower == null)
                return false;

            string strName = GetValue(objPower, "name", string.Empty);
            objPower.ParentNode?.RemoveChild(objPower);
            RemoveBonusImprovements(ImprovementSource.Power, strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Updates an Adept Power's free-form notes using its root-list position.</summary>
        public bool SetAdeptPowerNotes(int intPowerId, string strNotes)
        {
            XmlNode? objPower = GetAdeptPowerNodeById(intPowerId);
            if (objPower == null)
                return false;

            SetChildValue(objPower, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetAdeptPowerNodeById(int intPowerId)
        {
            if (intPowerId < 0)
                return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/powers/power");
            return objNodes != null && intPowerId < objNodes.Count ? objNodes[intPowerId] : null;
        }

        // Ported from frmCareer.cs/frmCreate.cs's CalculatePowerPoints().
        public CharacterDerivedValueData AdeptPowerPoints
        {
            get
            {
                var decUsed = AdeptPowers.Sum(p =>
                    decimal.TryParse(p.TotalPoints, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m);

                // Mystic Adepts split MAG between the Magician and Adept portions; a pure Adept
                // uses the character's full MAG.
                var intMag = MysticAdept ? MysticAdeptAdeptMagSplit : GetAttributeInt("MAG");
                var lstContributions = ImprovementManager
                    .DescribeAugmentedValueOf(Improvements, ImprovementType.AdeptPowerPoints)
                    .ToList();
                var intTotal = intMag + lstContributions.Sum(c => c.Value);

                var sb = new StringBuilder();
                sb.Append("MAG").Append(MysticAdept ? " (Adept-Anteil)" : string.Empty).Append(": ").Append(intMag);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Verfügbar: ").Append(intTotal);
                sb.Append('\n').Append("Verbraucht: ").Append(decUsed.ToString(LanguageManager.CurrentNumberFormatCulture));
                var decRemaining = intTotal - decUsed;
                sb.Append('\n').Append("Übrig: ").Append(decRemaining.ToString(LanguageManager.CurrentNumberFormatCulture));
                return new CharacterDerivedValueData((int)decimal.Truncate(decRemaining), sb.ToString());
            }
        }

        /// <summary>Ported from clsMainController.cs's CalculateFreeSpiritPowerPoints: a
        /// player-character Free Spirit's power points come from EDG (or MAG, under the
        /// FreeSpiritPowerPointsMag house rule) plus any FreeSpiritPowerPoints Improvement bonus;
        /// used points are the sum of owned Critter Powers' point costs. Null when the character
        /// isn't a PC Free Spirit (Metatype != "Free Spirit", or a critter rather than a PC -
        /// matches legacy's Metatype/IsCritter gate).</summary>
        private IReadOnlyList<CharacterPowerData> ReadAdeptPowers()
        {
            var lstPowers = new List<CharacterPowerData>();
            var objNodes = Document.SelectNodes("/character/powers/power");
            if (objNodes == null) return lstPowers;
            for (int intPowerId = 0; intPowerId < objNodes.Count; intPowerId++)
            {
                XmlNode objNode = objNodes[intPowerId]!;
                lstPowers.Add(new CharacterPowerData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "rating", "0"),
                    GetValue(objNode, "pointsperlevel", "0"),
                    GetValue(objNode, "discounted", "False"),
                    GetValue(objNode, "discountedgeas", "False"), intPowerId,
                    GetValue(objNode, "notes", string.Empty)));
            }
            return lstPowers;
        }

    }
}
