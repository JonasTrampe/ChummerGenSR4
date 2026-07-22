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

        public string Alias
        {
            get => GetValue("/character/alias", string.Empty);
            set => SetRootValue("alias", value);
        }

        public string CloudDocumentId
        {
            get => GetValue("/character/clouddocumentid", string.Empty);
            set => SetRootValue("clouddocumentid", value);
        }

        public string CloudLastKnownRevisionId
        {
            get => GetValue("/character/cloudlastknownrevisionid", string.Empty);
            set => SetRootValue("cloudlastknownrevisionid", value);
        }

        public bool CloudIsShared
        {
            get => GetValue("/character/cloudisshared", "False") == "True";
            set => SetRootValue("cloudisshared", value ? "True" : "False");
        }

        public string CloudMetadataDisplayName
        {
            get => GetValue("/character/clouddisplayname", string.Empty);
            set => SetRootValue("clouddisplayname", value);
        }

        public string CloudMetadataDescription
        {
            get => GetValue("/character/clouddescription", string.Empty);
            set => SetRootValue("clouddescription", value);
        }

        public string CloudMetadataImageUrl
        {
            get => GetValue("/character/cloudimageurl", string.Empty);
            set => SetRootValue("cloudimageurl", value);
        }

        public string Metatype => GetValue("/character/metatype", string.Empty);

        public string MetatypeCategory => GetValue("/character/metatypecategory", string.Empty);

        public bool Adept => GetValue("/character/adept", "False") == "True";

        public bool Magician => GetValue("/character/magician", "False") == "True";

        public bool MysticAdept => Adept && Magician;

        public bool Awakened => Adept || Magician;

        public int MysticAdeptAdeptMagSplit =>
            int.TryParse(GetValue("/character/magsplitadept", "0"), out var i) ? i : 0;

        public int MysticAdeptMagicianMagSplit =>
            int.TryParse(GetValue("/character/magsplitmagician", "0"), out var i) ? i : 0;

        public bool Technomancer => GetValue("/character/technomancer", "False") == "True";

        public IReadOnlyList<CharacterCommlinkData> Commlinks => ReadCommlinks();

        /// <summary>Matrix "System" stat, only meaningful for A.I./technocritter/protosapient
        /// characters (drone/sprite-style characters whose Matrix Initiative uses this instead of
        /// a Commlink's Response) - see MatrixInitiative.</summary>
        public int SystemResponse => int.TryParse(GetValue("/character/response", "0"), out var i) ? i : 0;

        /// <summary>True for A.I., technocritter, and protosapient characters, which compute
        /// Matrix Initiative/Passes differently (INT + Response instead of the human/Technomancer
        /// formulas, and always 3 Passes) - ported from the metatype checks scattered through
        /// clsCharacter.cs's MatrixInitiative/MatrixInitiativePasses.</summary>
        public bool IsMatrixNative => Metatype.EndsWith("A.I.")
            || MetatypeCategory is "Technocritters" or "Protosapients";

        /// <summary>Response rating of the character's equipped, active Commlink (0 if none),
        /// ported from clsCommonFunctions.FindCommlinks + Commlink.TotalResponse as used by
        /// MatrixInitiative. Searches every &lt;gear&gt; node anywhere in the document (so this
        /// does find Commlinks nested under Armor/Cyberware, same as the legacy scan), but doesn't
        /// separately check Vehicles' own onboard Commlinks the way FindCommlinks does. Uses the
        /// raw &lt;response&gt; value rather than TotalResponse (gear-mod bonuses to Response
        /// aren't modeled).</summary>
        private int ActiveCommlinkResponse()
        {
            var objNodes = Document.SelectNodes(
                "//gear[category = 'Commlinks' and equipped = 'True' and active = 'True']/response");
            return objNodes is { Count: > 0 } && int.TryParse(objNodes[0]!.InnerText, out var intResponse)
                ? intResponse
                : 0;
        }

        public void SetActiveCommlink(string strGuid)
        {
            XmlNodeList? objNodes = Document.SelectNodes("//gear[category = 'Commlinks']");
            if (objNodes == null)
                return;

            foreach (XmlNode objNode in objNodes)
            {
                string strNodeGuid = GetValue(objNode, "guid", string.Empty);
                SetChildValue(objNode, "active", strNodeGuid == strGuid ? "True" : "False");
            }
        }

        public string Karma => GetValue("/character/karma", "0");

        public string Nuyen
        {
            get => GetValue("/character/nuyen", "0");
            set => SetRootValue("nuyen", value);
        }

        /// <summary>Saved walking/running movement string (for example "10/25"), written by the
        /// legacy character save as &lt;movementwalk&gt;.</summary>
        public string WalkMovement => GetValue("/character/movementwalk", string.Empty);

        /// <summary>Saved swim movement string (for example "5"), written by the legacy save as
        /// &lt;movementswim&gt;.</summary>
        public string SwimMovement => GetValue("/character/movementswim", string.Empty);

        /// <summary>Saved fly movement string (for example "45/90"), written by the legacy save as
        /// &lt;movementfly&gt;. Empty for characters that do not fly.</summary>
        public string FlyMovement => GetValue("/character/movementfly", string.Empty);

        /// <summary>Total Karma earned over the character's career (sum of positive, non-refund
        /// Karma expense entries), ported from clsCharacter.cs's CareerKarma.</summary>
        public int CareerKarma => SumEarnedExpenses(KarmaExpenses);

        /// <summary>Total Nuyen earned over the character's career (sum of positive, non-refund
        /// Nuyen expense entries), ported from clsCharacter.cs's CareerNuyen.</summary>
        public int CareerNuyen => SumEarnedExpenses(NuyenExpenses);

        private static int SumEarnedExpenses(IReadOnlyList<CharacterExpenseData> lstExpenses)
        {
            int intTotal = 0;
            foreach (CharacterExpenseData expense in lstExpenses)
            {
                if (!expense.Refund && int.TryParse(expense.Amount, out var intAmount) && intAmount > 0)
                    intTotal += intAmount;
            }

            return intTotal;
        }

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
        public IReadOnlyList<CalendarWeek> Calendar => ReadCalendar();

        public IReadOnlyList<CharacterQualityData> Qualities => ReadQualities();

        /// <summary>
        /// Adds a quality using the character-file representation used by the legacy application.
        /// The rules definition stays in <c>qualities.xml</c>; a character save only records the
        /// chosen name, optional selection detail, and positive/negative category. Because this
        /// mutates the backing document, <see cref="CharacterFileService.Save"/> persists it.
        /// </summary>
        public void AddQuality(string strName, string strType, string strExtra = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A quality name is required.", nameof(strName));
            if (strType != "Positive" && strType != "Negative")
                throw new ArgumentException("A quality must be Positive or Negative.", nameof(strType));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objQualities = objRoot.SelectSingleNode("qualities");
            if (objQualities == null)
            {
                objQualities = Document.CreateElement("qualities");
                objRoot.AppendChild(objQualities);
            }

            var objQuality = Document.CreateElement("quality");
            AppendElement(objQuality, "name", strName.Trim());
            AppendElement(objQuality, "extra", strExtra.Trim());
            AppendElement(objQuality, "qualitytype", strType);
            objQualities.AppendChild(objQuality);
        }

        /// <summary>Removes the first saved quality matching its name, type, and optional detail.</summary>
        public bool RemoveQuality(string strName, string strType, string strExtra = "")
        {
            var objNodes = Document.SelectNodes("/character/qualities/quality");
            if (objNodes == null)
                return false;

            foreach (XmlNode objQuality in objNodes)
            {
                if (GetValue(objQuality, "name", string.Empty) != strName
                    || GetValue(objQuality, "qualitytype", string.Empty) != strType
                    || GetValue(objQuality, "extra", string.Empty) != strExtra)
                    continue;

                objQuality.ParentNode?.RemoveChild(objQuality);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a spell using the saved-character fields consumed by <see cref="Spells"/>.
        /// Rules metadata is selected from spells.xml by the UI and copied here so the character
        /// remains self-contained when saved and reopened.
        /// </summary>
        public void AddSpell(string strName, string strCategory, string strType, string strRange, string strDamage,
            string strDuration, string strDv, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A spell name is required.", nameof(strName));

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
            AppendElement(objSpell, "source", strSource);
            AppendElement(objSpell, "page", strPage);
            objSpells.AppendChild(objSpell);
        }

        /// <summary>Adds a root-level gear item in the minimal saved-character tree shape.</summary>
        public void AddGear(string strName, string strCategory, string strRating = "0")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A gear name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objGears = objRoot.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objRoot.AppendChild(objGears);
            }

            var objGear = Document.CreateElement("gear");
            AppendElement(objGear, "name", strName.Trim());
            AppendElement(objGear, "category", strCategory);
            AppendElement(objGear, "rating", strRating);
            AppendElement(objGear, "equipped", "False");
            objGear.AppendChild(Document.CreateElement("children"));
            objGears.AppendChild(objGear);
        }

        /// <summary>Removes the first root-level saved gear item matching its name/category/rating.</summary>
        public bool RemoveGear(string strName, string strCategory, string strRating = "0")
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/gears/gear");
            if (objNodes == null)
                return false;

            foreach (XmlNode objGear in objNodes)
            {
                if (!string.Equals(GetValue(objGear, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objGear, "category", string.Empty), strCategory, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objGear, "rating", "0"), strRating, StringComparison.Ordinal))
                    continue;

                objGear.ParentNode?.RemoveChild(objGear);
                return true;
            }

            return false;
        }

        /// <summary>Removes the first saved spell with the supplied name.</summary>
        public bool RemoveSpell(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/spells/spell");
            if (objNodes == null)
                return false;

            foreach (XmlNode objSpell in objNodes)
            {
                if (!string.Equals(GetValue(objSpell, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                objSpell.ParentNode?.RemoveChild(objSpell);
                return true;
            }

            return false;
        }

        private void AppendElement(XmlElement objParent, string strName, string strValue)
        {
            var objElement = Document.CreateElement(strName);
            objElement.InnerText = strValue;
            objParent.AppendChild(objElement);
        }

        private void SetRootValue(string strName, string strValue)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objElement = objRoot.SelectSingleNode(strName) as XmlElement;
            if (objElement == null)
            {
                objElement = Document.CreateElement(strName);
                objRoot.AppendChild(objElement);
            }

            objElement.InnerText = strValue ?? string.Empty;
        }

        private void SetChildValue(XmlNode objParent, string strName, string strValue)
        {
            XmlNode? objNode = objParent.SelectSingleNode(strName);
            if (objNode == null)
            {
                objNode = Document.CreateElement(strName);
                objParent.AppendChild(objNode);
            }

            objNode.InnerText = strValue ?? string.Empty;
        }

        public IReadOnlyList<CharacterTreeItemData> Gear => ReadTreeItems("/character/gears/gear", "children/gear");

        // Cyberware and bioware are saved to the same <cyberwares> list and only distinguished by
        // <improvementsource> ("Cyberware" vs "Bioware") - split here so each gets its own tree.
        public IReadOnlyList<CharacterTreeItemData> Cyberware =>
            ReadTreeItems("/character/cyberwares/cyberware[improvementsource != 'Bioware']", "children/cyberware");

        public IReadOnlyList<CharacterTreeItemData> Bioware =>
            ReadTreeItems("/character/cyberwares/cyberware[improvementsource = 'Bioware']", "children/cyberware");

        /// <summary>Armor is represented as a tree so installed armor modifications and optional
        /// saved armor sets remain visible instead of being flattened into a list.</summary>
        public IReadOnlyList<CharacterTreeItemData> Armor => ReadArmorTree();

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

        /// <summary>Damage Resistance dice pool, ported from frmCareer.cs's condition-monitor
        /// refresh: BOD + DamageResistance Improvements.</summary>
        public CharacterDerivedValueData DamageResistance
        {
            get
            {
                int intBody = GetAttributeInt("BOD");
                var sb = new StringBuilder();
                sb.Append("Konstitution: ").Append(intBody);

                var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.DamageResistance);
                int intTotal = intBody + lstContributions.Sum(c => c.Value);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Gesamt: ").Append(intTotal);

                return new CharacterDerivedValueData(intTotal, sb.ToString());
            }
        }

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

        /// <summary>Matrix Initiative, ported from clsCharacter.cs. Covers three of the four
        /// legacy branches:
        ///  - A.I./technocritter/protosapient: INT + Response (checked first - it overrides
        ///    everything else, same order as the legacy version).
        ///  - Technomancer (and not A.I.): (INT x 2) + 1 + LivingPersonaResponse Improvements.
        ///  - Otherwise: INT + active Commlink's Response + MatrixInitiative Improvements (the
        ///    default human/non-awakened path) - see ActiveCommlinkResponse's doc comment for its
        ///    scoped-down Gear search.
        /// NOT ported: Sprites using a fixed metatype-minimum value (that value comes from
        /// metatypes.xml data Core doesn't load), and the TechnomancerAllowCommlink house rule
        /// (which would let a Technomancer use this branch instead of their own).</summary>
        public CharacterInitiativeData MatrixInitiative
        {
            get
            {
                int intInt = GetAttributeInt("INT");
                int intWound = WoundModifiers;
                int intBase;
                var sb = new StringBuilder();

                if (IsMatrixNative)
                {
                    int intResponse = SystemResponse;
                    intBase = intInt + intResponse;
                    sb.Append("Intuition: ").Append(intInt);
                    sb.Append('\n').Append("System: ").Append(intResponse);
                }
                else if (Technomancer)
                {
                    var lstLivingPersona = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.LivingPersonaResponse);
                    intBase = (intInt * 2) + 1 + lstLivingPersona.Sum(c => c.Value);
                    sb.Append("(Intuition x 2) + 1: ").Append((intInt * 2) + 1);
                    AppendContributions(sb, lstLivingPersona);
                }
                else
                {
                    int intCommlinkResponse = ActiveCommlinkResponse();
                    var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.MatrixInitiative);
                    intBase = intInt + intCommlinkResponse + lstContributions.Sum(c => c.Value);
                    sb.Append("Intuition: ").Append(intInt);
                    if (intCommlinkResponse != 0)
                        sb.Append('\n').Append("Kommlink-Antwort: ").Append(intCommlinkResponse);
                    AppendContributions(sb, lstContributions);
                }

                int intAugmented = intBase + intWound;
                if (intWound != 0)
                    sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
                sb.Append('\n').Append("Gesamt: ").Append(Math.Max(intAugmented, 0));

                return new CharacterInitiativeData(intBase, Math.Max(intAugmented, 0), sb.ToString());
            }
        }

        /// <summary>Matrix Initiative Passes, ported from clsCharacter.cs: 3 base for
        /// Technomancers (1 otherwise), plus MatrixInitiativePass Improvements - except for A.I./
        /// technocritter/protosapient characters, who always get a fixed 3 regardless of the
        /// above (same override order as the legacy version). MatrixInitiativePassAdd
        /// Improvements always apply on top, even for A.I.s.</summary>
        public CharacterInitiativeData MatrixInitiativePasses
        {
            get
            {
                var sb = new StringBuilder();
                int intBase;
                int intPasses;

                if (IsMatrixNative)
                {
                    intBase = 3;
                    intPasses = 3;
                    sb.Append("Basis (A.I./technokritisches Metatyp): 3");
                }
                else
                {
                    var lstPassContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.MatrixInitiativePass);
                    intBase = Technomancer ? 3 : 1;
                    intPasses = intBase + lstPassContributions.Sum(c => c.Value);
                    sb.Append("Basis").Append(Technomancer ? " (Technomancer)" : "").Append(": ").Append(intBase);
                    AppendContributions(sb, lstPassContributions);
                }

                var lstAddContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.MatrixInitiativePassAdd);
                intPasses += lstAddContributions.Sum(c => c.Value);
                AppendContributions(sb, lstAddContributions);
                sb.Append('\n').Append("Gesamt: ").Append(intPasses);

                return new CharacterInitiativeData(intBase, intPasses, sb.ToString());
            }
        }

        public IReadOnlyList<CharacterWeaponData> Weapons => ReadWeapons();

        /// <summary>Weapons with their installed accessories, modifications, and mounted gear.</summary>
        public IReadOnlyList<CharacterTreeItemData> WeaponTrees => ReadWeaponTrees();

        public IReadOnlyList<CharacterSkillGroupData> SkillGroups => ReadSkillGroups();

        public IReadOnlyList<CharacterSkillData> Skills => ReadSkills();

        public IReadOnlyList<CharacterSkillData> KnowledgeSkills => ReadKnowledgeSkills();

        public void AddKnowledgeSkill(string strName, string strCategory)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSkills = objRoot.SelectSingleNode("skills");
            if (objSkills == null)
            {
                objSkills = Document.CreateElement("skills");
                objRoot.AppendChild(objSkills);
            }

            var objSkill = Document.CreateElement("skill");
            string strAttribute = AttributeForKnowledgeCategory(strCategory);
            AppendElement(objSkill, "name", strName.Trim());
            AppendElement(objSkill, "skillgroup", string.Empty);
            AppendElement(objSkill, "skillcategory", strCategory);
            AppendElement(objSkill, "grouped", "False");
            AppendElement(objSkill, "default", "False");
            AppendElement(objSkill, "rating", "1");
            AppendElement(objSkill, "ratingmax", "6");
            AppendElement(objSkill, "knowledge", "True");
            AppendElement(objSkill, "exotic", "False");
            AppendElement(objSkill, "spec", string.Empty);
            AppendElement(objSkill, "allowdelete", "True");
            AppendElement(objSkill, "attribute", strAttribute);
            AppendElement(objSkill, "totalvalue", "0");
            objSkills.AppendChild(objSkill);
        }

        public bool UpdateKnowledgeSkill(int intSkillId, string strName, string strRating, string strSpecialization,
            string strCategory)
        {
            XmlNode? objNode = GetKnowledgeSkillNode(intSkillId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "name", strName);
            SetChildValue(objNode, "rating", strRating);
            SetChildValue(objNode, "spec", strSpecialization);
            SetChildValue(objNode, "skillcategory", strCategory);
            SetChildValue(objNode, "attribute", AttributeForKnowledgeCategory(strCategory));
            return true;
        }

        public bool RemoveKnowledgeSkill(int intSkillId)
        {
            XmlNode? objNode = GetKnowledgeSkillNode(intSkillId);
            if (objNode?.ParentNode == null)
                return false;

            objNode.ParentNode.RemoveChild(objNode);
            return true;
        }

        // Enemies are saved into the same <contacts> list as regular contacts and are only
        // distinguished by <type>Enemy</type> - split here so each gets its own display list.
        public IReadOnlyList<CharacterContactData> Contacts => ReadContacts(blnEnemies: false);

        public IReadOnlyList<CharacterContactData> Enemies => ReadContacts(blnEnemies: true);

        public void AddContact(string strName, string strConnection, string strLoyalty, bool blnEnemy)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objContacts = objRoot.SelectSingleNode("contacts");
            if (objContacts == null)
            {
                objContacts = Document.CreateElement("contacts");
                objRoot.AppendChild(objContacts);
            }

            var objContact = Document.CreateElement("contact");
            AppendElement(objContact, "name", strName.Trim());
            AppendElement(objContact, "connection", strConnection);
            AppendElement(objContact, "loyalty", strLoyalty);
            AppendElement(objContact, "membership", "0");
            AppendElement(objContact, "areaofinfluence", "0");
            AppendElement(objContact, "magicalresources", "0");
            AppendElement(objContact, "matrixresources", "0");
            AppendElement(objContact, "type", blnEnemy ? "Enemy" : "Contact");
            AppendElement(objContact, "file", string.Empty);
            AppendElement(objContact, "notes", string.Empty);
            AppendElement(objContact, "groupname", string.Empty);
            AppendElement(objContact, "colour", "0");
            AppendElement(objContact, "free", "False");
            objContacts.AppendChild(objContact);
        }

        public bool UpdateContact(int intContactId, string strName, string strConnection, string strLoyalty)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "name", strName);
            SetChildValue(objNode, "connection", strConnection);
            SetChildValue(objNode, "loyalty", strLoyalty);
            return true;
        }

        public bool RemoveContact(int intContactId)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode?.ParentNode == null)
                return false;

            objNode.ParentNode.RemoveChild(objNode);
            return true;
        }

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

        /// <summary>
        /// Appends a Karma or Nuyen history entry in the same XML shape as the legacy career mode.
        /// Positive amounts are earnings; negative amounts are expenditures. The caller supplies
        /// the signed amount so refunds can be represented without a second write API.
        /// </summary>
        public void AddExpense(string strType, decimal decAmount, string strReason, DateTime? datDate = null)
        {
            if (strType != "Karma" && strType != "Nuyen")
                throw new ArgumentException("An expense must be Karma or Nuyen.", nameof(strType));
            if (decAmount == 0)
                throw new ArgumentOutOfRangeException(nameof(decAmount), "An expense amount cannot be zero.");
            if (string.IsNullOrWhiteSpace(strReason))
                throw new ArgumentException("An expense reason is required.", nameof(strReason));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objExpenses = objRoot.SelectSingleNode("expenses");
            if (objExpenses == null)
            {
                objExpenses = Document.CreateElement("expenses");
                objRoot.AppendChild(objExpenses);
            }

            var objExpense = Document.CreateElement("expense");
            AppendElement(objExpense, "guid", Guid.NewGuid().ToString());
            AppendElement(objExpense, "date", (datDate ?? DateTime.Now).ToString("O"));
            AppendElement(objExpense, "amount", decAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendElement(objExpense, "reason", strReason.Trim());
            AppendElement(objExpense, "type", strType);
            AppendElement(objExpense, "refund", "False");
            objExpenses.AppendChild(objExpense);
        }

        public string Gender
        {
            get => GetValue("/character/sex", string.Empty);
            set => SetRootValue("sex", value);
        }

        public string EyeColor
        {
            get => GetValue("/character/eyes", string.Empty);
            set => SetRootValue("eyes", value);
        }

        public string HairColor
        {
            get => GetValue("/character/hair", string.Empty);
            set => SetRootValue("hair", value);
        }

        public string Height
        {
            get => GetValue("/character/height", string.Empty);
            set => SetRootValue("height", value);
        }

        public string Weight
        {
            get => GetValue("/character/weight", string.Empty);
            set => SetRootValue("weight", value);
        }

        public string SkinColor
        {
            get => GetValue("/character/skin", string.Empty);
            set => SetRootValue("skin", value);
        }

        public string PlayerName
        {
            get => GetValue("/character/playername", string.Empty);
            set => SetRootValue("playername", value);
        }

        public string StreetCred
        {
            get => GetValue("/character/streetcred", "0");
            set => SetRootValue("streetcred", value);
        }

        public string Notoriety
        {
            get => GetValue("/character/notoriety", "0");
            set => SetRootValue("notoriety", value);
        }

        public string PublicAwareness
        {
            get => GetValue("/character/publicawareness", "0");
            set => SetRootValue("publicawareness", value);
        }

        public string Description
        {
            get => GetValue("/character/description", string.Empty);
            set => SetRootValue("description", value);
        }

        public string Background
        {
            get => GetValue("/character/background", string.Empty);
            set => SetRootValue("background", value);
        }

        public string Concept
        {
            get => GetValue("/character/concept", string.Empty);
            set => SetRootValue("concept", value);
        }

        public string Notes
        {
            get => GetValue("/character/notes", string.Empty);
            set => SetRootValue("notes", value);
        }

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

            var intBallisticRating = ComputeArmorRating(objNodes, "b", ImprovementType.BallisticArmor);
            var intImpactRating = ComputeArmorRating(objNodes, "i", ImprovementType.ImpactArmor);

            return new CharacterEncumbranceData(
                BuildArmorRatingValue(intBallisticRating, "b", "ballistisch", objNodes, ImprovementType.BallisticArmor),
                BuildArmorRatingValue(intImpactRating, "i", "Stoß", objNodes, ImprovementType.ImpactArmor),
                BuildEncumbranceValue(intTotalBallistic, intThreshold, "ballistisch", strThresholdNote, lstWorn),
                BuildEncumbranceValue(intTotalImpact, intThreshold, "Stoß", strThresholdNote, lstWorn));
        }

        private int ComputeArmorRating(XmlNodeList? objNodes, string strElement, ImprovementType eImprovementType)
        {
            var intHighest = 0;
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                    intHighest = Math.Max(intHighest, ParseArmorRating(GetValue(objNode, strElement, "0")));
            }

            return intHighest + ImprovementManager.ValueOf(Improvements, eImprovementType);
        }

        private CharacterDerivedValueData BuildArmorRatingValue(int intTotal, string strElement, string strKind,
            XmlNodeList? objNodes, ImprovementType eImprovementType)
        {
            var sb = new StringBuilder();
            sb.Append("Höchste getragene Panzerung (").Append(strKind).Append("):");
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                    sb.Append('\n').Append("  ").Append(GetValue(objNode, "name", string.Empty)).Append(": ")
                        .Append(ParseArmorRating(GetValue(objNode, strElement, "0")));
            }

            foreach (var objContribution in ImprovementManager.DescribeValueOf(Improvements, eImprovementType))
                sb.Append('\n').Append("  ").Append(objContribution.SourceName).Append(": ")
                    .Append(FormatSigned(objContribution.Value));
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
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

        private IReadOnlyList<CalendarWeek> ReadCalendar()
        {
            var lstWeeks = new List<CalendarWeek>();
            XmlNodeList? objNodes = Document.SelectNodes("/character/calendar/week");
            if (objNodes == null) return lstWeeks;
            foreach (XmlNode objNode in objNodes)
            {
                var objWeek = new CalendarWeek();
                objWeek.Load(objNode);
                lstWeeks.Add(objWeek);
            }
            return lstWeeks;
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

        private IReadOnlyList<CharacterTreeItemData> ReadArmorTree()
        {
            var lstArmor = new List<CharacterTreeItemData>();
            var dicSets = new Dictionary<string, CharacterTreeItemData>(StringComparer.Ordinal);
            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null) return lstArmor;

            foreach (XmlNode objNode in objNodes)
            {
                var objArmor = ReadTreeItem(objNode, "armormods/armormod", "gears/gear");
                string strSetName = GetValue(objNode, "armorname", string.Empty);
                if (string.IsNullOrWhiteSpace(strSetName))
                {
                    lstArmor.Add(objArmor);
                    continue;
                }

                if (!dicSets.TryGetValue(strSetName, out var objSet))
                {
                    objSet = new CharacterTreeItemData(strSetName, "Armor set");
                    dicSets.Add(strSetName, objSet);
                    lstArmor.Add(objSet);
                }
                objSet.Children.Add(objArmor);
            }

            return lstArmor;
        }

        private IReadOnlyList<CharacterTreeItemData> ReadWeaponTrees()
        {
            var lstWeapons = new List<CharacterTreeItemData>();
            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return lstWeapons;
            foreach (XmlNode objNode in objNodes)
                lstWeapons.Add(ReadTreeItem(objNode, "accessories/accessory", "weaponmods/weaponmod", "gears/gear", "ammos/ammo"));
            return lstWeapons;
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
            int intSkillId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") != "True")
                {
                    lstSkills.Add(BuildSkillData(intSkillId, objNode, GetValue(objNode, "skillgroup", string.Empty),
                        GetValue(objNode, "grouped", "False") == "True"));
                }

                intSkillId++;
            }

            return lstSkills;
        }

        private IReadOnlyList<CharacterSkillData> ReadKnowledgeSkills()
        {
            var lstSkills = new List<CharacterSkillData>();
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return lstSkills;
            int intSkillId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "knowledge", "False") == "True")
                {
                    lstSkills.Add(BuildSkillData(intSkillId, objNode, string.Empty, blnIsGroupLocked: false));
                }

                intSkillId++;
            }

            return lstSkills;
        }

        private CharacterSkillData BuildSkillData(int intSkillId, XmlNode objNode, string strSkillGroup, bool blnIsGroupLocked)
        {
            string strName = GetValue(objNode, "name", string.Empty);
            string strAttribute = GetValue(objNode, "attribute", string.Empty);
            string strCategory = GetValue(objNode, "skillcategory", string.Empty);
            string strSpecialization = GetValue(objNode, "spec", string.Empty);
            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            bool blnAllowDelete = GetValue(objNode, "allowdelete", "False") == "True";
            bool blnKnowledge = GetValue(objNode, "knowledge", "False") == "True";

            (string strRatingDisplay, int intPool, string strTooltip) = ComputeSkillDicePool(
                strName, strSkillGroup, strCategory, strAttribute, intRating, strSpecialization);

            return new CharacterSkillData(intSkillId, strName, strAttribute, intRating.ToString(), strRatingDisplay,
                intPool.ToString(), strTooltip, strSpecialization, strCategory, blnIsGroupLocked, blnAllowDelete,
                blnKnowledge);
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
            int intContactId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                var blnIsEnemy = GetValue(objNode, "type", "Contact") == "Enemy";
                if (blnIsEnemy == blnEnemies)
                {
                    lstContacts.Add(new CharacterContactData(intContactId,
                        GetValue(objNode, "name", string.Empty),
                        GetValue(objNode, "connection", "0"),
                        GetValue(objNode, "loyalty", "0"),
                        blnIsEnemy));
                }

                intContactId++;
            }

            return lstContacts;
        }

        private XmlNode? GetContactNode(int intContactId)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/contacts/contact");
            return objNodes != null && intContactId >= 0 && intContactId < objNodes.Count
                ? objNodes[intContactId]
                : null;
        }

        private XmlNode? GetKnowledgeSkillNode(int intSkillId)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null || intSkillId < 0 || intSkillId >= objNodes.Count)
                return null;

            XmlNode objNode = objNodes[intSkillId]!;
            return GetValue(objNode, "knowledge", "False") == "True" ? objNode : null;
        }

        private static string AttributeForKnowledgeCategory(string strCategory)
            => strCategory is "Street" or "Interest" or "Language" ? "INT" : "LOG";

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
                    GetValue(objNode, "pointsperlevel", "0"),
                    GetValue(objNode, "discounted", "False"),
                    GetValue(objNode, "discountedgeas", "False")));
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
                    GetValue(objNode, "amount", "0"), GetValue(objNode, "reason", string.Empty),
                    GetValue(objNode, "refund", "False") == "True"));
            }

            return lstExpenses;
        }

        private IReadOnlyList<CharacterCommlinkData> ReadCommlinks()
        {
            var lstCommlinks = new List<CharacterCommlinkData>();
            XmlNodeList? objNodes = Document.SelectNodes("//gear[category = 'Commlinks']");
            if (objNodes == null)
                return lstCommlinks;

            foreach (XmlNode objNode in objNodes)
            {
                string strGuid = GetValue(objNode, "guid", string.Empty);
                if (string.IsNullOrEmpty(strGuid))
                    continue;

                lstCommlinks.Add(new CharacterCommlinkData(
                    strGuid,
                    GetValue(objNode, "name", string.Empty),
                    int.TryParse(GetValue(objNode, "response", "0"), out var intResponse) ? intResponse : 0,
                    GetValue(objNode, "equipped", "False") == "True",
                    GetValue(objNode, "active", "False") == "True"));
            }

            return lstCommlinks;
        }

        private static CharacterTreeItemData ReadTreeItem(XmlNode objNode, params string[] lstChildXPaths)
        {
            var objItem = new CharacterTreeItemData(GetValue(objNode, "name", string.Empty),
                GetValue(objNode, "category", string.Empty), GetValue(objNode, "rating", "0"),
                GetValue(objNode, "equipped", "False") == "True");
            foreach (string strChildXPath in lstChildXPaths)
            {
                if (string.IsNullOrEmpty(strChildXPath)) continue;
                var objChildren = objNode.SelectNodes(strChildXPath);
                if (objChildren == null) continue;
                foreach (XmlNode objChild in objChildren)
                    objItem.Children.Add(ReadTreeItem(objChild, strChildXPath));
            }
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
        internal CharacterEncumbranceData(CharacterDerivedValueData ballisticRating, CharacterDerivedValueData impactRating,
            CharacterDerivedValueData ballisticPenalty, CharacterDerivedValueData impactPenalty)
        {
            BallisticRating = ballisticRating;
            ImpactRating = impactRating;
            BallisticPenalty = ballisticPenalty;
            ImpactPenalty = impactPenalty;
        }

        public CharacterDerivedValueData BallisticRating { get; }
        public CharacterDerivedValueData ImpactRating { get; }
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
        internal CharacterTreeItemData(string strName, string strCategory = "", string strRating = "0",
            bool blnEquipped = false)
        {
            Name = strName;
            Category = strCategory;
            Rating = strRating;
            Equipped = blnEquipped;
            Children = new List<CharacterTreeItemData>();
        }

        public string Name { get; private set; }

        /// <summary>Empty for item types that don't save one (e.g. Quality nodes don't reuse this
        /// class). Gear/Cyberware/Armor/Weapon all use the same &lt;category&gt; element name.</summary>
        public string Category { get; }

        public string Rating { get; }

        /// <summary>Whether the item is currently worn/active/installed - only meaningful for
        /// item types that track it (Gear, Armor, Cyberware all do; not everything does).</summary>
        public bool Equipped { get; }

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
        internal CharacterSkillData(int intSkillId, string strName, string strAttribute, string strBaseRating,
            string strRating, string strTotalValue, string strPoolTooltip, string strSpecialization,
            string strCategory, bool blnIsGroupLocked, bool blnAllowDelete, bool blnKnowledgeSkill)
        {
            SkillId = intSkillId;
            Name = strName;
            Attribute = strAttribute;
            BaseRating = strBaseRating;
            Rating = strRating;
            TotalValue = strTotalValue;
            PoolTooltip = strPoolTooltip;
            Specialization = strSpecialization;
            Category = strCategory;
            IsGroupLocked = blnIsGroupLocked;
            AllowDelete = blnAllowDelete;
            KnowledgeSkill = blnKnowledgeSkill;
        }

        public int SkillId { get; }
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
        public bool AllowDelete { get; private set; }
        public bool KnowledgeSkill { get; private set; }
    }

    public sealed class CharacterContactData
    {
        internal CharacterContactData(int intContactId, string strName, string strConnection, string strLoyalty,
            bool blnIsEnemy)
        {
            ContactId = intContactId;
            Name = strName;
            Connection = strConnection;
            Loyalty = strLoyalty;
            IsEnemy = blnIsEnemy;
        }

        public int ContactId { get; }
        public string Name { get; }
        public string Connection { get; }
        public string Loyalty { get; }
        public bool IsEnemy { get; }
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
        internal CharacterPowerData(string strName, string strExtra, string strRating, string strPointsPerLevel,
            string strDiscountedAdeptWay, string strDiscountedGeas)
        {
            Name = strName;
            Extra = strExtra;
            Rating = strRating;
            PointsPerLevel = strPointsPerLevel;
            DiscountedAdeptWay = bool.TryParse(strDiscountedAdeptWay, out var blnDiscountedAdeptWay)
                && blnDiscountedAdeptWay;
            DiscountedGeas = bool.TryParse(strDiscountedGeas, out var blnDiscountedGeas)
                && blnDiscountedGeas;
        }

        public string Name { get; }
        public string Extra { get; }
        public string Rating { get; }
        public string PointsPerLevel { get; }
        public bool DiscountedAdeptWay { get; }
        public bool DiscountedGeas { get; }

        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";

        public decimal Discount
        {
            get
            {
                if (!DiscountedAdeptWay && !DiscountedGeas)
                    return 1.0m;

                decimal decMultiplier = 1.0m;
                if (DiscountedAdeptWay)
                    decMultiplier -= 0.25m;
                if (DiscountedGeas)
                    decMultiplier -= 0.25m;
                return decMultiplier;
            }
        }

        public string CalculatedPointsPerLevel
        {
            get
            {
                if (!decimal.TryParse(PointsPerLevel, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var decPerLevel))
                    return PointsPerLevel;

                return (decPerLevel * Discount).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public string TotalPoints
        {
            get
            {
                if (!decimal.TryParse(CalculatedPointsPerLevel, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var decPerLevel)
                    || !decimal.TryParse(Rating, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var decRating))
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
        internal CharacterExpenseData(string strDate, string strAmount, string strReason, bool blnRefund)
        {
            Date = strDate;
            Amount = strAmount;
            Reason = strReason;
            Refund = blnRefund;
        }

        public string Date { get; }
        public string Amount { get; }
        public string Reason { get; }
        public bool Refund { get; }

        public string DisplayDate =>
            System.DateTime.TryParse(Date, out var datValue) ? datValue.ToString("dd.MM.yyyy") : Date;
    }

    public sealed class CharacterCommlinkData
    {
        internal CharacterCommlinkData(string strGuid, string strName, int intResponse, bool blnEquipped, bool blnActive)
        {
            Guid = strGuid;
            Name = strName;
            Response = intResponse;
            Equipped = blnEquipped;
            Active = blnActive;
        }

        public string Guid { get; }
        public string Name { get; }
        public int Response { get; }
        public bool Equipped { get; }
        public bool Active { get; }
    }
}
