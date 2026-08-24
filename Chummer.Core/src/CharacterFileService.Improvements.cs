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
        public string WalkMovement => ComputeMovement("land", ImprovementType.MovementPercent);

        /// <summary>Calculated swim movement, including SwimPercent Improvements.</summary>
        public string SwimMovement => ComputeMovement("Swim", ImprovementType.SwimPercent);

        /// <summary>Calculated fly movement, including FlyPercent/FlySpeed Improvements.</summary>
        private bool SetEdgeRemaining(int intRemaining)
        {
            int intMaximum = Edge.Maximum;
            if (intRemaining < 0 || intRemaining > intMaximum) return false;
            XmlElement objImprovements = Document.DocumentElement?.SelectSingleNode("improvements") as XmlElement
                ?? Document.CreateElement("improvements");
            if (objImprovements.ParentNode == null) Document.DocumentElement?.AppendChild(objImprovements);
            XmlNode? objExisting = objImprovements.SelectSingleNode("improvement[improvementsource = 'EdgeUse' and improvedname = 'EDG']");
            if (intRemaining == intMaximum)
            {
                if (objExisting != null) objImprovements.RemoveChild(objExisting);
            }
            else
            {
                XmlElement objImprovement = objExisting as XmlElement ?? Document.CreateElement("improvement");
                if (objExisting == null) objImprovements.AppendChild(objImprovement);
                SetChildValue(objImprovement, "improvementttype", "Attribute");
                SetChildValue(objImprovement, "improvementsource", "EdgeUse");
                SetChildValue(objImprovement, "improvedname", "EDG");
                SetChildValue(objImprovement, "sourcename", "edgeuse");
                SetChildValue(objImprovement, "aug", (intRemaining - intMaximum).ToString());
                SetChildValue(objImprovement, "rating", "1");
                SetChildValue(objImprovement, "enabled", "True");
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Total Karma earned over the character's career (sum of positive, non-refund
        /// Karma expense entries), ported from clsCharacter.cs's CareerKarma.</summary>
        private string ComputeMovement(string strKind, ImprovementType objPercentType)
        {
            string strMovement = GetValue("/character/movement", string.Empty);
            if (string.Equals(strMovement, "Special", StringComparison.OrdinalIgnoreCase))
                return "0";
            if (string.IsNullOrWhiteSpace(strMovement))
            {
                string strSaved = strKind == "land" ? GetValue("/character/movementwalk", string.Empty)
                    : strKind == "Swim" ? GetValue("/character/movementswim", string.Empty)
                    : GetValue("/character/movementfly", string.Empty);
                return string.IsNullOrWhiteSpace(strSaved) ? "0" : strSaved;
            }

            foreach (string strEntry in strMovement.Split(','))
            {
                string strValue = strEntry.Trim();
                bool blnSwim = strValue.StartsWith("Swim", StringComparison.OrdinalIgnoreCase);
                bool blnFly = strValue.StartsWith("Fly", StringComparison.OrdinalIgnoreCase);
                if ((strKind == "land" && (blnSwim || blnFly)) || (strKind == "Swim" && !blnSwim))
                    continue;
                if (strKind == "Swim") strValue = strValue.Substring(4).Trim();
                if (strKind == "land" || strKind == "Swim")
                    return ApplyMovementPercent(strValue, ImprovementManager.ValueOf(Improvements, objPercentType));
            }
            return "0";
        }

        private (double Base, double Total) ComputeEssenceDecimal()
        {
            double dblMax = double.TryParse(GetValue("/character/attributes/attribute[name = 'ESS']/metatypemax", "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var dblParsedMax) ? dblParsedMax : 0;
            var lstEssenceImprovements = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Essence);
            var lstEssenceMaxImprovements = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.EssenceMax);
            double dblBase = dblMax + lstEssenceImprovements.Sum(c => c.Value) + lstEssenceMaxImprovements.Sum(c => c.Value);

            var (dblCyberware, dblBioware, dblHoles) = SumCyberwareEssence();
            double dblHigher = Math.Max(dblCyberware, dblBioware);
            double dblLower = Math.Min(dblCyberware, dblBioware);
            double dblTotal = dblBase - dblHigher - (dblLower / 2) - dblHoles;

            // Legacy's CyborgEssence improvement is an explicit fixed-Essence override used by
            // cyborg bodies: installed ware no longer changes the displayed Essence; it is 0.1.
            if (Improvements.Any(i => i.Enabled && i.Type == ImprovementType.CyborgEssence))
                dblTotal = 0.1;

            return (dblBase, dblTotal);
        }

        /// <summary>Ported from clsCharacter.cs's EssencePenalty: whole points of Essence lost from
        /// the character's Essence maximum (metatype ESS max plus any EssenceMax Improvements),
        /// rounded up. 0 once fully healed/uninstalled back to the character's max Essence.</summary>
        private static void AppendContributions(StringBuilder sb, IReadOnlyList<(string SourceName, int Value)> lstContributions)
        {
            foreach (var (strSource, intValue) in lstContributions)
                sb.Append('\n').Append(DescribeSource(strSource)).Append(": ").Append(FormatSigned(intValue));
        }

        // Some legacy Improvements store the source item's GUID in SourceName instead of a
        // readable name (Core doesn't yet cross-reference that back to the actual gear/power/
        // quality that granted it - see PORTING_PLAN.md) - fall back to a generic label rather
        // than showing a raw GUID in a tooltip.
        // Cached per instance and invalidated whenever Changed fires (see the CharacterDocument
        // constructor) rather than recomputed on every access: ImprovementManager.ValueOf/
        // AugmentedValueOf/DescribeValueOf is called from ComputeSkillDicePool (once per skill),
        // per-attribute, and per-item bonus calculations, so a full character reload could hit
        // this dozens to 100+ times - each access previously re-walked the whole
        // /character/improvements/improvement XML subtree and rebuilt every Improvement object
        // from scratch, the same "expensive work redone per call" bug GetCharacterOptions() had.
        private List<Improvement>? _lstCachedImprovements;
        public IReadOnlyList<Improvement> Improvements => _lstCachedImprovements ??= ReadImprovements().ToList();

        /// <summary>Removes every &lt;improvement&gt; sharing <paramref name="strSourceName"/> whose
        /// improvementsource is "Custom" - ported from frmCareer.cs's cmdDeleteImprovement_Click,
        /// which only ever calls RemoveImprovements(ImprovementSource.Custom, ...): manually-created
        /// Improvements (frmCreateImprovement, not ported here - see PORTING_PLAN.md) are the
        /// only ones legacy itself lets the user delete from this list, since every other source
        /// (Quality/Cyberware/Metamagic/...) is a side effect of some other owned item and must be
        /// removed by removing that item instead.</summary>
        public bool RemoveCustomImprovement(string strSourceName)
        {
            if (string.IsNullOrWhiteSpace(strSourceName))
                return false;

            var objNodes = Document.SelectNodes("/character/improvements/improvement");
            if (objNodes == null)
                return false;

            bool blnRemovedAny = false;
            foreach (XmlNode objNode in objNodes.Cast<XmlNode>().ToList())
            {
                if (GetValue(objNode, "improvementsource", string.Empty) != "Custom"
                    || GetValue(objNode, "sourcename", string.Empty) != strSourceName)
                    continue;

                objNode.ParentNode?.RemoveChild(objNode);
                blnRemovedAny = true;
            }

            if (blnRemovedAny)
                Changed?.Invoke();
            return blnRemovedAny;
        }

        /// <summary>A curated subset of frmCreateImprovement.cs's ~50-type improvements.xml
        /// catalog - the types that map onto an <see cref="ImprovementType"/> this port's
        /// <see cref="BonusApplier"/> already parses and that already have a real consuming
        /// calculation elsewhere, rather than the full catalog (several of whose types have no
        /// consumer in this port at all yet - see the BonusApplier class doc comment).</summary>
        private void ApplySelectedImprovement(XmlNode? objXmlBonus, ImprovementSource eSource, string strSourceName,
            string strSelected, string strRating)
        {
            if (objXmlBonus == null || string.IsNullOrWhiteSpace(strSelected))
                return;

            if (objXmlBonus.SelectSingleNode("selecttext") != null)
            {
                AppendImprovement(new ImprovementSpec(ImprovementType.Text, strSelected.Trim()), eSource, strSourceName);
                return;
            }

            XmlNode? objSelectSkill = objXmlBonus.SelectSingleNode("selectskill");
            if (objSelectSkill != null)
            {
                bool blnAddToRating = objSelectSkill["applytorating"]?.InnerText == "yes";
                if (objSelectSkill["val"] != null)
                    AppendImprovement(new ImprovementSpec(ImprovementType.Skill, strSelected.Trim(),
                        Value: (int)RatingExpression.Evaluate(objSelectSkill["val"]!.InnerText, strRating),
                        AddToRating: blnAddToRating), eSource, strSourceName);
                if (objSelectSkill["max"] != null)
                    AppendImprovement(new ImprovementSpec(ImprovementType.Skill, strSelected.Trim(),
                        Maximum: (int)RatingExpression.Evaluate(objSelectSkill["max"]!.InnerText, strRating),
                        AddToRating: blnAddToRating), eSource, strSourceName);
                return;
            }

            XmlNode? objSelectAttribute = objXmlBonus.SelectSingleNode("selectattribute");
            if (objSelectAttribute != null)
            {
                // "affectbase" (e.g. the Improved Physical Attribute power) raises the Karma-cost
                // basis, not just the shown/augmented value - ported from clsImprovement.cs's
                // selectattribute handler, matching ParseSpecificAttribute's own affectbase check.
                string strAttribute = objSelectAttribute["affectbase"] != null
                    ? strSelected.Trim() + "Base" : strSelected.Trim();
                AppendImprovement(new ImprovementSpec(ImprovementType.Attribute, strAttribute,
                    Minimum: (int)RatingExpression.Evaluate(objSelectAttribute["min"]?.InnerText ?? string.Empty, strRating),
                    Maximum: (int)RatingExpression.Evaluate(objSelectAttribute["max"]?.InnerText ?? string.Empty, strRating),
                    Augmented: (int)RatingExpression.Evaluate(objSelectAttribute["val"]?.InnerText ?? string.Empty, strRating),
                    AugmentedMaximum: (int)RatingExpression.Evaluate(objSelectAttribute["aug"]?.InnerText ?? string.Empty, strRating)),
                    eSource, strSourceName);
                return;
            }

            XmlNode? objSelectSkillGroup = objXmlBonus.SelectSingleNode("selectskillgroup");
            if (objSelectSkillGroup != null && objSelectSkillGroup["bonus"] != null)
            {
                bool blnAddToRating = objSelectSkillGroup["applytorating"]?.InnerText == "yes";
                AppendImprovement(new ImprovementSpec(ImprovementType.SkillGroup, strSelected.Trim(),
                    Value: (int)RatingExpression.Evaluate(objSelectSkillGroup["bonus"]!.InnerText, strRating),
                    AddToRating: blnAddToRating), eSource, strSourceName);
            }
        }

        /// <summary>Removes the first saved quality matching its name, type, and optional detail,
        /// along with any Improvements its own &lt;bonus&gt; block granted on add.</summary>
        private void ApplyBonus(XmlNode? nodBonus, ImprovementSource eSource, string strSourceName,
            string strRating = "1", string strUnique = "")
        {
            IReadOnlyList<ImprovementSpec> lstSpecs = BonusApplier.Parse(nodBonus, strRating, strUnique);
            foreach (ImprovementSpec objSpec in lstSpecs)
                AppendImprovement(objSpec, eSource, strSourceName);
        }

        /// <summary>Persists one <see cref="ImprovementSpec"/> as an &lt;improvement&gt; element,
        /// in the shape <see cref="Improvement.Load"/> expects. Shared by <see cref="ApplyBonus"/>
        /// (rules-data-driven specs) and callers that construct a spec directly, like <see
        /// cref="RaiseInitiateGrade"/>'s MAG/RES-boosting Improvement (ported from
        /// clsImprovement.cs's CreateImprovement's actual XML write).</summary>
        private void AppendImprovement(ImprovementSpec objSpec, ImprovementSource eSource, string strSourceName)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objImprovements = objRoot.SelectSingleNode("improvements") as XmlElement;
            if (objImprovements == null)
            {
                objImprovements = Document.CreateElement("improvements");
                objRoot.AppendChild(objImprovements);
            }

            var objImprovement = Document.CreateElement("improvement");
            AppendElement(objImprovement, "improvementttype", objSpec.Type.ToString());
            AppendElement(objImprovement, "improvedname", objSpec.ImprovedName);
            AppendElement(objImprovement, "sourcename", strSourceName);
            AppendElement(objImprovement, "min", objSpec.Minimum.ToString(CultureInfo.InvariantCulture));
            AppendElement(objImprovement, "max", objSpec.Maximum.ToString(CultureInfo.InvariantCulture));
            AppendElement(objImprovement, "aug", objSpec.Augmented.ToString(CultureInfo.InvariantCulture));
            AppendElement(objImprovement, "augmax", objSpec.AugmentedMaximum.ToString(CultureInfo.InvariantCulture));
            AppendElement(objImprovement, "val", objSpec.Value.ToString(CultureInfo.InvariantCulture));
            AppendElement(objImprovement, "rating", objSpec.Rating.ToString(CultureInfo.InvariantCulture));
            AppendElement(objImprovement, "unique", objSpec.UniqueName);
            AppendElement(objImprovement, "improvementsource", eSource.ToString());
            AppendElement(objImprovement, "addtorating", objSpec.AddToRating.ToString());
            AppendElement(objImprovement, "enabled", "True");
            AppendElement(objImprovement, "custom", (eSource == ImprovementSource.Custom).ToString());
            objImprovements.AppendChild(objImprovement);
        }

        /// <summary>Removes every &lt;improvement&gt; sharing <paramref name="eSource"/> and
        /// <paramref name="strSourceName"/> - ported from clsImprovement.cs's
        /// RemoveImprovements(source, sourceName), used when the item that granted a bonus (a
        /// Quality, Adept Power, etc.) is itself removed.</summary>
        private void RemoveBonusImprovements(ImprovementSource eSource, string strSourceName)
        {
            var objNodes = Document.SelectNodes("/character/improvements/improvement");
            if (objNodes == null)
                return;

            foreach (XmlNode objNode in objNodes.Cast<XmlNode>().ToList())
            {
                if (GetValue(objNode, "improvementsource", string.Empty) != eSource.ToString()
                    || GetValue(objNode, "sourcename", string.Empty) != strSourceName)
                    continue;

                objNode.ParentNode?.RemoveChild(objNode);
            }
        }

        /// <summary>Refreshes rules-data improvements for Core item types whose current rules
        /// entry can be resolved. This is the safe Core counterpart to the legacy Special/Reapply
        /// Improvements command: an item missing from current rules data is deliberately left
        /// untouched so an old save never loses its retained legacy improvements. Interactive
        /// select-senseware powers are also retained until their selected child-item lifecycle is
        /// represented by Core, rather than silently dropping that choice.</summary>
        /// <returns>The number of persisted items whose known rules-data improvements were refreshed.</returns>
        private IReadOnlyList<Improvement> ReadImprovements()
        {
            var lstImprovements = new List<Improvement>();
            var objNodes = Document.SelectNodes("/character/improvements/improvement");
            if (objNodes == null) return lstImprovements;
            foreach (XmlNode objNode in objNodes)
                lstImprovements.Add(Improvement.Load(objNode));
            return lstImprovements;
        }

    }
}
