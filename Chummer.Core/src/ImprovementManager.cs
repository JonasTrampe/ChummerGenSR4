#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Chummer.Core;

/// <summary>
/// Aggregation queries over a character's <see cref="Improvement"/> list, ported from
/// clsImprovement.cs's <c>ImprovementManager.ValueOf</c>.
///
/// Two edge cases from the legacy version were deliberately NOT ported (both are narrow and
/// each needs its own follow-up):
///  - The Technomancer/Gear MatrixInitiativePass exclusion (Technomancers can't benefit from
///    Gear-sourced Matrix Initiative Pass bonuses) - needs CharacterDocument.RESEnabled, which
///    doesn't exist in Core yet.
///  - The "precedence0"/"precedence1" UniqueName overrides (used by a handful of metatype/
///    quality bonuses to say "ignore every other bonus of this type, use only mine") - the
///    general UniqueName-dedup-to-max behavior below covers the common case; precedence
///    overrides are rare enough to add once a save file actually needs them for a test to fail against.
/// </summary>
public static class ImprovementManager
{
    /// <summary>
    /// Sum of all enabled, non-custom Improvements' <see cref="Improvement.Value"/> for the
    /// given type (and optionally a specific <see cref="Improvement.ImprovedName"/>, e.g. an
    /// attribute code). Bonuses that share a UniqueName are deduplicated to the single highest
    /// value among them, mirroring the legacy "only the best bonus of a named group counts" rule
    /// (e.g. multiple sources of the same Cyberware Essence discount don't stack).
    /// </summary>
    public static int ValueOf(IReadOnlyList<Improvement> lstImprovements, ImprovementType eType,
        string? strImprovedName = null, bool blnAddToRating = false)
    {
        var intValue = 0;
        var dicHighestByUniqueName = new Dictionary<string, int>();

        foreach (var objImprovement in lstImprovements)
        {
            if (!objImprovement.Enabled || objImprovement.Custom || objImprovement.Type != eType)
                continue;
            if (objImprovement.AddToRating != blnAddToRating)
                continue;
            if (strImprovedName != null && objImprovement.ImprovedName != strImprovedName)
                continue;

            if (objImprovement.UniqueName != string.Empty)
            {
                if (!dicHighestByUniqueName.TryGetValue(objImprovement.UniqueName, out var intHighest)
                    || objImprovement.Value > intHighest)
                    dicHighestByUniqueName[objImprovement.UniqueName] = objImprovement.Value;
            }
            else
            {
                intValue += objImprovement.Value;
            }
        }

        return intValue + dicHighestByUniqueName.Values.Sum();
    }

    /// <summary>Same as <see cref="ValueOf"/> but summing <see cref="Improvement.Augmented"/>
    /// instead - used for augmented-only bonuses (e.g. Wired Reflexes' Reaction boost, which
    /// raises the augmented value but not the character's base/unaugmented Reaction).</summary>
    public static int AugmentedValueOf(IReadOnlyList<Improvement> lstImprovements, ImprovementType eType,
        string? strImprovedName = null)
    {
        var intValue = 0;
        foreach (var objImprovement in lstImprovements)
        {
            if (!objImprovement.Enabled || objImprovement.Custom || objImprovement.Type != eType)
                continue;
            if (strImprovedName != null && objImprovement.ImprovedName != strImprovedName)
                continue;

            intValue += objImprovement.Augmented;
        }

        return intValue;
    }

    /// <summary>
    /// Per-source breakdown of what feeds into <see cref="ValueOf"/> for the same type/name/
    /// AddToRating filter - one entry per contributing Improvement's SourceName and Value, so a
    /// UI can show "why is this +5" when several different augmentations stack (e.g. Wired
    /// Reflexes +2 REA, a quality +1 REA, a spell +2 REA all contributing to the same total).
    /// Improvements sharing a UniqueName are still deduplicated to the single highest value
    /// among them (matching ValueOf), so this always sums to the same total ValueOf returns for
    /// the same arguments - it just doesn't collapse same-source-name entries together.
    /// </summary>
    public static IReadOnlyList<(string SourceName, int Value)> DescribeValueOf(
        IReadOnlyList<Improvement> lstImprovements, ImprovementType eType, string? strImprovedName = null,
        bool blnAddToRating = false)
    {
        var lstContributions = new List<(string SourceName, int Value)>();
        var dicHighestByUniqueName = new Dictionary<string, (string SourceName, int Value)>();

        foreach (var objImprovement in lstImprovements)
        {
            if (!objImprovement.Enabled || objImprovement.Custom || objImprovement.Type != eType)
                continue;
            if (objImprovement.AddToRating != blnAddToRating)
                continue;
            if (strImprovedName != null && objImprovement.ImprovedName != strImprovedName)
                continue;
            if (objImprovement.Value == 0)
                continue;

            if (objImprovement.UniqueName != string.Empty)
            {
                if (!dicHighestByUniqueName.TryGetValue(objImprovement.UniqueName, out var highest)
                    || objImprovement.Value > highest.Value)
                    dicHighestByUniqueName[objImprovement.UniqueName] = (objImprovement.SourceName, objImprovement.Value);
            }
            else
            {
                lstContributions.Add((objImprovement.SourceName, objImprovement.Value));
            }
        }

        lstContributions.AddRange(dicHighestByUniqueName.Values);
        return lstContributions;
    }

    /// <summary>Per-source breakdown for <see cref="AugmentedValueOf"/>, for tooltip display.</summary>
    public static IReadOnlyList<(string SourceName, int Value)> DescribeAugmentedValueOf(
        IReadOnlyList<Improvement> lstImprovements, ImprovementType eType, string? strImprovedName = null)
    {
        var lstContributions = new List<(string SourceName, int Value)>();
        foreach (var objImprovement in lstImprovements)
        {
            if (!objImprovement.Enabled || objImprovement.Custom || objImprovement.Type != eType)
                continue;
            if (strImprovedName != null && objImprovement.ImprovedName != strImprovedName)
                continue;
            if (objImprovement.Augmented == 0)
                continue;

            lstContributions.Add((objImprovement.SourceName, objImprovement.Augmented));
        }

        return lstContributions;
    }
}
