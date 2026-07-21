using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// Platform-neutral reader and writer for the existing Chummer character-file format.
	/// This is deliberately a small extraction from <c>Character</c>: callers can display and
	/// round-trip a save before the complete legacy domain object has been moved into Core.
	/// </summary>
	public sealed class CharacterFileService
	{
		public CharacterDocument Load(Stream objStream, string strSourceName)
		{
			if (objStream == null) throw new ArgumentNullException(nameof(objStream));
			Trace.TraceInformation("Loading Chummer character from {0}", strSourceName);
			try
			{
				XmlDocument objDocument = new XmlDocument();
				objDocument.Load(objStream);
				if (objDocument.DocumentElement == null || objDocument.DocumentElement.Name != "character")
					throw new InvalidDataException("The selected file is not a Chummer character document.");

				CharacterDocument objCharacter = new CharacterDocument(objDocument, strSourceName);
				Trace.TraceInformation("Loaded Chummer character {0} from {1}", objCharacter.DisplayName, strSourceName);
				return objCharacter;
			}
			catch (Exception ex)
			{
				Trace.TraceError("Failed to load Chummer character from {0}: {1}", strSourceName, ex);
				throw;
			}
		}

		public void Save(CharacterDocument objCharacter, Stream objStream, string strTargetName)
		{
			if (objCharacter == null) throw new ArgumentNullException(nameof(objCharacter));
			if (objStream == null) throw new ArgumentNullException(nameof(objStream));
			Trace.TraceInformation("Saving Chummer character {0} to {1}", objCharacter.DisplayName, strTargetName);
			try
			{
				objCharacter.Document.Save(objStream);
				Trace.TraceInformation("Saved Chummer character {0} to {1}", objCharacter.DisplayName, strTargetName);
			}
			catch (Exception ex)
			{
				Trace.TraceError("Failed to save Chummer character {0} to {1}: {2}", objCharacter.DisplayName, strTargetName, ex);
				throw;
			}
		}
	}

	public sealed class CharacterDocument
	{
		internal XmlDocument Document { get; private set; }
		public string DisplayName { get; private set; }
		public string Name { get { return GetValue("/character/name", DisplayName); } }
		public string Alias { get { return GetValue("/character/alias", string.Empty); } }
		public string Metatype { get { return GetValue("/character/metatype", string.Empty); } }
		public string Karma { get { return GetValue("/character/karma", "0"); } }
		public string Nuyen { get { return GetValue("/character/nuyen", "0"); } }
		public IReadOnlyList<CharacterAttributeData> Attributes { get { return ReadAttributes(); } }
		public IReadOnlyList<CharacterQualityData> Qualities { get { return ReadQualities(); } }
		public IReadOnlyList<CharacterTreeItemData> Gear { get { return ReadTreeItems("/character/gears/gear", "children/gear"); } }
		public IReadOnlyList<CharacterTreeItemData> Cyberware { get { return ReadTreeItems("/character/cyberwares/cyberware", "children/cyberware"); } }
		public IReadOnlyList<CharacterTreeItemData> Armor { get { return ReadTreeItems("/character/armors/armor", null); } }
		public IReadOnlyList<CharacterWeaponData> Weapons { get { return ReadWeapons(); } }
		public IReadOnlyList<CharacterSkillGroupData> SkillGroups { get { return ReadSkillGroups(); } }
		public IReadOnlyList<CharacterSkillData> Skills { get { return ReadSkills(); } }

		internal CharacterDocument(XmlDocument objDocument, string strDisplayName)
		{
			Document = objDocument;
			DisplayName = strDisplayName;
		}

		private string GetValue(string strXPath, string strFallback)
		{
			XmlNode objNode = Document.SelectSingleNode(strXPath);
			return string.IsNullOrEmpty(objNode == null ? null : objNode.InnerText) ? strFallback : objNode.InnerText;
		}

		private IReadOnlyList<CharacterAttributeData> ReadAttributes()
		{
			List<CharacterAttributeData> lstAttributes = new List<CharacterAttributeData>();
			XmlNodeList objNodes = Document.SelectNodes("/character/attributes/attribute");
			if (objNodes == null) return lstAttributes;
			foreach (XmlNode objNode in objNodes)
			{
				lstAttributes.Add(new CharacterAttributeData(
					GetValue(objNode, "name", string.Empty), GetValue(objNode, "value", "0"),
					GetValue(objNode, "totalvalue", GetValue(objNode, "value", "0")),
					GetValue(objNode, "metatypemin", "0"), GetValue(objNode, "metatypemax", "0"),
					GetValue(objNode, "metatypeaugmax", GetValue(objNode, "metatypemax", "0"))));
			}
			return lstAttributes;
		}

		private IReadOnlyList<CharacterQualityData> ReadQualities()
		{
			List<CharacterQualityData> lstQualities = new List<CharacterQualityData>();
			XmlNodeList objNodes = Document.SelectNodes("/character/qualities/quality");
			if (objNodes == null) return lstQualities;
			foreach (XmlNode objNode in objNodes)
				lstQualities.Add(new CharacterQualityData(GetValue(objNode, "name", string.Empty), GetValue(objNode, "extra", string.Empty), GetValue(objNode, "qualitytype", string.Empty)));
			return lstQualities;
		}

		private IReadOnlyList<CharacterTreeItemData> ReadTreeItems(string strXPath, string strChildXPath)
		{
			List<CharacterTreeItemData> lstItems = new List<CharacterTreeItemData>();
			XmlNodeList objNodes = Document.SelectNodes(strXPath);
			if (objNodes == null) return lstItems;
			foreach (XmlNode objNode in objNodes)
				lstItems.Add(ReadTreeItem(objNode, strChildXPath));
			return lstItems;
		}

		private IReadOnlyList<CharacterWeaponData> ReadWeapons()
		{
			List<CharacterWeaponData> lstWeapons = new List<CharacterWeaponData>();
			XmlNodeList objNodes = Document.SelectNodes("/character/weapons/weapon");
			if (objNodes == null) return lstWeapons;
			foreach (XmlNode objNode in objNodes)
				lstWeapons.Add(new CharacterWeaponData(GetValue(objNode, "name", string.Empty), GetValue(objNode, "category", string.Empty), GetValue(objNode, "damage", string.Empty), GetValue(objNode, "ammo", string.Empty)));
			return lstWeapons;
		}

		private IReadOnlyList<CharacterSkillGroupData> ReadSkillGroups()
		{
			List<CharacterSkillGroupData> lstGroups = new List<CharacterSkillGroupData>();
			XmlNodeList objNodes = Document.SelectNodes("/character/skillgroups/skillgroup");
			if (objNodes == null) return lstGroups;
			foreach (XmlNode objNode in objNodes)
				lstGroups.Add(new CharacterSkillGroupData(GetValue(objNode, "name", string.Empty), GetValue(objNode, "rating", "0")));
			return lstGroups;
		}

		private IReadOnlyList<CharacterSkillData> ReadSkills()
		{
			List<CharacterSkillData> lstSkills = new List<CharacterSkillData>();
			XmlNodeList objNodes = Document.SelectNodes("/character/skills/skill");
			if (objNodes == null) return lstSkills;
			foreach (XmlNode objNode in objNodes)
			{
				if (GetValue(objNode, "knowledge", "False") == "True") continue;
				lstSkills.Add(new CharacterSkillData(
					GetValue(objNode, "name", string.Empty), GetValue(objNode, "attribute", string.Empty),
					GetValue(objNode, "rating", "0"), GetValue(objNode, "totalvalue", "0"),
					GetValue(objNode, "spec", string.Empty), GetValue(objNode, "grouped", "False") == "True"));
			}
			return lstSkills;
		}

		private static CharacterTreeItemData ReadTreeItem(XmlNode objNode, string strChildXPath)
		{
			CharacterTreeItemData objItem = new CharacterTreeItemData(GetValue(objNode, "name", string.Empty));
			XmlNodeList objChildren = string.IsNullOrEmpty(strChildXPath) ? null : objNode.SelectNodes(strChildXPath);
			if (objChildren != null)
				foreach (XmlNode objChild in objChildren)
					objItem.Children.Add(ReadTreeItem(objChild, strChildXPath));
			return objItem;
		}

		private static string GetValue(XmlNode objNode, string strName, string strFallback)
		{
			XmlNode objChild = objNode.SelectSingleNode(strName);
			return string.IsNullOrEmpty(objChild == null ? null : objChild.InnerText) ? strFallback : objChild.InnerText;
		}
	}

	public sealed class CharacterAttributeData
	{
		public string Code { get; private set; }
		public string Value { get; private set; }
		public string TotalValue { get; private set; }
		public string Minimum { get; private set; }
		public string Maximum { get; private set; }
		public string AugmentedMaximum { get; private set; }

		internal CharacterAttributeData(string strCode, string strValue, string strTotalValue, string strMinimum, string strMaximum, string strAugmentedMaximum)
		{
			Code = strCode;
			Value = strValue;
			TotalValue = strTotalValue;
			Minimum = strMinimum;
			Maximum = strMaximum;
			AugmentedMaximum = strAugmentedMaximum;
		}
	}

	public sealed class CharacterQualityData
	{
		public string Name { get; private set; }
		public string Extra { get; private set; }
		public string Type { get; private set; }

		internal CharacterQualityData(string strName, string strExtra, string strType)
		{
			Name = strName;
			Extra = strExtra;
			Type = strType;
		}

		public string DisplayName { get { return string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")"; } }
	}

	public sealed class CharacterTreeItemData
	{
		public string Name { get; private set; }
		public List<CharacterTreeItemData> Children { get; private set; }

		internal CharacterTreeItemData(string strName)
		{
			Name = strName;
			Children = new List<CharacterTreeItemData>();
		}
	}

	public sealed class CharacterWeaponData
	{
		public string Name { get; private set; }
		public string Category { get; private set; }
		public string Damage { get; private set; }
		public string Ammo { get; private set; }

		internal CharacterWeaponData(string strName, string strCategory, string strDamage, string strAmmo)
		{
			Name = strName;
			Category = strCategory;
			Damage = strDamage;
			Ammo = strAmmo;
		}

		public string DisplayName
		{
			get
			{
				string strDetails = string.IsNullOrEmpty(Damage) ? Category : Damage;
				return string.IsNullOrEmpty(strDetails) ? Name : Name + " (" + strDetails + ")";
			}
		}
	}

	public sealed class CharacterSkillGroupData
	{
		public string Name { get; private set; }
		public string Rating { get; private set; }

		internal CharacterSkillGroupData(string strName, string strRating)
		{
			Name = strName;
			Rating = strRating;
		}
	}

	public sealed class CharacterSkillData
	{
		public string Name { get; private set; }
		public string Attribute { get; private set; }
		public string Rating { get; private set; }
		public string TotalValue { get; private set; }
		public string Specialization { get; private set; }
		public bool IsGroupLocked { get; private set; }

		internal CharacterSkillData(string strName, string strAttribute, string strRating, string strTotalValue, string strSpecialization, bool blnIsGroupLocked)
		{
			Name = strName;
			Attribute = strAttribute;
			Rating = strRating;
			TotalValue = strTotalValue;
			Specialization = strSpecialization;
			IsGroupLocked = blnIsGroupLocked;
		}
	}
