using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     Platform-neutral reader and writer for the existing Chummer character-file format.
    ///     This is deliberately a small extraction from <c>Character</c>: callers can display and
    ///     round-trip a save before the complete legacy domain object has been moved into Core.
    /// </summary>
    public sealed class CharacterFileService
    {
        public CharacterDocument Load(Stream objStream, string strSourceName)
        {
            if (objStream == null) throw new ArgumentNullException(nameof(objStream));
            Trace.TraceInformation("Loading Chummer character from {0}", strSourceName);
            try
            {
                var objDocument = new XmlDocument();
                objDocument.Load(objStream);
                if (objDocument.DocumentElement == null || objDocument.DocumentElement.Name != "character")
                    throw new InvalidDataException("The selected file is not a Chummer character document.");

                var objCharacter = new CharacterDocument(objDocument, strSourceName);
                Trace.TraceInformation("Loaded Chummer character {0} from {1}", objCharacter.DisplayName,
                    strSourceName);
                return objCharacter;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to load Chummer character from {0}: {1}", strSourceName, ex);
                throw;
            }
        }

        public void Save(CharacterDocument objCharacter, Stream objStream, string strTargetName)
        {
            if (objCharacter == null) throw new ArgumentNullException(nameof(objCharacter));
            if (objStream == null) throw new ArgumentNullException(nameof(objStream));
            Trace.TraceInformation("Saving Chummer character {0} to {1}", objCharacter.DisplayName, strTargetName);
            try
            {
                objCharacter.Document.Save(objStream);
                Trace.TraceInformation("Saved Chummer character {0} to {1}", objCharacter.DisplayName, strTargetName);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to save Chummer character {0} to {1}: {2}", objCharacter.DisplayName,
                    strTargetName, ex);
                throw;
            }
        }
    }

    public sealed class CharacterDocument
    {
        internal CharacterDocument(XmlDocument objDocument, string strDisplayName)
        {
            Document = objDocument;
            DisplayName = strDisplayName;
        }

        internal XmlDocument Document { get; }
        public string DisplayName { get; }

        public string Name => GetValue("/character/name", DisplayName);

        public string Alias => GetValue("/character/alias", string.Empty);

        public string Metatype => GetValue("/character/metatype", string.Empty);

        public string Karma => GetValue("/character/karma", "0");

        public string Nuyen => GetValue("/character/nuyen", "0");

        public CharacterConditionData Condition =>
            new(GetAttributeValue("ESS"),
                GetValue("/character/physicalcmfilled", "0"), GetValue("/character/stuncmfilled", "0"),
                ComputePhysicalCm(), ComputeStunCm());

        /// <summary>Sums two attributes plus any Improvements of the given type against no specific
        /// ImprovedName (used by the "Special Attribute Tests": Composure, Judge Intentions, Lift and
        /// Carry, Memory). Each attribute is listed on its own tooltip line by its German name, then
        /// one line per contributing Improvement (source name + signed value) when several stack.</summary>
        private CharacterDerivedValueData SumAttributesWithImprovements(ImprovementType eType,
            params (string Code, string Label)[] attributes)
        {
            var sb = new StringBuilder();
            int intTotal = 0;
            for (int i = 0; i < attributes.Length; i++)
            {
                int intValue = GetAttributeInt(attributes[i].Code);
                intTotal += intValue;
                if (i > 0) sb.Append('\n');
                sb.Append(attributes[i].Label).Append(": ").Append(intValue);
            }

            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, eType);
            intTotal += lstContributions.Sum(c => c.Value);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);

            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        /// <summary>Appends one tooltip line per contribution as "SourceName: +N" (or "-N").</summary>
        private static void AppendContributions(StringBuilder sb, IReadOnlyList<(string SourceName, int Value)> lstContributions)
        {
            foreach (var (strSource, intValue) in lstContributions)
                sb.Append('\n').Append(DescribeSource(strSource)).Append(": ").Append(FormatSigned(intValue));
        }

        // Some legacy Improvements store the source item's GUID in SourceName instead of a
        // readable name (Core doesn't yet cross-reference that back to the actual gear/power/
        // quality that granted it - see PORTING_PLAN.md) - fall back to a generic label rather
        // than showing a raw GUID in a tooltip.
        private static string DescribeSource(string strSource)
        {
            if (string.IsNullOrEmpty(strSource))
                return "Sonstiges";
            return Guid.TryParse(strSource, out _) ? "Sonstige Ausrüstung/Fähigkeit" : strSource;
        }

        private static string FormatSigned(int intValue) => intValue >= 0 ? "+" + intValue : intValue.ToString();

        public IReadOnlyList<CharacterAttributeData> Attributes => ReadAttributes();

        /// <summary>Raw bonus/modifier records - see Improvement.cs and ImprovementManager.cs for
        /// what these actually drive. Most callers want a derived value (like Condition above)
        /// rather than this list directly.</summary>
        public IReadOnlyList<Improvement> Improvements => ReadImprovements();

        public IReadOnlyList<CharacterQualityData> Qualities => ReadQualities();

        public IReadOnlyList<CharacterTreeItemData> Gear => ReadTreeItems("/character/gears/gear", "children/gear");

        // Cyberware and bioware are saved to the same <cyberwares> list and only distinguished by
        // <improvementsource> ("Cyberware" vs "Bioware") - split here so each gets its own tree.
        public IReadOnlyList<CharacterTreeItemData> Cyberware =>
            ReadTreeItems("/character/cyberwares/cyberware[improvementsource != 'Bioware']", "children/cyberware");

        public IReadOnlyList<CharacterTreeItemData> Bioware =>
            ReadTreeItems("/character/cyberwares/cyberware[improvementsource = 'Bioware']", "children/cyberware");

        public IReadOnlyList<CharacterTreeItemData> Armor => ReadTreeItems("/character/armors/armor", "");

        /// <summary>Armor encumbrance penalty (a negative dice-pool modifier, 0 if under threshold),
        /// ported from clsCharacter.cs's BallisticArmorEncumbrance/ImpactArmorEncumbrance. This is a
        /// deliberately scoped-down v1: it covers the vanilla rule (BOD*2, or *3 if any worn armor is
        /// Military Grade, Form-Fitting armor counted at half rating) but not yet:
        ///  - ArmorMod bonuses to ballistic/impact rating (base &lt;b&gt;/&lt;i&gt; values only)
        ///  - the SoftWeave Improvement's STR-based reduction
        ///  - the IgnoreArmorEncumbrance / AlternateArmorEncumbrance / NoSingleArmorEncumbrance house
        ///    rules (CharacterOptions isn't loaded per-character yet - see PORTING_PLAN.md Phase 3)
        /// </summary>
        public CharacterEncumbranceData ArmorEncumbrance => ComputeArmorEncumbrance();

        /// <summary>Composure (WIL + CHA + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData Composure =>
            SumAttributesWithImprovements(ImprovementType.Composure, ("WIL", "Willenskraft"), ("CHA", "Charisma"));

        /// <summary>Judge Intentions (INT + CHA + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData JudgeIntentions =>
            SumAttributesWithImprovements(ImprovementType.JudgeIntentions, ("INT", "Intuition"), ("CHA", "Charisma"));

        /// <summary>Lifting and Carrying (STR + BOD + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData LiftAndCarry =>
            SumAttributesWithImprovements(ImprovementType.LiftAndCarry, ("STR", "Stärke"), ("BOD", "Konstitution"));

        /// <summary>Memory (LOG + WIL + Improvements), ported from clsCharacter.cs.</summary>
        public CharacterDerivedValueData Memory =>
            SumAttributesWithImprovements(ImprovementType.Memory, ("LOG", "Logik"), ("WIL", "Willenskraft"));

        /// <summary>Dice-pool penalty from current Physical/Stun damage, ported from clsCharacter.cs's
        /// WoundModifiers. Despite the name this doesn't actually look at how many condition-monitor
        /// boxes are filled in - the legacy implementation only sums Improvements sourced from
        /// ConditionMonitor (i.e. this is 0 unless something is granting/removing wound-penalty
        /// immunity, not a live "you're hurt" calculation); kept as its own property so Initiative
        /// below reads the same way the original does.</summary>
        public int WoundModifiers => Improvements
            .Where(i => i.Enabled && i.Source == ImprovementSource.ConditionMonitor)
            .Sum(i => i.Value);

        /// <summary>Initiative (INT + REA, base/augmented shown as "base (augmented)" when they
        /// differ), ported from clsCharacter.cs. Simplified: the legacy version also clamps to a
        /// per-metatype maximum via a "special INI attribute" that's never actually populated from
        /// the save file in normal play (always defaults to unconstrained) - not ported since Core
        /// doesn't load metatype data. Never goes below 0.</summary>
        public CharacterInitiativeData Initiative
        {
            get
            {
                int intInt = GetAttributeInt("INT");
                int intRea = GetAttributeInt("REA");
                int intBase = intInt + intRea;
                var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Initiative);
                int intWound = WoundModifiers;
                int intAugmented = intBase + lstContributions.Sum(c => c.Value) + intWound;

                var sb = new StringBuilder();
                sb.Append("Intuition: ").Append(intInt).Append('\n');
                sb.Append("Reaktion: ").Append(intRea);
                AppendContributions(sb, lstContributions);
                if (intWound != 0)
                    sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
                sb.Append('\n').Append("Gesamt: ").Append(Math.Max(intAugmented, 0));

                return new CharacterInitiativeData(intBase, Math.Max(intAugmented, 0), sb.ToString());
            }
        }

        /// <summary>Initiative Passes (1 base, plus Improvements), ported from clsCharacter.cs.</summary>
        public CharacterInitiativeData InitiativePasses
        {
            get
            {
                var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.InitiativePass)
                    .Concat(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.InitiativePassAdd))
                    .ToList();
                int intPasses = 1 + lstContributions.Sum(c => c.Value);

                var sb = new StringBuilder();
                sb.Append("Basis: 1");
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Gesamt: ").Append(intPasses);

                return new CharacterInitiativeData(1, intPasses, sb.ToString());
            }
        }

        /// <summary>Astral Initiative (INT x 2, plus wound modifiers), ported from clsCharacter.cs.
        /// Always 3 Passes for every character in the legacy version too (AstralInitiativePasses
        /// is a hardcoded "3", not computed), so that side isn't exposed as its own property.</summary>
        public CharacterInitiativeData AstralInitiative
        {
            get
            {
                int intInt = GetAttributeInt("INT");
                int intBase = intInt * 2;
                int intWound = WoundModifiers;
                int intAugmented = intBase + intWound;

                var sb = new StringBuilder();
                sb.Append("Intuition x 2: ").Append(intBase);
                if (intWound != 0)
                    sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
                sb.Append('\n').Append("Gesamt: ").Append(Math.Max(intAugmented, 0));

                return new CharacterInitiativeData(intBase, Math.Max(intAugmented, 0), sb.ToString());
            }
        }

        /// <summary>Matrix Initiative (INT, plus Improvements), ported from clsCharacter.cs. This
        /// is deliberately just the default non-awakened-non-technomancer human path - the
        /// legacy version also branches on: an active Commlink's Response bonus, Technomancers
        /// using (INT x 2) + 1 plus Living Persona bonuses instead, Sprites using a fixed
        /// metatype-minimum value, and A.I./technocritter/protosapient characters using
        /// INT + System instead. None of that branching data (RES-enabled state, equipped gear's
        /// "active commlink" flag, metatype category) is modeled in Core yet.</summary>
        public CharacterInitiativeData MatrixInitiative
        {
            get
            {
                int intInt = GetAttributeInt("INT");
                var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.MatrixInitiative);
                int intWound = WoundModifiers;
                int intBase = intInt + lstContributions.Sum(c => c.Value);
                int intAugmented = intBase + intWound;

                var sb = new StringBuilder();
                sb.Append("Intuition: ").Append(intInt);
                AppendContributions(sb, lstContributions);
                if (intWound != 0)
                    sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
                sb.Append('\n').Append("Gesamt: ").Append(Math.Max(intAugmented, 0));

                return new CharacterInitiativeData(intBase, Math.Max(intAugmented, 0), sb.ToString());
            }
        }

        /// <summary>Matrix Initiative Passes (1 base for non-Technomancers, plus Improvements),
        /// ported from clsCharacter.cs. Doesn't cover the Technomancer (3 base) or A.I./
        /// technocritter/protosapient (always 3) special cases - see MatrixInitiative's doc
        /// comment for why.</summary>
        public CharacterInitiativeData MatrixInitiativePasses
        {
            get
            {
                var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.MatrixInitiativePass)
                    .Concat(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.MatrixInitiativePassAdd))
                    .ToList();
                int intPasses = 1 + lstContributions.Sum(c => c.Value);

                var sb = new StringBuilder();
                sb.Append("Basis: 1");
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Gesamt: ").Append(intPasses);

                return new CharacterInitiativeData(1, intPasses, sb.ToString());
            }
        }

        public IReadOnlyList<CharacterWeaponData> Weapons => ReadWeapons();

        public IReadOnlyList<CharacterSkillGroupData> SkillGroups => ReadSkillGroups();

        public IReadOnlyList<CharacterSkillData> Skills => ReadSkills();

        public IReadOnlyList<CharacterSkillData> KnowledgeSkills => ReadKnowledgeSkills();

        // Enemies are saved into the same <contacts> list as regular contacts and are only
        // distinguished by <type>Enemy</type> - split here so each gets its own display list.
        public IReadOnlyList<CharacterContactData> Contacts => ReadContacts(blnEnemies: false);

        public IReadOnlyList<CharacterContactData> Enemies => ReadContacts(blnEnemies: true);

        public IReadOnlyList<CharacterMartialArtData> MartialArts => ReadMartialArts();

        public IReadOnlyList<CharacterMartialArtManeuverData> MartialArtManeuvers => ReadMartialArtManeuvers();

        public IReadOnlyList<CharacterPowerData> AdeptPowers => ReadAdeptPowers();

        public IReadOnlyList<CharacterSpellData> Spells => ReadSpells();

        public IReadOnlyList<CharacterSpiritData> Spirits => ReadSpirits();

        public IReadOnlyList<CharacterInitiationGradeData> InitiationGrades => ReadInitiationGrades();

        public IReadOnlyList<CharacterLifestyleData> Lifestyles => ReadLifestyles();

        public IReadOnlyList<CharacterWeaponData> Vehicles => ReadVehicles();

        // Karma and Nuyen history entries are saved into the same <expenses> list and only
        // distinguished by <type> - split here to feed the two separate history lists/charts.
        public IReadOnlyList<CharacterExpenseData> KarmaExpenses => ReadExpenses(strType: "Karma");

        public IReadOnlyList<CharacterExpenseData> NuyenExpenses => ReadExpenses(strType: "Nuyen");

        public string Gender => GetValue("/character/sex", string.Empty);

        public string EyeColor => GetValue("/character/eyes", string.Empty);

        public string HairColor => GetValue("/character/hair", string.Empty);

        public string Height => GetValue("/character/height", string.Empty);

        public string Weight => GetValue("/character/weight", string.Empty);

        public string SkinColor => GetValue("/character/skin", string.Empty);

        public string PlayerName => GetValue("/character/playername", string.Empty);

        public string StreetCred => GetValue("/character/streetcred", "0");

        public string Notoriety => GetValue("/character/notoriety", "0");

        public string PublicAwareness => GetValue("/character/publicawareness", "0");

        public string Description => GetValue("/character/description", string.Empty);

        public string Background => GetValue("/character/background", string.Empty);

        public string Concept => GetValue("/character/concept", string.Empty);

        public string Notes => GetValue("/character/notes", string.Empty);

        private string GetValue(string strXPath, string strFallback)
        {
            var objNode = Document.SelectSingleNode(strXPath);
            return string.IsNullOrEmpty(objNode == null ? null : objNode.InnerText) ? strFallback : objNode.InnerText;
        }

        private string GetAttributeValue(string strCode)
        {
            var objNode =
                Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']/totalvalue");
            return string.IsNullOrEmpty(objNode == null ? null : objNode.InnerText) ? "0" : objNode.InnerText;
        }

        private int GetAttributeInt(string strCode)
            => int.TryParse(GetAttributeValue(strCode), out var intValue) ? intValue : 0;

        // Ported from clsCharacter.cs's PhysicalCM/StunCM properties. The A.I./technocritter/
        // protosapient special cases (no BOD -> half System instead, no Stun track at all)
        // aren't ported since Core doesn't read metatype category yet - flag if a save file
        // needs it.
        private CharacterDerivedValueData ComputePhysicalCm()
        {
            var dblBod = double.TryParse(GetAttributeValue("BOD"), out var d) ? d : 0;
            var intBase = (int)Math.Ceiling(dblBod / 2) + 8;
            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.PhysicalCm);
            var intTotal = intBase + lstContributions.Sum(c => c.Value);

            var sb = new StringBuilder();
            sb.Append("Basis (8 + Konstitution/2 aufgerundet): ").Append(intBase);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        private CharacterDerivedValueData ComputeStunCm()
        {
            var dblWil = double.TryParse(GetAttributeValue("WIL"), out var d) ? d : 0;
            var intBase = (int)Math.Ceiling(dblWil / 2) + 8;
            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.StunCm);
            var intTotal = intBase + lstContributions.Sum(c => c.Value);

            var sb = new StringBuilder();
            sb.Append("Basis (8 + Willenskraft/2 aufgerundet): ").Append(intBase);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        private CharacterEncumbranceData ComputeArmorEncumbrance()
        {
            var objNodes = Document.SelectNodes("/character/armors/armor[equipped = 'True']");
            var dblBod = double.TryParse(GetAttributeValue("BOD"), out var d) ? d : 0;

            var intMultiplier = 2;
            var intTotalBallistic = 0;
            var intTotalImpact = 0;
            var lstWorn = new List<string>();
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    if (GetValue(objNode, "category", string.Empty) == "Military Grade Armor")
                        intMultiplier = 3;

                    var strName = GetValue(objNode, "name", string.Empty);
                    var blnFormFitting = strName.StartsWith("Form-Fitting");
                    var intBallistic = ParseArmorRating(GetValue(objNode, "b", "0"));
                    var intImpact = ParseArmorRating(GetValue(objNode, "i", "0"));
                    var intCountedBallistic = blnFormFitting ? intBallistic / 2 : intBallistic;
                    var intCountedImpact = blnFormFitting ? intImpact / 2 : intImpact;
                    intTotalBallistic += intCountedBallistic;
                    intTotalImpact += intCountedImpact;
                    lstWorn.Add(strName + " (ballistisch " + FormatSigned(intCountedBallistic)
                        + ", Stoß " + FormatSigned(intCountedImpact) + (blnFormFitting ? ", Anschmiegsam: halbiert" : "") + ")");
                }
            }

            var intThreshold = (int)(dblBod * intMultiplier);
            var strThresholdNote = "Schwelle: Konstitution " + dblBod + " x " + intMultiplier + " = " + intThreshold
                + (intMultiplier == 3 ? " (Militärgraderüstung getragen)" : "");

            return new CharacterEncumbranceData(
                BuildEncumbranceValue(intTotalBallistic, intThreshold, "ballistisch", strThresholdNote, lstWorn),
                BuildEncumbranceValue(intTotalImpact, intThreshold, "Stoß", strThresholdNote, lstWorn));
        }

        private static CharacterDerivedValueData BuildEncumbranceValue(int intTotal, int intThreshold,
            string strKind, string strThresholdNote, IReadOnlyList<string> lstWorn)
        {
            var intPenalty = ComputeEncumbrancePenalty(intTotal, intThreshold);
            var sb = new StringBuilder();
            sb.Append("Getragene Panzerung (").Append(strKind).Append("): ").Append(intTotal);
            foreach (var strItem in lstWorn)
                sb.Append('\n').Append("  ").Append(strItem);
            sb.Append('\n').Append(strThresholdNote);
            sb.Append('\n').Append("Behinderung: ").Append(intPenalty);
            return new CharacterDerivedValueData(intPenalty, sb.ToString());
        }

        // Armor ratings in the save file can carry a "+2" style mod suffix on top of the base
        // number (matching the legacy TotalBallistic/TotalImpact display format) - only the
        // leading integer is used here since per-mod bonuses aren't modeled yet (see
        // ArmorEncumbrance's doc comment).
        private static int ParseArmorRating(string strRating)
        {
            var strLeading = new string(strRating.TakeWhile(c => char.IsDigit(c) || c == '-').ToArray());
            return int.TryParse(strLeading, out var intValue) ? intValue : 0;
        }

        private static int ComputeEncumbrancePenalty(int intTotal, int intThreshold)
        {
            if (intTotal <= intThreshold) return 0;
            return -(int)Math.Ceiling((intTotal - intThreshold) / 2.0);
        }

        private IReadOnlyList<Improvement> ReadImprovements()
        {
            var lstImprovements = new List<Improvement>();
            var objNodes = Document.SelectNodes("/character/improvements/improvement");
            if (objNodes == null) return lstImprovements;
            foreach (XmlNode objNode in objNodes)
                lstImprovements.Add(Improvement.Load(objNode));
            return lstImprovements;
        }

        private IReadOnlyList<CharacterAttributeData> ReadAttributes()
        {
            var lstAttributes = new List<CharacterAttributeData>();
            var objNodes = Document.SelectNodes("/character/attributes/attribute");
            if (objNodes == null) return lstAttributes;
            foreach (XmlNode objNode in objNodes)
                lstAttributes.Add(new CharacterAttributeData(
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "value", "0"),
                    GetValue(objNode, "totalvalue", GetValue(objNode, "value", "0")),
                    GetValue(objNode, "metatypemin", "0"), GetValue(objNode, "metatypemax", "0"),
                    GetValue(objNode, "metatypeaugmax", GetValue(objNode, "metatypemax", "0"))));

            return lstAttributes;
        }

        private IReadOnlyList<CharacterQualityData> ReadQualities()
        {
            var lstQualities = new List<CharacterQualityData>();
            var objNodes = Document.SelectNodes("/character/qualities/quality");
            if (objNodes == null) return lstQualities;
            foreach (XmlNode objNode in objNodes)
                lstQualities.Add(new CharacterQualityData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "qualitytype", string.Empty)));
            return lstQualities;
        }

        private IReadOnlyList<CharacterTreeItemData> ReadTreeItems(string strXPath, string strChildXPath)
        {
            var lstItems = new List<CharacterTreeItemData>();
            var objNodes = Document.SelectNodes(strXPath);
            if (objNodes == null) return lstItems;
            foreach (XmlNode objNode in objNodes)
                lstItems.Add(ReadTreeItem(objNode, strChildXPath));
            return lstItems;
        }

        private IReadOnlyList<CharacterWeaponData> ReadWeapons()
        {
            var lstWeapons = new List<CharacterWeaponData>();
            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return lstWeapons;
            foreach (XmlNode objNode in objNodes)
                lstWeapons.Add(new CharacterWeaponData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "category", string.Empty), GetValue(objNode, "damage", string.Empty),
                    GetValue(objNode, "ammo", string.Empty)));
            return lstWeapons;
        }

        private IReadOnlyList<CharacterSkillGroupData> ReadSkillGroups()
        {
            var lstGroups = new List<CharacterSkillGroupData>();
            var objNodes = Document.SelectNodes("/character/skillgroups/skillgroup");
            if (objNodes == null) return lstGroups;
            foreach (XmlNode objNode in objNodes)
                lstGroups.Add(new CharacterSkillGroupData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "rating", "0")));
            return lstGroups;
        }

        private IReadOnlyList<CharacterSkillData> ReadSkills()
        {
            var lstSkills = new List<CharacterSkillData>();
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return lstSkills;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") == "True") continue;
                lstSkills.Add(BuildSkillData(objNode, GetValue(objNode, "skillgroup", string.Empty),
                    GetValue(objNode, "grouped", "False") == "True"));
            }

            return lstSkills;
        }

        private IReadOnlyList<CharacterSkillData> ReadKnowledgeSkills()
        {
            var lstSkills = new List<CharacterSkillData>();
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return lstSkills;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") != "True") continue;
                // Knowledge skills aren't organized into skill groups.
                lstSkills.Add(BuildSkillData(objNode, string.Empty, blnIsGroupLocked: false));
            }

            return lstSkills;
        }

        private CharacterSkillData BuildSkillData(XmlNode objNode, string strSkillGroup, bool blnIsGroupLocked)
        {
            string strName = GetValue(objNode, "name", string.Empty);
            string strAttribute = GetValue(objNode, "attribute", string.Empty);
            string strCategory = GetValue(objNode, "skillcategory", string.Empty);
            string strSpecialization = GetValue(objNode, "spec", string.Empty);
            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;

            (string strRatingDisplay, int intPool, string strTooltip) = ComputeSkillDicePool(
                strName, strSkillGroup, strCategory, strAttribute, intRating, strSpecialization);

            return new CharacterSkillData(strName, strAttribute, intRating.ToString(), strRatingDisplay,
                intPool.ToString(), strTooltip, strSpecialization, strCategory, blnIsGroupLocked);
        }

        /// <summary>Ported from clsUnique.cs's Skill.TotalRating (the dice pool) and
        /// Skill.RatingModifiers/DicePoolModifiers, which is where "skills have augmentations
        /// too" - Skillwire/Adept powers/gear can raise a skill's effective rating
        /// (RatingModifiers, added before the 1.5x-rating cap) separately from bonuses that only
        /// affect the pool without touching the displayed rating (DicePoolModifiers). Both are
        /// aggregated across Skill/SkillGroup/SkillCategory-targeted Improvements, same as the
        /// legacy version.
        ///
        /// Deliberately NOT ported (all narrow, all house-rule or edge-case paths): Skillsoft/
        /// Activesoft rating overrides, the Mystic Adept MAG-split, SwapSkillAttribute, Enhanced
        /// Articulation, defaulting with Rating 0 (a skill at Rating 0 always computes to a Pool
        /// of 0 here, whereas the legacy game rules let some skills default off the linked
        /// attribute alone), the EnforceMaximumSkillRatingModifier/CapSkillRating house rules, and
        /// the metatype-talent MetaRatingModifier bonus.
        /// </summary>
        private (string RatingDisplay, int Pool, string Tooltip) ComputeSkillDicePool(string strName,
            string strSkillGroup, string strCategory, string strAttribute, int intRating, string strSpecialization)
        {
            var lstRatingContributions = SkillImprovementContributions(strName, strSkillGroup, strCategory, blnAddToRating: true);
            var lstPoolContributions = SkillImprovementContributions(strName, strSkillGroup, strCategory, blnAddToRating: false);
            int intRatingMod = lstRatingContributions.Sum(c => c.Value);
            int intPoolMod = lstPoolContributions.Sum(c => c.Value);
            int intAttributeValue = GetAttributeInt(strAttribute);
            int intWound = WoundModifiers;
            int intAugmentedRating = intRating + intRatingMod;

            string strRatingDisplay = intRatingMod == 0
                ? intRating.ToString()
                : intRating + " (" + intAugmentedRating + ")";

            int intPool = intRating == 0
                ? 0
                : Math.Max(0, intAugmentedRating + intPoolMod + intAttributeValue + intWound);

            var sb = new StringBuilder();
            sb.Append("Fertigkeitswert: ").Append(intRating);
            AppendContributions(sb, lstRatingContributions);
            sb.Append('\n').Append("Attribut (").Append(strAttribute).Append("): ").Append(intAttributeValue);
            AppendContributions(sb, lstPoolContributions);
            if (intWound != 0)
                sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
            if (!string.IsNullOrEmpty(strSpecialization))
                sb.Append('\n').Append("Spezialisierung \"").Append(strSpecialization).Append("\": +2 bei Anwendung");
            sb.Append('\n').Append("Würfelpool: ").Append(intPool);

            return (strRatingDisplay, intPool, sb.ToString());
        }

        private IReadOnlyList<(string SourceName, int Value)> SkillImprovementContributions(string strName,
            string strSkillGroup, string strCategory, bool blnAddToRating)
        {
            var lstContributions = new List<(string SourceName, int Value)>(
                ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Skill, strName, blnAddToRating));
            if (!string.IsNullOrEmpty(strSkillGroup))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.SkillGroup, strSkillGroup, blnAddToRating));
            if (!string.IsNullOrEmpty(strCategory))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.SkillCategory, strCategory, blnAddToRating));
            return lstContributions;
        }

        private IReadOnlyList<CharacterContactData> ReadContacts(bool blnEnemies)
        {
            var lstContacts = new List<CharacterContactData>();
            var objNodes = Document.SelectNodes("/character/contacts/contact");
            if (objNodes == null) return lstContacts;
            foreach (XmlNode objNode in objNodes)
            {
                var blnIsEnemy = GetValue(objNode, "type", "Contact") == "Enemy";
                if (blnIsEnemy != blnEnemies) continue;
                lstContacts.Add(new CharacterContactData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "connection", "0"), GetValue(objNode, "loyalty", "0")));
            }

            return lstContacts;
        }

        private IReadOnlyList<CharacterMartialArtData> ReadMartialArts()
        {
            var lstMartialArts = new List<CharacterMartialArtData>();
            var objNodes = Document.SelectNodes("/character/martialarts/martialart");
            if (objNodes == null) return lstMartialArts;
            foreach (XmlNode objNode in objNodes)
            {
                var lstAdvantages = new List<string>();
                var objAdvantageNodes = objNode.SelectNodes("martialartadvantages/martialartadvantage");
                if (objAdvantageNodes != null)
                    foreach (XmlNode objAdvantageNode in objAdvantageNodes)
                        lstAdvantages.Add(GetValue(objAdvantageNode, "name", string.Empty));

                lstMartialArts.Add(new CharacterMartialArtData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "rating", "0"), GetValue(objNode, "source", string.Empty), lstAdvantages));
            }

            return lstMartialArts;
        }

        private IReadOnlyList<CharacterMartialArtManeuverData> ReadMartialArtManeuvers()
        {
            var lstManeuvers = new List<CharacterMartialArtManeuverData>();
            var objNodes = Document.SelectNodes("/character/martialartmaneuvers/martialartmaneuver");
            if (objNodes == null) return lstManeuvers;
            foreach (XmlNode objNode in objNodes)
                lstManeuvers.Add(new CharacterMartialArtManeuverData(GetValue(objNode, "name", string.Empty)));
            return lstManeuvers;
        }

        private IReadOnlyList<CharacterPowerData> ReadAdeptPowers()
        {
            var lstPowers = new List<CharacterPowerData>();
            var objNodes = Document.SelectNodes("/character/powers/power");
            if (objNodes == null) return lstPowers;
            foreach (XmlNode objNode in objNodes)
                lstPowers.Add(new CharacterPowerData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "rating", "0"),
                    GetValue(objNode, "pointsperlevel", "0")));
            return lstPowers;
        }

        private IReadOnlyList<CharacterSpellData> ReadSpells()
        {
            var lstSpells = new List<CharacterSpellData>();
            var objNodes = Document.SelectNodes("/character/spells/spell");
            if (objNodes == null) return lstSpells;
            foreach (XmlNode objNode in objNodes)
                lstSpells.Add(new CharacterSpellData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "category", string.Empty), GetValue(objNode, "type", string.Empty),
                    GetValue(objNode, "range", string.Empty), GetValue(objNode, "damage", string.Empty),
                    GetValue(objNode, "duration", string.Empty), GetValue(objNode, "dv", string.Empty),
                    GetValue(objNode, "source", string.Empty), GetValue(objNode, "page", string.Empty)));
            return lstSpells;
        }

        private IReadOnlyList<CharacterSpiritData> ReadSpirits()
        {
            var lstSpirits = new List<CharacterSpiritData>();
            var objNodes = Document.SelectNodes("/character/spirits/spirit");
            if (objNodes == null) return lstSpirits;
            foreach (XmlNode objNode in objNodes)
                lstSpirits.Add(new CharacterSpiritData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "crittername", string.Empty), GetValue(objNode, "services", "0"),
                    GetValue(objNode, "force", "0"), GetValue(objNode, "bound", "False") == "True",
                    GetValue(objNode, "type", "Spirit")));
            return lstSpirits;
        }

        private IReadOnlyList<CharacterInitiationGradeData> ReadInitiationGrades()
        {
            var lstGrades = new List<CharacterInitiationGradeData>();
            var objNodes = Document.SelectNodes("/character/initiationgrades/initiationgrade");
            if (objNodes == null) return lstGrades;
            foreach (XmlNode objNode in objNodes)
                lstGrades.Add(new CharacterInitiationGradeData(GetValue(objNode, "grade", "0"),
                    GetValue(objNode, "group", "False") == "True", GetValue(objNode, "ordeal", "False") == "True",
                    GetValue(objNode, "res", "False") == "True"));
            return lstGrades;
        }

        private IReadOnlyList<CharacterLifestyleData> ReadLifestyles()
        {
            var lstLifestyles = new List<CharacterLifestyleData>();
            var objNodes = Document.SelectNodes("/character/lifestyles/lifestyle");
            if (objNodes == null) return lstLifestyles;
            foreach (XmlNode objNode in objNodes)
                lstLifestyles.Add(new CharacterLifestyleData(
                    GetValue(objNode, "lifestylename", GetValue(objNode, "name", string.Empty)),
                    GetValue(objNode, "cost", "0"), GetValue(objNode, "months", "0")));
            return lstLifestyles;
        }

        private IReadOnlyList<CharacterWeaponData> ReadVehicles()
        {
            var lstVehicles = new List<CharacterWeaponData>();
            var objNodes = Document.SelectNodes("/character/vehicles/vehicle");
            if (objNodes == null) return lstVehicles;
            foreach (XmlNode objNode in objNodes)
                lstVehicles.Add(new CharacterWeaponData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "category", string.Empty), string.Empty, string.Empty));
            return lstVehicles;
        }

        private IReadOnlyList<CharacterExpenseData> ReadExpenses(string strType)
        {
            var lstExpenses = new List<CharacterExpenseData>();
            var objNodes = Document.SelectNodes("/character/expenses/expense");
            if (objNodes == null) return lstExpenses;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "type", string.Empty) != strType) continue;
                lstExpenses.Add(new CharacterExpenseData(GetValue(objNode, "date", string.Empty),
                    GetValue(objNode, "amount", "0"), GetValue(objNode, "reason", string.Empty)));
            }

            return lstExpenses;
        }

        private static CharacterTreeItemData ReadTreeItem(XmlNode objNode, string strChildXPath)
        {
            var objItem = new CharacterTreeItemData(GetValue(objNode, "name", string.Empty));
            var objChildren = string.IsNullOrEmpty(strChildXPath) ? null : objNode.SelectNodes(strChildXPath);
            if (objChildren != null)
                foreach (XmlNode objChild in objChildren)
                    objItem.Children.Add(ReadTreeItem(objChild, strChildXPath));
            return objItem;
        }

        private static string GetValue(XmlNode objNode, string strName, string strFallback)
        {
            var objChild = objNode.SelectSingleNode(strName);
            return string.IsNullOrEmpty(objChild == null ? null : objChild.InnerText)
                ? strFallback
                : objChild.InnerText;
        }
    }

    public sealed class CharacterAttributeData
    {
        internal CharacterAttributeData(string strCode, string strValue, string strTotalValue, string strMinimum,
            string strMaximum, string strAugmentedMaximum)
        {
            Code = strCode;
            Value = strValue;
            TotalValue = strTotalValue;
            Minimum = strMinimum;
            Maximum = strMaximum;
            AugmentedMaximum = strAugmentedMaximum;
        }

        public string Code { get; private set; }
        public string Value { get; private set; }
        public string TotalValue { get; private set; }
        public string Minimum { get; private set; }
        public string Maximum { get; private set; }
        public string AugmentedMaximum { get; private set; }
    }

    public sealed class CharacterConditionData
    {
        internal CharacterConditionData(string strEssence, string strPhysicalDamage, string strStunDamage,
            CharacterDerivedValueData physicalCm, CharacterDerivedValueData stunCm)
        {
            Essence = strEssence;
            PhysicalDamage = strPhysicalDamage;
            StunDamage = strStunDamage;
            PhysicalCm = physicalCm;
            StunCm = stunCm;
        }

        public string Essence { get; private set; }
        public string PhysicalDamage { get; private set; }
        public string StunDamage { get; private set; }

        /// <summary>Total Physical Condition Monitor boxes (8 + half BOD, rounded up, plus Improvements).</summary>
        public CharacterDerivedValueData PhysicalCm { get; }

        /// <summary>Total Stun Condition Monitor boxes (8 + half WIL, rounded up, plus Improvements).</summary>
        public CharacterDerivedValueData StunCm { get; }
    }

    /// <summary>Armor encumbrance dice-pool penalties - see CharacterDocument.ArmorEncumbrance.</summary>
    public sealed class CharacterEncumbranceData
    {
        internal CharacterEncumbranceData(CharacterDerivedValueData ballisticPenalty, CharacterDerivedValueData impactPenalty)
        {
            BallisticPenalty = ballisticPenalty;
            ImpactPenalty = impactPenalty;
        }

        public CharacterDerivedValueData BallisticPenalty { get; }
        public CharacterDerivedValueData ImpactPenalty { get; }
    }

    /// <summary>Base vs. augmented value for a derived stat that's normally shown as
    /// "base (augmented)" when they differ, e.g. Initiative or Initiative Passes.</summary>
    public sealed class CharacterInitiativeData
    {
        internal CharacterInitiativeData(int intBase, int intAugmented, string strTooltip)
        {
            Base = intBase;
            Augmented = intAugmented;
            Tooltip = strTooltip;
        }

        public int Base { get; }
        public int Augmented { get; }

        /// <summary>Mouseover breakdown of every attribute/Improvement that fed into Augmented.</summary>
        public string Tooltip { get; }

        /// <summary>"5" if Base == Augmented, otherwise "5 (7)".</summary>
        public string Display => Base == Augmented ? Base.ToString() : Base + " (" + Augmented + ")";
    }

    /// <summary>A computed number plus a mouseover explanation of how it was derived - the
    /// tooltip lists the base attribute(s) and each individual contributing Improvement's source
    /// name and value, so several stacking augmentations (cyberware + quality + spell, etc.) are
    /// each visible rather than collapsed into one opaque total.</summary>
    public sealed class CharacterDerivedValueData
    {
        internal CharacterDerivedValueData(int intValue, string strTooltip)
        {
            Value = intValue;
            Tooltip = strTooltip;
        }

        public int Value { get; }
        public string Tooltip { get; }
    }

    public sealed class CharacterQualityData
    {
        internal CharacterQualityData(string strName, string strExtra, string strType)
        {
            Name = strName;
            Extra = strExtra;
            Type = strType;
        }

        public string Name { get; }
        public string Extra { get; }
        public string Type { get; private set; }

        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";
    }

    public sealed class CharacterTreeItemData
    {
        internal CharacterTreeItemData(string strName)
        {
            Name = strName;
            Children = new List<CharacterTreeItemData>();
        }

        public string Name { get; private set; }
        public List<CharacterTreeItemData> Children { get; }
    }

    public sealed class CharacterWeaponData
    {
        internal CharacterWeaponData(string strName, string strCategory, string strDamage, string strAmmo)
        {
            Name = strName;
            Category = strCategory;
            Damage = strDamage;
            Ammo = strAmmo;
        }

        public string Name { get; }
        public string Category { get; }
        public string Damage { get; }
        public string Ammo { get; private set; }

        public string DisplayName
        {
            get
            {
                var strDetails = string.IsNullOrEmpty(Damage) ? Category : Damage;
                return string.IsNullOrEmpty(strDetails) ? Name : Name + " (" + strDetails + ")";
            }
        }
    }

    public sealed class CharacterSkillGroupData
    {
        internal CharacterSkillGroupData(string strName, string strRating)
        {
            Name = strName;
            Rating = strRating;
        }

        public string Name { get; private set; }
        public string Rating { get; private set; }
    }

    public sealed class CharacterSkillData
    {
        internal CharacterSkillData(string strName, string strAttribute, string strBaseRating, string strRating,
            string strTotalValue, string strPoolTooltip, string strSpecialization, string strCategory,
            bool blnIsGroupLocked)
        {
            Name = strName;
            Attribute = strAttribute;
            BaseRating = strBaseRating;
            Rating = strRating;
            TotalValue = strTotalValue;
            PoolTooltip = strPoolTooltip;
            Specialization = strSpecialization;
            Category = strCategory;
            IsGroupLocked = blnIsGroupLocked;
        }

        public string Name { get; private set; }
        public string Attribute { get; }

        /// <summary>The skill's own rating with no Improvements applied - <see cref="Rating"/> is
        /// what UI should actually display.</summary>
        public string BaseRating { get; }

        /// <summary>Rating for display, e.g. "3" or "3 (5)" when skill-rating-boosting
        /// Improvements (Skillwire, an Adept power, etc.) raise it above the base value - same
        /// "base (augmented)" convention as attributes.</summary>
        public string Rating { get; }

        /// <summary>The computed dice pool (skill rating + Improvements + linked attribute +
        /// wound modifiers) - see CharacterDocument's skill-reading code for the full formula and
        /// its documented simplifications.</summary>
        public string TotalValue { get; }

        public string PoolTooltip { get; }
        public string Specialization { get; }
        public string Category { get; private set; }
        public bool IsGroupLocked { get; private set; }
    }

    public sealed class CharacterContactData
    {
        internal CharacterContactData(string strName, string strConnection, string strLoyalty)
        {
            Name = strName;
            Connection = strConnection;
            Loyalty = strLoyalty;
        }

        public string Name { get; }
        public string Connection { get; }
        public string Loyalty { get; }
    }

    public sealed class CharacterMartialArtData
    {
        internal CharacterMartialArtData(string strName, string strRating, string strSource,
            IReadOnlyList<string> lstAdvantages)
        {
            Name = strName;
            Rating = strRating;
            Source = strSource;
            Advantages = lstAdvantages;
        }

        public string Name { get; }
        public string Rating { get; }
        public string Source { get; }
        public IReadOnlyList<string> Advantages { get; }
    }

    public sealed class CharacterMartialArtManeuverData
    {
        internal CharacterMartialArtManeuverData(string strName)
        {
            Name = strName;
        }

        public string Name { get; }
    }

    public sealed class CharacterPowerData
    {
        internal CharacterPowerData(string strName, string strExtra, string strRating, string strPointsPerLevel)
        {
            Name = strName;
            Extra = strExtra;
            Rating = strRating;
            PointsPerLevel = strPointsPerLevel;
        }

        public string Name { get; }
        public string Extra { get; }
        public string Rating { get; }
        public string PointsPerLevel { get; }

        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";

        public string TotalPoints
        {
            get
            {
                if (!decimal.TryParse(PointsPerLevel, out var decPerLevel)
                    || !decimal.TryParse(Rating, out var decRating))
                    return PointsPerLevel;
                return (decPerLevel * decRating).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    public sealed class CharacterSpellData
    {
        internal CharacterSpellData(string strName, string strCategory, string strType, string strRange,
            string strDamage, string strDuration, string strDv, string strSource, string strPage)
        {
            Name = strName;
            Category = strCategory;
            Type = strType;
            Range = strRange;
            Damage = strDamage;
            Duration = strDuration;
            Dv = strDv;
            Source = strSource;
            Page = strPage;
        }

        public string Name { get; }
        public string Category { get; }
        public string Type { get; }
        public string Range { get; }
        public string Damage { get; }
        public string Duration { get; }
        public string Dv { get; }
        public string Source { get; }
        public string Page { get; }
    }

    public sealed class CharacterSpiritData
    {
        internal CharacterSpiritData(string strName, string strCritterName, string strServices, string strForce,
            bool blnBound, string strType)
        {
            Name = strName;
            CritterName = strCritterName;
            Services = strServices;
            Force = strForce;
            Bound = blnBound;
            Type = strType;
        }

        public string Name { get; }
        public string CritterName { get; }
        public string Services { get; }
        public string Force { get; }
        public bool Bound { get; }
        public string Type { get; }

        public string DisplayName => string.IsNullOrEmpty(CritterName) ? Name : Name + " (" + CritterName + ")";
    }

    public sealed class CharacterInitiationGradeData
    {
        internal CharacterInitiationGradeData(string strGrade, bool blnGroup, bool blnOrdeal, bool blnTechnomancer)
        {
            Grade = strGrade;
            Group = blnGroup;
            Ordeal = blnOrdeal;
            Technomancer = blnTechnomancer;
        }

        public string Grade { get; }
        public bool Group { get; }
        public bool Ordeal { get; }
        public bool Technomancer { get; }

        public string DisplayName
        {
            get
            {
                var strLabel = (Technomancer ? "Submersion" : "Initiatengrad") + " " + Grade;
                if (Ordeal) strLabel += " (Prüfung)";
                if (Group) strLabel += " (Gruppe)";
                return strLabel;
            }
        }
    }

    public sealed class CharacterLifestyleData
    {
        internal CharacterLifestyleData(string strName, string strCost, string strMonths)
        {
            Name = strName;
            Cost = strCost;
            Months = strMonths;
        }

        public string Name { get; }
        public string Cost { get; }
        public string Months { get; }
    }

    public sealed class CharacterExpenseData
    {
        internal CharacterExpenseData(string strDate, string strAmount, string strReason)
        {
            Date = strDate;
            Amount = strAmount;
            Reason = strReason;
        }

        public string Date { get; }
        public string Amount { get; }
        public string Reason { get; }

        public string DisplayDate =>
            System.DateTime.TryParse(Date, out var datValue) ? datValue.ToString("dd.MM.yyyy") : Date;
    }
}