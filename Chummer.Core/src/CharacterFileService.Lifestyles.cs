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
        public IReadOnlyList<CharacterLifestyleData> Lifestyles => ReadLifestyles();

        /// <summary>Adds a base lifestyle using the character-file shape written by the legacy application.
        /// <paramref name="strDice"/>/<paramref name="strMultiplier"/> are optional (empty for
        /// existing callers) - when supplied they're used directly by <see
        /// cref="GetLifestyleNuyenRollInfo"/> instead of that method's by-name lifestyles.xml
        /// re-lookup, which only works for plain Lifestyles whose saved name still matches a
        /// lifestyles.xml entry (see <see cref="AddAdvancedLifestyle"/>, whose custom player-chosen
        /// name never would).</summary>
        public void AddLifestyle(string strName, string strCost, string strMonths = "1", string strDice = "",
            string strMultiplier = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A lifestyle name is required.", nameof(strName));
            var objRoot = Document.DocumentElement ?? throw new InvalidOperationException("Character document has no root element.");
            var objLifestyles = objRoot.SelectSingleNode("lifestyles") as XmlElement;
            if (objLifestyles == null) { objLifestyles = Document.CreateElement("lifestyles"); objRoot.AppendChild(objLifestyles); }
            var objLifestyle = Document.CreateElement("lifestyle");
            AppendElement(objLifestyle, "lifestylename", strName);
            AppendElement(objLifestyle, "cost", strCost);
            AppendElement(objLifestyle, "months", strMonths);
            if (!string.IsNullOrEmpty(strDice)) AppendElement(objLifestyle, "dice", strDice);
            if (!string.IsNullOrEmpty(strMultiplier)) AppendElement(objLifestyle, "multiplier", strMultiplier);
            objLifestyles.AppendChild(objLifestyle);
            Changed?.Invoke();
        }

        /// <summary>Names offered for one of an Advanced Lifestyle's five aspects - ported from
        /// frmSelectAdvancedLifestyle.cs's Load handler's Comforts/Entertainment/Necessities/
        /// Neighborhood/Security ComboBox population (Advanced type only - this port doesn't
        /// model Safehouse/BoltHole's separate &lt;slp&gt; LP override).</summary>
        public IReadOnlyList<string> GetLifestyleAspectOptions(string strAspectTag)
        {
            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            var lstNames = new List<string>();
            foreach (XmlNode objAspect in objLifestylesDoc.SelectNodes(
                         $"/chummer/{AspectContainerTag(strAspectTag)}/{strAspectTag}")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objAspect["name"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strName))
                    lstNames.Add(strName);
            }

            return lstNames;
        }

        /// <summary>One Positive/Negative Quality an Advanced Lifestyle can take - ported from
        /// frmSelectAdvancedLifestyle.cs's quality tree population (lifestyles.xml has its own
        /// quality catalog, separate from qualities.xml's character Qualities), filtered to
        /// entries whose &lt;allowed&gt; list includes "Advanced".</summary>
        public sealed record AdvancedLifestylePreview(int Lp, int Cost, int Dice, int Multiplier);

        /// <summary>Non-mutating LP/Cost/Dice/Multiplier computation shared by <see
        /// cref="AddAdvancedLifestyle"/> and the picker dialog's live preview, so the two can
        /// never drift out of sync.</summary>
        private static int AspectLp(XmlDocument objLifestylesDoc, string strAspectTag, string strValue)
        {
            XmlNode? objXmlAspect = objLifestylesDoc.SelectSingleNode(
                $"/chummer/{AspectContainerTag(strAspectTag)}/{strAspectTag}[name = '{strValue}']");
            return int.TryParse(objXmlAspect?["lp"]?.InnerText, out var lp) ? lp : 0;
        }

        /// <summary>lifestyles.xml's five aspect list container names don't all follow simple
        /// "+s" pluralization ("necessity"/"security" -&gt; "necessities"/"securities").</summary>
        public bool RemoveLifestyle(string strName)
        {
            XmlNodeList? objLifestyles = Document.SelectNodes("/character/lifestyles/lifestyle");
            if (objLifestyles == null) return false;
            foreach (XmlNode objLifestyle in objLifestyles)
            {
                string strSavedName = GetValue(objLifestyle, "lifestylename", GetValue(objLifestyle, "name", string.Empty));
                if (!string.Equals(strSavedName, strName, StringComparison.Ordinal)) continue;
                objLifestyle.ParentNode?.RemoveChild(objLifestyle);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool RemoveLifestyle(int intLifestyleId)
        {
            XmlNode? objLifestyle = GetLifestyleNodeById(intLifestyleId);
            if (objLifestyle == null)
                return false;
            objLifestyle.ParentNode?.RemoveChild(objLifestyle);
            Changed?.Invoke();
            return true;
        }

        public bool SetLifestyleNotes(int intLifestyleId, string strNotes)
        {
            XmlNode? objLifestyle = GetLifestyleNodeById(intLifestyleId);
            if (objLifestyle == null)
                return false;
            SetChildValue(objLifestyle, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetLifestyleNodeById(int intLifestyleId)
        {
            if (intLifestyleId < 0)
                return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/lifestyles/lifestyle");
            return objNodes != null && intLifestyleId < objNodes.Count ? objNodes[intLifestyleId] : null;
        }

        /// <summary>The dice/multiplier/bonus needed to prompt for a starting-Nuyen roll, ported
        /// from frmCreate.cs's ConfirmSaveCreatedCharacter.</summary>
        public sealed record LifestyleNuyenRollInfo(int Dice, int Multiplier, int Extra);

        /// <summary>Ported from frmCreate.cs's ConfirmSaveCreatedCharacter: auto-adds a Street
        /// Lifestyle if the character has none yet, then computes the dice/multiplier for the
        /// highest-multiplier Lifestyle owned and the "+1 per 100 leftover Nuyen, capped at 3x
        /// dice" bonus. Returns null once the character is already Created.</summary>
        public LifestyleNuyenRollInfo? GetLifestyleNuyenRollInfo()
        {
            if (Created)
                return null;
            if (Lifestyles.Count == 0)
                AddLifestyle("Street", "0");

            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            int intDice = 0, intMultiplier = 0;
            foreach (CharacterLifestyleData objLifestyle in Lifestyles)
            {
                int intLifestyleMultiplier;
                int intLifestyleDice;
                if (!string.IsNullOrEmpty(objLifestyle.Multiplier))
                {
                    // Own persisted Dice/Multiplier (Advanced Lifestyles, or any Lifestyle added
                    // since this field started being written) - see AddLifestyle's doc comment.
                    intLifestyleMultiplier = int.TryParse(objLifestyle.Multiplier, out var lm) ? lm : 0;
                    intLifestyleDice = int.TryParse(objLifestyle.Dice, out var ld) ? ld : 0;
                }
                else
                {
                    XmlNode? objXmlLifestyle = objLifestylesDoc.SelectSingleNode(
                        $"/chummer/lifestyles/lifestyle[name = '{objLifestyle.Name}']");
                    if (objXmlLifestyle == null)
                        continue;
                    intLifestyleMultiplier = int.TryParse(GetValue(objXmlLifestyle, "multiplier", "0"), out var m) ? m : 0;
                    intLifestyleDice = int.TryParse(GetValue(objXmlLifestyle, "dice", "0"), out var d) ? d : 0;
                }

                if (intLifestyleMultiplier > intMultiplier)
                {
                    intMultiplier = intLifestyleMultiplier;
                    intDice = intLifestyleDice;
                }
            }

            int intNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
            int intExtra = Math.Min((int)Math.Floor(intNuyen / 100.0), intDice * 3);
            return new LifestyleNuyenRollInfo(intDice, intMultiplier, intExtra);
        }

        /// <summary>Applies a manually-entered starting-Nuyen dice-roll result (see
        /// <see cref="GetLifestyleNuyenRollInfo"/>), then finalizes creation - ported from
        /// frmCreate.cs's ConfirmSaveCreatedCharacter.</summary>
        public bool FinalizeCreationWithLifestyleNuyenRoll(int intDiceResult)
        {
            LifestyleNuyenRollInfo? objInfo = GetLifestyleNuyenRollInfo();
            if (objInfo == null)
                return false;

            int intStartingNuyen = Math.Max(0, (intDiceResult + objInfo.Extra) * objInfo.Multiplier);
            Nuyen = intStartingNuyen.ToString();
            return FinalizeCreation();
        }

        private IReadOnlyList<CharacterLifestyleData> ReadLifestyles()
        {
            var lstLifestyles = new List<CharacterLifestyleData>();
            var objNodes = Document.SelectNodes("/character/lifestyles/lifestyle");
            if (objNodes == null) return lstLifestyles;
            for (int intLifestyleId = 0; intLifestyleId < objNodes.Count; intLifestyleId++)
            {
                XmlNode objNode = objNodes[intLifestyleId]!;
                lstLifestyles.Add(new CharacterLifestyleData(
                    GetValue(objNode, "lifestylename", GetValue(objNode, "name", string.Empty)),
                    GetValue(objNode, "cost", "0"), GetValue(objNode, "months", "0"),
                    GetValue(objNode, "dice", string.Empty), GetValue(objNode, "multiplier", string.Empty),
                    intLifestyleId, GetValue(objNode, "notes", string.Empty)));
            }
            return lstLifestyles;
        }

    }
}
