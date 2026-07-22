using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>Platform-neutral language string catalog loaded from Chummer language XML files.</summary>
    public sealed class LanguageStringCatalog
    {
        private readonly Dictionary<string, string> _dicStrings = new();
        public XmlDocument? DataDocument { get; private set; }

        public void Reset()
        {
            _dicStrings.Clear();
            DataDocument = null;
        }

        public void LoadBase(string strLanguageDirectory)
        {
            LoadFile(Path.Combine(strLanguageDirectory, "en-us.xml"), true);
        }

        public void ApplyLanguage(string strLanguageDirectory, string strLanguage)
        {
            if (strLanguage != "en-us")
            {
                LoadFile(Path.Combine(strLanguageDirectory, strLanguage + ".xml"), false);
                var strDataPath = Path.Combine(strLanguageDirectory, strLanguage + "_data.xml");
                if (File.Exists(strDataPath))
                    try
                    {
                        DataDocument = new XmlDocument();
                        DataDocument.Load(strDataPath);
                    }
                    catch
                    {
                        DataDocument = new XmlDocument();
                    }
            }
        }

        public string GetString(string strKey)
        {
            return _dicStrings[strKey].Replace("\\n", "\n");
        }

        public List<string> VerifyLanguage(string strLanguageDirectory, string strLanguage)
        {
            var dicEnglish = ReadStrings(Path.Combine(strLanguageDirectory, "en-us.xml"));
            var dicLanguage = ReadStrings(Path.Combine(strLanguageDirectory, strLanguage + ".xml"));
            var lstMessages = new List<string>();
            foreach (var strKey in dicEnglish.Keys)
                if (!dicLanguage.ContainsKey(strKey))
                    lstMessages.Add("Missing String: " + strKey);
            foreach (var strKey in dicLanguage.Keys)
                if (!dicEnglish.ContainsKey(strKey))
                    lstMessages.Add("Unused String: " + strKey);
            return lstMessages;
        }

        private static Dictionary<string, string> ReadStrings(string strPath)
        {
            var objDocument = new XmlDocument();
            objDocument.Load(strPath);
            var dicStrings = new Dictionary<string, string>();
            foreach (XmlNode objNode in GetStringNodes(objDocument))
                dicStrings[GetRequiredValue(objNode, "key")] = GetRequiredValue(objNode, "text");
            return dicStrings;
        }

        private void LoadFile(string strPath, bool blnReplace)
        {
            var objDocument = new XmlDocument();
            objDocument.Load(strPath);
            foreach (XmlNode objNode in GetStringNodes(objDocument))
            {
                var strKey = GetRequiredValue(objNode, "key");
                if (blnReplace || _dicStrings.ContainsKey(strKey))
                    _dicStrings[strKey] = GetRequiredValue(objNode, "text");
            }
        }

        private static XmlNodeList GetStringNodes(XmlDocument objDocument)
        {
            return objDocument.SelectNodes("/chummer/strings/string")
                   ?? throw new InvalidDataException("Language file does not contain a strings section.");
        }

        private static string GetRequiredValue(XmlNode objNode, string strName)
        {
            return objNode[strName]?.InnerText
                   ?? throw new InvalidDataException("Language string is missing the required '" + strName + "' value.");
        }
    }
}
