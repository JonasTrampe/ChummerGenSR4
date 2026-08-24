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
        public bool ExtendAnyDetectionSpellEnabled => GetCharacterOptions().ExtendAnyDetectionSpell;

        /// <summary>Whether the house rule permits changing a weapon accessory or modification's
        /// <c>included</c> flag after it has been added to a base weapon.</summary>
        public bool ConfirmKarmaExpenseEnabled => GetCharacterOptions().ConfirmKarmaExpense;

        /// <summary>A Magician's chosen casting Tradition (traditions.xml's &lt;name&gt;), e.g.
        /// "Hermetic" - drives <see cref="DrainResistance"/>'s formula.</summary>
        public string Tradition
        {
            get => GetValue("/character/tradition", string.Empty);
            set => SetRootValue("tradition", value);
        }

        /// <summary>A Technomancer's chosen Stream (streams.xml's &lt;name&gt;), e.g. "Default" -
        /// drives <see cref="FadingResistance"/>'s formula.</summary>
        public string Stream
        {
            get => GetValue("/character/stream", string.Empty);
            set => SetRootValue("stream", value);
        }

        /// <summary>Magician Drain resistance pool, ported from clsCharacter.cs: the chosen
        /// Tradition's two-attribute &lt;drain&gt; formula (traditions.xml) plus DrainResistance
        /// Improvements. Null if the character isn't a Magician or hasn't picked a Tradition yet.</summary>
        public CharacterDerivedValueData? DrainResistance =>
            ComputeTraditionResistancePool(Tradition, "traditions.xml", ImprovementType.DrainResistance, Magician);

        /// <summary>Technomancer Fading resistance pool - same shape as <see
        /// cref="DrainResistance"/> but keyed by <see cref="Stream"/>/streams.xml/
        /// FadingResistance.</summary>
        public CharacterDerivedValueData? FadingResistance =>
            ComputeTraditionResistancePool(Stream, "streams.xml", ImprovementType.FadingResistance, Technomancer);

        private CharacterDerivedValueData? ComputeTraditionResistancePool(string strTraditionName,
            string strDataFile, ImprovementType eType, bool blnApplicable)
        {
            if (!blnApplicable || string.IsNullOrWhiteSpace(strTraditionName))
                return null;

            XmlDocument objDoc = XmlManager.Instance.Load(strDataFile);
            XmlNode? objXmlTradition = objDoc.SelectSingleNode(
                $"/chummer/traditions/tradition[name = '{strTraditionName}']");
            string strDrain = objXmlTradition?.SelectSingleNode("drain")?.InnerText ?? string.Empty;
            string[] astrCodes = strDrain.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (astrCodes.Length == 0)
                return null;

            return SumAttributesWithImprovements(eType,
                astrCodes.Select(strCode => (strCode, GetAttributeLabel(strCode))).ToArray());
        }

        /// <summary>Ported from frmCareer.cs's cmdAddSpell_Click: after character creation,
        /// learning a Spell costs a flat KarmaSpell Karma (no formula, single option-driven
        /// constant, unlike Qualities' BP-scaled cost).</summary>
        public int GetSpellCareerKarmaCost() => GetCharacterOptions().KarmaSpell;

        public bool AddSpell(string strName, string strCategory, string strType, string strRange, string strDamage,
            string strDuration, string strDv, string strSource, string strPage, bool blnExtended = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A spell name is required.", nameof(strName));
            if (blnExtended && (!GetCharacterOptions().ExtendAnyDetectionSpell
                || !string.Equals(strCategory, "Detection", StringComparison.Ordinal)))
                return false;

            bool blnEnforceCreationBudget = !Created && StartingBuildPoints > 0;
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intCreationCost = blnKarmaBuild ? GetCharacterOptions().KarmaSpell : 3;
            int intPool = int.TryParse(blnKarmaBuild ? Karma : Bp, out int intParsedPool) ? intParsedPool : 0;
            if (blnEnforceCreationBudget && intPool < intCreationCost)
                return false;

            int intCareerKarmaCost = 0;
            if (!blnEnforceCreationBudget && Created)
            {
                intCareerKarmaCost = GetSpellCareerKarmaCost();
                if (intCareerKarmaCost > ParseInteger(Karma))
                    return false;
            }

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSpells = objRoot.SelectSingleNode("spells");
            if (objSpells == null)
            {
                objSpells = Document.CreateElement("spells");
                objRoot.AppendChild(objSpells);
            }

            var objSpell = Document.CreateElement("spell");
            AppendElement(objSpell, "name", strName.Trim());
            AppendElement(objSpell, "category", strCategory);
            AppendElement(objSpell, "type", strType);
            AppendElement(objSpell, "range", strRange);
            AppendElement(objSpell, "damage", strDamage);
            AppendElement(objSpell, "duration", strDuration);
            AppendElement(objSpell, "dv", strDv);
            AppendElement(objSpell, "extended", blnExtended.ToString());
            AppendElement(objSpell, "source", strSource);
            AppendElement(objSpell, "page", strPage);
            if (blnEnforceCreationBudget)
                AppendElement(objSpell, "creationcost", intCreationCost.ToString(CultureInfo.InvariantCulture));
            objSpells.AppendChild(objSpell);
            if (blnEnforceCreationBudget)
            {
                if (blnKarmaBuild)
                    Karma = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
                else
                    Bp = (intPool - intCreationCost).ToString(CultureInfo.InvariantCulture);
            }
            else if (intCareerKarmaCost > 0)
            {
                Karma = (ParseInteger(Karma) - intCareerKarmaCost).ToString(CultureInfo.InvariantCulture);
                var objUndo = new ExpenseUndo();
                objUndo.CreateKarma(KarmaExpenseType.AddSpell, strName.Trim());
                AddExpense("Karma", -intCareerKarmaCost, "Zauber gelernt: " + strName.Trim(), null, objUndo);
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Returns the displayed Drain Value for an Extended Detection spell. This is
        /// the legacy Spell.DisplayDV rule: keep the stored formula intact and add two to its
        /// trailing numeric modifier.</summary>
        public static string GetSpellDrainValue(string strDv, bool blnExtended)
        {
            if (!blnExtended)
                return strDv;

            int intFormulaEnd = strDv.LastIndexOf(')');
            if (intFormulaEnd < 0)
                return strDv + "+2";

            string strFormula = strDv.Substring(0, intFormulaEnd + 1);
            string strSuffix = strDv.Substring(intFormulaEnd + 1);
            if (!int.TryParse(strSuffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intModifier))
                return strFormula + "+2";

            intModifier += 2;
            return intModifier == 0 ? strFormula : strFormula + (intModifier > 0 ? "+" : string.Empty)
                + intModifier.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>One selectable Drain-Value modifier for a homebrew Spell's category, ported
        /// from frmCreateSpell.cs's per-category ChangeModifiers() checkbox catalog (Text+Tag).
        /// <see cref="Key"/> is a stable identifier this port invented (legacy had no such key,
        /// just positional checkbox controls) for <see cref="ComputeCustomSpellDv"/>/
        /// <see cref="AddCustomSpell"/> to reference.</summary>
        public sealed class SpellModifierOption
        {
            internal SpellModifierOption(string strKey, string strLabel, int intDv)
            {
                Key = strKey;
                Label = strLabel;
                Dv = intDv;
            }

            public string Key { get; }
            public string Label { get; }
            public int Dv { get; }
        }

        /// <summary>Ported from frmCreateSpell.cs's ChangeModifiers() switch. The mutual-exclusion
        /// enable/disable wiring between checkboxes (e.g. Combat's Direct/Indirect) is UI-only
        /// guidance in legacy - not ported here, since it doesn't change what DV a given
        /// combination computes to; the caller is free to let a player check an unusual
        /// combination, same as if they'd hand-edited a save file.</summary>
        public static IReadOnlyList<SpellModifierOption> GetSpellModifierOptions(string strCategory) => strCategory switch
        {
            "Detection" => new[]
            {
                new SpellModifierOption("directional", "Directional", 0),
                new SpellModifierOption("area", "Area", 0),
                new SpellModifierOption("psychic", "Psychic", 0),
                new SpellModifierOption("active", "Active", 0),
                new SpellModifierOption("passive", "Passive", 0),
                new SpellModifierOption("basicdetection", "Basic Detection (ex: Detect Life)", 0),
                new SpellModifierOption("complexdetection", "Complex Detection (ex: Detect Enemies)", 1),
                new SpellModifierOption("basicanalyze", "Basic Analyze (ex: Analyze Device)", 1),
                new SpellModifierOption("complexanalyze", "Complex Analyze (ex: Analyze Truth)", 2),
                new SpellModifierOption("invasiveanalyze", "Invasive Analyze (ex: Mind Probe)", 4),
                new SpellModifierOption("improvedsense", "Improved Sense", 1),
                new SpellModifierOption("newsense", "New Sense", 2),
                new SpellModifierOption("psychicsense", "Psychic Sense (ie, telepathy, precognition)", 4),
                new SpellModifierOption("extendedarea", "Extended Area", 2)
            },
            "Health" => new[]
            {
                new SpellModifierOption("curative", "Curative", 0),
                new SpellModifierOption("increasesinitiativepasses", "Increases Initiative Passes", 4),
                new SpellModifierOption("cosmeticeffect", "Cosmetic Effect", -2),
                new SpellModifierOption("negative", "Negative Health Spell", 2),
                new SpellModifierOption("restrictedeffect", "Restricted Effect (ie, Symptoms Only)", -2)
            },
            "Illusion" => new[]
            {
                new SpellModifierOption("obvious", "Obvious", -1),
                new SpellModifierOption("realistic", "Realistic", 0),
                new SpellModifierOption("singlesense", "Single-Sense", -2),
                new SpellModifierOption("multisense", "Multi-Sense", 0),
                new SpellModifierOption("hidesconceals", "Illusion Hides or Conceals", 2)
            },
            "Manipulation" => new[]
            {
                new SpellModifierOption("environmental", "Environmental", -2),
                new SpellModifierOption("mental", "Mental", 0),
                new SpellModifierOption("physical", "Physical", 0),
                new SpellModifierOption("minorchange", "Minor Change", 0),
                new SpellModifierOption("majorchange", "Major Change", 2),
                new SpellModifierOption("elementaleffect", "Elemental effect", 2)
            },
            _ => new[] // Combat.
            {
                new SpellModifierOption("direct", "Direct", 0),
                new SpellModifierOption("indirect", "Indirect", 0),
                new SpellModifierOption("elemental", "Element effects", 2),
                new SpellModifierOption("physicaldamage", "Physical damage", 0),
                new SpellModifierOption("stundamage", "Stun damage", -1)
            }
        };

        /// <summary>Ported from frmCreateSpell.cs's CalculateDrain(). <paramref
        /// name="setCheckedKeys"/> are <see cref="SpellModifierOption.Key"/>s from
        /// <see cref="GetSpellModifierOptions"/> for <paramref name="strCategory"/>;
        /// <paramref name="intNumberOfEffects"/> only matters for Combat's "elemental" and
        /// Manipulation's "elementaleffect" keys, whose DV multiplies by it (nudNumberOfEffects).</summary>
        public static string ComputeCustomSpellDv(string strCategory, string strType, string strRange, bool blnArea,
            bool blnRestricted, bool blnVeryRestricted, string strDuration, IReadOnlySet<string> setCheckedKeys,
            int intNumberOfEffects)
        {
            int intDv = strType == "M" ? 0 : 1;
            intDv += strRange == "T" ? -2 : 0;
            if (blnArea)
                intDv += 2;
            if (blnRestricted)
                intDv -= 1;
            if (blnVeryRestricted)
                intDv -= 2;

            bool blnCurative = strCategory == "Health" && setCheckedKeys.Contains("curative");
            if (strDuration == "P" && !blnCurative)
                intDv += 2;

            foreach (SpellModifierOption objOption in GetSpellModifierOptions(strCategory))
            {
                if (!setCheckedKeys.Contains(objOption.Key))
                    continue;
                bool blnMultiplied = (strCategory == "Combat" && objOption.Key == "elemental")
                    || (strCategory == "Manipulation" && objOption.Key == "elementaleffect");
                intDv += blnMultiplied ? objOption.Dv * intNumberOfEffects : objOption.Dv;
            }

            string strBase = blnCurative ? "(Damage Value)" : "(F/2)";
            string strDvSuffix = intDv == 0 ? "" : intDv > 0 ? "+" + intDv : intDv.ToString(CultureInfo.InvariantCulture);
            return strBase + strDvSuffix;
        }

        /// <summary>Ported from frmCreateSpell.cs's AcceptForm: builds a homebrew Spell from the
        /// same category/type/range/duration/modifier choices <see cref="ComputeCustomSpellDv"/>
        /// uses, then adds it exactly like a rules-data spells.xml pick (<see cref="AddSpell"/>) -
        /// legacy's own AcceptForm does the same thing (assembles a Spell record locally, no
        /// separate write path). Source/page are hardcoded to "SM"/159 (Street Magic's homebrew-
        /// spell-creation rules), matching legacy. Not ported: the Descriptors/Limited/Restriction-
        /// text fields, which this port's Spell model doesn't carry for any spell (rules-data ones
        /// don't either) - purely descriptive, not consumed by any calculation.</summary>
        public bool AddCustomSpell(string strName, string strCategory, string strType, string strRange, bool blnArea,
            bool blnRestricted, bool blnVeryRestricted, string strDuration, IReadOnlySet<string> setCheckedKeys,
            int intNumberOfEffects)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            string strDv = ComputeCustomSpellDv(strCategory, strType, strRange, blnArea, blnRestricted,
                blnVeryRestricted, strDuration, setCheckedKeys, intNumberOfEffects);
            string strDamage = strCategory == "Combat"
                ? (setCheckedKeys.Contains("physicaldamage") ? "P" : "S")
                : string.Empty;
            string strFullRange = blnArea ? strRange + " (A)" : strRange;

            AddSpell(strName, strCategory, strType, strFullRange, strDamage, strDuration, strDv, "SM", "159");
            return true;
        }

        /// <summary>Adds a root-level gear item in the minimal saved-character tree shape. Deducts its
        /// cost (evaluated at the given rating, times quantity) from Nuyen, matching the legacy
        /// "buying gear costs money" rule. Returns false (adding nothing) if this is "Ammo:
        /// Stick-n-Shock" and <see cref="StickNShockAllowed"/> rejects it.</summary>
        public bool RemoveSpell(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/spells/spell");
            if (objNodes == null)
                return false;

            foreach (XmlNode objSpell in objNodes)
            {
                string strSavedName = GetValue(objSpell, "name", string.Empty);
                bool blnExtended = string.Equals(GetValue(objSpell, "extended", "False"), "True",
                    StringComparison.OrdinalIgnoreCase);
                string strDisplayName = blnExtended ? strSavedName + ", Extended" : strSavedName;
                if (!string.Equals(strSavedName, strName.Trim(), StringComparison.Ordinal)
                    && !string.Equals(strDisplayName, strName.Trim(), StringComparison.Ordinal))
                    continue;

                int intCreationRefund = ParseInteger(GetValue(objSpell, "creationcost", "0"));
                objSpell.ParentNode?.RemoveChild(objSpell);
                if (!Created && StartingBuildPoints > 0 && intCreationRefund > 0)
                {
                    bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                    int intPool = ParseInteger(blnKarmaBuild ? Karma : Bp);
                    if (blnKarmaBuild)
                        Karma = (intPool + intCreationRefund).ToString(CultureInfo.InvariantCulture);
                    else
                        Bp = (intPool + intCreationRefund).ToString(CultureInfo.InvariantCulture);
                }
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Updates a spell's free-form notes using its root-list position. This avoids
        /// relying on spell names, which are not unique in hand-edited older character files.</summary>
        public bool SetSpellNotes(int intSpellId, string strNotes)
        {
            XmlNode? objSpell = GetSpellNodeById(intSpellId);
            if (objSpell == null)
                return false;

            SetChildValue(objSpell, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetSpellNodeById(int intSpellId)
        {
            if (intSpellId < 0)
                return null;
            XmlNodeList? objNodes = Document.SelectNodes("/character/spells/spell");
            return objNodes != null && intSpellId < objNodes.Count ? objNodes[intSpellId] : null;
        }

        private IReadOnlyList<CharacterSpellData>? _cachedSpells;
        public IReadOnlyList<CharacterSpellData> Spells
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedSpells short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedSpells ??= ReadSpells();
            }
        }

        /// <summary>Maximum Force a summoned Spirit/Sprite may have - ported from frmCareer.cs's/
        /// frmCreate.cs's cmdAddSpirit_Click and DVTooltip's shared branch: a Mystic Adept normally
        /// caps this at <see cref="MysticAdeptMagicianMagSplit"/> (the Magician-side share of MAG),
        /// not the character's full MAG, unless the "Spirit Force based on total MAG" house rule
        /// (<see cref="CharacterOptions.SpiritForceBasedOnTotalMag"/>) is enabled, in which case
        /// full MAG applies regardless of the split - matching legacy's own escape hatch for tables
        /// that don't want the RAW Mystic Adept split enforced here. Non-Mystic-Adept Magicians
        /// (and Technomancers summoning Sprites via RES) always use their full linked attribute.
        /// 0 if the character can't summon at all.</summary>
        private IReadOnlyList<CharacterSpellData> ReadSpells()
        {
            var lstSpells = new List<CharacterSpellData>();
            var objNodes = Document.SelectNodes("/character/spells/spell");
            if (objNodes == null) return lstSpells;
            for (int intSpellId = 0; intSpellId < objNodes.Count; intSpellId++)
            {
                XmlNode objNode = objNodes[intSpellId]!;
                string strCategory = GetValue(objNode, "category", string.Empty);
                (int intPool, string strTooltip) = ComputeSpellDicePool(strCategory);
                bool blnExtended = string.Equals(GetValue(objNode, "extended", "False"), "True",
                    StringComparison.OrdinalIgnoreCase);
                lstSpells.Add(new CharacterSpellData(GetValue(objNode, "name", string.Empty),
                    strCategory, GetValue(objNode, "type", string.Empty),
                    GetValue(objNode, "range", string.Empty), GetValue(objNode, "damage", string.Empty),
                    GetValue(objNode, "duration", string.Empty), GetValue(objNode, "dv", string.Empty),
                    GetValue(objNode, "source", string.Empty), GetValue(objNode, "page", string.Empty),
                    intPool.ToString(), strTooltip, blnExtended, intSpellId,
                    GetValue(objNode, "notes", string.Empty)));
            }
            return lstSpells;
        }

        /// <summary>Ported from clsUnique.cs's Spell.DicePool/DicePoolTooltip: the Spellcasting
        /// skill's TotalRating, +2 if the skill's own Specialization matches the spell's Category,
        /// plus any SpellCategory Improvements targeting that Category.</summary>
    }
}
