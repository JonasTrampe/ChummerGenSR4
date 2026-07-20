using System;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// A Martial Art Maneuver.
	/// </summary>
	public class MartialArtManeuver
	{
		private Guid _guiID = new Guid();
		private string _strName = "";
		private string _strSource = "";
		private string _strPage = "";
		private string _strNotes = "";

		#region Constructor, Create, Save, Load, and Print Methods
		public MartialArtManeuver()
		{
			// Create the GUID for the new Martial Art Maneuver.
			_guiID = Guid.NewGuid();
		}

		/// Create a Martial Art Maneuver from an XmlNode.
		/// <param name="objXmlManeuverNode">XmlNode to create the object from.</param>
		public void Create(XmlNode objXmlManeuverNode)
		{
			_strName = objXmlManeuverNode["name"].InnerText;
			_strSource = objXmlManeuverNode["source"].InnerText;
			_strPage = objXmlManeuverNode["page"].InnerText;
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("martialartmaneuver");
			objWriter.WriteElementString("guid", _guiID.ToString());
			objWriter.WriteElementString("name", _strName);
			objWriter.WriteElementString("source", _strSource);
			objWriter.WriteElementString("page", _strPage);
			objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteEndElement();
		}

		/// <summary>
		/// Load the Martial Art Maneuver from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		public void Load(XmlNode objNode)
		{
			_guiID = Guid.Parse(objNode["guid"].InnerText);
			_strName = objNode["name"].InnerText;
			_strSource = objNode["source"].InnerText;
			_strPage = objNode["page"].InnerText;
			try
			{
				_strNotes = objNode["notes"].InnerText;
			}
			catch
			{
			}
		}

		/// <summary>
		/// Print the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Print(XmlTextWriter objWriter, string strDisplayName, string strSource, string strPage, bool blnPrintNotes)
		{
			objWriter.WriteStartElement("martialartmaneuver");
			objWriter.WriteElementString("name", strDisplayName);
			objWriter.WriteElementString("source", strSource);
			objWriter.WriteElementString("page", strPage);
			if (blnPrintNotes)
				objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteEndElement();
		}
		#endregion

		#region Properties
		/// <summary>
		/// Internal identifier which will be used to identify this Martial Art Maneuver in the Improvement system.
		/// </summary>
		public string InternalId
		{
			get
			{
				return _guiID.ToString();
			}
		}

		/// <summary>
		/// Name.
		/// </summary>
		public string Name
		{
			get
			{
				return _strName;
			}
			set
			{
				_strName = value;
			}
		}

		/// <summary>
		/// The name of the object as it should be displayed on printouts (translated name only).
		/// </summary>
		public string DisplayNameShort
		{
			get
			{
				string strReturn = _strName;
				// Get the translated name if applicable.
				return _strName;
			}
		}

		/// <summary>
		/// The name of the object as it should be displayed in lists. Name (Extra).
		/// </summary>
		public string DisplayName
		{
			get
			{
				string strReturn = DisplayNameShort;

				return strReturn;
			}
		}

		/// <summary>
		/// Sourcebook.
		/// </summary>
		public string Source
		{
			get
			{
				return _strSource;
			}
			set
			{
				_strSource = value;
			}
		}

		/// <summary>
		/// Page.
		/// </summary>
		public string Page
		{
			get
			{
				string strReturn = _strPage;
				// Get the translated name if applicable.
				return _strName;
			}
			set
			{
				_strPage = value;
			}
		}

		/// <summary>
		/// Notes.
		/// </summary>
		public string Notes
		{
			get
			{
				return _strNotes;
			}
			set
			{
				_strNotes = value;
			}
		}
		#endregion
	}

}

