using System;
using System.Drawing;
using System.IO;
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
            Name = GetRequiredValue(objNode, "name");
            Connection = Convert.ToInt32(GetRequiredValue(objNode, "connection"));
            Loyalty = Convert.ToInt32(GetRequiredValue(objNode, "loyalty"));
            Membership = GetOptionalIntValue(objNode, "membership");
            AreaOfInfluence = GetOptionalIntValue(objNode, "areaofinfluence");
            MagicalResources = GetOptionalIntValue(objNode, "magicalresources");
            MatrixResources = GetOptionalIntValue(objNode, "matrixresources");
            EntityType = ConvertToContactType(GetRequiredValue(objNode, "type"));
            FileName = objNode["file"]?.InnerText ?? string.Empty;
            RelativeFileName = objNode["relative"]?.InnerText ?? string.Empty;
            Notes = objNode["notes"]?.InnerText ?? string.Empty;
            GroupName = objNode["groupname"]?.InnerText ?? string.Empty;

            if (int.TryParse(objNode["colour"]?.InnerText, out int intColour))
                _objColour = Color.FromArgb(intColour);

            Free = bool.TryParse(objNode["free"]?.InnerText, out bool blnFree) && blnFree;
        }

        private static string GetRequiredValue(XmlNode objNode, string strName)
        {
            return objNode[strName]?.InnerText
                   ?? throw new InvalidDataException("Contact is missing the required '" + strName + "' value.");
        }

        private static int GetOptionalIntValue(XmlNode objNode, string strName)
        {
            return int.TryParse(objNode[strName]?.InnerText, out int intValue) ? intValue : 0;
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
