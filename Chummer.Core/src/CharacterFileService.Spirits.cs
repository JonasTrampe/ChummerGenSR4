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
        public CharacterDerivedValueData? FreeSpiritPowerPoints
        {
            get
            {
                if (!string.Equals(Metatype, "Free Spirit", StringComparison.Ordinal) || IsCritter)
                    return null;

                var decUsed = CritterPowers.Where(p => p.CountsTowardsLimit).Sum(p =>
                    decimal.TryParse(p.Points, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m);

                bool blnUseMag = GetCharacterOptions().FreeSpiritPowerPointsMag;
                var intBase = GetAttributeInt(blnUseMag ? "MAG" : "EDG");
                var lstContributions = ImprovementManager
                    .DescribeAugmentedValueOf(Improvements, ImprovementType.FreeSpiritPowerPoints)
                    .ToList();
                var intTotal = intBase + lstContributions.Sum(c => c.Value);

                var sb = new StringBuilder();
                sb.Append(blnUseMag ? "MAG" : "EDG").Append(": ").Append(intBase);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Verfügbar: ").Append(intTotal);
                sb.Append('\n').Append("Verbraucht: ").Append(decUsed.ToString(CultureInfo.GetCultureInfo("de-DE")));
                var decRemaining = intTotal - decUsed;
                sb.Append('\n').Append("Übrig: ").Append(decRemaining.ToString(CultureInfo.GetCultureInfo("de-DE")));
                return new CharacterDerivedValueData((int)decimal.Truncate(decRemaining), sb.ToString());
            }
        }

        /// <summary>Remaining innate-power slots for a Free Sprite. Every counted power consumes
        /// one slot; EDG plus FreeSpiritPowerPoints improvements supplies the available slots.</summary>
        public int MaxSpiritForce
        {
            get
            {
                if (Technomancer)
                    return GetAttributeInt("RES");
                if (!Magician)
                    return 0;
                if (MysticAdept && !GetCharacterOptions().SpiritForceBasedOnTotalMag)
                    return MysticAdeptMagicianMagSplit;
                return GetAttributeInt("MAG");
            }
        }

        public bool AddSpirit(string strName, string strCritterName, string strType, string strForce,
            string strServices, bool blnBound = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A spirit name is required.", nameof(strName));

            int intServices = ParseInteger(strServices);
            if (intServices < 0)
                return false;
            bool blnEnforceCreationBudget = !Created && StartingBuildPoints > 0;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intCreationCost = intServices * (blnKarmaBuild ? GetCharacterOptions().KarmaSpirit
                : GetCharacterOptions().BpSpirit);
            int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
            if (blnEnforceCreationBudget && !IgnoreRules && intCreationCost > intPool)
                return false;

            // Ported from frmCreate.cs's cmdAddSpirit_Click: at creation, the number of
            // Spirits/Sprites cannot exceed CHA (career mode has no such cap - frmCareer.cs's own
            // cmdAddSpirit_Click never checks it).
            if (blnEnforceCreationBudget && !IgnoreRules && Spirits.Count >= GetAttributeInt("CHA"))
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSpirits = objRoot.SelectSingleNode("spirits");
            if (objSpirits == null)
            {
                objSpirits = Document.CreateElement("spirits");
                objRoot.AppendChild(objSpirits);
            }

            var objSpirit = Document.CreateElement("spirit");
            AppendElement(objSpirit, "name", strName.Trim());
            AppendElement(objSpirit, "crittername", strCritterName);
            AppendElement(objSpirit, "services", strServices);
            AppendElement(objSpirit, "force", strForce);
            AppendElement(objSpirit, "bound", blnBound ? "True" : "False");
            AppendElement(objSpirit, "type", strType);
            if (blnEnforceCreationBudget)
                AppendElement(objSpirit, "creationcost", intCreationCost.ToString(CultureInfo.InvariantCulture));
            objSpirits.AppendChild(objSpirit);
            if (blnEnforceCreationBudget)
            {
                if (blnKarmaBuild)
                    Karma = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
                else
                    Bp = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first saved Spirit/Sprite matching name+type+force - legacy has no
        /// stable per-entry identity for Spirits either, so this matches the same
        /// first-occurrence-by-fields approach RemoveLifestyle/RemoveCyberware already use.</summary>
        public bool RemoveSpirit(string strName, string strType, string strForce)
        {
            var objNodes = Document.SelectNodes("/character/spirits/spirit");
            if (objNodes == null) return false;

            foreach (XmlNode objSpirit in objNodes)
            {
                if (!string.Equals(GetValue(objSpirit, "name", string.Empty), strName, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objSpirit, "type", "Spirit"), strType, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objSpirit, "force", "0"), strForce, StringComparison.Ordinal))
                    continue;

                RefundCreationCost(objSpirit);
                objSpirit.ParentNode?.RemoveChild(objSpirit);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Updates a Spirit/Sprite note by the same transient list identity used by the
        /// reader. Legacy spirit entries do not carry a GUID, and duplicate summons are valid.</summary>
        public bool SetSpiritNotes(int intSpiritId, string strNotes)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/spirits/spirit");
            if (objNodes == null || intSpiritId < 0 || intSpiritId >= objNodes.Count)
                return false;

            SetChildValue(objNodes[intSpiritId]!, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private IReadOnlyList<CharacterSpiritData> ReadSpirits()
        {
            var lstSpirits = new List<CharacterSpiritData>();
            var objNodes = Document.SelectNodes("/character/spirits/spirit");
            if (objNodes == null) return lstSpirits;
            for (int intSpiritId = 0; intSpiritId < objNodes.Count; intSpiritId++)
            {
                XmlNode objNode = objNodes[intSpiritId]!;
                lstSpirits.Add(new CharacterSpiritData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "crittername", string.Empty), GetValue(objNode, "services", "0"),
                    GetValue(objNode, "force", "0"), GetValue(objNode, "bound", "False") == "True",
                    GetValue(objNode, "type", "Spirit"), intSpiritId, GetValue(objNode, "notes", string.Empty)));
            }
            return lstSpirits;
        }

    }
}
