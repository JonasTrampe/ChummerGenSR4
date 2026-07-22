using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using Chummer.Core;

namespace Chummer
{
	/// <summary>
	/// WinForms-only tree adapters for equipment construction. Keeping these overloads out of
	/// the domain models lets the same model construction be used by the Avalonia client.
	/// </summary>
	public static class WinFormsEquipmentTreeExtensions
	{
		public static void Create(this Quality objQuality, XmlNode objXmlQuality, Character objCharacter, QualitySource objQualitySource, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes, string strForceValue = "")
		{
			int intWeaponCount = objWeapons == null ? 0 : objWeapons.Count;
			objQuality.Create(objXmlQuality, objCharacter, objQualitySource, objWeapons, strForceValue);
			AddWeaponNodes(objWeapons, objWeaponNodes, intWeaponCount);
		}

		public static void Create(this Vehicle objVehicle, XmlNode objXmlVehicle, TreeNode objNode, ContextMenuStrip cmsVehicle, ContextMenuStrip cmsVehicleGear, ContextMenuStrip cmsVehicleWeapon, ContextMenuStrip cmsVehicleWeaponAccessory, ContextMenuStrip cmsVehicleWeaponMod, bool blnCreateChildren = true)
		{
			objVehicle.Create(objXmlVehicle, blnCreateChildren);
			objNode.Text = objVehicle.DisplayName;
			objNode.Tag = objVehicle.InternalId;

			foreach (VehicleMod objVehicleMod in objVehicle.Mods)
			{
				TreeNode objVehicleModNode = new TreeNode
				{
					Text = objVehicleMod.DisplayName,
					Tag = objVehicleMod.InternalId,
					ForeColor = SystemColors.GrayText,
					ContextMenuStrip = cmsVehicle,
				};
				foreach (Weapon objWeapon in objVehicleMod.Weapons)
					objVehicleModNode.Nodes.Add(new CommonFunctions(null).BuildWeaponNode(objWeapon, cmsVehicleWeapon, cmsVehicleWeaponAccessory, cmsVehicleWeaponMod));
				objNode.Nodes.Add(objVehicleModNode);
			}

			foreach (Gear objGear in objVehicle.Gear)
			{
				TreeNode objGearNode = new TreeNode();
				PopulateGearNode(objGear, objGearNode, null);
				objGearNode.ContextMenuStrip = cmsVehicleGear;
				objNode.Nodes.Add(objGearNode);
			}

			if (objNode.Nodes.Count > 0)
				objNode.Expand();
		}

		public static void Create(this Armor objArmor, XmlNode objXmlArmorNode, TreeNode objNode, ContextMenuStrip cmsArmorMod, bool blnSkipCost = false, bool blnCreateChildren = true)
		{
			objArmor.Create(objXmlArmorNode, blnSkipCost, blnCreateChildren);
			objNode.Text = objArmor.DisplayName;
			objNode.Tag = objArmor.InternalId;

			foreach (ArmorMod objArmorMod in objArmor.ArmorMods)
			{
				TreeNode objArmorModNode = new TreeNode
				{
					Text = objArmorMod.DisplayName,
					Tag = objArmorMod.InternalId,
					ContextMenuStrip = cmsArmorMod,
				};
				objNode.Nodes.Add(objArmorModNode);
			}

			foreach (Gear objGear in objArmor.Gear)
			{
				TreeNode objGearNode = new TreeNode();
				PopulateGearNode(objGear, objGearNode, null);
				objNode.Nodes.Add(objGearNode);
			}

			if (objNode.Nodes.Count > 0)
				objNode.Expand();
		}

		public static void Create(this ArmorMod objArmorMod, XmlNode objXmlArmorNode, TreeNode objNode, int intRating, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes, bool blnSkipCost = false)
		{
			int intWeaponCount = objWeapons == null ? 0 : objWeapons.Count;
			objArmorMod.Create(objXmlArmorNode, intRating, objWeapons, blnSkipCost);
			PopulateNode(objNode, objArmorMod);
			AddWeaponNodes(objWeapons, objWeaponNodes, intWeaponCount);
		}

		public static void Create(this Gear objGear, XmlNode objXmlGear, Character objCharacter, TreeNode objNode, int intRating, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes, string strForceValue = "", bool blnHacked = false, bool blnInherent = false, bool blnAddImprovements = true, bool blnCreateChildren = true, bool blnAerodynamic = false)
		{
			objGear.Create(objXmlGear, objCharacter, intRating, objWeapons, objWeaponNodes, strForceValue, blnHacked, blnInherent, blnAddImprovements, blnCreateChildren, blnAerodynamic);
			PopulateGearNode(objGear, objNode, objCharacter);
		}

