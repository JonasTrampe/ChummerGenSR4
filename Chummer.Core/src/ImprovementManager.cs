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
}
