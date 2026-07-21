using System;
using System.IO;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     A Focus.
    /// </summary>
    public class Focus
    {
        private Guid _guiGearId;
        private Guid _guiId;

        #region Constructor, Create, Save, and Load Methods

        public Focus()
        {
            // Create the GUID for the new Focus.
            _guiId = Guid.NewGuid();
        }

        /// <summary>
        ///     Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("focus");
            objWriter.WriteElementString("guid", _guiId.ToString());
            objWriter.WriteElementString("name", Name);
            objWriter.WriteElementString("gearid", _guiGearId.ToString());
            objWriter.WriteElementString("rating", Rating.ToString());
            objWriter.WriteEndElement();
        }

        /// <summary>
        ///     Load the Focus from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
			_guiId = Guid.Parse(RequiredValue(objNode, "guid"));
			Name = RequiredValue(objNode, "name");
			Rating = Convert.ToInt32(RequiredValue(objNode, "rating"));
			_guiGearId = Guid.Parse(RequiredValue(objNode, "gearid"));
		}

		private static string RequiredValue(XmlNode objNode, string strName)
			=> objNode[strName]?.InnerText ?? throw new InvalidDataException("Focus is missing '" + strName + "'.");

        #endregion

        #region Properties

        /// <summary>
        ///     Internal identifier which will be used to identify this Focus in the Improvement system.
        /// </summary>
        public string InternalId => _guiId.ToString();

        /// <summary>
        ///     Foci's name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     GUID of the linked Gear.
        /// </summary>
        public string GearId
        {
            get => _guiGearId.ToString();
            set => _guiGearId = Guid.Parse(value);
        }

        /// <summary>
        ///     Rating of the Foci.
        /// </summary>
        public int Rating { get; set; }

        #endregion
    }
}
