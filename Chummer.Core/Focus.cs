using System;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// A Focus.
	/// </summary>
	public class Focus
	{
		private Guid _guiID = new Guid();
		private string _strName = "";
		private Guid _guiGearId = new Guid();
		private int _intRating = 0;

		#region Constructor, Create, Save, and Load Methods
		public Focus()
		{
			// Create the GUID for the new Focus.
			_guiID = Guid.NewGuid();
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("focus");
			objWriter.WriteElementString("guid", _guiID.ToString());
			objWriter.WriteElementString("name", _strName);
			objWriter.WriteElementString("gearid", _guiGearId.ToString());
			objWriter.WriteElementString("rating", _intRating.ToString());
			objWriter.WriteEndElement();
		}

		/// <summary>
		/// Load the Focus from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		public void Load(XmlNode objNode)
		{
			_guiID = Guid.Parse(objNode["guid"].InnerText);
			_strName = objNode["name"].InnerText;
			_intRating = Convert.ToInt32(objNode["rating"].InnerText);
			_guiGearId = Guid.Parse(objNode["gearid"].InnerText);
		}
		#endregion

		#region Properties
		/// <summary>
		/// Internal identifier which will be used to identify this Focus in the Improvement system.
		/// </summary>
		public string InternalId
		{
			get
			{
				return _guiID.ToString();
			}
		}

		/// <summary>
		/// Foci's name.
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
		/// GUID of the linked Gear.
		/// </summary>
		public string GearId
		{
			get
			{
				return _guiGearId.ToString();
			}
			set
			{
				_guiGearId = Guid.Parse(value);
			}
		}

		/// <summary>
		/// Rating of the Foci.
		/// </summary>
		public int Rating
		{
			get
			{
				return _intRating;
			}
			set
			{
				_intRating = value;
			}
		}
		#endregion
	}

}

