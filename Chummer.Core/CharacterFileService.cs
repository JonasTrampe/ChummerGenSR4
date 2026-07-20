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
