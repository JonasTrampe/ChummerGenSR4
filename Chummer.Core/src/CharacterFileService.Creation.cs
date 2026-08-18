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
        public string Metatype => GetValue("/character/metatype", string.Empty);

        public string MetatypeCategory => GetValue("/character/metatypecategory", string.Empty);

        public bool IsSprite => Metatype.EndsWith("Sprite", StringComparison.Ordinal);

        /// <summary>Whether this Sprite has completed the legacy "Convert to Free Sprite"
        /// special command. The original Sprite metatype is retained; legacy changes only the
        /// metatype category.</summary>
        public bool IsFreeSprite => string.Equals(MetatypeCategory, "Free Sprite", StringComparison.Ordinal);

        /// <summary>Replaces a character's base metatype while retaining the number of natural
        /// attribute points purchased above each old minimum. This is the non-interactive Core
        /// half of legacy frmCreate.ChangeMetatype: callers supply the picker result, while Core
        /// rejects the operation before mutating if a player-selected quality requires the old
        /// metatype or metavariant.</summary>
        public bool TryReplaceMetatype(NewCharacterMetatype objTarget, string strMetavariantName = "")
        {
            if (objTarget == null || string.IsNullOrWhiteSpace(objTarget.Name)
                || !IsBookEnabled(objTarget.Source))
                return false;

            NewCharacterMetavariant? objVariant = string.IsNullOrWhiteSpace(strMetavariantName)
                ? null
                : objTarget.Metavariants.FirstOrDefault(v => string.Equals(v.Name, strMetavariantName,
                    StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(strMetavariantName) && (objVariant == null || !IsBookEnabled(objVariant.Source)))
                return false;
            if (HasSelectedQualityDependingOn(Metatype, Metavariant))
                return false;

            XmlDocument objMetatypes = XmlManager.Instance.Load("metatypes.xml");
            XmlNode? objTargetRule = objMetatypes.SelectSingleNode(
                "/chummer/metatypes/metatype[name = '" + objTarget.Name + "']");
            if (objTargetRule == null)
                return false;
            XmlNode? objVariantRule = string.IsNullOrEmpty(strMetavariantName) ? null
                : objTargetRule.SelectSingleNode("metavariants/metavariant[name = '" + strMetavariantName + "']");

            // Legacy removes the old Metatype and Metavariant improvement sets before the
            // picker result is applied. Do it only after every rejection condition above.
            RemoveBonusImprovements(ImprovementSource.Metatype, Metatype);
            RemoveBonusImprovements(ImprovementSource.Metavariant, Metavariant);
            RemoveMetatypeQualities();

            var dicPurchased = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string strCode in s_astrMetatypeAttributeCodes)
                dicPurchased[strCode] = Math.Max(0, GetAttributeBaseInt(strCode) - GetAttributeMinimum(strCode));

            int intBp = objVariant?.Bp ?? objTarget.Bp;
            SetRootValue("metatype", objTarget.Name);
            SetRootValue("metatypebp", intBp.ToString(CultureInfo.InvariantCulture));
            SetRootValue("metavariant", objVariant?.Name ?? string.Empty);
            SetRootValue("metatypecategory", objTarget.Category);
            SetRootValue("movement", objTarget.Movement);
            string[] astrMovement = objTarget.Movement.Split('/', StringSplitOptions.TrimEntries);
            SetRootValue("movementwalk", astrMovement.Length > 0 ? astrMovement[0] : string.Empty);
            SetRootValue("movementswim", astrMovement.Length > 1 ? astrMovement[1] : string.Empty);

            foreach (string strCode in s_astrMetatypeAttributeCodes)
            {
                if (!objTarget.AttributeRanges.TryGetValue(strCode, out var objRange))
                    continue;
                XmlNode? objAttribute = GetAttributeNode(strCode);
                if (objAttribute == null)
                    continue;
                int intMin = ParseInteger(objRange.Min);
                int intMax = ParseInteger(objRange.Max);
                int intValue = Math.Clamp(intMin + dicPurchased[strCode], intMin, intMax);
                SetChildValue(objAttribute, "metatypemin", objRange.Min);
                SetChildValue(objAttribute, "metatypemax", objRange.Max);
                SetChildValue(objAttribute, "metatypeaugmax", objRange.Aug);
                SetChildValue(objAttribute, "value", intValue.ToString(CultureInfo.InvariantCulture));
                SetChildValue(objAttribute, "totalvalue", intValue.ToString(CultureInfo.InvariantCulture));
            }
            ApplyBonus(objTargetRule.SelectSingleNode("bonus"), ImprovementSource.Metatype, objTarget.Name);
            if (objVariantRule != null)
                ApplyBonus(objVariantRule.SelectSingleNode("bonus"), ImprovementSource.Metavariant,
                    strMetavariantName);
            AddMetatypeQualities(objTargetRule);
            if (objVariantRule != null)
                AddMetatypeQualities(objVariantRule);
            Changed?.Invoke();
            return true;
        }

        public int StartingBuildPoints => int.TryParse(GetValue("/character/startingbuildpoints", "0"), out var i) ? i : 0;

        /// <summary>Creation budget state suitable for UI summaries. The persisted pool remains
        /// authoritative, while the individual categories make the spent total explainable and
        /// expose any document parts that have not yet been ported to the tracker.</summary>
        public int NuyenPointsMax
        {
            get
            {
                if (GetCharacterOptions().UnrestrictedNuyen)
                    return StartingBuildPoints > 0 ? StartingBuildPoints : 1000;
                return int.TryParse(GetValue("/character/nuyenmaxbp", "0"), out var v) ? v : 0;
            }
        }

        /// <summary>Nuyen granted per point spent, for the current <see cref="BuildMethod"/>.</summary>
        public int EssencePenalty
        {
            get
            {
                var (dblBase, dblTotal) = ComputeEssenceDecimal();
                return (int)Math.Ceiling(dblBase - dblTotal);
            }
        }

        /// <summary>Ported from frmCareer.cs's MetatypeSelected() (lblMAG/lblRES.Text): the MAG/RES
        /// value shown to the player is reduced point-for-point by <see cref="EssencePenalty"/>.
        /// Under the EssLossReducesMaximumOnly house rule, the raw value is instead only clamped
        /// down if it exceeds the attribute's metatype maximum - which Essence loss itself never
        /// reduces here, faithfully matching a legacy quirk where this house rule doesn't actually
        /// lower the maximum anywhere in the codebase, only changes how the current value clamps
        /// against the (unchanged) one. Only affects the displayed attribute value - calculations
        /// that read MAG/RES via <see cref="GetAttributeInt"/> (Adept Power Points, Awakened, etc.)
        /// intentionally keep using the raw, unadjusted total, matching legacy.</summary>
        private bool IgnoreRules => string.Equals(GetValue("/character/ignorerules", "False"), "True",
            StringComparison.OrdinalIgnoreCase);

    }
}
