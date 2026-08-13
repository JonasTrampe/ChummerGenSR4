using System;
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
        public CharacterClipboardItem? Item { get; private set; }

        public bool Copy(CharacterDocument objCharacter, string strItemXPath, ClipboardContentType objType)
        {
            if (objCharacter == null || string.IsNullOrWhiteSpace(strItemXPath))
                return false;
            XmlNode? objNode = objCharacter.Document.SelectSingleNode(strItemXPath);
            if (objNode == null || objNode.NodeType != XmlNodeType.Element)
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
            XmlNode? objTarget = objCharacter.Document.SelectSingleNode(strTargetCollectionXPath);
            if (objTarget == null || objTarget.NodeType != XmlNodeType.Element)
                return false;
            var objSource = new XmlDocument();
            objSource.LoadXml(Item.Xml);
            objTarget.AppendChild(objCharacter.Document.ImportNode(objSource.DocumentElement!, true));
            objCharacter.NotifyChanged();
            return true;
        }

        public void Clear() => Item = null;
    }
}
