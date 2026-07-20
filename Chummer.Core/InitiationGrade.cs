using System;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// An Initiation Grade.
	/// </summary>
	public class InitiationGrade
	{
		private Guid _guiID = new Guid();
		private bool _blnGroup = false;
		private bool _blnOrdeal = false;
		private bool _blnTechnomancer = false;
		private int _intGrade = 0;
		private string _strNotes = "";

		private readonly double _dblKarmaInitiation;

		#region Constructor, Create, Save, and Load Methods
		public InitiationGrade(double dblKarmaInitiation)
		{
			// Create the GUID for the new InitiationGrade.
			_guiID = Guid.NewGuid();
			_dblKarmaInitiation = dblKarmaInitiation;
		}

		/// Create an Intiation Grade from an XmlNode and return the TreeNodes for it.
		/// <param name="intGrade">Grade number.</param>
		/// <param name="blnTechnomancer">Whether or not the character is a Technomancer.</param>
		/// <param name="blnGroup">Whether or not a Group was used.</param>
		/// <param name="blnOrdeal">Whether or not an Ordeal was used.</param>
		public void Create(int intGrade, bool blnTechnomancer, bool blnGroup, bool blnOrdeal)
		{
			_intGrade = intGrade;
			_blnTechnomancer = blnTechnomancer;
			_blnGroup = blnGroup;
			_blnOrdeal = blnOrdeal;
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("initiationgrade");
			objWriter.WriteElementString("guid", _guiID.ToString());
			objWriter.WriteElementString("res", _blnTechnomancer.ToString());
			objWriter.WriteElementString("grade", _intGrade.ToString());
			objWriter.WriteElementString("group", _blnGroup.ToString());
			objWriter.WriteElementString("ordeal", _blnOrdeal.ToString());
			objWriter.WriteElementString("notes", _strNotes);
			objWriter.WriteEndElement();
		}

		/// <summary>
		/// Load the Initiation Grade from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		public void Load(XmlNode objNode)
		{
			_guiID = Guid.Parse(objNode["guid"].InnerText);
			_blnTechnomancer = Convert.ToBoolean(objNode["res"].InnerText);
			_intGrade = Convert.ToInt32(objNode["grade"].InnerText);
			_blnGroup = Convert.ToBoolean(objNode["group"].InnerText);
			_blnOrdeal = Convert.ToBoolean(objNode["ordeal"].InnerText);
			try
			{
				_strNotes = objNode["notes"].InnerText;
			}
			catch
			{
			}
		}
		#endregion

		#region Properties
		/// <summary>
		/// Internal identifier which will be used to identify this Initiation Grade in the Improvement system.
		/// </summary>
		public string InternalId
		{
			get
			{
				return _guiID.ToString();
			}
		}

		/// <summary>
		/// Initiate Grade.
		/// </summary>
		public int Grade
		{
			get
			{
				return _intGrade;
			}
			set
			{
				_intGrade = value;
			}
		}

		/// <summary>
		/// Whether or not a Group was used.
		/// </summary>
		public bool Group
		{
			get
			{
				return _blnGroup;
			}
			set
			{
				_blnGroup = value;
			}
		}

		/// <summary>
		/// Whether or not an Ordeal was used.
		/// </summary>
		public bool Ordeal
		{
			get
			{
				return _blnOrdeal;
			}
			set
			{
				_blnOrdeal = value;
			}
		}

		/// <summary>
		/// Whether or not the Initiation Grade is for a Technomancer.
		/// </summary>
		public bool Technomancer
		{
			get
			{
				return _blnTechnomancer;
			}
			set
			{
				_blnTechnomancer = value;
			}
		}
		#endregion

		#region Complex Properties
		/// <summary>
		/// The Initiation Grade's Karma cost.
		/// </summary>
		public int KarmaCost
		{
			get
			{
				int intCost = 0;
				double dblCost = 10.0 + (_intGrade * _dblKarmaInitiation);
				double dblMultiplier = 1.0;
				
				// Discount for Group.
				if (_blnGroup)
					dblMultiplier -= 0.2;

				// Discount for Ordeal.
				if (_blnOrdeal)
					dblMultiplier -= 0.2;

				intCost = Convert.ToInt32(Math.Ceiling(dblCost * dblMultiplier));

				return intCost;
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

