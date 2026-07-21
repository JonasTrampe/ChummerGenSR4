using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Chummer.Core;

/// <summary>
/// Platform-neutral UI-string language manager, ported from the legacy WinForms
/// <c>clsLanguageManager.cs</c>. Only the loading/lookup logic both hosts actually share was
/// ported - the WinForms-specific "walk this Form/Control tree and set .Text from .Tag"
/// translation methods were left behind, since Avalonia handles that through bindings, not a
/// control-tree walk.
/// </summary>
public sealed class LanguageManager
{
    public static LanguageManager Instance { get; } = new();

    private readonly LanguageStringCatalog _objCatalog = new();
    private string _strLanguage = string.Empty;

    private LanguageManager()
    {
    }

    /// <summary>Whether the base (en-us) language file loaded successfully.</summary>
    public bool Loaded { get; private set; }

    /// <summary>XmlDocument that holds item name (data) translations for the active language.</summary>
    public XmlDocument? DataDoc => _objCatalog.DataDocument;

    /// <summary>
    /// Load the base English strings (once per process) and, if a non-English language is
    /// requested, overlay its strings and data translations on top. Mirrors the legacy
    /// behavior of only ever applying the first non-English language requested for the
    /// process lifetime - <paramref name="strLanguage"/> is otherwise ignored on later calls.
    /// </summary>
    /// <param name="strLanguage">Language code to load, e.g. "de-de". "en-us" is the built-in base.</param>
    public void Load(string strLanguage)
    {
        var strLanguageDirectory = Path.Combine(AppContext.BaseDirectory, "data", "lang");
        if (!Loaded)
        {
            try
            {
                _objCatalog.LoadBase(strLanguageDirectory);
                Loaded = true;
            }
            catch
            {
                // No usable en-us.xml on disk - GetString() will throw for callers. The legacy
                // version popped a MessageBox and called Application.Exit() here, which isn't
                // appropriate for a library with no UI or process ownership of its own.
                return;
            }
        }

        if (strLanguage != "en-us" && _strLanguage == string.Empty)
        {
            try
            {
                _strLanguage = strLanguage;
                _objCatalog.ApplyLanguage(strLanguageDirectory, strLanguage);
            }
            catch
            {
                // Keep the base English strings loaded; just skip the overlay.
            }
        }
    }

    /// <summary>Retrieve a translated UI string by key.</summary>
    public string GetString(string strKey) => _objCatalog.GetString(strKey);

    /// <summary>Check the keys in the selected language file against the English version.</summary>
    public List<string> VerifyStrings(string strLanguage) =>
        _objCatalog.VerifyLanguage(Path.Combine(AppContext.BaseDirectory, "data", "lang"), strLanguage);

    /// <summary>
    /// Attempt to translate an attribute abbreviation ("BOD", "AGI", ...) used as an "Extra"
    /// value. The legacy version also looked up weapon/skill/mentor/paragon category names via
    /// XmlManager's data-file cache; that lookup wasn't ported since nothing in the Avalonia
    /// port needs it yet - add it back here if that changes.
    /// </summary>
    public string TranslateExtra(string strExtra)
    {
        if (_strLanguage == "en-us" || string.IsNullOrWhiteSpace(strExtra))
            return strExtra;

        var strKey = strExtra switch
        {
            "BOD" => "String_AttributeBODShort",
            "AGI" => "String_AttributeAGIShort",
            "REA" => "String_AttributeREAShort",
            "STR" => "String_AttributeSTRShort",
            "CHA" => "String_AttributeCHAShort",
            "INT" => "String_AttributeINTShort",
            "LOG" => "String_AttributeLOGShort",
            "WIL" => "String_AttributeWILShort",
            "EDG" => "String_AttributeEDGShort",
            "MAG" => "String_AttributeMAGShort",
            "RES" => "String_AttributeRESShort",
            _ => string.Empty,
        };

        if (strKey == string.Empty)
            return strExtra;

        try
        {
            return _objCatalog.GetString(strKey);
        }
        catch
        {
            return strExtra;
        }
    }
}
