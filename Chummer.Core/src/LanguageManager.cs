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
    private readonly object _lockObject = new();

    private LanguageManager()
    {
    }

    /// <summary>Whether the base (en-us) language file loaded successfully.</summary>
    public bool Loaded { get; private set; }

    /// <summary>XmlDocument that holds item name (data) translations for the active language.</summary>
    public XmlDocument? DataDoc => _objCatalog.DataDocument;

    /// <summary>
    /// Load the base English strings and, if a non-English language is requested, overlay its
    /// strings and data translations on top. Unlike the first Avalonia port draft, this method
    /// fully reloads on every call so runtime language changes can take effect.
    /// </summary>
    /// <param name="strLanguage">Language code to load, e.g. "de-de". "en-us" is the built-in base.</param>
    public void Load(string strLanguage)
    {
        // Several call sites (GlobalOptions/XmlManager's own static constructors, every
        // `new CharacterOptions()`) lazily re-trigger a reload on first access, independently of
        // App's own startup Load - under concurrent/multi-threaded access (this class has no way
        // to know it's not the only caller) this lock keeps concurrent reload attempts from
        // interleaving. LanguageStringCatalog.Load itself builds the new catalog off to the side
        // and only swaps it in atomically at the end, so a concurrent GetString on another thread
        // never observes a half-populated/empty catalog either.
        lock (_lockObject)
        {
            var strLanguageDirectory = Path.Combine(AppContext.BaseDirectory, "data", "lang");
            try
            {
                _objCatalog.Load(strLanguageDirectory, strLanguage);
                Loaded = true;
            }
            catch
            {
                // No usable en-us.xml on disk - GetString() will throw for callers. The legacy
                // version popped a MessageBox and called Application.Exit() here, which isn't
                // appropriate for a library with no UI or process ownership of its own.
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
        if (GlobalOptions.Instance.Language == "en-us" || string.IsNullOrWhiteSpace(strExtra))
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
