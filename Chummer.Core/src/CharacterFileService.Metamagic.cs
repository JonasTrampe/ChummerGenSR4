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
        private void RefreshRatingScaledMetamagicImprovements(int intNewGrade)
        {
            if (Metamagics.Count == 0)
                return;

            XmlDocument objMetamagicsDoc = XmlManager.Instance.Load("metamagic.xml");
            foreach (CharacterMetamagicData objMetamagic in Metamagics)
            {
                XmlNode? objXmlMetamagic = objMetamagicsDoc.SelectSingleNode(
                    $"/chummer/metamagics/metamagic[name = '{objMetamagic.Name}']");
                XmlNode? objXmlBonus = objXmlMetamagic?.SelectSingleNode("bonus");
                if (objXmlBonus == null || !objXmlBonus.InnerXml.Contains("Rating"))
                    continue;

                RemoveBonusImprovements(ImprovementSource.Metamagic, objMetamagic.Name);
                ApplyBonus(objXmlBonus, ImprovementSource.Metamagic, objMetamagic.Name, intNewGrade.ToString());
            }
        }

        private IReadOnlyList<CharacterMetamagicData>? _cachedMetamagics;
        public IReadOnlyList<CharacterMetamagicData> Metamagics
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedMetamagics short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedMetamagics ??= ReadMetamagics();
            }
        }

        /// <summary>A Technomancer's Complex Forms - ported from clsUnique.cs's TechProgram class,
        /// data drawn from programs.xml (see frmSelectProgram.cs).</summary>
        public bool MetamagicRequiresTextSelection(string strName) =>
            FindBonusChild("metamagic.xml", "metamagics", "metamagic", strName, "selecttext") != null;

        /// <summary>Ported from clsUnique.cs's Metamagic.Create/Save, also applying the
        /// metamagic's own rules-data &lt;bonus&gt; block (see <see cref="ApplyBonus"/>) and,
        /// when the bonus is a &lt;selecttext&gt; node (see <see
        /// cref="MetamagicRequiresTextSelection"/>), <paramref name="strSelected"/> becomes a Text
        /// Improvement (see <see cref="ApplySelectedImprovement"/>).</summary>
        public void AddMetamagic(string strName, string strSource, string strPage, string strSelected = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A metamagic name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objMetamagics = objRoot.SelectSingleNode("metamagics");
            if (objMetamagics == null)
            {
                objMetamagics = Document.CreateElement("metamagics");
                objRoot.AppendChild(objMetamagics);
            }

            var objMetamagic = Document.CreateElement("metamagic");
            AppendElement(objMetamagic, "guid", Guid.NewGuid().ToString());
            AppendElement(objMetamagic, "name", strName.Trim());
            AppendElement(objMetamagic, "source", strSource);
            AppendElement(objMetamagic, "paidwithkarma", "False");
            AppendElement(objMetamagic, "page", strPage);
            AppendElement(objMetamagic, "improvementsource", "Metamagic");
            objMetamagics.AppendChild(objMetamagic);

            XmlDocument objMetamagicsDoc = XmlManager.Instance.Load("metamagic.xml");
            XmlNode? objXmlMetamagic = objMetamagicsDoc.SelectSingleNode(
                $"/chummer/metamagics/metamagic[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlMetamagic?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.Metamagic, strName.Trim());
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.Metamagic, strName.Trim(), strSelected, "1");

            Changed?.Invoke();
        }

        public bool RemoveMetamagic(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            var objNodes = Document.SelectNodes("/character/metamagics/metamagic");
            if (objNodes == null)
                return false;

            foreach (XmlNode objMetamagic in objNodes)
            {
                if (!string.Equals(GetValue(objMetamagic, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    continue;

                string strName = GetValue(objMetamagic, "name", string.Empty);
                objMetamagic.ParentNode?.RemoveChild(objMetamagic);
                RemoveBonusImprovements(ImprovementSource.Metamagic, strName);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Updates a Metamagic's free-form notes using its persistent character GUID.</summary>
        public bool SetMetamagicNotes(string strGuid, string strNotes)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;
            XmlNode? objMetamagic = Document.SelectSingleNode(
                $"/character/metamagics/metamagic[guid = '{strGuid}']");
            if (objMetamagic == null)
                return false;

            SetChildValue(objMetamagic, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private IReadOnlyList<CharacterMetamagicData> ReadMetamagics()
        {
            var lstMetamagics = new List<CharacterMetamagicData>();
            var objNodes = Document.SelectNodes("/character/metamagics/metamagic");
            if (objNodes == null) return lstMetamagics;
            foreach (XmlNode objNode in objNodes)
                lstMetamagics.Add(new CharacterMetamagicData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "source", string.Empty),
                    GetValue(objNode, "page", string.Empty), GetValue(objNode, "notes", string.Empty)));
            return lstMetamagics;
        }

    }
}
