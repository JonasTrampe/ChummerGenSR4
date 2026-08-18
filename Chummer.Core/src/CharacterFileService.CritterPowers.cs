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
        public CharacterDerivedValueData? FreeSpritePowerPoints
        {
            get
            {
                if (!IsFreeSprite)
                    return null;

                int intUsed = CritterPowers.Count(p => p.CountsTowardsLimit);
                int intBase = GetAttributeInt("EDG");
                var lstContributions = ImprovementManager
                    .DescribeAugmentedValueOf(Improvements, ImprovementType.FreeSpiritPowerPoints)
                    .ToList();
                int intTotal = intBase + lstContributions.Sum(c => c.Value);
                var sb = new StringBuilder();
                sb.Append("EDG: ").Append(intBase);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Verfügbar: ").Append(intTotal);
                sb.Append('\n').Append("Verbraucht: ").Append(intUsed);
                sb.Append('\n').Append("Übrig: ").Append(intTotal - intUsed);
                return new CharacterDerivedValueData(intTotal - intUsed, sb.ToString());
            }
        }

        public IReadOnlyList<CharacterCritterPowerData> CritterPowers => ReadCritterPowers();

        private IReadOnlyList<CharacterCritterPowerData> ReadCritterPowers()
        {
            var lstPowers = new List<CharacterCritterPowerData>();
            var objNodes = Document.SelectNodes("/character/critterpowers/critterpower");
            if (objNodes == null) return lstPowers;
            foreach (XmlNode objNode in objNodes)
                lstPowers.Add(new CharacterCritterPowerData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "points", "0"),
                    GetValue(objNode, "rating", "0"), GetValue(objNode, "notes", string.Empty),
                    GetValue(objNode, "counttowardslimit", "True") != "False"));
            return lstPowers;
        }

        /// <summary>Ported from clsUnique.cs's CritterPower.Create/Save, simplified to skip the
        /// &lt;bonus&gt; Improvement-creation path (matches AddMetamagic/AddCyberware etc.).</summary>
        /// <summary>Ported from clsUnique.cs's CritterPower.Create/Save. Also applies the power's
        /// own rules-data &lt;bonus&gt; block, scaled by <paramref name="strRating"/> for powers
        /// whose rules-data entry sets &lt;rating&gt;yes&lt;/rating&gt; (e.g. Armor (Ballistic));
        /// every other power ignores it and applies at a fixed Rating of 1 (matching legacy's
        /// nudCritterPowerRating being disabled/irrelevant for those). When the bonus is a
        /// &lt;selecttext&gt;/&lt;selectskill&gt;/&lt;selectattribute&gt; node, <paramref
        /// name="strExtra"/> becomes the corresponding Improvement (see <see
        /// cref="ApplySelectedImprovement"/>).</summary>
        public void AddCritterPower(string strName, string strPoints, string strSource, string strPage,
            string strExtra = "", string strRating = "1", bool blnCountTowardsLimit = true)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A critter power name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objPowers = objRoot.SelectSingleNode("critterpowers");
            if (objPowers == null)
            {
                objPowers = Document.CreateElement("critterpowers");
                objRoot.AppendChild(objPowers);
            }

            XmlDocument objPowersDoc = XmlManager.Instance.Load("critterpowers.xml");
            XmlNode? objXmlPower = objPowersDoc.SelectSingleNode(
                $"/chummer/powers/power[name = '{strName.Trim()}']");
            bool blnHasRating = objXmlPower?.SelectSingleNode("rating")?.InnerText == "yes";

            var objPower = Document.CreateElement("critterpower");
            AppendElement(objPower, "guid", Guid.NewGuid().ToString());
            AppendElement(objPower, "name", strName.Trim());
            AppendElement(objPower, "extra", strExtra.Trim());
            AppendElement(objPower, "category", objXmlPower?.SelectSingleNode("category")?.InnerText ?? string.Empty);
            AppendElement(objPower, "type", objXmlPower?.SelectSingleNode("type")?.InnerText ?? string.Empty);
            AppendElement(objPower, "action", objXmlPower?.SelectSingleNode("action")?.InnerText ?? string.Empty);
            AppendElement(objPower, "range", objXmlPower?.SelectSingleNode("range")?.InnerText ?? string.Empty);
            AppendElement(objPower, "duration", objXmlPower?.SelectSingleNode("duration")?.InnerText ?? string.Empty);
            AppendElement(objPower, "points", strPoints);
            AppendElement(objPower, "counttowardslimit", blnCountTowardsLimit ? "True" : "False");
            AppendElement(objPower, "rating", blnHasRating ? strRating : "0");
            AppendElement(objPower, "source", strSource);
            AppendElement(objPower, "page", strPage);
            objPowers.AppendChild(objPower);

            XmlNode? objXmlBonus = objXmlPower?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.CritterPower, strName.Trim(),
                blnHasRating ? strRating : "1");
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.CritterPower, strName.Trim(), strExtra,
                blnHasRating ? strRating : "1");

            Changed?.Invoke();
        }

        /// <summary>Converts an ordinary Sprite to a Free Sprite. Legacy grants the non-counting
        /// Denial power and changes only metatypecategory. The method is idempotent to prevent an
        /// API host from adding duplicate Denial entries.</summary>
        public bool RemoveCritterPower(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            var objNodes = Document.SelectNodes("/character/critterpowers/critterpower");
            if (objNodes == null)
                return false;

            foreach (XmlNode objPower in objNodes)
            {
                if (!string.Equals(GetValue(objPower, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    continue;

                string strName = GetValue(objPower, "name", string.Empty);
                objPower.ParentNode?.RemoveChild(objPower);
                RemoveBonusImprovements(ImprovementSource.CritterPower, strName);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        public bool SetCritterPowerNotes(string strGuid, string strNotes)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;
            XmlNode? objPower = Document.SelectSingleNode(
                $"/character/critterpowers/critterpower[guid = '{strGuid}']");
            if (objPower == null)
                return false;
            SetChildValue(objPower, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Whether adding this Metamagic prompts for a free-text detail - ported from
        /// clsImprovement.cs's selecttext bonus node (e.g. Attunement (Animal/Item)'s target).</summary>
    }
}
