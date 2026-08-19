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
        #region Clipboard

        /// <summary>Builds a stable, self-locating absolute XPath for an already-resolved node
        /// (found via one of this class's own By-Id lookups), for handing to
        /// <see cref="CharacterClipboard"/>.Copy - avoids depending on a persisted &lt;guid&gt;
        /// child that not every collection's items actually carry (Armor/Cyberware/Lifestyle are
        /// identified by a position-based Id, not a guid).</summary>
        private static string GetNodePath(XmlNode objNode)
        {
            if (objNode.ParentNode == null || objNode.ParentNode.NodeType == XmlNodeType.Document)
                return "/" + objNode.Name;

            int intPosition = 1;
            for (XmlNode? objSibling = objNode.PreviousSibling; objSibling != null; objSibling = objSibling.PreviousSibling)
                if (objSibling.Name == objNode.Name)
                    intPosition++;
            return GetNodePath(objNode.ParentNode) + "/" + objNode.Name + "[" + intPosition + "]";
        }

        private bool CopyNode(XmlNode? objNode, ClipboardContentType eType)
            => objNode != null && CharacterClipboard.Instance.Copy(this, GetNodePath(objNode), eType);

        private bool PasteIntoCollection(string strContainerName, ClipboardContentType eType)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            if (objRoot.SelectSingleNode(strContainerName) == null)
                objRoot.AppendChild(Document.CreateElement(strContainerName));
            return CharacterClipboard.Instance.Paste(this, "/character/" + strContainerName, eType);
        }

        /// <summary>Whether the shared clipboard currently holds an item of the given type - lets a
        /// host enable/disable its Paste button per collection, mirroring frmCreate.cs's
        /// RefreshPasteStatus.</summary>
        public bool HasClipboardItemOfType(ClipboardContentType eType)
            => CharacterClipboard.Instance.Item?.ContentType == eType;

        public bool CopyGear(int intGearId) => CopyNode(GetGearNodeById(intGearId), ClipboardContentType.Gear);
        public bool PasteGear() => PasteIntoCollection("gears", ClipboardContentType.Gear);

        public bool CopyWeapon(int intWeaponId) => CopyNode(GetWeaponNodeById(intWeaponId), ClipboardContentType.Weapon);
        public bool PasteWeapon() => PasteIntoCollection("weapons", ClipboardContentType.Weapon);

        public bool CopyArmor(int intArmorId) => CopyNode(GetArmorNodeById(intArmorId), ClipboardContentType.Armor);
        public bool PasteArmor() => PasteIntoCollection("armors", ClipboardContentType.Armor);

        public bool CopyCyberware(int intCyberwareId, bool blnBioware) => CopyNode(GetCyberwareNodeById(intCyberwareId),
            blnBioware ? ClipboardContentType.Bioware : ClipboardContentType.Cyberware);
        public bool PasteCyberware(bool blnBioware) => PasteIntoCollection(blnBioware ? "biowares" : "cyberwares",
            blnBioware ? ClipboardContentType.Bioware : ClipboardContentType.Cyberware);

        public bool CopyVehicle(Guid guiVehicleId) => CopyNode(GetVehicleNode(guiVehicleId), ClipboardContentType.Vehicle);
        public bool PasteVehicle() => PasteIntoCollection("vehicles", ClipboardContentType.Vehicle);

        public bool CopyLifestyle(int intLifestyleId) => CopyNode(GetLifestyleNodeById(intLifestyleId), ClipboardContentType.Lifestyle);
        public bool PasteLifestyle() => PasteIntoCollection("lifestyles", ClipboardContentType.Lifestyle);

        #endregion
    }
}
