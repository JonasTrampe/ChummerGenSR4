using System;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>Immutable, in-memory clipboard item copied from one character collection.</summary>
    public sealed class CharacterClipboardItem
    {
        internal CharacterClipboardItem(ClipboardContentType objType, string strName, string strXml)
        {
            ContentType = objType;
            Name = strName;
            Xml = strXml;
        }

        public ClipboardContentType ContentType { get; }
        public string Name { get; }
        internal string Xml { get; }
    }

    /// <summary>Platform-neutral clipboard for character collection items. Hosts supply the
    /// selected-item and target-collection XPath, keeping paste scoped to a known collection.</summary>
    public sealed class CharacterClipboard
    {
        // Ported from frmCreate.cs's mnuEditPaste_Click: legacy re-validates the clipboard's actual
        // XML root element (e.g. Clipboard.SelectSingleNode("/character/lifestyle")) as a second,
        // independent check beyond the ClipboardContentType tag comparison - catching a caller bug
        // that tagged the wrong type at Copy time before mismatched XML gets imported. Commlink/
        // OperatingSystem are saved as plain <gear> (category distinguishes them, not the element
        // name) and Cyberware/Bioware both save as <cyberware> (their parent container differs
        // instead), matching this port's unified AddCyberware(blnBioware)/AddGear shape.
        private static readonly Dictionary<ClipboardContentType, string> s_dicExpectedRootElement = new()
        {
            [ClipboardContentType.Gear] = "gear",
            [ClipboardContentType.Commlink] = "gear",
            [ClipboardContentType.OperatingSystem] = "gear",
            [ClipboardContentType.Cyberware] = "cyberware",
            [ClipboardContentType.Bioware] = "cyberware",
            [ClipboardContentType.Armor] = "armor",
            [ClipboardContentType.Weapon] = "weapon",
            [ClipboardContentType.Vehicle] = "vehicle",
            [ClipboardContentType.Lifestyle] = "lifestyle",
        };

        // Ported from clsOptions.cs's GlobalOptions.Instance.Clipboard/.ClipboardContentType: a
        // single, process-wide clipboard shared by every open character window, not one per
        // character - so copying from one character and pasting into another works, same as legacy.
        public static CharacterClipboard Instance { get; } = new();

        public CharacterClipboardItem? Item { get; private set; }

        public bool Copy(CharacterDocument objCharacter, string strItemXPath, ClipboardContentType objType)
        {
            if (objCharacter == null || string.IsNullOrWhiteSpace(strItemXPath))
                return false;
            if (!s_dicExpectedRootElement.TryGetValue(objType, out string? strExpectedTag))
                return false;
            XmlNode? objNode = objCharacter.Document.SelectSingleNode(strItemXPath);
            if (objNode == null || objNode.NodeType != XmlNodeType.Element || objNode.Name != strExpectedTag)
                return false;
            Item = new CharacterClipboardItem(objType, objNode["name"]?.InnerText ?? objNode.Name, objNode.OuterXml);
            return true;
        }

        public bool Paste(CharacterDocument objCharacter, string strTargetCollectionXPath,
            ClipboardContentType objExpectedType)
        {
            if (objCharacter == null || Item == null || Item.ContentType != objExpectedType
                || string.IsNullOrWhiteSpace(strTargetCollectionXPath))
                return false;
            if (!s_dicExpectedRootElement.TryGetValue(objExpectedType, out string? strExpectedTag))
                return false;
            XmlNode? objTarget = objCharacter.Document.SelectSingleNode(strTargetCollectionXPath);
            if (objTarget == null || objTarget.NodeType != XmlNodeType.Element)
                return false;
            var objSource = new XmlDocument();
            objSource.LoadXml(Item.Xml);
            if (objSource.DocumentElement == null || objSource.DocumentElement.Name != strExpectedTag)
                return false;
            objTarget.AppendChild(objCharacter.Document.ImportNode(objSource.DocumentElement, true));
            objCharacter.NotifyChanged();
            return true;
        }

        public void Clear() => Item = null;
    }
}
