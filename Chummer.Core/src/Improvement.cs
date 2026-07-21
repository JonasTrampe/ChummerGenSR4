#nullable enable
using System;
using System.Xml;

namespace Chummer.Core;

/// <summary>
/// A single bonus/modifier record, ported from clsImprovement.cs's <c>Improvement</c> class.
/// This is the read model only (Phase 1.1 of the porting plan) - creating/removing
/// Improvements as gear/qualities/powers are added is a later phase once Core has a write
/// path at all.
/// </summary>
public sealed class Improvement
{
    public string UniqueName { get; }
    public string ImprovedName { get; }
    public string SourceName { get; }
    public int Minimum { get; }
    public int Maximum { get; }
    public int Augmented { get; }
    public int AugmentedMaximum { get; }
    public int Value { get; }
    public int Rating { get; }
    public ImprovementType Type { get; }
    public ImprovementSource Source { get; }
    public bool Custom { get; }
    public bool AddToRating { get; }
    public bool Enabled { get; }

    private Improvement(string strUniqueName, string strImprovedName, string strSourceName, int intMinimum,
        int intMaximum, int intAugmented, int intAugmentedMaximum, int intValue, int intRating,
        ImprovementType eType, ImprovementSource eSource, bool blnCustom, bool blnAddToRating, bool blnEnabled)
    {
        UniqueName = strUniqueName;
        ImprovedName = strImprovedName;
        SourceName = strSourceName;
        Minimum = intMinimum;
        Maximum = intMaximum;
        Augmented = intAugmented;
        AugmentedMaximum = intAugmentedMaximum;
        Value = intValue;
        Rating = intRating;
        Type = eType;
        Source = eSource;
        Custom = blnCustom;
        AddToRating = blnAddToRating;
        Enabled = blnEnabled;
    }

    /// <summary>Parse one &lt;improvement&gt; node. Unknown/malformed type or source values fall back
    /// to disabled so a corrupt entry can't silently be double-counted as something else.</summary>
    public static Improvement Load(XmlNode objNode)
    {
        var blnTypeOk = Enum.TryParse(GetValue(objNode, "improvementttype"), out ImprovementType eType);
        var blnSourceOk = Enum.TryParse(GetValue(objNode, "improvementsource"), out ImprovementSource eSource);

        return new Improvement(
            GetValue(objNode, "unique"),
            GetValue(objNode, "improvedname"),
            GetValue(objNode, "sourcename"),
            GetInt(objNode, "min"),
            GetInt(objNode, "max"),
            GetInt(objNode, "aug"),
            GetInt(objNode, "augmax"),
            GetInt(objNode, "val"),
            GetInt(objNode, "rating", 1),
            eType,
            eSource,
            GetBool(objNode, "custom"),
            GetBool(objNode, "addtorating"),
            blnTypeOk && blnSourceOk && GetBool(objNode, "enabled", true));
    }

    private static string GetValue(XmlNode objNode, string strName)
        => objNode[strName]?.InnerText ?? string.Empty;

    private static int GetInt(XmlNode objNode, string strName, int intFallback = 0)
        => int.TryParse(objNode[strName]?.InnerText, out var intValue) ? intValue : intFallback;

    private static bool GetBool(XmlNode objNode, string strName, bool blnFallback = false)
        => bool.TryParse(objNode[strName]?.InnerText, out var blnValue) ? blnValue : blnFallback;
}
