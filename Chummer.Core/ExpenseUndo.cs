using System;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// Undo information for an Expense Log Entry.
	/// </summary>
	public class ExpenseUndo
	{
		private KarmaExpenseType _objKarmaExpenseType;
		private NuyenExpenseType _objNuyenExpenseType;
		private string _strObjectId;
		private int _intQty = 0;
		private string _strExtra = "";

		#region Helper Methods
		/// <summary>
		/// Convert a string to a KarmaExpenseType.
		/// </summary>
		/// <param name="strValue">String value to convert.</param>
		public KarmaExpenseType ConvertToKarmaExpenseType(string strValue)
		{
			switch (strValue)
			{
				case "AddComplexForm":
					return KarmaExpenseType.AddComplexForm;
				case "AddComplexFormOption":
					return KarmaExpenseType.AddComplexFormOption;
				case "AddMartialArt":
					return KarmaExpenseType.AddMartialArt;
				case "AddMartialArtManeuver":
					return KarmaExpenseType.AddMartialArtManeuver;
				case "AddMetamagic":
					return KarmaExpenseType.AddMetamagic;
				case "AddQuality":
					return KarmaExpenseType.AddQuality;
				case "AddSkill":
					return KarmaExpenseType.AddSkill;
				case "AddSpell":
					return KarmaExpenseType.AddSpell;
				case "BindFocus":
					return KarmaExpenseType.BindFocus;
				case "ImproveAttribute":
					return KarmaExpenseType.ImproveAttribute;
				case "ImproveComplexForm":
					return KarmaExpenseType.ImproveComplexForm;
				case "ImproveComplexFormOption":
					return KarmaExpenseType.ImproveComplexFormOption;
				case "ImproveInitiateGrade":
					return KarmaExpenseType.ImproveInitiateGrade;
				case "ImproveMartialArt":
					return KarmaExpenseType.ImproveMartialArt;
				case "ImproveSkill":
					return KarmaExpenseType.ImproveSkill;
				case "ImproveSkillGroup":
					return KarmaExpenseType.ImproveSkillGroup;
				case "ManualAdd":
					return KarmaExpenseType.ManualAdd;
				case "ManualSubtract":
					return KarmaExpenseType.ManualSubtract;
				case "RemoveQuality":
					return KarmaExpenseType.RemoveQuality;
				case "SkillSpec":
					return KarmaExpenseType.SkillSpec;
				case "JoinGroup":
					return KarmaExpenseType.JoinGroup;
				case "LeaveGroup":
					return KarmaExpenseType.LeaveGroup;
				default:
					return KarmaExpenseType.ManualAdd;
			}
		}

		/// <summary>
		/// Convert a string to a NuyenExpenseType.
		/// </summary>
		/// <param name="strValue">String value to convert.</param>
		public NuyenExpenseType ConvertToNuyenExpenseType(string strValue)
		{
			switch (strValue)
			{
				case "AddArmor":
					return NuyenExpenseType.AddArmor;
				case "AddArmorGear":
					return NuyenExpenseType.AddArmorGear;
				case "AddArmorMod":
					return NuyenExpenseType.AddArmorMod;
				case "AddCyberware":
					return NuyenExpenseType.AddCyberware;
				case "AddGear":
					return NuyenExpenseType.AddGear;
				case "AddVehicle":
					return NuyenExpenseType.AddVehicle;
				case "AddVehicleGear":
					return NuyenExpenseType.AddVehicleGear;
				case "AddVehicleMod":
					return NuyenExpenseType.AddVehicleMod;
				case "AddVehicleWeapon":
					return NuyenExpenseType.AddVehicleWeapon;
				case "AddVehicleWeaponAccessory":
					return NuyenExpenseType.AddVehicleWeaponAccessory;
				case "AddVehicleWeaponMod":
					return NuyenExpenseType.AddVehicleWeaponMod;
				case "AddWeapon":
					return NuyenExpenseType.AddWeapon;
				case "AddWeaponAccessory":
					return NuyenExpenseType.AddWeaponAccessory;
				case "AddWeaponMod":
					return NuyenExpenseType.AddWeaponMod;
				case "IncreaseLifestyle":
					return NuyenExpenseType.IncreaseLifestyle;
				case "ManualAdd":
					return NuyenExpenseType.ManualAdd;
				case "ManualSubtract":
					return NuyenExpenseType.ManualSubtract;
				case "AddVehicleModCyberware":
					return NuyenExpenseType.AddVehicleModCyberware;
				case "AddCyberwareGear":
					return NuyenExpenseType.AddCyberwareGear;
				case "AddWeaponGear":
					return NuyenExpenseType.AddWeaponGear;
				case "CredstickDeposit":
					return NuyenExpenseType.CredstickDeposit;
				case "CredstickWithdrawal":
					return NuyenExpenseType.CredstickWithdrawal;
				default:
					return NuyenExpenseType.ManualAdd;
			}
		}
		#endregion

		#region Constructor, Create, Save, and Load Methods
		/// <summary>
		/// Create the ExpenseUndo Entry.
		/// </summary>
		/// <param name="objExpenseType">Karma expense type.</param>
		/// <param name="strObjectId">Object identifier.</param>
		public void CreateKarma(KarmaExpenseType objExpenseType, string strObjectId)
		{
			_objKarmaExpenseType = objExpenseType;
			_strObjectId = strObjectId;
		}

		/// <summary>
		/// Create the ExpenseUndo Entry.
		/// </summary>
		/// <param name="objExpenseType">Nuyen expense type.</param>
		/// <param name="strObjectId">Object identifier.</param>
		/// <param name="intQty">Amount of Nuyen.</param>
		public void CreateNuyen(NuyenExpenseType objExpenseType, string strObjectId, int intQty = 0)
		{
			_objNuyenExpenseType = objExpenseType;
			_strObjectId = strObjectId;
			_intQty = intQty;
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("undo");
			objWriter.WriteElementString("karmatype", _objKarmaExpenseType.ToString());
			objWriter.WriteElementString("nuyentype", _objNuyenExpenseType.ToString());
			objWriter.WriteElementString("objectid", _strObjectId);
			objWriter.WriteElementString("qty", _intQty.ToString());
			objWriter.WriteElementString("extra", _strExtra);
			objWriter.WriteEndElement();
		}

		/// <summary>
		/// Load the KarmaLogEntry from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		public void Load(XmlNode objNode)
		{
			_objKarmaExpenseType = ConvertToKarmaExpenseType(objNode["karmatype"].InnerText);
			_objNuyenExpenseType = ConvertToNuyenExpenseType(objNode["nuyentype"].InnerText);
			_strObjectId = objNode["objectid"].InnerText;
			_intQty = Convert.ToInt32(objNode["qty"].InnerText);
			_strExtra = objNode["extra"].InnerText;
		}
		
		#endregion

		#region Properties
		/// <summary>
		/// Karma Expense Type.
		/// </summary>
		public KarmaExpenseType KarmaType
		{
			get
			{
				return _objKarmaExpenseType;
			}
			set
			{
				_objKarmaExpenseType = value;
			}
		}

		/// <summary>
		/// Nuyen Expense Type.
		/// </summary>
		public NuyenExpenseType NuyenType
		{
			get
			{
				return _objNuyenExpenseType;
			}
			set
			{
				_objNuyenExpenseType = value;
			}
		}

		/// <summary>
		/// Object InternalId.
		/// </summary>
		public string ObjectId
		{
			get
			{
				return _strObjectId;
			}
			set
			{
				_strObjectId = value;
			}
		}

		/// <summary>
		/// Quantity of items added (Nuyen only).
		/// </summary>
		public int Qty
		{
			get
			{
				return _intQty;
			}
			set
			{
				_intQty = value;
			}
		}

		/// <summary>
		/// Extra information.
		/// </summary>
		public string Extra
		{
			get
			{
				return _strExtra;
			}
			set
			{
				_strExtra = value;
			}
		}
		#endregion
	}

}

