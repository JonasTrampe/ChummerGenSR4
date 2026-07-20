using System;
using System.Drawing;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// A Contact or Enemy.
	/// </summary>
	public class Contact
	{
		private string _strName = "";
		private int _intConnection = 1;
		private int _intLoyalty = 1;
		private int _intMembership = 0;
		private int _intAreaOfInfluence = 0;
		private int _intMagicalResources = 0;
		private int _intMatrixResources = 0;
		private string _strGroupName = "";
		private ContactType _objContactType = ContactType.Contact;
		private string _strFileName = "";
		private string _strRelativeName = "";
		private string _strNotes = "";
		private Color _objColour;
		private bool _blnFree = false;

		#region Helper Methods
		/// <summary>
		/// Convert a string to a ContactType.
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
		public Contact()
		{
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("contact");
			objWriter.WriteElementString("name", _strName);
			objWriter.WriteElementString("connection", _intConnection.ToString());
			objWriter.WriteElementString("loyalty", _intLoyalty.ToString());
			objWriter.WriteElementString("membership", _intMembership.ToString());
			objWriter.WriteElementString("areaofinfluence", _intAreaOfInfluence.ToString());
			objWriter.WriteElementString("magicalresources", _intMagicalResources.ToString());
			objWriter.WriteElementString("matrixresources", _intMatrixResources.ToString());
			objWriter.WriteElementString("type", _objContactType.ToString());
			objWriter.WriteElementString("file", _strFileName);
			objWriter.WriteElementString("relative", _strRelativeName);
			objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteElementString("groupname", _strGroupName);
			objWriter.WriteElementString("colour", _objColour.ToArgb().ToString());
			objWriter.WriteElementString("free", _blnFree.ToString());
			objWriter.WriteEndElement();
		}

		/// <summary>
		/// Load the Contact from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		public void Load(XmlNode objNode)
		{
			_strName = objNode["name"].InnerText;
			_intConnection = Convert.ToInt32(objNode["connection"].InnerText);
			_intLoyalty = Convert.ToInt32(objNode["loyalty"].InnerText);
			try
			{
				_intMembership = Convert.ToInt32(objNode["membership"].InnerText);
				_intAreaOfInfluence = Convert.ToInt32(objNode["areaofinfluence"].InnerText);
				_intMagicalResources = Convert.ToInt32(objNode["magicalresources"].InnerText);
				_intMatrixResources = Convert.ToInt32(objNode["matrixresources"].InnerText);
			}
			catch
			{
			}
			_objContactType = ConvertToContactType(objNode["type"].InnerText);
			try
			{
				_strFileName = objNode["file"].InnerText;
			}
			catch
			{
			}
			try
			{
				_strRelativeName = objNode["relative"].InnerText;
			}
			catch
			{
			}
			try
			{
				_strNotes = objNode["notes"].InnerText;
			}
			catch
			{
			}
			try
			{
				_strGroupName = objNode["groupname"].InnerText;
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
				_blnFree = Convert.ToBoolean(objNode["free"].InnerText);
			}
			catch
			{
			}
		}

		/// <summary>
		/// Print the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Print(XmlTextWriter objWriter, string strType, bool blnPrintNotes)
		{
			objWriter.WriteStartElement("contact");
			objWriter.WriteElementString("name", _strName);
			if (Group == 0)
				objWriter.WriteElementString("connection", _intConnection.ToString());
			else
				objWriter.WriteElementString("connection", _intConnection.ToString() + " (" + Group.ToString() + ")");
			objWriter.WriteElementString("loyalty", _intLoyalty.ToString());
			objWriter.WriteElementString("type", strType);
			if (blnPrintNotes)
				objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteEndElement();
		}
		#endregion

		#region Properties
		/// <summary>
		/// Name of the Contact.
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
		/// Contact's Connection Rating.
		/// </summary>
		public int Connection
		{
			get
			{
				return _intConnection;
			}
			set
			{
				_intConnection = value;
			}
		}

		/// <summary>
		/// Contact's Loyalty Rating (or Enemy's Incidence Rating).
		/// </summary>
		public int Loyalty
		{
			get
			{
				return _intLoyalty;
			}
			set
			{
				_intLoyalty = value;
			}
		}

		/// <summary>
		/// Contact's Group Rating (applies to Contacts only).
		/// </summary>
		public int Group
		{
			get
			{
				return _intMembership + _intAreaOfInfluence + _intMagicalResources + _intMatrixResources;
			}
		}

		/// <summary>
		/// Connection Modifier: Membership.
		/// </summary>
		public int Membership
		{
			get
			{
				return _intMembership;
			}
			set
			{
				_intMembership = value;
			}
		}

		/// <summary>
		/// Connection Modifier: Area of Influence.
		/// </summary>
		public int AreaOfInfluence
		{
			get
			{
				return _intAreaOfInfluence;
			}
			set
			{
				_intAreaOfInfluence = value;
			}
		}

		/// <summary>
		/// Connection Modifier: Magical Resources.
		/// </summary>
		public int MagicalResources
		{
			get
			{
				return _intMagicalResources;
			}
			set
			{
				_intMagicalResources = value;
			}
		}

		/// <summary>
		/// Connection Modifier: Matrix Resources:
		/// </summary>
		public int MatrixResources
		{
			get
			{
				return _intMatrixResources;
			}
			set
			{
				_intMatrixResources = value;
			}
		}

		/// <summary>
		/// The Contact's type, either Contact or Enemy.
		/// </summary>
		public ContactType EntityType
		{
			get
			{
				return _objContactType;
			}
			set
			{
				_objContactType = value;
			}
		}

		/// <summary>
		/// Name of the save file for this Contact.
		/// </summary>
		public string FileName
		{
			get
			{
				return _strFileName;
			}
			set
			{
				_strFileName = value;
			}
		}

		/// <summary>
		/// Relative path to the save file.
		/// </summary>
		public string RelativeFileName
		{
			get
			{
				return _strRelativeName;
			}
			set
			{
				_strRelativeName = value;
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

		/// <summary>
		/// Group Name.
		/// </summary>
		public string GroupName
		{
			get
			{
				return _strGroupName;
			}
			set
			{
				_strGroupName = value;
			}
		}

		/// <summary>
		/// Contact Colour.
		/// </summary>
		public Color Colour
		{
			get
			{
				return _objColour;
			}
			set
			{
				_objColour = value;
			}
		}

		/// <summary>
		/// Whether or not this is a free contact.
		/// </summary>
		public bool Free
		{
			get
			{
				return _blnFree;
			}
			set
			{
				_blnFree = value;
			}
		}
		#endregion
	}

	/// <summary>
	/// A Critter Power.
	/// </summary>
}

