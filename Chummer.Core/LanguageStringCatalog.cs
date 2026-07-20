using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Chummer
{
	/// <summary>Platform-neutral language string catalog loaded from Chummer language XML files.</summary>
	public sealed class LanguageStringCatalog
	{
		private readonly Dictionary<string, string> _dicStrings = new Dictionary<string, string>();
		public XmlDocument DataDocument { get; private set; }

		public void LoadBase(string strLanguageDirectory)
		{
			LoadFile(Path.Combine(strLanguageDirectory, "en-us.xml"), true);
		}

		public void ApplyLanguage(string strLanguageDirectory, string strLanguage)
		{
			if (strLanguage != "en-us")
			{
				LoadFile(Path.Combine(strLanguageDirectory, strLanguage + ".xml"), false);
				string strDataPath = Path.Combine(strLanguageDirectory, strLanguage + "_data.xml");
				if (File.Exists(strDataPath))
				{
					try
					{
						DataDocument = new XmlDocument();
						DataDocument.Load(strDataPath);
					}
					catch
					{
						DataDocument = null;
					}
				}
			}
		}

		public string GetString(string strKey)
		{
			return _dicStrings[strKey].Replace("\\n", "\n");
		}

		private void LoadFile(string strPath, bool blnReplace)
		{
			XmlDocument objDocument = new XmlDocument();
			objDocument.Load(strPath);
			foreach (XmlNode objNode in objDocument.SelectNodes("/chummer/strings/string"))
			{
				string strKey = objNode["key"].InnerText;
				if (blnReplace || _dicStrings.ContainsKey(strKey))
					_dicStrings[strKey] = objNode["text"].InnerText;
			}
		}
	}
}
