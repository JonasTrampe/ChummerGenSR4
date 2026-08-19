using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Core
{
    public sealed partial class CharacterDocument
    {
        public int MysticAdeptMagicianMagSplit =>
            int.TryParse(GetValue("/character/magsplitmagician", "0"), out var i) ? i : 0;

        /// <summary>Ported from frmCareer.cs's nudMysticAdeptMAGMagician_ValueChanged: sets how
        /// many points of a Mystic Adept's MAG rating go to spellcasting, with the remainder
        /// (down to 0, and further reduced by EssencePenalty as in legacy) going to Adept
        /// Powers.</summary>
        public bool Created => GetValue("/character/created", "False") == "True";

        /// <summary>Ported from frmCreate.cs's ConfirmSaveCreatedCharacter: flips the character
        /// into career mode, at which point every UI already bound to IsCreateMode/Created
        /// (attribute/skill spinners, Nuyen entry, etc.) switches from the Create-suffixed
        /// point-spending methods to the normal Karma-spending ones. The starting-Lifestyle-Nuyen
        /// dice roll is handled separately by <see cref="GetLifestyleNuyenRollInfo"/> and
        /// <see cref="FinalizeCreationWithLifestyleNuyenRoll"/> (callers should prefer that
        /// overload when a UI can prompt for the roll). Deliberately simplified vs. the legacy
        /// flow: no validation gate - unspent Karma/BP simply stays on the character as-is (for
        /// Karma builds this is exactly what legacy does too, since Karma left over from creation
        /// carries over as career Karma).</summary>
        public int NuyenPoints => int.TryParse(GetValue("/character/nuyenbp", "0"), out var v) ? v : 0;

        /// <summary>Maximum number of points that may be sunk into starting Nuyen during creation -
        /// ordinarily the persisted priority-table value, but with the UnrestrictedNuyen house
        /// rule on, ported from clsCharacter.cs's NuyenMaximumBP: the character's whole starting
        /// point/Karma budget instead (falling back to 1000 for saves with no recorded starting
        /// total, same as legacy).</summary>
        public bool SpendEdge() => SetEdgeRemaining(Edge.Remaining - 1);

        public bool RegainEdge() => SetEdgeRemaining(Edge.Remaining + 1);

        /// <summary>Permanently reduces the EDG attribute's own base value by 1 - ported from
        /// frmCareer.cs's cmdBurnEdge_Click. Distinct from SpendEdge/RegainEdge, which only track
        /// how much of the current maximum is currently used up; this lowers the maximum itself and
        /// can't be undone. False if EDG is already at 0.</summary>
        private string ComputeFlyMovement()
        {
            string strMovement = GetValue("/character/movement", string.Empty);
            if (string.Equals(strMovement, "Special", StringComparison.OrdinalIgnoreCase)) return "0";
            foreach (string strEntry in strMovement.Split(','))
                if (strEntry.Trim().StartsWith("Fly", StringComparison.OrdinalIgnoreCase))
                    return ApplyMovementPercent(strEntry.Trim().Substring(3).Trim(), ImprovementManager.ValueOf(Improvements, ImprovementType.FlyPercent));

            int intFlySpeed = ImprovementManager.ValueOf(Improvements, ImprovementType.FlySpeed);
            if (intFlySpeed == 0) return "0";
            string strBase = intFlySpeed > 0 ? intFlySpeed.ToString() : MultiplyMovement(WalkMovement, -intFlySpeed);
            return ApplyMovementPercent(strBase, ImprovementManager.ValueOf(Improvements, ImprovementType.FlyPercent));
        }

        private static string MultiplyMovement(string strMovement, int intMultiplier)
        {
            string[] parts = strMovement.Split('/');
            return string.Join("/", parts.Select(p => int.TryParse(p, out int value) ? (value * intMultiplier).ToString() : "0"));
        }

        private static string ApplyMovementPercent(string strMovement, int intPercent)
        {
            return string.Join("/", strMovement.Split('/').Select(p => int.TryParse(p.Trim(), out int value)
                ? (value + (int)Math.Floor(value * (intPercent / 100.0))).ToString() : "0"));
        }

        public CharacterConditionData Condition =>
            new(ComputeEssence(),
                GetValue("/character/physicalcmfilled", "0"), GetValue("/character/stuncmfilled", "0"),
                ComputePhysicalCm(), ComputeStunCm());

        /// <summary>Adjusts filled Physical condition-monitor boxes. The stored value is always
        /// clamped to the currently calculated monitor size, matching the usable range in the
        /// legacy career form.</summary>
        public bool AdjustPhysicalDamage(int intDelta) => AdjustConditionDamage("physicalcmfilled", ComputePhysicalCm().Value, intDelta);

        /// <summary>Adjusts filled Stun condition-monitor boxes. The stored value is always
        /// clamped to the currently calculated monitor size.</summary>
        private string ComputeEssence() => ComputeEssenceDecimal().Total.ToString("0.##", CultureInfo.InvariantCulture);

        private bool AdjustConditionDamage(string strElementName, int intMaximum, int intDelta)
        {
            int intCurrent = int.TryParse(GetValue("/character/" + strElementName, "0"), out var intValue)
                ? intValue
                : 0;
            int intNewValue = Math.Clamp(intCurrent + intDelta, 0, Math.Max(0, intMaximum));
            if (intNewValue == intCurrent)
                return false;

            SetRootValue(strElementName, intNewValue.ToString());
            return true;
        }

        public int WoundModifiers
        {
            get
            {
                int intPhysical = int.TryParse(GetValue("/character/physicalcmfilled", "0"), out var p) ? p : 0;
                int intStun = int.TryParse(GetValue("/character/stuncmfilled", "0"), out var s) ? s : 0;
                int intDamagePenalty = -((Math.Max(0, intPhysical) + 2) / 3) - ((Math.Max(0, intStun) + 2) / 3);
                int intImprovement = Improvements.Where(i => i.Enabled && i.Source == ImprovementSource.ConditionMonitor)
                    .Sum(i => i.Value);
                return intDamagePenalty + intImprovement;
            }
        }

        /// <summary>Initiative (INT + REA, base/augmented shown as "base (augmented)" when they
        /// differ), ported from clsCharacter.cs. Simplified: the legacy version also clamps to a
        /// per-metatype maximum via a "special INI attribute" that's never actually populated from
        /// the save file in normal play (always defaults to unconstrained) - not ported since Core
        /// doesn't load metatype data. Never goes below 0.</summary>
        private static CharacterDerivedValueData BuildEncumbranceValue(int intTotal, int intThreshold,
            string strKind, string strThresholdNote, IReadOnlyList<string> lstWorn, bool blnIgnoreEncumbrance,
            bool blnNoSinglePiecePenalty, int intPenaltyBonus)
        {
            var sb = new StringBuilder();
            sb.Append("Getragene Panzerung (").Append(strKind).Append("): ").Append(intTotal);
            foreach (var strItem in lstWorn)
                sb.Append('\n').Append("  ").Append(strItem);
            sb.Append('\n').Append(strThresholdNote);

            int intPenalty;
            if (blnIgnoreEncumbrance)
            {
                intPenalty = 0;
                sb.Append('\n').Append("Behinderung durch Panzerung ignoriert (Hausregel)");
            }
            else if (blnNoSinglePiecePenalty && intTotal > intThreshold)
            {
                intPenalty = 0;
                sb.Append('\n').Append("Keine Behinderung bei nur einem Panzerungsstück (Hausregel)");
            }
            else
            {
                intPenalty = ComputeEncumbrancePenalty(intTotal, intThreshold) - intPenaltyBonus;
                sb.Append('\n').Append("Behinderung: ").Append(intPenalty);
            }

            return new CharacterDerivedValueData(intPenalty, sb.ToString());
        }

        // Armor ratings in the save file can carry a leading "+" marking stacking bonus armor
        // (see ComputeArmorRating) - int.TryParse already accepts a leading "+" so this just
        // strips anything trailing the number (per-mod bonuses aren't modeled yet, see
        // ArmorEncumbrance's doc comment).
        private static int ComputeEncumbrancePenalty(int intTotal, int intThreshold)
        {
            if (intTotal <= intThreshold) return 0;
            return -(int)Math.Ceiling((intTotal - intThreshold) / 2.0);
        }

    }
}
