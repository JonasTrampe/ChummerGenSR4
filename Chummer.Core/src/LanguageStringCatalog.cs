using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>Platform-neutral language string catalog loaded from Chummer language XML files.
    /// Multiple call sites reload this lazily on first access (GlobalOptions, XmlManager) - under
    /// concurrent test execution (or any multi-threaded host) that reload could previously race
    /// against a concurrent <see cref="GetString"/> read on another thread, intermittently
    /// throwing KeyNotFoundException mid-repopulation. Guarded by a lock so a reload always
    /// completes atomically from a reader's perspective.</summary>
    public sealed class LanguageStringCatalog
    {
        private readonly object _lockObject = new();
        private Dictionary<string, string> _dicStrings = new();
        public XmlDocument? DataDocument { get; private set; }

        /// <summary>Loads the base en-us strings and, if <paramref name="strLanguage"/> isn't
        /// "en-us", overlays that language's strings/data on top - all built into a local working
        /// dictionary that only replaces the live <see cref="_dicStrings"/>/<see
        /// cref="DataDocument"/> in one atomic swap at the very end. Reset()/LoadBase()/
        /// ApplyLanguage() used to be three separate public calls each mutating the shared field
        /// directly, which left a real window (the moment right after a Reset(), before the next
        /// LoadFile() repopulated it) where a concurrent <see cref="GetString"/> on another thread
        /// could observe a genuinely empty catalog and throw - this reload can be called from
        /// several unrelated first-access sites (GlobalOptions, XmlManager, every
        /// `new CharacterOptions()`), so that's not a hypothetical, it reproduced reliably under
        /// this port's own parallel test run.</summary>
        public void Load(string strLanguageDirectory, string strLanguage)
        {
            var dicNew = new Dictionary<string, string>();
            LoadFileInto(dicNew, Path.Combine(strLanguageDirectory, "en-us.xml"), true);

            XmlDocument? objNewDataDocument = null;
            if (strLanguage != "en-us")
            {
                LoadFileInto(dicNew, Path.Combine(strLanguageDirectory, strLanguage + ".xml"), false);
                var strDataPath = Path.Combine(strLanguageDirectory, strLanguage + "_data.xml");
                if (File.Exists(strDataPath))
                {
                    try
                    {
                        objNewDataDocument = new XmlDocument();
                        objNewDataDocument.Load(strDataPath);
                    }
                    catch
                    {
                        objNewDataDocument = new XmlDocument();
                    }
                }
            }

            lock (_lockObject)
            {
                _dicStrings = dicNew;
                DataDocument = objNewDataDocument;
            }
        }

        public string GetString(string strKey)
        {
            Dictionary<string, string> dicSnapshot;
            lock (_lockObject) dicSnapshot = _dicStrings;
            return dicSnapshot[strKey].Replace("\\n", "\n");
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

        private static void LoadFileInto(Dictionary<string, string> dicTarget, string strPath, bool blnReplace)
        {
            var objDocument = new XmlDocument();
            objDocument.Load(strPath);
            foreach (XmlNode objNode in GetStringNodes(objDocument))
            {
                var strKey = GetRequiredValue(objNode, "key");
                if (blnReplace || dicTarget.ContainsKey(strKey))
                    dicTarget[strKey] = GetRequiredValue(objNode, "text");
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
