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
        public IReadOnlyList<CharacterContactData> Contacts => ReadContacts(blnEnemies: false);

        public IReadOnlyList<CharacterContactData> Enemies => ReadContacts(blnEnemies: true);

        /// <summary>Pets are persisted as Contact entries with <c>type=Pet</c>, matching the legacy app.</summary>
        private IReadOnlyList<CharacterContactData>? _cachedPets;
        public IReadOnlyList<CharacterContactData> Pets
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedPets short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedPets ??= ReadPets();
            }
        }

        public bool AddContact(string strName, string strConnection, string strLoyalty, bool blnEnemy,
            string strType = "")
        {
            int intPreviousCreationCost = GetCreationContactCost();
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objContacts = objRoot.SelectSingleNode("contacts");
            if (objContacts == null)
            {
                objContacts = Document.CreateElement("contacts");
                objRoot.AppendChild(objContacts);
            }

            var objContact = Document.CreateElement("contact");
            AppendElement(objContact, "name", strName.Trim());
            AppendElement(objContact, "connection", strConnection);
            AppendElement(objContact, "loyalty", strLoyalty);
            AppendElement(objContact, "membership", "0");
            AppendElement(objContact, "areaofinfluence", "0");
            AppendElement(objContact, "magicalresources", "0");
            AppendElement(objContact, "matrixresources", "0");
            AppendElement(objContact, "type", string.IsNullOrEmpty(strType) ? (blnEnemy ? "Enemy" : "Contact") : strType);
            AppendElement(objContact, "file", string.Empty);
            AppendElement(objContact, "notes", string.Empty);
            AppendElement(objContact, "groupname", string.Empty);
            AppendElement(objContact, "colour", "0");
            AppendElement(objContact, "free", "False");
            objContacts.AppendChild(objContact);
            if (!ApplyCreationContactBudget(intPreviousCreationCost))
            {
                objContacts.RemoveChild(objContact);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a Pet contact, the same representation used by the legacy PetControl.</summary>
        public void AddPet(string strName) => AddContact(strName, "0", "0", blnEnemy: false, strType: "Pet");

        public bool UpdateContact(int intContactId, string strName, string strConnection, string strLoyalty)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            int intPreviousCreationCost = GetCreationContactCost();
            string strPreviousName = GetValue(objNode, "name", string.Empty);
            string strPreviousConnection = GetValue(objNode, "connection", "0");
            string strPreviousLoyalty = GetValue(objNode, "loyalty", "0");
            SetChildValue(objNode, "name", strName);
            SetChildValue(objNode, "connection", strConnection);
            SetChildValue(objNode, "loyalty", strLoyalty);
            if (!ApplyCreationContactBudget(intPreviousCreationCost))
            {
                SetChildValue(objNode, "name", strPreviousName);
                SetChildValue(objNode, "connection", strPreviousConnection);
                SetChildValue(objNode, "loyalty", strPreviousLoyalty);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public bool RemoveContact(int intContactId)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode?.ParentNode == null)
                return false;

            int intPreviousCreationCost = GetCreationContactCost();
            XmlNode objParent = objNode.ParentNode;
            XmlNode? objNextSibling = objNode.NextSibling;
            objParent.RemoveChild(objNode);
            if (!ApplyCreationContactBudget(intPreviousCreationCost))
            {
                if (objNextSibling == null)
                    objParent.AppendChild(objNode);
                else
                    objParent.InsertBefore(objNode, objNextSibling);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        public bool UpdateContactNotes(int intContactId, string strNotes)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "notes", strNotes);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Associates a Contact/Pet with another saved character file.</summary>
        public bool UpdateContactFile(int intContactId, string strFileName, string strRelativeFileName)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "file", strFileName);
            SetChildValue(objNode, "relative", strRelativeFileName);
            Changed?.Invoke();
            return true;
        }

        public bool SetContactFree(int intContactId, bool blnFree)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            int intPreviousCreationCost = GetCreationContactCost();
            string strPrevious = GetValue(objNode, "free", "False");
            SetChildValue(objNode, "free", blnFree ? "True" : "False");
            if (!ApplyCreationContactBudget(intPreviousCreationCost))
            {
                SetChildValue(objNode, "free", strPrevious);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sets a Group contact's profession label and the four modifiers that sum into
        /// its Group Rating - ported from frmSelectContactConnection.cs.</summary>
        public bool UpdateContactGroup(int intContactId, string strGroupName, int intMembership,
            int intAreaOfInfluence, int intMagicalResources, int intMatrixResources)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            int intPreviousCreationCost = GetCreationContactCost();
            string strPreviousGroupName = GetValue(objNode, "groupname", string.Empty);
            string strPreviousMembership = GetValue(objNode, "membership", "0");
            string strPreviousArea = GetValue(objNode, "areaofinfluence", "0");
            string strPreviousMagical = GetValue(objNode, "magicalresources", "0");
            string strPreviousMatrix = GetValue(objNode, "matrixresources", "0");
            SetChildValue(objNode, "groupname", strGroupName);
            SetChildValue(objNode, "membership", intMembership.ToString());
            SetChildValue(objNode, "areaofinfluence", intAreaOfInfluence.ToString());
            SetChildValue(objNode, "magicalresources", intMagicalResources.ToString());
            SetChildValue(objNode, "matrixresources", intMatrixResources.ToString());
            if (!ApplyCreationContactBudget(intPreviousCreationCost))
            {
                SetChildValue(objNode, "groupname", strPreviousGroupName);
                SetChildValue(objNode, "membership", strPreviousMembership);
                SetChildValue(objNode, "areaofinfluence", strPreviousArea);
                SetChildValue(objNode, "magicalresources", strPreviousMagical);
                SetChildValue(objNode, "matrixresources", strPreviousMatrix);
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        private int GetCreationContactCost()
            => !Created && StartingBuildPoints > 0 ? ContactPointsUsed : 0;

        /// <summary>Applies the change in the complete contact/enemy cost after a tentative
        /// mutation. Free-contact allowances can shift which individual entry is free, so the
        /// aggregate before/after delta is the only stable creation-budget source of truth.</summary>
        private bool ApplyCreationContactBudget(int intPreviousCost)
        {
            if (Created || StartingBuildPoints <= 0)
                return true;

            int intDelta = ContactPointsUsed - intPreviousCost;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
            if (intDelta > intPool)
                return false;

            if (blnKarmaBuild)
                Karma = (intPool - intDelta).ToString(CultureInfo.InvariantCulture);
            else
                Bp = (intPool - intDelta).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>Karma/BP spent at chargen on Contacts, minus what Enemies refund, after the
        /// FreeContacts (CHA x multiplier) and FreeContactsFlat house rules - ported from
        /// frmCreate.cs. The Avalonia creation flow uses this aggregate as the authoritative
        /// before/after delta when mutating contacts, since the free allowance is shared across
        /// entries and cannot be persisted as a fixed per-contact cost.</summary>
        public int ContactPointsUsed
        {
            get
            {
                var objOptions = GetCharacterOptions();
                bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                int intRate = blnKarmaBuild ? objOptions.KarmaContact : objOptions.BpContact;

                int intUsed = Contacts.Where(c => !c.Free)
                    .Sum(c => (ParseInteger(c.Connection) + c.GroupRating + ParseInteger(c.Loyalty)) * intRate);
                int intRefund = Enemies.Where(c => !c.Free)
                    .Sum(c => (ParseInteger(c.Connection) + c.GroupRating + ParseInteger(c.Loyalty)) * intRate);
                intUsed -= intRefund;

                if (objOptions.FreeContacts)
                {
                    int intFreePoints = GetAttributeInt("CHA") * objOptions.FreeContactsMultiplier;
                    if (blnKarmaBuild)
                        intFreePoints *= objOptions.KarmaContact;
                    intUsed = Math.Max(0, intUsed - intFreePoints);
                }

                if (objOptions.FreeContactsFlat)
                {
                    int intFreePoints = objOptions.FreeContactsFlatNumber;
                    if (blnKarmaBuild)
                        intFreePoints *= objOptions.KarmaContact;
                    intUsed = Math.Max(0, intUsed - intFreePoints);
                }

                return intUsed;
            }
        }

        private IReadOnlyList<CharacterContactData> ReadContacts(bool blnEnemies)
        {
            var lstContacts = new List<CharacterContactData>();
            var objNodes = Document.SelectNodes("/character/contacts/contact");
            if (objNodes == null) return lstContacts;
            int intContactId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                var blnIsEnemy = GetValue(objNode, "type", "Contact") == "Enemy";
                if (blnIsEnemy == blnEnemies)
                {
                    lstContacts.Add(new CharacterContactData(intContactId,
                        GetValue(objNode, "name", string.Empty),
                        GetValue(objNode, "connection", "0"),
                        GetValue(objNode, "loyalty", "0"),
                        blnIsEnemy,
                        GetValue(objNode, "notes", string.Empty),
                        GetValue(objNode, "free", "False") == "True",
                        GetValue(objNode, "groupname", string.Empty),
                        ParseInteger(GetValue(objNode, "membership", "0")),
                        ParseInteger(GetValue(objNode, "areaofinfluence", "0")),
                        ParseInteger(GetValue(objNode, "magicalresources", "0")),
                        ParseInteger(GetValue(objNode, "matrixresources", "0")),
                        GetValue(objNode, "file", string.Empty),
                        GetValue(objNode, "relative", string.Empty)));
                }

                intContactId++;
            }

            return lstContacts;
        }

        private IReadOnlyList<CharacterContactData> ReadPets()
        {
            var lstPets = new List<CharacterContactData>();
            var objNodes = Document.SelectNodes("/character/contacts/contact");
            if (objNodes == null) return lstPets;
            int intContactId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "type", "Contact") == "Pet")
                {
                    lstPets.Add(new CharacterContactData(intContactId, GetValue(objNode, "name", string.Empty),
                        GetValue(objNode, "connection", "0"), GetValue(objNode, "loyalty", "0"), false,
                        GetValue(objNode, "notes", string.Empty), GetValue(objNode, "free", "False") == "True",
                        GetValue(objNode, "groupname", string.Empty), ParseInteger(GetValue(objNode, "membership", "0")),
                        ParseInteger(GetValue(objNode, "areaofinfluence", "0")), ParseInteger(GetValue(objNode, "magicalresources", "0")),
                        ParseInteger(GetValue(objNode, "matrixresources", "0")),
                        GetValue(objNode, "file", string.Empty), GetValue(objNode, "relative", string.Empty)));
                }
                intContactId++;
            }
            return lstPets;
        }

        private XmlNode? GetContactNode(int intContactId)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/contacts/contact");
            return objNodes != null && intContactId >= 0 && intContactId < objNodes.Count
                ? objNodes[intContactId]
                : null;
        }

    }
}
