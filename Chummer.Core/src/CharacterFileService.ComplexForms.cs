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
        private IReadOnlyList<CharacterComplexFormData> ReadComplexForms()
        {
            var lstForms = new List<CharacterComplexFormData>();
            var objNodes = Document.SelectNodes("/character/techprograms/techprogram");
            if (objNodes == null) return lstForms;
            for (int intFormId = 0; intFormId < objNodes.Count; intFormId++)
            {
                XmlNode objNode = objNodes[intFormId]!;
                var lstOptions = new List<(string Name, string Rating)>();
                var objOptionNodes = objNode.SelectNodes("programoptions/programoption");
                if (objOptionNodes != null)
                    foreach (XmlNode objOptionNode in objOptionNodes)
                        lstOptions.Add((GetValue(objOptionNode, "name", string.Empty),
                            GetValue(objOptionNode, "rating", "0")));

                lstForms.Add(new CharacterComplexFormData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "category", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "rating", "0"), lstOptions,
                    GetValue(objNode, "notes", string.Empty)));
            }

            return lstForms;
        }

        /// <summary>Ported from clsUnique.cs's TechProgram.Create/Save. Also applies the program's
        /// own rules-data &lt;bonus&gt; block at Rating 1 (matching the always-1 saved Rating), and,
        /// when the bonus is a &lt;selecttext&gt;/&lt;selectskill&gt;/&lt;selectattribute&gt; node,
        /// <paramref name="strExtra"/> becomes the corresponding Improvement (see <see
        /// cref="ApplySelectedImprovement"/>).</summary>
        public bool AddComplexForm(string strName, string strCategory, string strSource, string strPage,
            string strExtra = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A complex form name is required.", nameof(strName));

            int intCost = ComputeComplexFormKarmaCost(strCategory, 1);
            bool blnCareer = Created;
            bool blnEnforceCreationBudget = !blnCareer && StartingBuildPoints > 0;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intCreationCost = blnKarmaBuild ? intCost
                : (GetCharacterOptions().AlternateComplexFormCost ? 3 : 1);
            int intPool = int.TryParse(blnKarmaBuild ? Karma : Bp, out int intParsedPool) ? intParsedPool : 0;
            if ((blnCareer && intPool < intCost) || (blnEnforceCreationBudget && intPool < intCreationCost))
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objForms = objRoot.SelectSingleNode("techprograms");
            if (objForms == null)
            {
                objForms = Document.CreateElement("techprograms");
                objRoot.AppendChild(objForms);
            }

            var objForm = Document.CreateElement("techprogram");
            AppendElement(objForm, "guid", Guid.NewGuid().ToString());
            AppendElement(objForm, "name", strName.Trim());
            AppendElement(objForm, "category", strCategory);
            AppendElement(objForm, "rating", "1");
            AppendElement(objForm, "extra", strExtra.Trim());
            AppendElement(objForm, "source", strSource);
            AppendElement(objForm, "page", strPage);
            if (blnEnforceCreationBudget)
                AppendElement(objForm, "creationcost", intCreationCost.ToString(CultureInfo.InvariantCulture));
            objForms.AppendChild(objForm);

            XmlDocument objProgramsDoc = XmlManager.Instance.Load("programs.xml");
            XmlNode? objXmlProgram = objProgramsDoc.SelectSingleNode(
                $"/chummer/programs/program[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlProgram?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.ComplexForm, strName.Trim());
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.ComplexForm, strName.Trim(), strExtra, "1");

            if (blnCareer && int.TryParse(Karma, out int intKarma))
                Karma = (intKarma - intCost).ToString(CultureInfo.InvariantCulture);
            else if (blnEnforceCreationBudget)
            {
                if (blnKarmaBuild)
                    Karma = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
                else
                    Bp = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Program Options offered for a Complex Form of the given category - ported
        /// from frmSelectProgramOption.cs's Load handler: an option from programs.xml's
        /// /chummer/options/option is offered if it has no &lt;programtypes&gt; restriction, or one
        /// of its &lt;programtype&gt; entries matches <paramref name="strCategory"/> (the Complex
        /// Form's own &lt;category&gt;, e.g. "Common Use"/"Hacking").</summary>
        public IReadOnlyList<string> GetComplexFormOptionChoices(string strCategory)
        {
            XmlDocument objProgramsDoc = XmlManager.Instance.Load("programs.xml");
            var lstOptions = new List<string>();
            foreach (XmlNode objOption in objProgramsDoc.SelectNodes("/chummer/options/option")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objOption["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(strName))
                    continue;

                XmlNodeList? objTypeNodes = objOption.SelectNodes("programtypes/programtype");
                bool blnAdd = objTypeNodes == null || objTypeNodes.Count == 0
                    || objTypeNodes.Cast<XmlNode>().Any(objType => objType.InnerText == strCategory);
                if (blnAdd)
                    lstOptions.Add(strName);
            }

            return lstOptions;
        }

        /// <summary>Adds a Program Option to an existing Complex Form - ported from
        /// clsUnique.cs's TechProgramOption.Create. Rating starts at 1 unless the option's
        /// &lt;maxrating&gt; is explicitly "0" (legacy defaults maxrating to 6 otherwise). Not
        /// ported: legacy's rare per-option
        /// &lt;bonus&gt; application (only 1 of 25 real programs.xml options has one) and the
        /// Complex Form capacity gating in frmCreate.cs (moot here since saved Complex Forms are
        /// always Rating 1, i.e. always CalculatedCapacity 0 under the "Rating/2" formula).</summary>
        public bool AddComplexFormOption(string strGuid, string strOptionName)
        {
            if (string.IsNullOrWhiteSpace(strGuid) || string.IsNullOrWhiteSpace(strOptionName))
                return false;

            var objNodes = Document.SelectNodes("/character/techprograms/techprogram");
            if (objNodes == null)
                return false;

            XmlNode? objForm = objNodes.Cast<XmlNode>().FirstOrDefault(
                n => string.Equals(GetValue(n, "guid", string.Empty), strGuid, StringComparison.Ordinal));
            if (objForm is not XmlElement objFormElement)
                return false;

            XmlDocument objProgramsDoc = XmlManager.Instance.Load("programs.xml");
            XmlNode? objXmlOption = objProgramsDoc.SelectSingleNode(
                $"/chummer/options/option[name = '{strOptionName.Trim()}']");
            if (objXmlOption == null)
                return false;

            var objOptions = objFormElement.SelectSingleNode("programoptions");
            if (objOptions is not XmlElement objOptionsElement)
            {
                objOptionsElement = Document.CreateElement("programoptions");
                objFormElement.AppendChild(objOptionsElement);
            }

            // Legacy defaults maxrating to 6 (>0) unless the node's InnerText is non-empty and says
            // otherwise, so an explicit "0" is the only value that keeps the starting Rating at 0.
            string strMaxRating = objXmlOption["maxrating"]?.InnerText ?? string.Empty;
            var objOptionElement = Document.CreateElement("programoption");
            AppendElement(objOptionElement, "name", strOptionName.Trim());
            AppendElement(objOptionElement, "rating", strMaxRating == "0" ? "0" : "1");
            AppendElement(objOptionElement, "source", objXmlOption["source"]?.InnerText ?? string.Empty);
            AppendElement(objOptionElement, "page", objXmlOption["page"]?.InnerText ?? string.Empty);
            objOptionsElement.AppendChild(objOptionElement);

            Changed?.Invoke();
            return true;
        }

        public bool RemoveComplexForm(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            var objNodes = Document.SelectNodes("/character/techprograms/techprogram");
            if (objNodes == null)
                return false;

            foreach (XmlNode objForm in objNodes)
            {
                if (!string.Equals(GetValue(objForm, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    continue;

                string strName = GetValue(objForm, "name", string.Empty);
                int intCreationRefund = ParseInteger(GetValue(objForm, "creationcost", "0"));
                objForm.ParentNode?.RemoveChild(objForm);
                RemoveBonusImprovements(ImprovementSource.ComplexForm, strName);
                if (!Created && StartingBuildPoints > 0 && intCreationRefund > 0)
                {
                    bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                    int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
                    if (blnKarmaBuild)
                        Karma = (intPool + intCreationRefund).ToString(CultureInfo.InvariantCulture);
                    else
                        Bp = (intPool + intCreationRefund).ToString(CultureInfo.InvariantCulture);
                }
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        public bool SetComplexFormNotes(string strGuid, string strNotes)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;
            XmlNode? objForm = Document.SelectSingleNode(
                $"/character/techprograms/techprogram[guid = '{strGuid}']");
            if (objForm == null)
                return false;
            SetChildValue(objForm, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>A Critter/Free Spirit's innate powers - ported (read-only) from clsUnique.cs's
        /// CritterPower class. No add/remove/picker yet, only read+print support.</summary>
    }
}
