using System;
using System.Drawing;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     A Contact or Enemy.
    /// </summary>
    public class Contact
    {
        private Color _objColour;

        #region Helper Methods

        /// <summary>
        ///     Convert a string to a ContactType.
        /// </summary>
        /// <param name="strValue">String value to convert.</param>
        public ContactType ConvertToContactType(string strValue)
        {
            switch (strValue)
            {
                case "Contact":
                    return ContactType.Contact;
                case "Pet":
                    return ContactType.Pet;
                default:
                    return ContactType.Enemy;
            }
        }

        #endregion

        #region Constructor, Save, Load, and Print Methods

        /// <summary>
        ///     Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("contact");
            objWriter.WriteElementString("name", Name);
            objWriter.WriteElementString("connection", Connection.ToString());
            objWriter.WriteElementString("loyalty", Loyalty.ToString());
            objWriter.WriteElementString("membership", Membership.ToString());
            objWriter.WriteElementString("areaofinfluence", AreaOfInfluence.ToString());
            objWriter.WriteElementString("magicalresources", MagicalResources.ToString());
            objWriter.WriteElementString("matrixresources", MatrixResources.ToString());
            objWriter.WriteElementString("type", EntityType.ToString());
            objWriter.WriteElementString("file", FileName);
            objWriter.WriteElementString("relative", RelativeFileName);
            objWriter.WriteElementString("notes", Notes);
            objWriter.WriteElementString("groupname", GroupName);
            objWriter.WriteElementString("colour", _objColour.ToArgb().ToString());
            objWriter.WriteElementString("free", Free.ToString());
            objWriter.WriteEndElement();
        }

        /// <summary>
        ///     Load the Contact from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
            Name = objNode["name"].InnerText;
            Connection = Convert.ToInt32(objNode["connection"].InnerText);
            Loyalty = Convert.ToInt32(objNode["loyalty"].InnerText);
            try
            {
                Membership = Convert.ToInt32(objNode["membership"].InnerText);
                AreaOfInfluence = Convert.ToInt32(objNode["areaofinfluence"].InnerText);
                MagicalResources = Convert.ToInt32(objNode["magicalresources"].InnerText);
                MatrixResources = Convert.ToInt32(objNode["matrixresources"].InnerText);
            }
            catch
            {
            }

            EntityType = ConvertToContactType(objNode["type"].InnerText);
            try
            {
                FileName = objNode["file"].InnerText;
            }
            catch
            {
            }

            try
            {
                RelativeFileName = objNode["relative"].InnerText;
            }
            catch
            {
            }

            try
            {
                Notes = objNode["notes"].InnerText;
            }
            catch
            {
            }

            try
            {
                GroupName = objNode["groupname"].InnerText;
            }
            catch
            {
            }

            try
            {
                _objColour = Color.FromArgb(Convert.ToInt32(objNode["colour"].InnerText));
            }
            catch
            {
            }

            try
            {
                Free = Convert.ToBoolean(objNode["free"].InnerText);
            }
            catch
            {
            }
        }

        /// <summary>
        ///     Print the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Print(XmlTextWriter objWriter, string strType, bool blnPrintNotes)
        {
            objWriter.WriteStartElement("contact");
            objWriter.WriteElementString("name", Name);
            if (Group == 0)
                objWriter.WriteElementString("connection", Connection.ToString());
            else
                objWriter.WriteElementString("connection", Connection + " (" + Group + ")");
            objWriter.WriteElementString("loyalty", Loyalty.ToString());
            objWriter.WriteElementString("type", strType);
            if (blnPrintNotes)
                objWriter.WriteElementString("notes", Notes);
            objWriter.WriteEndElement();
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Name of the Contact.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     Contact's Connection Rating.
        /// </summary>
        public int Connection { get; set; } = 1;

        /// <summary>
        ///     Contact's Loyalty Rating (or Enemy's Incidence Rating).
        /// </summary>
        public int Loyalty { get; set; } = 1;

        /// <summary>
        ///     Contact's Group Rating (applies to Contacts only).
        /// </summary>
        public int Group => Membership + AreaOfInfluence + MagicalResources + MatrixResources;

        /// <summary>
        ///     Connection Modifier: Membership.
        /// </summary>
        public int Membership { get; set; }

        /// <summary>
        ///     Connection Modifier: Area of Influence.
        /// </summary>
        public int AreaOfInfluence { get; set; }

        /// <summary>
        ///     Connection Modifier: Magical Resources.
        /// </summary>
        public int MagicalResources { get; set; }

        /// <summary>
        ///     Connection Modifier: Matrix Resources:
        /// </summary>
        public int MatrixResources { get; set; }

        /// <summary>
        ///     The Contact's type, either Contact or Enemy.
        /// </summary>
        public ContactType EntityType { get; set; } = ContactType.Contact;

        /// <summary>
        ///     Name of the save file for this Contact.
        /// </summary>
        public string FileName { get; set; } = "";

        /// <summary>
        ///     Relative path to the save file.
        /// </summary>
        public string RelativeFileName { get; set; } = "";

        /// <summary>
        ///     Notes.
        /// </summary>
        public string Notes { get; set; } = "";

        /// <summary>
        ///     Group Name.
        /// </summary>
        public string GroupName { get; set; } = "";

        /// <summary>
        ///     Contact Colour.
        /// </summary>
        public Color Colour
        {
            get => _objColour;
            set => _objColour = value;
        }

        /// <summary>
        ///     Whether or not this is a free contact.
        /// </summary>
        public bool Free { get; set; }

        #endregion
    }
}

/// <summary>
/// A Critter Power.
/// </summary>