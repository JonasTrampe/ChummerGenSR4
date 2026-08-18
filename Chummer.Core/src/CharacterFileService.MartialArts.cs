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
        public IReadOnlyList<CharacterMartialArtData> MartialArts => ReadMartialArts();

        public IReadOnlyList<CharacterMartialArtManeuverData> MartialArtManeuvers => ReadMartialArtManeuvers();

        /// <summary>Ported from frmSelectMartialArt.cs: adds the Martial Art with its full set of
        /// rules-data advantages snapshotted in (matches how ReadMartialArts expects to find them
        /// nested under martialartadvantages, not re-resolved from martialarts.xml every load).</summary>
        public bool AddMartialArt(string strName, IReadOnlyList<string> lstAdvantages, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A martial art name is required.", nameof(strName));

            bool blnEnforceCreationBudget = !Created && StartingBuildPoints > 0;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            CharacterOptions objOptions = GetCharacterOptions();
            int intCreationCost = blnKarmaBuild ? 5 * objOptions.KarmaQuality : objOptions.BpMartialArt;
            int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
            if (blnEnforceCreationBudget && !IgnoreRules
                && (intCreationCost > intPool || ExceedsPositiveQualityLimit(intCreationCost, blnKarmaBuild)))
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objMartialArts = objRoot.SelectSingleNode("martialarts");
            if (objMartialArts == null)
            {
                objMartialArts = Document.CreateElement("martialarts");
                objRoot.AppendChild(objMartialArts);
            }

            var objMartialArt = Document.CreateElement("martialart");
            AppendElement(objMartialArt, "name", strName.Trim());
            AppendElement(objMartialArt, "rating", "1");
            AppendElement(objMartialArt, "source", strSource);
            AppendElement(objMartialArt, "page", strPage);
            if (blnEnforceCreationBudget)
                AppendElement(objMartialArt, "creationcost", intCreationCost.ToString(CultureInfo.InvariantCulture));
            var objAdvantages = Document.CreateElement("martialartadvantages");
            foreach (string strAdvantage in lstAdvantages)
            {
                var objAdvantage = Document.CreateElement("martialartadvantage");
                AppendElement(objAdvantage, "guid", Guid.NewGuid().ToString());
                AppendElement(objAdvantage, "name", strAdvantage);
                objAdvantages.AppendChild(objAdvantage);
            }
            objMartialArt.AppendChild(objAdvantages);
            objMartialArts.AppendChild(objMartialArt);
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

        /// <summary>Ported from frmSelectMartialArt.cs's Maneuver tab.</summary>
        public bool AddMartialArtManeuver(string strName, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A maneuver name is required.", nameof(strName));

            if (!IgnoreRules && MartialArtManeuvers.Count >= MartialArts.Sum(a => ParseInteger(a.Rating) * 2))
                return false;

            bool blnEnforceCreationBudget = !Created && StartingBuildPoints > 0;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intCreationCost = blnKarmaBuild ? GetCharacterOptions().KarmaManeuver
                : GetCharacterOptions().BpMartialArtManeuver;
            int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
            if (blnEnforceCreationBudget && !IgnoreRules && intCreationCost > intPool)
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objManeuvers = objRoot.SelectSingleNode("martialartmaneuvers");
            if (objManeuvers == null)
            {
                objManeuvers = Document.CreateElement("martialartmaneuvers");
                objRoot.AppendChild(objManeuvers);
            }

            var objManeuver = Document.CreateElement("martialartmaneuver");
            AppendElement(objManeuver, "guid", Guid.NewGuid().ToString());
            AppendElement(objManeuver, "name", strName.Trim());
            AppendElement(objManeuver, "source", strSource);
            AppendElement(objManeuver, "page", strPage);
            if (blnEnforceCreationBudget)
                AppendElement(objManeuver, "creationcost", intCreationCost.ToString(CultureInfo.InvariantCulture));
            objManeuvers.AppendChild(objManeuver);
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

        /// <summary>Removes the first saved Martial Art matching its name (and any nested
        /// martialartadvantage entries with it).</summary>
        public bool RemoveMartialArt(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/martialarts/martialart");
            if (objNodes == null)
                return false;

            foreach (XmlNode objMartialArt in objNodes)
            {
                if (!string.Equals(GetValue(objMartialArt, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                return RemoveMartialArtNode(objMartialArt);
            }

            return false;
        }

        /// <summary>Removes the first saved Martial Art Maneuver matching its name.</summary>
        public bool RemoveMartialArtManeuver(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/martialartmaneuvers/martialartmaneuver");
            if (objNodes == null)
                return false;

            foreach (XmlNode objManeuver in objNodes)
            {
                if (!string.Equals(GetValue(objManeuver, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                return RemoveMartialArtManeuverNode(objManeuver);
            }

            return false;
        }

        public bool RemoveMartialArt(int intMartialArtId)
        {
            XmlNode? objMartialArt = GetMartialArtNodeById(intMartialArtId);
            if (objMartialArt == null)
                return false;
            return RemoveMartialArtNode(objMartialArt);
        }

        public bool RemoveMartialArtManeuver(int intManeuverId)
        {
            XmlNode? objManeuver = GetMartialArtManeuverNodeById(intManeuverId);
            if (objManeuver == null)
                return false;
            return RemoveMartialArtManeuverNode(objManeuver);
        }

        private bool RemoveMartialArtNode(XmlNode objMartialArt)
        {
            RefundCreationCost(objMartialArt);
            objMartialArt.ParentNode?.RemoveChild(objMartialArt);
            Changed?.Invoke();
            return true;
        }

        private bool RemoveMartialArtManeuverNode(XmlNode objManeuver)
        {
            RefundCreationCost(objManeuver);
            objManeuver.ParentNode?.RemoveChild(objManeuver);
            Changed?.Invoke();
            return true;
        }

        public bool SetMartialArtNotes(int intMartialArtId, string strNotes)
        {
            XmlNode? objMartialArt = GetMartialArtNodeById(intMartialArtId);
            if (objMartialArt == null)
                return false;
            SetChildValue(objMartialArt, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        public bool SetMartialArtManeuverNotes(int intManeuverId, string strNotes)
        {
            XmlNode? objManeuver = GetMartialArtManeuverNodeById(intManeuverId);
            if (objManeuver == null)
                return false;
            SetChildValue(objManeuver, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetMartialArtNodeById(int intMartialArtId)
        {
            if (intMartialArtId < 0) return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/martialarts/martialart");
            return objNodes != null && intMartialArtId < objNodes.Count ? objNodes[intMartialArtId] : null;
        }

        private XmlNode? GetMartialArtManeuverNodeById(int intManeuverId)
        {
            if (intManeuverId < 0) return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/martialartmaneuvers/martialartmaneuver");
            return objNodes != null && intManeuverId < objNodes.Count ? objNodes[intManeuverId] : null;
        }

        private IReadOnlyList<CharacterMartialArtData> ReadMartialArts()
        {
            var lstMartialArts = new List<CharacterMartialArtData>();
            var objNodes = Document.SelectNodes("/character/martialarts/martialart");
            if (objNodes == null) return lstMartialArts;
            for (int intMartialArtId = 0; intMartialArtId < objNodes.Count; intMartialArtId++)
            {
                XmlNode objNode = objNodes[intMartialArtId]!;
                var lstAdvantages = new List<string>();
                var objAdvantageNodes = objNode.SelectNodes("martialartadvantages/martialartadvantage");
                if (objAdvantageNodes != null)
                    foreach (XmlNode objAdvantageNode in objAdvantageNodes)
                        lstAdvantages.Add(GetValue(objAdvantageNode, "name", string.Empty));

                lstMartialArts.Add(new CharacterMartialArtData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "rating", "0"), GetValue(objNode, "source", string.Empty),
                    GetValue(objNode, "page", string.Empty), lstAdvantages, intMartialArtId,
                    GetValue(objNode, "notes", string.Empty)));
            }

            return lstMartialArts;
        }

        private IReadOnlyList<CharacterMartialArtManeuverData> ReadMartialArtManeuvers()
        {
            var lstManeuvers = new List<CharacterMartialArtManeuverData>();
            var objNodes = Document.SelectNodes("/character/martialartmaneuvers/martialartmaneuver");
            if (objNodes == null) return lstManeuvers;
            for (int intManeuverId = 0; intManeuverId < objNodes.Count; intManeuverId++)
            {
                XmlNode objNode = objNodes[intManeuverId]!;
                lstManeuvers.Add(new CharacterMartialArtManeuverData(GetValue(objNode, "name", string.Empty),
                    intManeuverId, GetValue(objNode, "notes", string.Empty)));
            }
            return lstManeuvers;
        }

    }
}