		public static void Create(this Gear objGear, XmlNode objXmlGear, Character objCharacter, int intRating, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes, string strForceValue = "", bool blnHacked = false, bool blnInherent = false, bool blnAddImprovements = true, bool blnCreateChildren = true, bool blnAerodynamic = false)
		{
			int intWeaponCount = objWeapons == null ? 0 : objWeapons.Count;
			objGear.Create(objXmlGear, objCharacter, intRating, objWeapons, strForceValue, blnHacked, blnInherent, blnAddImprovements, blnCreateChildren, blnAerodynamic);
			AddWeaponNodes(objWeapons, objWeaponNodes, intWeaponCount);
		}

		public static void Copy(this Gear objGear, Gear objSource, TreeNode objNode, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes)
		{
			objGear.Copy(objSource, objWeapons);
			PopulateGearNode(objGear, objNode, null);
		}

		public static void Copy(this Gear objGear, Gear objSource, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes)
		{
			objGear.Copy(objSource, objWeapons);
		}

		public static void Create(this Commlink objCommlink, XmlNode objXmlGear, Character objCharacter, TreeNode objNode, int intRating, bool blnAddImprovements = true, bool blnCreateChildren = true)
		{
			objCommlink.Create(objXmlGear, objCharacter, intRating, blnAddImprovements, blnCreateChildren);
			PopulateGearNode(objCommlink, objNode, objCharacter);
		}

		public static void Copy(this Commlink objCommlink, Commlink objSource, TreeNode objNode, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes)
		{
			objCommlink.Copy(objSource, objWeapons);
			PopulateGearNode(objCommlink, objNode, null);
		}

		public static void Copy(this Commlink objCommlink, Commlink objSource, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes)
		{
			objCommlink.Copy(objSource, objWeapons);
		}

		public static void Create(this OperatingSystem objOperatingSystem, XmlNode objXmlGear, Character objCharacter, TreeNode objNode, int intRating, bool blnAddImprovements = true, bool blnCreateChildren = true)
		{
			objOperatingSystem.Create(objXmlGear, objCharacter, intRating, blnAddImprovements, blnCreateChildren);
			PopulateGearNode(objOperatingSystem, objNode, objCharacter);
		}

		public static void Copy(this OperatingSystem objOperatingSystem, OperatingSystem objSource, TreeNode objNode, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes)
		{
			objOperatingSystem.Copy(objSource, objWeapons);
			PopulateGearNode(objOperatingSystem, objNode, null);
		}

		public static void Copy(this OperatingSystem objOperatingSystem, OperatingSystem objSource, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes)
		{
			objOperatingSystem.Copy(objSource, objWeapons);
		}

		public static void Create(this Cyberware objCyberware, XmlNode objXmlCyberware, Character objCharacter, Grade objGrade, Improvement.ImprovementSource objSource, int intRating, TreeNode objNode, List<Weapon> objWeapons, List<TreeNode> objWeaponNodes, bool blnCreateImprovements = true, bool blnCreateChildren = true, string strForced = "")
		{
			int intWeaponCount = objWeapons == null ? 0 : objWeapons.Count;
			objCyberware.Create(objXmlCyberware, objCharacter, objGrade, objSource, intRating, objWeapons, blnCreateImprovements, blnCreateChildren, strForced);
			objNode.Text = objCyberware.DisplayName;
			objNode.Tag = objCyberware.InternalId;
			new CommonFunctions(objCharacter).BuildCyberwareTree(objCyberware, objNode, null, null);
			AddWeaponNodes(objWeapons, objWeaponNodes, intWeaponCount);
		}

		private static void PopulateNode(TreeNode objNode, ArmorMod objArmorMod)
		{
			objNode.Text = objArmorMod.DisplayName;
			objNode.Tag = objArmorMod.InternalId;
		}

		private static void PopulateGearNode(Gear objGear, TreeNode objNode, Character objCharacter)
		{
			objNode.Text = objGear.DisplayName;
			objNode.Tag = objGear.InternalId;
			if (objCharacter != null)
				new CommonFunctions(objCharacter).BuildGearTree(objGear, objNode, null);
		}

		private static void AddWeaponNodes(List<Weapon> objWeapons, List<TreeNode> objWeaponNodes, int intStartIndex)
		{
			if (objWeapons == null || objWeaponNodes == null)
				return;

			for (int i = intStartIndex; i < objWeapons.Count; i++)
			{
				Weapon objWeapon = objWeapons[i];
				TreeNode objWeaponNode = new TreeNode
				{
					Text = objWeapon.DisplayName,
					Tag = objWeapon.InternalId,
				};
				objWeaponNode.ForeColor = SystemColors.GrayText;
				objWeaponNodes.Add(objWeaponNode);
			}
		}
	}
}
