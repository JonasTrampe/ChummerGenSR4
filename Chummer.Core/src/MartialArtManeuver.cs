using System;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     A Martial Art Maneuver.
    /// </summary>
    public class MartialArtManeuver
    {
        private Guid _guiId;
        private string _strPage = "";

        #region Constructor, Create, Save, Load, and Print Methods

        public MartialArtManeuver()
        {
            // Create the GUID for the new Martial Art Maneuver.
            _guiId = Guid.NewGuid();
        }

        /// Create a Martial Art Maneuver from an XmlNode.
        /// <param name="objXmlManeuverNode">XmlNode to create the object from.</param>
        public void Create(XmlNode objXmlManeuverNode)
        {
            Name = objXmlManeuverNode["name"].InnerText;
            Source = objXmlManeuverNode["source"].InnerText;
            _strPage = objXmlManeuverNode["page"].InnerText;
        }

        /// <summary>
        ///     Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("martialartmaneuver");
            objWriter.WriteElementString("guid", _guiId.ToString());
            objWriter.WriteElementString("name", Name);
            objWriter.WriteElementString("source", Source);
            objWriter.WriteElementString("page", _strPage);
            objWriter.WriteElementString("notes", Notes);
            objWriter.WriteEndElement();
        }

        /// <summary>
        ///     Load the Martial Art Maneuver from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
            _guiId = Guid.Parse(objNode["guid"].InnerText);
            Name = objNode["name"].InnerText;
            Source = objNode["source"].InnerText;
            _strPage = objNode["page"].InnerText;
            try
            {
                Notes = objNode["notes"].InnerText;
            }
            catch
            {
            }
        }

        /// <summary>
        ///     Print the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Print(XmlTextWriter objWriter, string strDisplayName, string strSource, string strPage,
            bool blnPrintNotes)
        {
            objWriter.WriteStartElement("martialartmaneuver");
            objWriter.WriteElementString("name", strDisplayName);
            objWriter.WriteElementString("source", strSource);
            objWriter.WriteElementString("page", strPage);
            if (blnPrintNotes)
                objWriter.WriteElementString("notes", Notes);
            objWriter.WriteEndElement();
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Internal identifier which will be used to identify this Martial Art Maneuver in the Improvement system.
        /// </summary>
        public string InternalId => _guiId.ToString();

        /// <summary>
        ///     Name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     The name of the object as it should be displayed on printouts (translated name only).
        /// </summary>
        public string DisplayNameShort
        {
            get
            {
                var strReturn = Name;
                // Get the translated name if applicable.
                return Name;
            }
        }

        /// <summary>
        ///     The name of the object as it should be displayed in lists. Name (Extra).
        /// </summary>
        public string DisplayName
        {
            get
            {
                var strReturn = DisplayNameShort;

                return strReturn;
            }
        }

        /// <summary>
        ///     Sourcebook.
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>
        ///     Page.
        /// </summary>
        public string Page
        {
            get
            {
                var strReturn = _strPage;
                // Get the translated name if applicable.
                return Name;
            }
            set => _strPage = value;
        }

        /// <summary>
        ///     Notes.
        /// </summary>
        public string Notes { get; set; } = "";

        #endregion
    }
}