using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Chummer.Core
{
    /// <summary>
    ///     Drop-in replacement for the small slice of the Win32 Registry API (CreateSubKey / GetValue /
    ///     SetValue / DeleteValue) that clsOptions.cs and frmOptions.cs use for settings storage. Backs
    ///     onto a plain XML file under ApplicationData (%AppData% on Windows, ~/.config on Mono/Linux)
    ///     instead of the registry, since Mono's registry emulation (~/.mono/registry) isn't something
    ///     worth depending on for the Linux port. All values are stored and returned as strings, matching
    ///     how every existing call site already used the registry (SetValue was only ever called with
    ///     strings, and GetValue results were always immediately parsed via Convert.To*/ToString()).
    ///     On first use, if no settings file exists yet but legacy HKCU\Software\Chummer registry values
    ///     do (upgrade from a previous Windows install), those values are copied into the new file once.
    ///     The registry values themselves are left untouched in case of rollback.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly string[] LegacySubKeyPaths = ["Software\\Chummer", "Software\\Chummer\\Sourcebook"];

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChummerGenSR4", "settings.xml");

        private static Dictionary<string, Dictionary<string, string>> _dicData = [];
        private static readonly Lock LockObject = new();

        public static SettingsRegistryKey CurrentUser => new();

        private static Dictionary<string, Dictionary<string, string>> Data
        {
            get
            {
                lock (LockObject)
                {
                    if (_dicData.Count == 0)
                    {
                        _dicData = Load();
                        if (_dicData.Count == 0)
                            MigrateFromWindowsRegistry();
                    }

                    return _dicData;
                }
            }
        }

        private static Dictionary<string, Dictionary<string, string>> Load()
        {
            var dicResult = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(SettingsFilePath))
                return dicResult;

            try
            {
                var objDocument = XDocument.Load(SettingsFilePath);
                foreach (var objKeyElement in objDocument.Root?.Elements("key") ?? Enumerable.Empty<XElement>())
                {
                    var strPath = objKeyElement.Attribute("path")?.Value;
                    if (string.IsNullOrEmpty(strPath))
                        continue;

                    var dicValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var objValueElement in objKeyElement.Elements("value"))
                    {
                        var strName = objValueElement.Attribute("name")?.Value;
                        if (!string.IsNullOrEmpty(strName))
                            dicValues[strName] = objValueElement.Value;
                    }

                    dicResult[strPath] = dicValues;
                }
            }
            catch
            {
                // Corrupt or unreadable settings file - fall back to empty settings rather than crash.
            }

            return dicResult;
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath) ?? string.Empty);
                var objDocument = new XDocument(new XElement("settings",
                    _dicData.Select(objKey => new XElement("key",
                        new XAttribute("path", objKey.Key),
                        objKey.Value.Select(objValue => new XElement("value",
                            new XAttribute("name", objValue.Key), objValue.Value))))));
                objDocument.Save(SettingsFilePath);
            }
            catch
            {
                // Best-effort persistence - a failed write shouldn't crash the caller (matches how
                // the old registry calls were always wrapped in try/catch by their callers).
            }
        }

        private static void MigrateFromWindowsRegistry()
        {
            if (RuntimeInfo.IsWindows)
                return;
#pragma warning disable CA1416
            try
            {
                var blnMigratedAnything = false;
                foreach (var strSubKeyPath in LegacySubKeyPaths)
                {

                    var objRegistryKey = Registry.CurrentUser.OpenSubKey(strSubKeyPath);

                    if (objRegistryKey == null)
                        continue;

                    var dicValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var strValueName in objRegistryKey.GetValueNames())
                    {
                        var objValue = objRegistryKey.GetValue(strValueName);
                        if (objValue != null)
                        {
                            dicValues[strValueName] = objValue.ToString() ?? "";
                            blnMigratedAnything = true;
                        }
                    }

                    if (dicValues.Count > 0)
                        _dicData[strSubKeyPath] = dicValues;
                }

                if (blnMigratedAnything)
                    Save();
            }
            catch
            {
                // Migration is a nice-to-have, not a hard requirement - never let it block startup.
            }
#pragma warning restore CA1416
        }

        internal static string GetValue(string strSubKeyPath, string strName)
        {
            lock (LockObject)
            {
                return Data.TryGetValue(strSubKeyPath, out var dicValues) &&
                       dicValues.TryGetValue(strName, out var strValue)
                    ? strValue
                    : "";
            }
        }

        internal static void SetValue(string strSubKeyPath, string strName, string strValue)
        {
            lock (LockObject)
            {
                if (!Data.TryGetValue(strSubKeyPath, out var dicValues))
                {
                    dicValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    Data[strSubKeyPath] = dicValues;
                }

                dicValues[strName] = strValue;
                Save();
            }
        }

        internal static void DeleteValue(string strSubKeyPath, string strName)
        {
            lock (LockObject)
            {
                if (Data.TryGetValue(strSubKeyPath, out var dicValues) && dicValues.Remove(strName))
                    Save();
            }
        }
    }

    /// <summary>
    ///     Mimics the tiny slice of System.Win32.RegistryKey's API (CreateSubKey/GetValue/SetValue/
    ///     DeleteValue) that this codebase actually uses, so call sites only needed a type swap rather
    ///     than a full rewrite. See <see cref="SettingsStore" /> for the backing implementation.
    /// </summary>
    public sealed class SettingsRegistryKey
    {
        private string SubKeyPath { get; init; } = string.Empty;

        public SettingsRegistryKey CreateSubKey(string strSubKeyPath)
        {
            return new SettingsRegistryKey { SubKeyPath = strSubKeyPath };
        }

        public object GetValue(string strName)
        {
            return SettingsStore.GetValue(SubKeyPath, strName);
        }

        public void SetValue(string strName, object objValue)
        {
            SettingsStore.SetValue(SubKeyPath, strName, objValue.ToString() ?? string.Empty);
        }

        public void DeleteValue(string strName)
        {
            SettingsStore.DeleteValue(SubKeyPath, strName);
        }
    }
}