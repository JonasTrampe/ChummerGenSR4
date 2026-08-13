using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Chummer.Tests")]

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
                // Match legacy save formatting (tab indent, UTF-16); CloseOutput=false since
                // callers read the stream back after Save() returns.
                var objSettings = new XmlWriterSettings
                {
                    Encoding = Encoding.Unicode,
                    Indent = true,
                    IndentChars = "\t",
                    CloseOutput = false
                };
                using (var objWriter = XmlWriter.Create(objStream, objSettings))
                {
                    objCharacter.Document.Save(objWriter);
                }

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

        public string Metavariant => GetValue("/character/metavariant", string.Empty);

        public string MetatypeCategory => GetValue("/character/metatypecategory", string.Empty);

        public bool IsCritter => GetValue("/character/critter", "False") == "True";

        public bool Adept => GetValue("/character/adept", "False") == "True";

        public bool Magician => GetValue("/character/magician", "False") == "True";

        public bool MysticAdept => Adept && Magician;

        public bool Awakened => Adept || Magician;

        public int MysticAdeptAdeptMagSplit =>
            int.TryParse(GetValue("/character/magsplitadept", "0"), out var i) ? i : 0;

        public int MysticAdeptMagicianMagSplit =>
            int.TryParse(GetValue("/character/magsplitmagician", "0"), out var i) ? i : 0;

        /// <summary>Ported from frmCreate.cs's nudMysticAdeptMAGMagician_ValueChanged: sets how
        /// many points of a Mystic Adept's MAG rating go to spellcasting, with the remainder
        /// (down to 0) going to Adept Powers.</summary>
        public bool SetMysticAdeptMagicianMagSplit(int intMagicianPoints)
        {
            if (!MysticAdept)
                return false;

            int intMag = GetAttributeInt("MAG");
            int intMagician = Math.Clamp(intMagicianPoints, 0, intMag);
            SetChildValue(Document.DocumentElement!, "magsplitmagician", intMagician.ToString());
            SetChildValue(Document.DocumentElement!, "magsplitadept", (intMag - intMagician).ToString());
            Changed?.Invoke();
            return true;
        }

        public bool Technomancer => GetValue("/character/technomancer", "False") == "True";

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

        private static string GetAttributeLabel(string strCode) => strCode switch
        {
            "BOD" => "Konstitution",
            "AGI" => "Geschicklichkeit",
            "REA" => "Reaktion",
            "STR" => "Stärke",
            "CHA" => "Charisma",
            "INT" => "Intuition",
            "LOG" => "Logik",
            "WIL" => "Willenskraft",
            "MAG" => "Magie",
            "RES" => "Resonanz",
            "EDG" => "Edge",
            _ => strCode,
        };

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

        /// <summary>Sprites use their metatype's fixed Initiative minimum for Matrix Initiative,
        /// rather than INT/Response or a living persona. The resolved minimum is persisted in a
        /// character save's INI attribute, just as the legacy character model consumes it.</summary>
        public bool IsSprite => Metatype.EndsWith("Sprite", StringComparison.Ordinal);

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
                "//gear[category = 'Commlink' and equipped = 'True' and active = 'True']/response");
            return objNodes is { Count: > 0 } && int.TryParse(objNodes[0]!.InnerText, out var intResponse)
                ? intResponse
                : 0;
        }

        public void SetActiveCommlink(string strGuid)
        {
            XmlNodeList? objNodes = Document.SelectNodes("//gear[category = 'Commlink']");
            if (objNodes == null)
                return;

            foreach (XmlNode objNode in objNodes)
            {
                string strNodeGuid = GetValue(objNode, "guid", string.Empty);
                SetChildValue(objNode, "active", strNodeGuid == strGuid ? "True" : "False");
            }
        }

        public string Karma
        {
            get => GetValue("/character/karma", "0");
            set => SetRootValue("karma", value);
        }

        /// <summary>False during character creation, true once the character has entered career mode.</summary>
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
        public bool FinalizeCreation()
        {
            if (Created)
                return false;

            SetRootValue("created", "True");
            return true;
        }

        /// <summary>"Karma" or "BP" - which build method this character was created with.</summary>
        public string BuildMethod => GetValue("/character/buildmethod", "Karma");

        /// <summary>Remaining Build Points, only meaningful when <see cref="BuildMethod"/> is "BP".</summary>
        public string Bp
        {
            get => GetValue("/character/bp", "0");
            set => SetRootValue("bp", value);
        }

        /// <summary>The starting BP/Karma total the character was created with (unlike <see
        /// cref="Bp"/>/<see cref="Karma"/>, which shrink as points are spent) - only set for
        /// characters created through <see cref="NewCharacterFactory"/>; "0" for older save files.
        /// Used by <see cref="RaiseAttributeCreate"/>'s AllowExceedAttributeBp gate.</summary>
        public int StartingBuildPoints => int.TryParse(GetValue("/character/startingbuildpoints", "0"), out var i) ? i : 0;

        public string Nuyen
        {
            get => GetValue("/character/nuyen", "0");
            set => SetRootValue("nuyen", value);
        }

        /// <summary>Number of points sunk into starting Nuyen during creation (each costs 1 Karma
        /// or 1 BP, depending on <see cref="BuildMethod"/>, and grants a build-specific Nuyen payout).</summary>
        public int NuyenPoints => int.TryParse(GetValue("/character/nuyenbp", "0"), out var v) ? v : 0;

        /// <summary>Maximum number of points that may be sunk into starting Nuyen during creation -
        /// ordinarily the persisted priority-table value, but with the UnrestrictedNuyen house
        /// rule on, ported from clsCharacter.cs's NuyenMaximumBP: the character's whole starting
        /// point/Karma budget instead (falling back to 1000 for saves with no recorded starting
        /// total, same as legacy).</summary>
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
        public int NuyenPerPoint
        {
            get
            {
                var objOptions = GetCharacterOptions();
                return string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase)
                    ? objOptions.KarmaNuyenPer
                    : objOptions.NuyenPerBp;
            }
        }

        /// <summary>Create mode: spends one point (1 Karma or 1 BP) on starting Nuyen.</summary>
        public bool RaiseNuyenCreate()
        {
            if (NuyenPoints >= NuyenPointsMax)
                return false;

            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            if (blnKarmaBuild)
            {
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intKarma < 1)
                    return false;
                Karma = (intKarma - 1).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intBp < 1)
                    return false;
                Bp = (intBp - 1).ToString();
            }

            SetRootValue("nuyenbp", (NuyenPoints + 1).ToString());
            int intNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
            Nuyen = (intNuyen + NuyenPerPoint).ToString();
            return true;
        }

        /// <summary>Create mode: refunds one point (1 Karma or 1 BP) previously spent on starting Nuyen.</summary>
        public bool LowerNuyenCreate()
        {
            if (NuyenPoints <= 0)
                return false;

            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            if (blnKarmaBuild)
            {
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + 1).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + 1).ToString();
            }

            SetRootValue("nuyenbp", (NuyenPoints - 1).ToString());
            int intNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
            Nuyen = (intNuyen - NuyenPerPoint).ToString();
            return true;
        }

        /// <summary>Calculated walking/running movement, including MovementPercent Improvements.</summary>
        public string WalkMovement => ComputeMovement("land", ImprovementType.MovementPercent);

        /// <summary>Calculated swim movement, including SwimPercent Improvements.</summary>
        public string SwimMovement => ComputeMovement("Swim", ImprovementType.SwimPercent);

        /// <summary>Calculated fly movement, including FlyPercent/FlySpeed Improvements.</summary>
        public string FlyMovement => ComputeFlyMovement();

        /// <summary>Current and maximum Edge. Spent Edge is stored by the legacy application as
        /// an EdgeUse Attribute improvement with a negative augmented value.</summary>
        public CharacterEdgeData Edge
        {
            get
            {
                int intMaximum = GetAttributeInt("EDG");
                int intUsed = Improvements.Where(i => i.Enabled && i.Source == ImprovementSource.EdgeUse
                    && i.Type == ImprovementType.Attribute && i.ImprovedName == "EDG")
                    .Sum(i => i.Augmented * i.Rating);
                return new CharacterEdgeData(Math.Clamp(intMaximum + intUsed, 0, intMaximum), intMaximum);
            }
        }

        public bool SpendEdge() => SetEdgeRemaining(Edge.Remaining - 1);

        public bool RegainEdge() => SetEdgeRemaining(Edge.Remaining + 1);

        /// <summary>Permanently reduces the EDG attribute's own base value by 1 - ported from
        /// frmCareer.cs's cmdBurnEdge_Click. Distinct from SpendEdge/RegainEdge, which only track
        /// how much of the current maximum is currently used up; this lowers the maximum itself and
        /// can't be undone. False if EDG is already at 0.</summary>
        public bool BurnEdge()
        {
            int intCurrent = GetAttributeInt("EDG");
            if (intCurrent <= 0)
                return false;
            bool blnOk = SetAttributeValue("EDG", intCurrent - 1);
            if (blnOk)
                Changed?.Invoke();
            return blnOk;
        }

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
        public bool AdjustStunDamage(int intDelta) => AdjustConditionDamage("stuncmfilled", ComputeStunCm().Value, intDelta);

        /// <summary>Sum of installed Cyberware's own Essence cost (excludes Bioware and Essence Holes).</summary>
        public double CyberwareEssence => SumCyberwareEssence().Cyberware;

        /// <summary>Sum of installed Bioware's own Essence cost (excludes Cyberware and Essence Holes).</summary>
        public double BiowareEssence => SumCyberwareEssence().Bioware;

        private (double Cyberware, double Bioware, double Holes) SumCyberwareEssence()
        {
            double dblCyberware = 0, dblBioware = 0, dblHoles = 0;
            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    double dblEss = double.TryParse(GetValue(objNode, "ess", "0"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var dblParsedEss) ? dblParsedEss : 0;
                    if (GetValue(objNode, "name", string.Empty) == "Essence Hole")
                        dblHoles += dblEss;
                    else if (GetValue(objNode, "improvementsource", string.Empty) == "Bioware")
                        dblBioware += dblEss;
                    else
                        dblCyberware += dblEss;
                }
            }

            return (dblCyberware, dblBioware, dblHoles);
        }

        // Ported from clsCharacter.cs's Essence property (CyborgEssence override not ported).
        private string ComputeEssence() => ComputeEssenceDecimal().Total.ToString("0.##", CultureInfo.InvariantCulture);

        private (double Base, double Total) ComputeEssenceDecimal()
        {
            double dblMax = double.TryParse(GetValue("/character/attributes/attribute[name = 'ESS']/metatypemax", "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var dblParsedMax) ? dblParsedMax : 0;
            var lstEssenceImprovements = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Essence);
            double dblBase = dblMax + lstEssenceImprovements.Sum(c => c.Value);

            var (dblCyberware, dblBioware, dblHoles) = SumCyberwareEssence();
            double dblHigher = Math.Max(dblCyberware, dblBioware);
            double dblLower = Math.Min(dblCyberware, dblBioware);
            double dblTotal = dblBase - dblHigher - (dblLower / 2) - dblHoles;

            return (dblBase, dblTotal);
        }

        /// <summary>Ported from clsCharacter.cs's EssencePenalty: whole points of Essence lost from
        /// the character's Essence maximum (metatype ESS max plus any EssenceMax Improvements),
        /// rounded up. 0 once fully healed/uninstalled back to the character's max Essence.</summary>
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
        private int ApplyEssencePenaltyToAttribute(string strCode, string strRawTotalValue)
        {
            int intRaw = int.TryParse(strRawTotalValue, out var intParsed) ? intParsed : 0;
            int intPenalty = EssencePenalty;
            if (intPenalty == 0)
                return intRaw;

            if (GetCharacterOptions().EssLossReducesMaximumOnly)
            {
                int intMax = int.TryParse(
                    GetValue($"/character/attributes/attribute[name = '{strCode}']/metatypemax", "0"), out var mx)
                    ? mx
                    : 0;
                return intRaw > intMax ? intMax : intRaw;
            }

            return Math.Max(0, intRaw - intPenalty);
        }

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

        /// <summary>Removes every &lt;improvement&gt; sharing <paramref name="strSourceName"/> whose
        /// improvementsource is "Custom" - ported from frmCareer.cs's cmdDeleteImprovement_Click,
        /// which only ever calls RemoveImprovements(ImprovementSource.Custom, ...): manually-created
        /// Improvements (frmCreateImprovement, not ported here - see FEATURE_CHECKLIST.md) are the
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

        public IReadOnlyList<CalendarWeek> Calendar => ReadCalendar();

        /// <summary>Adds a calendar week in the same save-file representation as the legacy
        /// calendar. Years are unrestricted; weeks use Shadowrun's 1-52 calendar range.</summary>
        public CalendarWeek AddCalendarWeek(int intYear, int intWeek, string strNotes = "")
        {
            if (intWeek is < 1 or > 52)
                throw new ArgumentOutOfRangeException(nameof(intWeek), "A calendar week must be between 1 and 52.");

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCalendar = objRoot.SelectSingleNode("calendar") as XmlElement;
            if (objCalendar == null)
            {
                objCalendar = Document.CreateElement("calendar");
                objRoot.AppendChild(objCalendar);
            }

            var objWeek = new CalendarWeek(intYear, intWeek) { Notes = strNotes ?? string.Empty };
            var objNode = Document.CreateElement("week");
            AppendElement(objNode, "guid", objWeek.InternalId);
            AppendElement(objNode, "year", intYear.ToString());
            AppendElement(objNode, "week", intWeek.ToString());
            AppendElement(objNode, "notes", objWeek.Notes);
            objCalendar.AppendChild(objNode);
            Changed?.Invoke();
            return objWeek;
        }

        /// <summary>Changes a saved calendar week's note text.</summary>
        public bool UpdateCalendarWeekNotes(string strInternalId, string strNotes)
        {
            XmlNode? objWeek = FindCalendarWeek(strInternalId);
            if (objWeek == null)
                return false;

            SetChildValue(objWeek, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves the complete saved calendar so its first week starts at the selected
        /// year/week, retaining the relative sequence of every following entry.</summary>
        public bool ChangeCalendarStart(int intYear, int intWeek)
        {
            if (intWeek is < 1 or > 52)
                throw new ArgumentOutOfRangeException(nameof(intWeek), "A calendar week must be between 1 and 52.");

            XmlNodeList? objWeeks = Document.SelectNodes("/character/calendar/week");
            if (objWeeks is not { Count: > 0 })
                return false;

            XmlNode objFirst = objWeeks[0]!;
            int intOldYear = int.TryParse(GetValue(objFirst, "year", "0"), out var intParsedYear) ? intParsedYear : intYear;
            int intOldWeek = int.TryParse(GetValue(objFirst, "week", "1"), out var intParsedWeek) ? intParsedWeek : intWeek;
            int intWeekOffset = (intYear - intOldYear) * 52 + intWeek - intOldWeek;

            foreach (XmlNode objWeek in objWeeks)
            {
                int intCurrentYear = int.TryParse(GetValue(objWeek, "year", "0"), out var intParsedCurrentYear) ? intParsedCurrentYear : intOldYear;
                int intCurrentWeek = int.TryParse(GetValue(objWeek, "week", "1"), out var intParsedCurrentWeek) ? intParsedCurrentWeek : intOldWeek;
                int intAbsoluteWeek = intCurrentYear * 52 + (intCurrentWeek - 1) + intWeekOffset;
                int intNewYear = Math.DivRem(intAbsoluteWeek, 52, out int intNewWeekIndex);
                if (intNewWeekIndex < 0) { intNewYear--; intNewWeekIndex += 52; }
                SetChildValue(objWeek, "year", intNewYear.ToString());
                SetChildValue(objWeek, "week", (intNewWeekIndex + 1).ToString());
            }

            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<CharacterQualityData> Qualities => ReadQualities();

        /// <summary>
        /// Adds a quality using the character-file representation used by the legacy application.
        /// The rules definition stays in <c>qualities.xml</c>; a character save only records the
        /// chosen name, optional selection detail, and positive/negative category. Because this
        /// mutates the backing document, <see cref="CharacterFileService.Save"/> persists it.
        /// </summary>
        /// <summary>Whether adding this Quality prompts for a free-text detail - ported from
        /// clsImprovement.cs's selecttext bonus node (e.g. Allergy's substance, Prejudiced's
        /// target). The UI should collect this from the player and pass it as <see
        /// cref="AddQuality"/>'s <paramref name="strName"/>-matching <c>strExtra</c>.</summary>
        public bool QualityRequiresTextSelection(string strName) =>
            FindBonusChild("qualities.xml", "qualities", "quality", strName, "selecttext") != null;

        /// <summary>Whether adding this Quality prompts for a Mentor Spirit (or Paragon, for
        /// Technomancers) pick - ported from clsImprovement.cs's selectmentorspirit/selectparagon
        /// bonus nodes (Quality "Mentor Spirit"/"The Beast's Way"). Returns the rules-data file to
        /// pick from ("mentors.xml"/"paragons.xml"), or null if this Quality doesn't need one.</summary>
        public string? QualityMentorSpiritDataFile(string strName)
        {
            XmlNode? objBonus = FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectmentorspirit");
            if (objBonus != null)
                return "mentors.xml";
            objBonus = FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectparagon");
            return objBonus != null ? "paragons.xml" : null;
        }

        /// <summary>Same as <see cref="QualityRequiresTextSelection"/> but for a Critter Power's
        /// &lt;selecttext&gt; bonus (e.g. Elemental Attack).</summary>
        public bool CritterPowerRequiresTextSelection(string strName) =>
            FindBonusChild("critterpowers.xml", "powers", "power", strName, "selecttext") != null;

        /// <summary>Same as <see cref="QualityRequiresTextSelection"/> but for a Complex Form's
        /// &lt;selecttext&gt; bonus (e.g. Knowsoft, Linguasoft).</summary>
        public bool ComplexFormRequiresTextSelection(string strName) =>
            FindBonusChild("programs.xml", "programs", "program", strName, "selecttext") != null;

        /// <summary>Same as <see cref="GetQualitySkillSelectionOptions"/> but for a Critter
        /// Power's &lt;selectskill&gt; bonus.</summary>
        public IReadOnlyList<string> GetCritterPowerSkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("critterpowers.xml", "powers", "power", strName, "selectskill"));

        /// <summary>Same as <see cref="GetQualityAttributeSelectionOptions"/> but for a Critter
        /// Power's &lt;selectattribute&gt; bonus.</summary>
        public IReadOnlyList<string> GetCritterPowerAttributeSelectionOptions(string strName) =>
            ExtractAttributeSelectionOptions(FindBonusChild("critterpowers.xml", "powers", "power", strName, "selectattribute"));

        /// <summary>Same as <see cref="GetQualitySkillSelectionOptions"/> but for a Complex
        /// Form's &lt;selectskill&gt; bonus (e.g. Activesoft).</summary>
        public IReadOnlyList<string> GetComplexFormSkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("programs.xml", "programs", "program", strName, "selectskill"));

        /// <summary>Options to offer the player when this Quality's &lt;bonus&gt; is (or
        /// includes) a &lt;selectskill&gt; node - ported from clsImprovement.cs's selectskill
        /// handler's skillgroup/skillcategory/excludecategory filtering. Empty if not applicable.</summary>
        public IReadOnlyList<string> GetQualitySkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectskill"));

        /// <summary>Same as <see cref="GetQualitySkillSelectionOptions"/> but for an Adept
        /// Power's &lt;selectskill&gt; bonus (e.g. Improved Ability).</summary>
        public IReadOnlyList<string> GetAdeptPowerSkillSelectionOptions(string strName) =>
            ExtractSkillSelectionOptions(FindBonusChild("powers.xml", "powers", "power", strName, "selectskill"));

        /// <summary>Options to offer the player when this Quality's &lt;bonus&gt; is (or
        /// includes) a &lt;selectattribute&gt; node - ported from clsImprovement.cs's
        /// selectattribute handler's attribute/excludeattribute filtering (plus MAG/RES only
        /// being offered when the character actually has them). Empty if not applicable.</summary>
        public IReadOnlyList<string> GetQualityAttributeSelectionOptions(string strName) =>
            ExtractAttributeSelectionOptions(FindBonusChild("qualities.xml", "qualities", "quality", strName, "selectattribute"));

        /// <summary>Same as <see cref="GetQualityAttributeSelectionOptions"/> but for an Adept
        /// Power's &lt;selectattribute&gt; bonus (e.g. Improved Physical Attribute).</summary>
        public IReadOnlyList<string> GetAdeptPowerAttributeSelectionOptions(string strName) =>
            ExtractAttributeSelectionOptions(FindBonusChild("powers.xml", "powers", "power", strName, "selectattribute"));

        private static XmlNode? FindBonusChild(string strDataFile, string strContainerTag, string strItemTag,
            string strName, string strChildTag)
        {
            XmlDocument objDoc = XmlManager.Instance.Load(strDataFile);
            XmlNode? objXmlItem = objDoc.SelectSingleNode(
                $"/chummer/{strContainerTag}/{strItemTag}[name = '{strName.Trim()}']");
            return objXmlItem?.SelectSingleNode($"bonus/{strChildTag}");
        }

        private IReadOnlyList<string> ExtractSkillSelectionOptions(XmlNode? objNode)
        {
            if (objNode == null)
                return Array.Empty<string>();

            IEnumerable<CharacterSkillData> query = Skills.Where(s => !s.KnowledgeSkill);

            string? strSkillGroup = objNode.Attributes?["skillgroup"]?.InnerText;
            if (!string.IsNullOrEmpty(strSkillGroup))
                query = query.Where(s => s.SkillGroup == strSkillGroup);

            string? strCategory = objNode.Attributes?["skillcategory"]?.InnerText;
            if (!string.IsNullOrEmpty(strCategory))
                query = query.Where(s => s.Category == strCategory);

            string? strExcludeCategory = objNode.Attributes?["excludecategory"]?.InnerText;
            if (!string.IsNullOrEmpty(strExcludeCategory))
            {
                var setExcluded = new HashSet<string>(strExcludeCategory.Split(','), StringComparer.Ordinal);
                query = query.Where(s => !setExcluded.Contains(s.Category));
            }

            // Ported from clsImprovement.cs's selectskill handler: Exotic Skills (Exotic Melee/
            // Ranged Weapon, Pilot Exotic Vehicle) all share the same bare Name but are only
            // actually distinguished by their Specialization (e.g. "Exotic Ranged Weapon (Bow)"
            // vs. "...(Grenade Launcher)"), so a character can own several. Using the bare Name
            // as the selectable/stored value would silently collapse them into one ambiguous
            // option - offer/store the full "Name (Specialization)" form for those instead.
            return query.Select(s => s.Exotic ? s.Name + " (" + s.Specialization + ")" : s.Name)
                .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        private IReadOnlyList<string> ExtractAttributeSelectionOptions(XmlNode? objNode)
        {
            if (objNode == null)
                return Array.Empty<string>();

            var lstAll = new List<string> { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" };
            if (Magician) lstAll.Add("MAG");
            if (Technomancer) lstAll.Add("RES");

            var lstInclude = objNode.SelectNodes("attribute")?.Cast<XmlNode>().Select(n => n.InnerText).ToList();
            if (lstInclude is { Count: > 0 })
                lstAll = lstAll.Where(lstInclude.Contains).ToList();

            var setExclude = objNode.SelectNodes("excludeattribute")?.Cast<XmlNode>()
                .Select(n => n.InnerText).ToHashSet();
            if (setExclude is { Count: > 0 })
                lstAll = lstAll.Where(a => !setExclude.Contains(a)).ToList();

            return lstAll;
        }

        /// <summary>Ported from frmSelectQuality.cs's cmdOK_Click/clsQuality.Create: also applies
        /// the quality's own rules-data &lt;bonus&gt; block (see <see cref="ApplyBonus"/>), matching
        /// legacy's CreateImprovements call on add. When the bonus is (or includes) a
        /// &lt;selecttext&gt;/&lt;selectskill&gt;/&lt;selectattribute&gt; node (see <see
        /// cref="QualityRequiresTextSelection"/>/<see cref="GetQualitySkillSelectionOptions"/>/
        /// <see cref="GetQualityAttributeSelectionOptions"/>), <paramref name="strExtra"/> becomes
        /// the corresponding Improvement - ported from clsImprovement.cs's
        /// selecttext/selectskill/selectattribute handlers. No real qualities.xml Quality combines
        /// more than one of these, so a single strExtra value is unambiguous.</summary>
        public void AddQuality(string strName, string strType, string strExtra = "",
            string strMentorSpirit = "", string strMentorChoice1 = "", string strMentorChoice2 = "")
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
            if (!string.IsNullOrWhiteSpace(strMentorSpirit))
            {
                AppendElement(objQuality, "mentorspirit", strMentorSpirit.Trim());
                AppendElement(objQuality, "mentorchoice1", strMentorChoice1.Trim());
                AppendElement(objQuality, "mentorchoice2", strMentorChoice2.Trim());
            }
            objQualities.AppendChild(objQuality);

            XmlDocument objQualitiesDoc = XmlManager.Instance.Load("qualities.xml");
            XmlNode? objXmlQuality = objQualitiesDoc.SelectSingleNode(
                $"/chummer/qualities/quality[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlQuality?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.Quality, strName.Trim());
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.Quality, strName.Trim(), strExtra, "1");

            string? strMentorDataFile = QualityMentorSpiritDataFile(strName);
            if (strMentorDataFile != null && !string.IsNullOrWhiteSpace(strMentorSpirit))
            {
                XmlDocument objMentorsDoc = XmlManager.Instance.Load(strMentorDataFile);
                XmlNode? objXmlMentor = objMentorsDoc.SelectSingleNode(
                    $"/chummer/mentors/mentor[name = '{strMentorSpirit.Trim()}']");
                ApplyBonus(objXmlMentor?.SelectSingleNode("bonus"), ImprovementSource.Quality, strName.Trim());

                if (!string.IsNullOrWhiteSpace(strMentorChoice1))
                    ApplyBonus(objXmlMentor?.SelectSingleNode(
                        $"choices/choice[name = '{strMentorChoice1.Trim()}']/bonus"),
                        ImprovementSource.Quality, strName.Trim());
                if (!string.IsNullOrWhiteSpace(strMentorChoice2))
                    ApplyBonus(objXmlMentor?.SelectSingleNode(
                        $"choices/choice[name = '{strMentorChoice2.Trim()}']/bonus"),
                        ImprovementSource.Quality, strName.Trim());
            }
        }

        /// <summary>Shared selecttext/selectskill/selectattribute application, ported from
        /// clsImprovement.cs's respective handlers - used by both <see cref="AddQuality"/> and
        /// <see cref="AddAdeptPower"/>. <paramref name="strRating"/> resolves any "Rating"
        /// reference in the selected node's own val/max/aug formulas (e.g. Improved Ability's
        /// "Rating"-scaled skill bonus) - Qualities always pass "1" since they have no Rating.</summary>
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
                AppendImprovement(new ImprovementSpec(ImprovementType.Attribute, strSelected.Trim(),
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
                RemoveBonusImprovements(ImprovementSource.Quality, strName);
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

        /// <summary>Adds a root-level gear item in the minimal saved-character tree shape. Deducts its
        /// cost (evaluated at the given rating, times quantity) from Nuyen, matching the legacy
        /// "buying gear costs money" rule. Returns false (adding nothing) if this is "Ammo:
        /// Stick-n-Shock" and <see cref="StickNShockAllowed"/> rejects it.</summary>
        public bool AddGear(string strName, string strCategory, string strRating = "0", string strQty = "1",
            string strCost = "", string strAvail = "", string strSource = "", string strPage = "",
            string strCapacity = "", string strResponse = "", string strSignal = "", string strSystemRating = "",
            string strFirewall = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A gear name is required.", nameof(strName));
            if (!StickNShockAllowed(strName))
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objGears = objRoot.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objRoot.AppendChild(objGears);
            }

            AppendGearNode(objGears, strName, strCategory, strRating, strQty, strCost, strAvail, strSource, strPage,
                strCapacity, strResponse, strSignal, strSystemRating, strFirewall);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Builds a custom Nexus (a build-your-own Matrix node, UN p.50) and adds it as a
        /// single root-level Gear item - ported from frmSelectNexus.cs's CalculateNexus. This
        /// port's Gear already carries direct Response/Signal/System/Firewall fields (unlike
        /// legacy, which assembles five separate child Gear items per attribute), so those four
        /// values are written straight onto the one Nexus Gear node; the Persona Limit has no
        /// equivalent field and is folded into the item's own name instead, matching how legacy's
        /// own top-level Nexus Gear name embeds the Processor rating. Cost/Avail formulas are
        /// copied verbatim per-tier, including legacy's Response-cost bug: Response 7-10 always
        /// costs 0 nuyen because CalculateNexus multiplies its own not-yet-assigned (still 0)
        /// running total instead of the rating - faithfully reproduced rather than fixed, since a
        /// real character built to match a legacy save must land on the exact same numbers.
        /// <paramref name="blnFree"/> matches the "Free!" checkbox (career mode only in legacy;
        /// here it's just an optional override).</summary>
        public bool AddNexus(int intProcessor, int intResponse, int intSystem, int intFirewall, int intSignal,
            int intPersona, bool blnFree = false)
        {
            // Legacy also computes a per-attribute Availability string for display (e.g.
            // Response's is (Response*4), +"F" past tier 2) - purely informational there (the
            // assembled Nexus Gear's own Avail is always "0"), so not reproduced here.
            int intResponseCost;
            if (intResponse <= 3)
                intResponseCost = intResponse * intProcessor * 50;
            else if (intResponse <= 6)
                intResponseCost = intResponse * intProcessor * 100;
            else
                intResponseCost = 0; // legacy bug: multiplies its own still-zero running total.

            int intSystemCost;
            if (intSystem <= 3)
                intSystemCost = intSystem * intPersona * 25;
            else if (intSystem <= 6)
                intSystemCost = intSystem * intPersona * 50;
            else
                intSystemCost = intSystem * intPersona * 300;

            int intFirewallCost;
            if (intFirewall <= 3)
                intFirewallCost = intFirewall * intProcessor * 25;
            else if (intFirewall <= 6)
                intFirewallCost = intFirewall * intProcessor * 50;
            else
                intFirewallCost = intFirewall * intProcessor * 250;

            int intSignalCost = intSignal switch
            {
                2 => 50,
                3 => 150,
                4 => 500,
                5 => 1000,
                6 => 3000,
                7 => 6500,
                8 => 8750,
                9 => 12250,
                10 => 17250,
                _ => 10,
            };

            int intCost = blnFree ? 0 : intResponseCost + intSystemCost + intFirewallCost + intSignalCost;

            string strName = $"Nexus (Processor {intProcessor})";
            return AddGear(strName, "Nexus", strCost: intCost.ToString(CultureInfo.InvariantCulture), strAvail: "0",
                strSource: "UN", strPage: "50", strResponse: intResponse.ToString(CultureInfo.InvariantCulture),
                strSignal: intSignal.ToString(CultureInfo.InvariantCulture),
                strSystemRating: intSystem.ToString(CultureInfo.InvariantCulture),
                strFirewall: intFirewall.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Ported from frmCareer.cs/frmCreate.cs's Stick-n-Shock weapon-category
        /// restriction checks (e.g. frmCareer.cs:24378), simplified at purchase time: legacy blocks
        /// loading Stick-n-Shock ammo into a specific excluded-category weapon; this instead blocks
        /// acquiring the ammo at all when the RestrictStickNShock house rule is on and the
        /// character owns no weapon outside the excluded categories to use it with. The actual
        /// loaded-into-a-specific-weapon concept exists now too (see ReloadWeapon/
        /// GetWeaponAmmoOptions, which does exclude Stick-n-Shock per-weapon there), but this
        /// broader "can they own it at all" gate is still checked at AddGear time.</summary>
        private bool StickNShockAllowed(string strName)
        {
            if (!string.Equals(strName, "Ammo: Stick-n-Shock", StringComparison.Ordinal))
                return true;

            var objOptions = GetCharacterOptions();
            if (!objOptions.RestrictStickNShock)
                return true;

            var setExcluded = objOptions.StickNShockExcludedWeaponCategories;
            return Weapons.Any(w => !setExcluded.Contains(w.Category));
        }

        /// <summary>Adds gear nested under an existing gear item (e.g. a Certified Credstick under
        /// a Commlink) - <paramref name="intParentGearId"/> is a <see cref="Gear"/> node's GearId,
        /// assigned in the same depth-first order the tree is displayed in. Also deducts its cost
        /// from Nuyen.</summary>
        public bool AddChildGear(int intParentGearId, string strName, string strCategory, string strRating = "0",
            string strQty = "1", string strCost = "", string strAvail = "", string strSource = "", string strPage = "",
            string strCapacity = "", string strResponse = "", string strSignal = "", string strSystemRating = "",
            string strFirewall = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A gear name is required.", nameof(strName));

            XmlNode? objParent = GetGearNodeById(intParentGearId);
            if (objParent == null)
                return false;

            if (GetCharacterOptions().EnforceCapacity
                && !GearCapacityAllowsChild(objParent, strCapacity, strQty))
                return false;

            var objChildren = objParent.SelectSingleNode("children") as XmlElement;
            if (objChildren == null)
            {
                objChildren = Document.CreateElement("children");
                objParent.AppendChild(objChildren);
            }

            AppendGearNode(objChildren, strName, strCategory, strRating, strQty, strCost, strAvail, strSource, strPage,
                strCapacity, strResponse, strSignal, strSystemRating, strFirewall);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from clsEquipment.cs's Gear.CapacityRemaining, honoring the
        /// EnforceCapacity house rule setting - same simplified capacity model as
        /// CharacterTreeItemData.CapacityRemaining (own capacity minus the sum of children's own
        /// capacity, no bracketed "[x]" capacity handling). Existing children with a non-numeric
        /// (bracketed) Capacity are treated as consuming 0, matching that same simplification.</summary>
        private static bool GearCapacityAllowsChild(XmlNode objParent, string strChildCapacity, string strChildQty)
        {
            double dblOwn = double.TryParse(GetValue(objParent, "capacity", string.Empty), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dOwn) ? dOwn : 0;
            var objExistingChildren = objParent.SelectNodes("children/gear");
            double dblUsed = 0;
            if (objExistingChildren != null)
                foreach (XmlNode objChild in objExistingChildren)
                    if (double.TryParse(GetValue(objChild, "capacity", string.Empty), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var dUsed))
                        dblUsed += dUsed;

            double dblNewCapacity = double.TryParse(strChildCapacity, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dNew) ? dNew : 0;
            int intQty = int.TryParse(strChildQty, out var q) ? q : 1;

            return dblUsed + dblNewCapacity * intQty <= dblOwn;
        }

        private void DeductGearCost(string strCost, string strRating, string strQty, string strAvail = "")
        {
            int intQty = int.TryParse(strQty, out var q) ? q : 1;
            double dblCost = RatingExpression.Evaluate(strCost, strRating) * intQty;
            dblCost = ApplyRestrictedForbiddenCostMultiplier(dblCost, strAvail);
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
            Nuyen = (dblNuyen - dblCost).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Ported from frmCareer.cs's per-item purchase handlers (e.g. tsGearAdd_Click):
        /// when the MultiplyRestrictedCost/MultiplyForbiddenCost house rules are on, an item whose
        /// raw Availability string ends in "R"/"F" has its cost multiplied by the matching
        /// RestrictedCostMultiplier/ForbiddenCostMultiplier.</summary>
        private double ApplyRestrictedForbiddenCostMultiplier(double dblCost, string strAvail)
        {
            if (string.IsNullOrEmpty(strAvail))
                return dblCost;

            CharacterOptions objOptions = GetCharacterOptions();
            char chLast = strAvail[strAvail.Length - 1];
            if (chLast == 'R' && objOptions.MultiplyRestrictedCost)
                return dblCost * objOptions.RestrictedCostMultiplier;
            if (chLast == 'F' && objOptions.MultiplyForbiddenCost)
                return dblCost * objOptions.ForbiddenCostMultiplier;
            return dblCost;
        }

        private void AppendGearNode(XmlNode objParentList, string strName, string strCategory, string strRating,
            string strQty, string strCost, string strAvail, string strSource, string strPage, string strCapacity,
            string strResponse, string strSignal, string strSystemRating, string strFirewall)
        {
            var objGear = Document.CreateElement("gear");
            AppendElement(objGear, "name", strName.Trim());
            AppendElement(objGear, "category", strCategory);
            AppendElement(objGear, "rating", strRating);
            AppendElement(objGear, "qty", strQty);
            AppendElement(objGear, "cost", strCost);
            AppendElement(objGear, "avail", strAvail);
            AppendElement(objGear, "capacity", strCapacity);
            AppendElement(objGear, "source", strSource);
            AppendElement(objGear, "page", strPage);
            AppendElement(objGear, "equipped", "False");
            // <guid>/<active> are what CharacterDocument.Commlinks matches Commlink-category gear
            // by - always written (harmless for non-Commlink gear) so a newly bought Commlink is
            // immediately recognized by the existing Commlink dropdown/MatrixInitiative calc.
            AppendElement(objGear, "guid", Guid.NewGuid().ToString());
            AppendElement(objGear, "active", "False");
            // <location> is only meaningful for direct onboard vehicle gear (see
            // AssignVehicleGearLocation) - always written (harmless elsewhere) to match the
            // guid/active always-write pattern above.
            AppendElement(objGear, "location", string.Empty);
            if (!string.IsNullOrEmpty(strResponse)) AppendElement(objGear, "response", strResponse);
            if (!string.IsNullOrEmpty(strSignal)) AppendElement(objGear, "signal", strSignal);
            if (!string.IsNullOrEmpty(strSystemRating)) AppendElement(objGear, "system", strSystemRating);
            if (!string.IsNullOrEmpty(strFirewall)) AppendElement(objGear, "firewall", strFirewall);
            objGear.AppendChild(Document.CreateElement("children"));
            objParentList.AppendChild(objGear);
        }

        /// <summary>Removes the gear matching this <see cref="Gear"/> tree's GearId, wherever it
        /// is nested (root-level or as another item's child).</summary>
        public bool RemoveGear(int intGearId)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear?.ParentNode == null)
                return false;

            objGear.ParentNode.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from frmSellItem.cs/frmCareer.cs's per-item "tsXxxSell_Click" handlers:
        /// refunds <paramref name="dblSellPercent"/> (0.0-1.0) of the item's current total cost
        /// back to Nuyen (logged as a Nuyen expense entry) before removing it. Refunds round to the
        /// nearest whole Nuyen, same as legacy's Convert.ToInt32.</summary>
        public bool SellGear(int intGearId, double dblSellPercent)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            int intRefund = ComputeSellRefund(ReadTreeItem(objGear).CalculatedCost, dblSellPercent);
            string strName = GetValue(objGear, "name", string.Empty);
            if (!RemoveGear(intGearId))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        private static int ComputeSellRefund(double dblCost, double dblSellPercent) =>
            (int)Math.Round(dblCost * dblSellPercent, MidpointRounding.AwayFromZero);

        private void ApplySellRefund(int intRefund, string strItemName)
        {
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
            Nuyen = (dblNuyen + intRefund).ToString(CultureInfo.InvariantCulture);
            if (intRefund != 0)
                AddExpense("Nuyen", intRefund, "Verkauft: " + strItemName);
        }

        /// <summary>Equips or unequips a gear item - matches the legacy tree's "angelegt" checkbox.</summary>
        public bool SetGearEquipped(int intGearId, bool blnEquipped)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            SetChildValue(objGear, "equipped", blnEquipped ? "True" : "False");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sets a gear item's quantity directly - matches the legacy tree's editable
        /// quantity spinner.</summary>
        public bool SetGearQuantity(int intGearId, string strQty)
        {
            XmlNode? objGear = GetGearNodeById(intGearId);
            if (objGear == null)
                return false;

            SetChildValue(objGear, "qty", strQty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves a gear item within the &lt;gears&gt; tree - either reordering it among its
        /// current siblings (inserted immediately before <paramref name="intTargetGearId"/>) or, with
        /// <paramref name="blnReparent"/>, making it a child of the target instead. GearIds are
        /// depth-first positions recomputed on every read (see <see cref="GetGearNodeById"/>), so
        /// callers must reload the tree after a successful move before issuing another one.</summary>
        public bool MoveGear(int intSourceGearId, int intTargetGearId, bool blnReparent)
        {
            XmlNode? objSource = GetGearNodeById(intSourceGearId);
            XmlNode? objTarget = GetGearNodeById(intTargetGearId);
            if (objSource == null || objTarget == null || objSource == objTarget || objSource.ParentNode == null)
                return false;

            // Refuse to move an item into its own subtree - that would either orphan the branch or
            // (for reparent) create a cycle.
            for (XmlNode? objCursor = objTarget; objCursor != null; objCursor = objCursor.ParentNode)
                if (objCursor == objSource)
                    return false;

            objSource.ParentNode.RemoveChild(objSource);

            if (blnReparent)
            {
                XmlNode? objChildren = objTarget.SelectSingleNode("children");
                if (objChildren == null)
                {
                    objChildren = Document.CreateElement("children");
                    objTarget.AppendChild(objChildren);
                }
                objChildren.AppendChild(objSource);
            }
            else
            {
                if (objTarget.ParentNode == null)
                    return false;
                objTarget.ParentNode.InsertBefore(objSource, objTarget);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a vehicle in the same persisted shape as the legacy Vehicle.Save method.
        /// The purchase price is deducted from Nuyen just like root-level gear.</summary>
        public void AddVehicle(string strName, string strCategory, string strHandling, string strAcceleration,
            string strSpeed, string strPilot, string strBody, string strArmor, string strSensor,
            string strDeviceRating, string strAvail, string strCost, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objVehicles = objRoot.SelectSingleNode("vehicles");
            if (objVehicles == null)
            {
                objVehicles = Document.CreateElement("vehicles");
                objRoot.AppendChild(objVehicles);
            }

            var objVehicle = Document.CreateElement("vehicle");
            AppendElement(objVehicle, "guid", Guid.NewGuid().ToString());
            AppendElement(objVehicle, "name", strName.Trim());
            AppendElement(objVehicle, "category", strCategory);
            AppendElement(objVehicle, "handling", strHandling);
            AppendElement(objVehicle, "accel", strAcceleration);
            AppendElement(objVehicle, "speed", strSpeed);
            AppendElement(objVehicle, "pilot", strPilot);
            AppendElement(objVehicle, "body", strBody);
            AppendElement(objVehicle, "armor", strArmor);
            AppendElement(objVehicle, "sensor", strSensor);
            AppendElement(objVehicle, "devicerating", strDeviceRating);
            AppendElement(objVehicle, "avail", strAvail);
            AppendElement(objVehicle, "cost", strCost);
            AppendElement(objVehicle, "addslots", "0");
            AppendElement(objVehicle, "source", strSource);
            AppendElement(objVehicle, "page", strPage);
            AppendElement(objVehicle, "physicalcmfilled", "0");
            AppendElement(objVehicle, "vehiclename", string.Empty);
            AppendElement(objVehicle, "homenode", "False");
            objVehicle.AppendChild(Document.CreateElement("mods"));
            objVehicle.AppendChild(Document.CreateElement("gears"));
            objVehicle.AppendChild(Document.CreateElement("weapons"));
            AppendElement(objVehicle, "notes", string.Empty);
            AppendElement(objVehicle, "discountedcost", "False");
            objVehicles.AppendChild(objVehicle);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
        }

        /// <summary>Removes one root-level vehicle identified by its saved name and category.</summary>
        public bool RemoveVehicle(string strName, string strCategory)
        {
            XmlNode? objVehicle = null;
            XmlNodeList? objVehicles = Document.SelectNodes("/character/vehicles/vehicle");
            if (objVehicles != null)
            {
                foreach (XmlNode objCandidate in objVehicles)
                {
                    if (GetValue(objCandidate, "name", string.Empty) != strName
                        || GetValue(objCandidate, "category", string.Empty) != strCategory)
                        continue;
                    objVehicle = objCandidate;
                    break;
                }
            }
            if (objVehicle?.ParentNode == null)
                return false;

            objVehicle.ParentNode.RemoveChild(objVehicle);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes one root vehicle by its persisted GUID. New UI code should prefer this
        /// overload because a character may own more than one vehicle with the same name.</summary>
        public bool RemoveVehicle(Guid guiVehicleId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle?.ParentNode == null)
                return false;

            objVehicle.ParentNode.RemoveChild(objVehicle);
            Changed?.Invoke();
            return true;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root Vehicle
        /// (including its own installed mods' cost; onboard Weapons/Gear aren't costed here, same
        /// simplification <see cref="CalculatedCost"/>'s tree reading already makes elsewhere).</summary>
        public bool SellVehicle(Guid guiVehicleId, double dblSellPercent)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            int intRefund = ComputeSellRefund(ReadTreeItem(objVehicle, "mods/mod").CalculatedCost, dblSellPercent);
            string strName = GetValue(objVehicle, "name", string.Empty);
            if (!RemoveVehicle(guiVehicleId))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        /// <summary>Depth-first (parent before children, root order preserved) walk of the whole
        /// &lt;gears&gt; tree - the same order <see cref="Gear"/> assigns GearIds in, so an ID
        /// found in the UI tree always resolves back to the same node here.</summary>
        private IEnumerable<XmlNode> EnumerateGearNodesDfs()
        {
            var objNodes = Document.SelectNodes("/character/gears/gear");
            if (objNodes == null) yield break;

            foreach (XmlNode objNode in objNodes)
            {
                foreach (XmlNode objDescendant in EnumerateGearNodeAndChildren(objNode))
                    yield return objDescendant;
            }
        }

        private IEnumerable<XmlNode> EnumerateGearNodeAndChildren(XmlNode objNode)
        {
            yield return objNode;
            var objChildren = objNode.SelectNodes("children/gear");
            if (objChildren == null) yield break;

            foreach (XmlNode objChild in objChildren)
            {
                foreach (XmlNode objDescendant in EnumerateGearNodeAndChildren(objChild))
                    yield return objDescendant;
            }
        }

        private XmlNode? GetGearNodeById(int intGearId)
        {
            int intCurrentId = 0;
            foreach (XmlNode objNode in EnumerateGearNodesDfs())
            {
                if (intCurrentId == intGearId)
                    return objNode;
                intCurrentId++;
            }

            return null;
        }

        /// <summary>Whether the AllowCyberwareEssDiscounts house rule is on - gates the Essence
        /// discount input in the Cyberware/Bioware picker (the discount itself is applied entirely
        /// client-side into the already-resolved Essence value passed to <see cref="AddCyberware"/>,
        /// same as the grade multiplier, so there's nothing else to enforce here).</summary>
        public bool AllowCyberwareEssenceDiscounts => GetCharacterOptions().AllowCyberwareEssDiscounts;

        /// <summary>Print-output house rules (frmOptions.cs's "House Rules" tab) - read by
        /// <see cref="CharacterSheetExporter"/> so sheet output honors them the same way legacy's
        /// clsCharacter.cs's PrintToStream does.</summary>
        public bool PrintSkillsWithZeroRating => GetCharacterOptions().PrintSkillsWithZeroRating;
        public bool PrintExpenses => GetCharacterOptions().PrintExpenses;
        public bool PrintLeadershipAlternates => GetCharacterOptions().PrintLeadershipAlternates;
        public bool PrintArcanaAlternates => GetCharacterOptions().PrintArcanaAlternates;
        public bool PrintNotesEnabled => GetCharacterOptions().PrintNotes;

        /// <summary>Adds a root-level Cyberware or Bioware item in the minimal saved-character tree
        /// shape used by <see cref="Cyberware"/>/<see cref="Bioware"/> and <see cref="ComputeEssence"/>
        /// - <paramref name="strEss"/>/<paramref name="strCost"/>/<paramref name="strAvail"/> are the
        /// already grade-and-rating-resolved values (the picker applies the Standard/Alphaware/
        /// Betaware/Deltaware multipliers from cyberware.xml/bioware.xml's &lt;grades&gt; before
        /// calling this - Cyberware.CalculatedESS's own further discount formulas aren't ported).</summary>
        /// <summary>Whether adding this Cyberware/Bioware prompts for a Left/Right side - ported
        /// from clsImprovement.cs's selectside bonus handler (frmSelectSide.cs; used by paired
        /// items like Single Cybereye). Legacy stores the pick directly on the item's own
        /// Location field rather than as an Improvement - see <see cref="AddCyberware"/>'s
        /// <paramref name="strSide"/>.</summary>
        public bool CyberwareRequiresSideSelection(string strName, bool blnBioware = false) =>
            FindBonusChild(blnBioware ? "bioware.xml" : "cyberware.xml", blnBioware ? "biowares" : "cyberwares",
                blnBioware ? "bioware" : "cyberware", strName, "selectside") != null;

        /// <summary>Whether adding this Cyberware/Bioware prompts for a Skill Group pick (e.g.
        /// Reflex Recorder (Skill Group)) - ported from clsImprovement.cs's selectskillgroup
        /// bonus handler (frmSelectSkillGroup.cs). The actual Improvement is created via <see
        /// cref="ApplySelectedImprovement"/> once <see cref="AddCyberware"/> is given the chosen
        /// group name.</summary>
        public bool CyberwareRequiresSkillGroupSelection(string strName, bool blnBioware = false) =>
            FindBonusChild(blnBioware ? "bioware.xml" : "cyberware.xml", blnBioware ? "biowares" : "cyberwares",
                blnBioware ? "bioware" : "cyberware", strName, "selectskillgroup") != null;

        /// <summary>Skill Group names to offer for this Cyberware/Bioware's selectskillgroup
        /// bonus, filtered by its excludecategory attribute exactly like frmSelectSkillGroup.cs's
        /// Load handler: a group is offered if skills.xml has at least one skill in that group
        /// whose category is not in the exclude list (or if there's no excludecategory at all).</summary>
        public IReadOnlyList<string> GetCyberwareSkillGroupOptions(string strName, bool blnBioware = false)
        {
            XmlNode? objSelectSkillGroup = FindBonusChild(blnBioware ? "bioware.xml" : "cyberware.xml",
                blnBioware ? "biowares" : "cyberwares", blnBioware ? "bioware" : "cyberware", strName, "selectskillgroup");

            string strExcludeCategory = objSelectSkillGroup?.Attributes?["excludecategory"]?.InnerText ?? string.Empty;
            HashSet<string>? setExclude = string.IsNullOrEmpty(strExcludeCategory)
                ? null
                : new HashSet<string>(strExcludeCategory.Split(',').Select(s => s.Trim()), StringComparer.Ordinal);

            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            var lstGroups = new List<string>();
            foreach (XmlNode objGroup in objSkillsDoc.SelectNodes("/chummer/skillgroups/name")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strGroup = objGroup.InnerText;
                if (setExclude == null)
                {
                    lstGroups.Add(strGroup);
                    continue;
                }

                bool blnHasIncludedSkill = (objSkillsDoc.SelectNodes(
                        $"/chummer/skills/skill[skillgroup = '{strGroup}']")?.Cast<XmlNode>()
                        ?? Enumerable.Empty<XmlNode>())
                    .Any(objSkill => !setExclude.Contains(objSkill["category"]?.InnerText ?? string.Empty));
                if (blnHasIncludedSkill)
                    lstGroups.Add(strGroup);
            }

            return lstGroups;
        }

        public void AddCyberware(string strName, string strCategory, string strRating, string strEss,
            string strCost, string strAvail, string strSource, string strPage, string strGrade = "Standard",
            bool blnBioware = false, string strSide = "", string strSelectedSkillGroup = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A cyberware name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCyberwares = objRoot.SelectSingleNode("cyberwares");
            if (objCyberwares == null)
            {
                objCyberwares = Document.CreateElement("cyberwares");
                objRoot.AppendChild(objCyberwares);
            }

            var objCyberware = Document.CreateElement("cyberware");
            AppendElement(objCyberware, "name", strName.Trim());
            AppendElement(objCyberware, "category", strCategory);
            AppendElement(objCyberware, "rating", strRating);
            AppendElement(objCyberware, "ess", strEss);
            AppendElement(objCyberware, "cost", strCost);
            AppendElement(objCyberware, "avail", strAvail);
            AppendElement(objCyberware, "source", strSource);
            AppendElement(objCyberware, "page", strPage);
            AppendElement(objCyberware, "grade", strGrade);
            AppendElement(objCyberware, "improvementsource", blnBioware ? "Bioware" : "Cyberware");
            AppendElement(objCyberware, "equipped", "True");
            AppendElement(objCyberware, "location", strSide.Trim());
            objCyberware.AppendChild(Document.CreateElement("children"));
            objCyberwares.AppendChild(objCyberware);
            DeductGearCost(strCost, strRating, "1", strAvail);

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            XmlNode? objXmlWare = objWareDoc.SelectSingleNode(
                $"/chummer/{(blnBioware ? "biowares/bioware" : "cyberwares/cyberware")}[name = '{strName.Trim()}']");
            ApplyBonus(objXmlWare?.SelectSingleNode("bonus"),
                blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim(), strRating);
            ApplySelectedImprovement(objXmlWare?.SelectSingleNode("bonus"),
                blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim(),
                strSelectedSkillGroup, strRating);

            Changed?.Invoke();
        }

        /// <summary>Cyberware/Bioware Suite names offered by <see cref="AddCyberwareSuite"/> -
        /// ported from frmSelectCyberwareSuite.cs's Load handler (its flat list, no category
        /// filter unlike frmSelectPACKSKit).</summary>
        public IReadOnlyList<string> GetCyberwareSuiteNames(bool blnBioware = false)
        {
            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            var lstNames = new List<string>();
            foreach (XmlNode objSuite in objWareDoc.SelectNodes("/chummer/suites/suite")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objSuite["name"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strName))
                    lstNames.Add(strName);
            }

            return lstNames;
        }

        /// <summary>Adds a pre-bundled Cyberware/Bioware Suite (e.g. "Aztechnology Topo") - ported
        /// from frmSelectCyberwareSuite.cs's ParseNode. A Suite fixes a single Grade for all of its
        /// parts and can nest cyberware within cyberware (plugins), so this builds the XML tree
        /// directly (mirroring <see cref="AddCyberware"/>'s own node shape and per-item bonus
        /// application) instead of reusing that single-item method, appending nested parts under
        /// their parent's own &lt;children&gt; exactly like <see cref="RemoveCyberware"/>'s tree
        /// already expects. Unlike <see cref="AddCyberware"/>, legacy's own Suite.TotalCost is
        /// informational only (frmCareer.cs deducts it separately) - not ported, matching
        /// AddCyberware's existing no-nuyen-deduction behavior for Cyberware/Bioware.</summary>
        public bool AddCyberwareSuite(string strSuiteName, bool blnBioware = false)
        {
            if (string.IsNullOrWhiteSpace(strSuiteName))
                return false;

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            XmlNode? objXmlSuite = objWareDoc.SelectSingleNode(
                $"/chummer/suites/suite[name = '{strSuiteName.Trim()}']");
            string strItemTag = blnBioware ? "bioware" : "cyberware";
            XmlNode? objXmlItems = objXmlSuite?.SelectSingleNode(strItemTag + "s");
            if (objXmlItems == null)
                return false;

            string strGrade = objXmlSuite!["grade"]?.InnerText ?? "Standard";
            XmlNode? objXmlGrade = objWareDoc.SelectSingleNode($"/chummer/grades/grade[name = '{strGrade}']");
            double dblGradeEss = double.TryParse(objXmlGrade?["ess"]?.InnerText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dEss) ? dEss : 1.0;
            double dblGradeCost = double.TryParse(objXmlGrade?["cost"]?.InnerText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dCost) ? dCost : 1.0;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCyberwares = objRoot.SelectSingleNode("cyberwares");
            if (objCyberwares == null)
            {
                objCyberwares = Document.CreateElement("cyberwares");
                objRoot.AppendChild(objCyberwares);
            }

            foreach (XmlNode objXmlItem in objXmlItems.SelectNodes(strItemTag)?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendCyberwareSuiteItem(objCyberwares, objXmlItem, objWareDoc, strGrade, dblGradeEss, dblGradeCost,
                    blnBioware);

            Changed?.Invoke();
            return true;
        }

        private void AppendCyberwareSuiteItem(XmlNode objParentList, XmlNode objXmlItem, XmlDocument objWareDoc,
            string strGrade, double dblGradeEss, double dblGradeCost, bool blnBioware)
        {
            string strName = objXmlItem["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(strName))
                return;

            string strItemTag = blnBioware ? "bioware" : "cyberware";
            XmlNode? objXmlWare = objWareDoc.SelectSingleNode(
                $"/chummer/{strItemTag}s/{strItemTag}[name = '{strName.Trim()}']");
            if (objXmlWare == null)
                return;

            string strRating = objXmlItem["rating"]?.InnerText ?? "0";
            double dblBaseEss = RatingExpression.Evaluate(objXmlWare["ess"]?.InnerText ?? "0", strRating);
            double dblBaseCost = RatingExpression.Evaluate(objXmlWare["cost"]?.InnerText ?? "0", strRating);
            string strEss = Math.Round(dblBaseEss * dblGradeEss, 2).ToString(CultureInfo.InvariantCulture);
            string strCost = ((int)(dblBaseCost * dblGradeCost)).ToString(CultureInfo.InvariantCulture);

            var objElement = Document.CreateElement(strItemTag);
            AppendElement(objElement, "name", strName.Trim());
            AppendElement(objElement, "category", objXmlWare["category"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "rating", strRating);
            AppendElement(objElement, "ess", strEss);
            AppendElement(objElement, "cost", strCost);
            AppendElement(objElement, "avail", objXmlWare["avail"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "source", objXmlWare["source"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "page", objXmlWare["page"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "grade", strGrade);
            AppendElement(objElement, "improvementsource", blnBioware ? "Bioware" : "Cyberware");
            AppendElement(objElement, "equipped", "True");
            AppendElement(objElement, "location", string.Empty);
            var objChildren = Document.CreateElement("children");
            objElement.AppendChild(objChildren);
            objParentList.AppendChild(objElement);

            ApplyBonus(objXmlWare.SelectSingleNode("bonus"),
                blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim(), strRating);

            foreach (XmlNode objXmlChild in objXmlItem.SelectNodes(strItemTag + "s/" + strItemTag)?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendCyberwareSuiteItem(objChildren, objXmlChild, objWareDoc, strGrade, dblGradeEss, dblGradeCost,
                    blnBioware);
        }

        /// <summary>Categories offered by <see cref="GetPacksKitNames"/> - ported from
        /// frmSelectPACKSKit.cs's category dropdown (real packs.xml data uses "Attribute Kits",
        /// "Skill Kits", "Adept Kits", "Complex Form Kits", "Spell Kits", "Gear Kits"; "Custom" is
        /// listed but has no real kits and is included here anyway for completeness).</summary>
        public IReadOnlyList<string> GetPacksKitCategories()
        {
            XmlDocument objPacksDoc = XmlManager.Instance.Load("packs.xml");
            var lstCategories = new List<string>();
            foreach (XmlNode objCategory in objPacksDoc.SelectNodes("/chummer/categories/category")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strCategory = objCategory.InnerText;
                if (!string.IsNullOrEmpty(strCategory))
                    lstCategories.Add(strCategory);
            }

            return lstCategories;
        }

        public IReadOnlyList<string> GetPacksKitNames(string strCategory)
        {
            XmlDocument objPacksDoc = XmlManager.Instance.Load("packs.xml");
            var lstNames = new List<string>();
            foreach (XmlNode objPack in objPacksDoc.SelectNodes(
                         $"/chummer/packs/pack[category = '{strCategory}']")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objPack["name"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strName))
                    lstNames.Add(strName);
            }

            return lstNames;
        }

        /// <summary>Applies a PACKS Kit (a bundled starting-gear preset) - ported from
        /// frmCreate.cs's AddPACKSKit. Reuses the same higher-level Add* methods this port already
        /// has for each item type wherever possible (Qualities, Spells, Adept Powers, Complex
        /// Forms, Armor, Weapons), matching their existing bonus-application behavior for free.
        /// Not ported (all rare in real packs.xml, documented rather than silently wrong):
        /// Vehicles (7 real kits), Martial Arts via &lt;selectmartialart&gt; (2), Spirits (1),
        /// Lifestyles (0 real kits use it) - and, within the sections that ARE ported, Armor
        /// Mods/nested Gear, Weapon Accessories/Mods, and Exotic Skills are all skipped the same
        /// way <see cref="AddArmor"/>/<see cref="AddWeapon"/> callers elsewhere in this port treat
        /// those as separate follow-up adds rather than kit-bundled content.</summary>
        public bool AddPacksKit(string strKitName, string strCategory)
        {
            if (string.IsNullOrWhiteSpace(strKitName))
                return false;

            XmlDocument objPacksDoc = XmlManager.Instance.Load("packs.xml");
            XmlNode? objXmlKit = objPacksDoc.SelectSingleNode(
                $"/chummer/packs/pack[name = '{strKitName.Trim()}' and category = '{strCategory}']");
            if (objXmlKit == null)
                return false;

            ApplyPacksAttributes(objXmlKit);
            ApplyPacksQualities(objXmlKit);
            ApplyPacksSkills(objXmlKit);
            ApplyPacksKnowledgeSkills(objXmlKit);
            ApplyPacksSpells(objXmlKit);
            ApplyPacksPowers(objXmlKit);
            ApplyPacksComplexForms(objXmlKit);
            ApplyPacksCyberwareOrBioware(objXmlKit, blnBioware: false);
            ApplyPacksCyberwareOrBioware(objXmlKit, blnBioware: true);
            ApplyPacksArmor(objXmlKit);
            ApplyPacksWeapons(objXmlKit);
            ApplyPacksGear(objXmlKit);
            ApplyPacksNuyen(objXmlKit);

            Changed?.Invoke();
            return true;
        }

        private void ApplyPacksAttributes(XmlNode objXmlKit)
        {
            XmlNode? objXmlAttributes = objXmlKit.SelectSingleNode("attributes");
            if (objXmlAttributes == null)
                return;

            // Legacy resets every Attribute to its Metatype minimum first, then applies each
            // given value adjusted by "- (6 - MetatypeMaximum)" to translate a human-scale (max 6)
            // value onto the current Metatype's own scale. SetAttributeValue takes the absolute
            // target value directly, so that translation isn't needed here - the given values are
            // applied as-is.
            foreach (XmlNode objXmlAttribute in objXmlAttributes.ChildNodes)
            {
                if (!int.TryParse(objXmlAttribute.InnerText, out int intValue))
                    continue;

                SetAttributeValue(objXmlAttribute.Name.ToUpperInvariant(), intValue);
            }
        }

        private void ApplyPacksQualities(XmlNode objXmlKit)
        {
            XmlNode? objXmlQualities = objXmlKit.SelectSingleNode("qualities");
            if (objXmlQualities == null)
                return;

            foreach (XmlNode objXmlQuality in objXmlQualities.SelectNodes("positive/quality")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AddQuality(objXmlQuality.InnerText, "Positive", objXmlQuality.Attributes?["select"]?.InnerText ?? string.Empty);

            foreach (XmlNode objXmlQuality in objXmlQualities.SelectNodes("negative/quality")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AddQuality(objXmlQuality.InnerText, "Negative", objXmlQuality.Attributes?["select"]?.InnerText ?? string.Empty);
        }

        private void ApplyPacksSkills(XmlNode objXmlKit)
        {
            XmlNode? objXmlSkills = objXmlKit.SelectSingleNode("skills");
            if (objXmlSkills == null)
                return;

            foreach (XmlNode objXmlSkill in objXmlSkills.SelectNodes("skill")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlSkill["name"]?.InnerText ?? string.Empty;
                XmlNode? objNode = Document.SelectSingleNode(
                    $"/character/skills/skill[name = '{strName}' and knowledge = 'False']");
                if (objNode == null || !int.TryParse(objXmlSkill["rating"]?.InnerText, out int intRating))
                    continue;

                int intMax = int.TryParse(GetValue(objNode, "ratingmax", "6"), out var m) ? m : 6;
                SetChildValue(objNode, "rating", Math.Min(intRating, intMax).ToString(CultureInfo.InvariantCulture));
                if (objXmlSkill["spec"] != null)
                    SetChildValue(objNode, "spec", objXmlSkill["spec"]!.InnerText);
            }

            foreach (XmlNode objXmlGroup in objXmlSkills.SelectNodes("skillgroup")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlGroup["name"]?.InnerText ?? string.Empty;
                if (int.TryParse(objXmlGroup["rating"]?.InnerText, out int intRating))
                    SetSkillGroupRating(strName, intRating);
            }
        }

        private void ApplyPacksKnowledgeSkills(XmlNode objXmlKit)
        {
            XmlNode? objXmlSkills = objXmlKit.SelectSingleNode("knowledgeskills");
            if (objXmlSkills == null)
                return;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSkillsRoot = objRoot.SelectSingleNode("skills");
            if (objSkillsRoot == null)
            {
                objSkillsRoot = Document.CreateElement("skills");
                objRoot.AppendChild(objSkillsRoot);
            }

            foreach (XmlNode objXmlSkill in objXmlSkills.SelectNodes("skill")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlSkill["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(strName))
                    continue;

                string strCategory = objXmlSkill["category"]?.InnerText ?? string.Empty;
                var objSkill = Document.CreateElement("skill");
                AppendElement(objSkill, "name", strName.Trim());
                AppendElement(objSkill, "skillgroup", string.Empty);
                AppendElement(objSkill, "skillcategory", strCategory);
                AppendElement(objSkill, "grouped", "False");
                AppendElement(objSkill, "default", "False");
                AppendElement(objSkill, "rating", objXmlSkill["rating"]?.InnerText ?? "1");
                AppendElement(objSkill, "ratingmax", "6");
                AppendElement(objSkill, "knowledge", "True");
                AppendElement(objSkill, "exotic", "False");
                AppendElement(objSkill, "spec", objXmlSkill["spec"]?.InnerText ?? string.Empty);
                AppendElement(objSkill, "allowdelete", "True");
                AppendElement(objSkill, "attribute", AttributeForKnowledgeCategory(strCategory));
                AppendElement(objSkill, "totalvalue", "0");
                objSkillsRoot.AppendChild(objSkill);
            }
        }

        private void ApplyPacksSpells(XmlNode objXmlKit)
        {
            XmlNode? objXmlSpells = objXmlKit.SelectSingleNode("spells");
            if (objXmlSpells == null)
                return;

            XmlDocument objSpellDoc = XmlManager.Instance.Load("spells.xml");
            var setExisting = new HashSet<string>(Spells.Select(s => s.Name), StringComparer.Ordinal);
            foreach (XmlNode objXmlSpell in objXmlSpells.SelectNodes("spell")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlSpell.InnerText;
                if (string.IsNullOrEmpty(strName) || setExisting.Contains(strName))
                    continue;

                XmlNode? objXmlSpellNode = objSpellDoc.SelectSingleNode($"/chummer/spells/spell[name = '{strName}']");
                if (objXmlSpellNode == null)
                    continue;

                AddSpell(strName, objXmlSpellNode["category"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["type"]?.InnerText ?? string.Empty, objXmlSpellNode["range"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["damage"]?.InnerText ?? string.Empty, objXmlSpellNode["duration"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["dv"]?.InnerText ?? string.Empty, objXmlSpellNode["source"]?.InnerText ?? string.Empty,
                    objXmlSpellNode["page"]?.InnerText ?? string.Empty);
                setExisting.Add(strName);
            }
        }

        private void ApplyPacksPowers(XmlNode objXmlKit)
        {
            XmlNode? objXmlPowers = objXmlKit.SelectSingleNode("powers");
            if (objXmlPowers == null)
                return;

            XmlDocument objPowerDoc = XmlManager.Instance.Load("powers.xml");
            foreach (XmlNode objXmlPower in objXmlPowers.SelectNodes("power")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlPower["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlPowerNode = objPowerDoc.SelectSingleNode($"/chummer/powers/power[name = '{strName}']");
                if (objXmlPowerNode == null)
                    continue;

                string strRating = objXmlPower["rating"]?.InnerText ?? "1";
                string strSelected = objXmlPower["name"]?.Attributes?["select"]?.InnerText ?? string.Empty;
                AddAdeptPower(strName, strRating, objXmlPowerNode["points"]?.InnerText ?? "0", strSelected);
            }
        }

        private void ApplyPacksComplexForms(XmlNode objXmlKit)
        {
            XmlNode? objXmlPrograms = objXmlKit.SelectSingleNode("programs");
            if (objXmlPrograms == null)
                return;

            XmlDocument objProgramDoc = XmlManager.Instance.Load("programs.xml");
            foreach (XmlNode objXmlProgram in objXmlPrograms.SelectNodes("program")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlProgram["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlProgramNode = objProgramDoc.SelectSingleNode($"/chummer/programs/program[name = '{strName}']");
                if (objXmlProgramNode == null)
                    continue;

                string strSelected = objXmlProgram.Attributes?["select"]?.InnerText ?? string.Empty;
                AddComplexForm(strName, objXmlProgramNode["category"]?.InnerText ?? string.Empty,
                    objXmlProgramNode["source"]?.InnerText ?? string.Empty, objXmlProgramNode["page"]?.InnerText ?? string.Empty,
                    strSelected);

                string strGuid = ComplexForms.LastOrDefault(f => f.Name == strName)?.Guid ?? string.Empty;
                if (strGuid.Length == 0)
                    continue;

                foreach (XmlNode objXmlOption in objXmlProgram.SelectNodes("options/option")?.Cast<XmlNode>()
                             ?? Enumerable.Empty<XmlNode>())
                {
                    string strOptionName = objXmlOption["name"]?.InnerText ?? string.Empty;
                    if (strOptionName.Length > 0)
                        AddComplexFormOption(strGuid, strOptionName);
                }
            }
        }

        private void ApplyPacksCyberwareOrBioware(XmlNode objXmlKit, bool blnBioware)
        {
            XmlNode? objXmlItems = objXmlKit.SelectSingleNode(blnBioware ? "biowares" : "cyberwares");
            if (objXmlItems == null)
                return;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCyberwares = objRoot.SelectSingleNode("cyberwares");
            if (objCyberwares == null)
            {
                objCyberwares = Document.CreateElement("cyberwares");
                objRoot.AppendChild(objCyberwares);
            }

            XmlDocument objWareDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            string strItemTag = blnBioware ? "bioware" : "cyberware";
            foreach (XmlNode objXmlItem in objXmlItems.SelectNodes(strItemTag)?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strGrade = objXmlItem["grade"]?.InnerText ?? "Standard";
                XmlNode? objXmlGrade = objWareDoc.SelectSingleNode($"/chummer/grades/grade[name = '{strGrade}']");
                double dblGradeEss = double.TryParse(objXmlGrade?["ess"]?.InnerText, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dEss) ? dEss : 1.0;
                double dblGradeCost = double.TryParse(objXmlGrade?["cost"]?.InnerText, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dCost) ? dCost : 1.0;

                AppendCyberwareSuiteItem(objCyberwares, objXmlItem, objWareDoc, strGrade, dblGradeEss, dblGradeCost,
                    blnBioware);
            }
        }

        private void ApplyPacksArmor(XmlNode objXmlKit)
        {
            XmlNode? objXmlArmors = objXmlKit.SelectSingleNode("armors");
            if (objXmlArmors == null)
                return;

            XmlDocument objArmorDoc = XmlManager.Instance.Load("armor.xml");
            foreach (XmlNode objXmlArmor in objXmlArmors.SelectNodes("armor")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlArmor["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlArmorNode = objArmorDoc.SelectSingleNode($"/chummer/armors/armor[name = '{strName}']");
                if (objXmlArmorNode == null)
                    continue;

                AddArmor(strName, objXmlArmorNode["category"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["b"]?.InnerText ?? string.Empty, objXmlArmorNode["i"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["armorcapacity"]?.InnerText ?? string.Empty, objXmlArmorNode["cost"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["avail"]?.InnerText ?? string.Empty, objXmlArmorNode["source"]?.InnerText ?? string.Empty,
                    objXmlArmorNode["page"]?.InnerText ?? string.Empty);
            }
        }

        private void ApplyPacksWeapons(XmlNode objXmlKit)
        {
            XmlNode? objXmlWeapons = objXmlKit.SelectSingleNode("weapons");
            if (objXmlWeapons == null)
                return;

            XmlDocument objWeaponDoc = XmlManager.Instance.Load("weapons.xml");
            foreach (XmlNode objXmlWeapon in objXmlWeapons.SelectNodes("weapon")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objXmlWeapon["name"]?.InnerText ?? string.Empty;
                XmlNode? objXmlWeaponNode = objWeaponDoc.SelectSingleNode($"/chummer/weapons/weapon[name = '{strName}']");
                if (objXmlWeaponNode == null)
                    continue;

                AddWeapon(strName, objXmlWeaponNode["category"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["damage"]?.InnerText ?? string.Empty, objXmlWeaponNode["ap"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["mode"]?.InnerText ?? string.Empty, objXmlWeaponNode["rc"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["ammo"]?.InnerText ?? string.Empty, objXmlWeaponNode["cost"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["avail"]?.InnerText ?? string.Empty, objXmlWeaponNode["source"]?.InnerText ?? string.Empty,
                    objXmlWeaponNode["page"]?.InnerText ?? string.Empty);
            }
        }

        private void ApplyPacksGear(XmlNode objXmlKit)
        {
            XmlNode? objXmlGears = objXmlKit.SelectSingleNode("gears");
            if (objXmlGears == null)
                return;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objGears = objRoot.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objRoot.AppendChild(objGears);
            }

            XmlDocument objGearDoc = XmlManager.Instance.Load("gear.xml");
            foreach (XmlNode objXmlItem in objXmlGears.SelectNodes("gear")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendPacksGearItem(objGears, objXmlItem, objGearDoc);
        }

        /// <summary>Recursively builds a Gear node (and any nested &lt;gears&gt;/&lt;gear&gt;
        /// plugins) directly, mirroring <see cref="AppendGearNode"/>'s own shape - ported from
        /// frmCreate.cs's AddPACKSGear, simplified to plain Gear (its Commlink/OperatingSystem
        /// special-casing has no effect on this port's flat Response/Signal/System/Firewall
        /// fields, which are read straight from the base gear.xml node either way).</summary>
        private void AppendPacksGearItem(XmlNode objParentList, XmlNode objXmlItem, XmlDocument objGearDoc)
        {
            string strName = objXmlItem["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrEmpty(strName))
                return;

            string? strCategory = objXmlItem["category"]?.InnerText;
            XmlNode? objXmlGear = string.IsNullOrEmpty(strCategory)
                ? objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strName.Trim()}']")
                : objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strName.Trim()}' and category = '{strCategory}']");
            if (objXmlGear == null)
                return;

            string strRating = objXmlItem["rating"]?.InnerText ?? "0";
            string strQty = objXmlItem["qty"]?.InnerText ?? "1";

            var objElement = Document.CreateElement("gear");
            AppendElement(objElement, "name", strName.Trim());
            AppendElement(objElement, "category", objXmlGear["category"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "rating", strRating);
            AppendElement(objElement, "qty", strQty);
            AppendElement(objElement, "cost", objXmlGear["cost"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "avail", objXmlGear["avail"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "capacity", objXmlGear["capacity"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "source", objXmlGear["source"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "page", objXmlGear["page"]?.InnerText ?? string.Empty);
            AppendElement(objElement, "equipped", "False");
            AppendElement(objElement, "guid", Guid.NewGuid().ToString());
            AppendElement(objElement, "active", "False");
            AppendElement(objElement, "location", string.Empty);
            string? strResponse = objXmlGear["response"]?.InnerText;
            if (!string.IsNullOrEmpty(strResponse)) AppendElement(objElement, "response", strResponse);
            string? strSignal = objXmlGear["signal"]?.InnerText;
            if (!string.IsNullOrEmpty(strSignal)) AppendElement(objElement, "signal", strSignal);
            string? strSystem = objXmlGear["system"]?.InnerText;
            if (!string.IsNullOrEmpty(strSystem)) AppendElement(objElement, "system", strSystem);
            string? strFirewall = objXmlGear["firewall"]?.InnerText;
            if (!string.IsNullOrEmpty(strFirewall)) AppendElement(objElement, "firewall", strFirewall);
            var objChildren = Document.CreateElement("children");
            objElement.AppendChild(objChildren);
            objParentList.AppendChild(objElement);

            foreach (XmlNode objXmlChild in objXmlItem.SelectNodes("gears/gear")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
                AppendPacksGearItem(objChildren, objXmlChild, objGearDoc);
        }

        private void ApplyPacksNuyen(XmlNode objXmlKit)
        {
            XmlNode? objXmlNuyenBp = objXmlKit.SelectSingleNode("nuyenbp");
            if (objXmlNuyenBp == null || !int.TryParse(objXmlNuyenBp.InnerText, out int intAmount))
                return;

            if (string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase))
                intAmount *= 2;

            int intCurrent = int.TryParse(Nuyen, out var n) ? n : 0;
            Nuyen = (intCurrent + intAmount).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Removes the first root-level saved Cyberware/Bioware item matching its
        /// name/category/rating and Cyberware-vs-Bioware source, along with any Improvements its
        /// own &lt;bonus&gt; block granted on add.</summary>
        public bool RemoveCyberware(string strName, string strCategory, string strRating, bool blnBioware = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes == null)
                return false;

            string strExpectedSource = blnBioware ? "Bioware" : "Cyberware";
            foreach (XmlNode objCyberware in objNodes)
            {
                if (!string.Equals(GetValue(objCyberware, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objCyberware, "category", string.Empty), strCategory, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objCyberware, "rating", "0"), strRating, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objCyberware, "improvementsource", string.Empty), strExpectedSource, StringComparison.Ordinal))
                    continue;

                objCyberware.ParentNode?.RemoveChild(objCyberware);
                RemoveBonusImprovements(blnBioware ? ImprovementSource.Bioware : ImprovementSource.Cyberware, strName.Trim());
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root
        /// Cyberware/Bioware item (including its own installed plugins' cost).</summary>
        public bool SellCyberware(string strName, string strCategory, string strRating, double dblSellPercent, bool blnBioware = false)
        {
            XmlNode? objNode = FindCyberwareNode(strName, strCategory, strRating, blnBioware);
            if (objNode == null)
                return false;

            int intRefund = ComputeSellRefund(ReadTreeItem(objNode, "children/cyberware").CalculatedCost, dblSellPercent);
            if (!RemoveCyberware(strName, strCategory, strRating, blnBioware))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        private XmlNode? FindCyberwareNode(string strName, string strCategory, string strRating, bool blnBioware)
        {
            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes == null)
                return null;

            string strExpectedSource = blnBioware ? "Bioware" : "Cyberware";
            foreach (XmlNode objCyberware in objNodes)
            {
                if (string.Equals(GetValue(objCyberware, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(objCyberware, "category", string.Empty), strCategory, StringComparison.Ordinal)
                    && string.Equals(GetValue(objCyberware, "rating", "0"), strRating, StringComparison.Ordinal)
                    && string.Equals(GetValue(objCyberware, "improvementsource", string.Empty), strExpectedSource, StringComparison.Ordinal))
                    return objCyberware;
            }

            return null;
        }

        /// <summary>Adjusts the filled physical condition-monitor boxes of a root vehicle.</summary>
        public bool AdjustVehicleDamage(string strName, string strCategory, int intDelta)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/vehicles/vehicle");
            if (objNodes == null) return false;
            foreach (XmlNode objVehicle in objNodes)
            {
                if (!string.Equals(GetValue(objVehicle, "name", string.Empty), strName, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objVehicle, "category", string.Empty), strCategory, StringComparison.Ordinal)) continue;
                int intCurrent = int.TryParse(GetValue(objVehicle, "physicalcmfilled", "0"), out var intValue) ? intValue : 0;
                int intNewValue = Math.Max(0, intCurrent + intDelta);
                if (intNewValue == intCurrent) return false;
                SetChildValue(objVehicle, "physicalcmfilled", intNewValue.ToString());
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Adjusts the filled physical condition-monitor boxes of a vehicle by GUID.</summary>
        public bool AdjustVehicleDamage(Guid guiVehicleId, int intDelta)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            int intCurrent = int.TryParse(GetValue(objVehicle, "physicalcmfilled", "0"), out var intParsed)
                ? intParsed : 0;
            SetChildValue(objVehicle, "physicalcmfilled", Math.Max(0, intCurrent + intDelta).ToString(CultureInfo.InvariantCulture));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a rules-data vehicle modification in the same persisted shape as
        /// <c>VehicleMod.Save</c>. The caller supplies the already selected rating; costs that
        /// reference Body are resolved against the owning vehicle before being deducted.</summary>
        /// <summary>Ported from clsEquipment.cs's Vehicle.Slots/SlotsUsed check that
        /// frmSelectVehicleMod.cs performs before letting a Mod be added. Returns false (and adds
        /// nothing) if the vehicle doesn't have enough free Slots left for it.</summary>
        public bool AddVehicleMod(Guid guiVehicleId, string strName, string strCategory, string strRating,
            string strSlots, string strAvail, string strCost, string strSource, string strPage, string strLimit = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle modification name is required.", nameof(strName));

            CharacterVehicleData? objVehicleData = Vehicles.FirstOrDefault(v => v.Guid == guiVehicleId.ToString());
            if (objVehicleData == null)
                return false;

            int intModSlots = (int)RatingExpression.Evaluate(strSlots, strRating);
            if (intModSlots > objVehicleData.SlotsRemaining)
                return false;

            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;

            XmlNode? objMods = objVehicle.SelectSingleNode("mods");
            if (objMods == null)
            {
                objMods = Document.CreateElement("mods");
                objVehicle.AppendChild(objMods);
            }

            var objMod = Document.CreateElement("mod");
            AppendElement(objMod, "guid", Guid.NewGuid().ToString());
            AppendElement(objMod, "name", strName.Trim());
            AppendElement(objMod, "category", strCategory);
            AppendElement(objMod, "limit", strLimit);
            AppendElement(objMod, "slots", strSlots);
            AppendElement(objMod, "rating", strRating);
            AppendElement(objMod, "maxrating", strRating);
            AppendElement(objMod, "response", "0");
            AppendElement(objMod, "system", "0");
            AppendElement(objMod, "firewall", "0");
            AppendElement(objMod, "signal", "0");
            AppendElement(objMod, "pilot", "0");
            AppendElement(objMod, "avail", strAvail);
            AppendElement(objMod, "cost", strCost);
            AppendElement(objMod, "extra", string.Empty);
            AppendElement(objMod, "source", strSource);
            AppendElement(objMod, "page", strPage);
            AppendElement(objMod, "included", "False");
            AppendElement(objMod, "installed", "True");
            AppendElement(objMod, "subsystems", string.Empty);
            objMod.AppendChild(Document.CreateElement("weapons"));
            AppendElement(objMod, "notes", string.Empty);
            AppendElement(objMod, "discountedcost", "False");
            objMods.AppendChild(objMod);

            var strBody = GetValue(objVehicle, "body", "0");
            DeductVehicleModCost(strCost, strRating, strBody, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds directly installed onboard gear to a vehicle using the legacy Gear.Save
        /// shape. This is separate from the character's root-level gear tree and is charged once
        /// at the selected rating and quantity.</summary>
        public bool AddVehicleGear(Guid guiVehicleId, string strName, string strCategory, string strRating = "0",
            string strQty = "1", string strCost = "", string strAvail = "", string strSource = "", string strPage = "",
            string strCapacity = "", string strResponse = "", string strSignal = "", string strSystemRating = "",
            string strFirewall = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle gear name is required.", nameof(strName));

            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;
            XmlNode? objGears = objVehicle.SelectSingleNode("gears");
            if (objGears == null)
            {
                objGears = Document.CreateElement("gears");
                objVehicle.AppendChild(objGears);
            }
            AppendGearNode(objGears, strName, strCategory, strRating, strQty, strCost, strAvail, strSource, strPage,
                strCapacity, strResponse, strSignal, strSystemRating, strFirewall);
            DeductGearCost(strCost, strRating, strQty, strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct vehicle modification identified by its persisted GUID.</summary>
        public bool RemoveVehicleMod(Guid guiVehicleId, Guid guiModId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objMod = objVehicle?.SelectSingleNode($"mods/mod[guid = '{guiModId}']");
            if (objMod?.ParentNode == null)
                return false;

            objMod.ParentNode.RemoveChild(objMod);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct onboard-gear item by its persisted GUID.</summary>
        public bool RemoveVehicleGear(Guid guiVehicleId, Guid guiGearId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objGear = objVehicle?.SelectSingleNode($"gears/gear[guid = '{guiGearId}']");
            if (objGear?.ParentNode == null)
                return false;
            objGear.ParentNode.RemoveChild(objGear);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns a direct onboard-gear item to one of the vehicle's own named storage
        /// locations (see <see cref="AddVehicleLocation"/>), or clears it back to unassigned with
        /// an empty <paramref name="strLocation"/>.</summary>
        public bool AssignVehicleGearLocation(Guid guiVehicleId, Guid guiGearId, string strLocation)
        {
            strLocation = strLocation?.Trim() ?? string.Empty;
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null)
                return false;
            if (strLocation.Length > 0 && objVehicle.SelectNodes("locations/location")?.Cast<XmlNode>()
                    .Any(node => string.Equals(node.InnerText, strLocation, StringComparison.Ordinal)) != true)
                return false;
            XmlNode? objGear = objVehicle.SelectSingleNode($"gears/gear[guid = '{guiGearId}']");
            if (objGear == null)
                return false;
            SetChildValue(objGear, "location", strLocation);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a direct vehicle weapon in the legacy Weapon.Save shape. Requires an
        /// available (not yet used by another direct weapon) installed "Weapon Mount"/"Mechanical
        /// Arm" mod - ported from frmCareer.cs's tsVehicleAddWeaponWeapon_Click, which refuses to
        /// add a weapon at all unless one of those is selected. The new weapon records which
        /// specific mount mod it occupies (&lt;vehiclemountguid&gt;, the first not already claimed
        /// by another direct weapon) rather than legacy's approach of nesting the weapon node
        /// physically under the mount VehicleMod - same one-weapon-per-mount-instance restriction,
        /// simplified to avoid restructuring this port's existing flat weapons/mods lists. Vehicle-
        /// class eligibility for the weapon itself isn't validated.</summary>
        public bool AddVehicleWeapon(Guid guiVehicleId, string strName, string strCategory, string strDamage,
            string strAp, string strMode, string strRc, string strAmmo, string strCost, string strAvail,
            string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A vehicle weapon name is required.", nameof(strName));
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null) return false;

            var setClaimedMountGuids = new HashSet<string>(
                objVehicle.SelectNodes("weapons/weapon")?.Cast<XmlNode>()
                    .Select(objWeaponNode => GetValue(objWeaponNode, "vehiclemountguid", string.Empty))
                    .Where(s => !string.IsNullOrEmpty(s)) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            string? strMountGuid = objVehicle.SelectNodes("mods/mod")?.Cast<XmlNode>()
                .FirstOrDefault(objMod =>
                {
                    string strModName = GetValue(objMod, "name", string.Empty);
                    bool blnIsMount = strModName.StartsWith("Weapon Mount", StringComparison.Ordinal)
                        || strModName.StartsWith("Mechanical Arm", StringComparison.Ordinal);
                    return blnIsMount && !setClaimedMountGuids.Contains(GetValue(objMod, "guid", string.Empty));
                })?.SelectSingleNode("guid")?.InnerText;
            if (strMountGuid == null)
                return false;

            XmlNode? objWeapons = objVehicle.SelectSingleNode("weapons");
            if (objWeapons == null)
            {
                objWeapons = Document.CreateElement("weapons");
                objVehicle.AppendChild(objWeapons);
            }

            var objWeapon = Document.CreateElement("weapon");
            AppendElement(objWeapon, "guid", Guid.NewGuid().ToString());
            AppendElement(objWeapon, "name", strName.Trim());
            AppendElement(objWeapon, "category", strCategory);
            AppendElement(objWeapon, "type", string.Empty);
            AppendElement(objWeapon, "spec", string.Empty);
            AppendElement(objWeapon, "spec2", string.Empty);
            AppendElement(objWeapon, "reach", "0");
            AppendElement(objWeapon, "vehiclemountguid", strMountGuid);
            AppendElement(objWeapon, "damage", strDamage);
            AppendElement(objWeapon, "ap", strAp);
            AppendElement(objWeapon, "mode", strMode);
            AppendElement(objWeapon, "rc", strRc);
            AppendElement(objWeapon, "ammo", strAmmo);
            AppendElement(objWeapon, "ammocategory", string.Empty);
            AppendElement(objWeapon, "ammoremaining", "0");
            AppendElement(objWeapon, "ammoremaining2", "0");
            AppendElement(objWeapon, "ammoremaining3", "0");
            AppendElement(objWeapon, "ammoremaining4", "0");
            AppendElement(objWeapon, "ammoloaded", Guid.Empty.ToString());
            AppendElement(objWeapon, "ammoloaded2", Guid.Empty.ToString());
            AppendElement(objWeapon, "ammoloaded3", Guid.Empty.ToString());
            AppendElement(objWeapon, "ammoloaded4", Guid.Empty.ToString());
            AppendElement(objWeapon, "conceal", "0");
            AppendElement(objWeapon, "avail", strAvail);
            AppendElement(objWeapon, "cost", strCost);
            AppendElement(objWeapon, "useskill", string.Empty);
            AppendElement(objWeapon, "range", string.Empty);
            AppendElement(objWeapon, "rangemultiply", "1");
            AppendElement(objWeapon, "fullburst", "0");
            AppendElement(objWeapon, "suppressive", "0");
            AppendElement(objWeapon, "source", strSource);
            AppendElement(objWeapon, "page", strPage);
            AppendElement(objWeapon, "weaponname", string.Empty);
            AppendElement(objWeapon, "included", "False");
            AppendElement(objWeapon, "installed", "True");
            AppendElement(objWeapon, "requireammo", "True");
            objWeapon.AppendChild(Document.CreateElement("accessories"));
            objWeapon.AppendChild(Document.CreateElement("weaponmods"));
            AppendElement(objWeapon, "location", string.Empty);
            AppendElement(objWeapon, "notes", string.Empty);
            AppendElement(objWeapon, "discountedcost", "False");
            objWeapons.AppendChild(objWeapon);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a direct vehicle weapon by its persisted GUID.</summary>
        public bool RemoveVehicleWeapon(Guid guiVehicleId, Guid guiWeaponId)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objWeapon = objVehicle?.SelectSingleNode($"weapons/weapon[guid = '{guiWeaponId}']");
            if (objWeapon?.ParentNode == null) return false;
            objWeapon.ParentNode.RemoveChild(objWeapon);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Persists a named storage/location bucket on a vehicle. Existing onboard gear
        /// is intentionally not reassigned here; assignment is a separate UI workflow.</summary>
        public bool AddVehicleLocation(Guid guiVehicleId, string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0) return false;
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            if (objVehicle == null || objVehicle.SelectNodes("locations/location")?.Cast<XmlNode>()
                    .Any(node => string.Equals(node.InnerText, strName, StringComparison.Ordinal)) == true)
                return false;
            XmlElement? objLocations = objVehicle.SelectSingleNode("locations") as XmlElement;
            if (objLocations == null)
            {
                objLocations = Document.CreateElement("locations");
                objVehicle.AppendChild(objLocations);
            }
            AppendElement(objLocations, "location", strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a named vehicle location, clearing it from any onboard gear that was
        /// assigned to it (mirrors <see cref="RemoveWeaponLocation"/>).</summary>
        public bool RemoveVehicleLocation(Guid guiVehicleId, string strName)
        {
            XmlNode? objVehicle = GetVehicleNode(guiVehicleId);
            XmlNode? objLocation = objVehicle?.SelectNodes("locations/location")?.Cast<XmlNode>()
                .FirstOrDefault(node => string.Equals(node.InnerText, strName, StringComparison.Ordinal));
            if (objLocation?.ParentNode == null) return false;
            objLocation.ParentNode.RemoveChild(objLocation);
            XmlNodeList? objGears = objVehicle?.SelectNodes("gears/gear");
            if (objGears != null)
                foreach (XmlNode objGear in objGears)
                    if (string.Equals(GetValue(objGear, "location", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objGear, "location", string.Empty);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? GetVehicleNode(Guid guiVehicleId)
            => Document.SelectSingleNode($"/character/vehicles/vehicle[guid = '{guiVehicleId}']");

        private void DeductVehicleModCost(string strCost, string strRating, string strBody, string strAvail = "")
        {
            string strExpression = strCost.Replace("Body", strBody, StringComparison.OrdinalIgnoreCase);
            double dblCost = RatingExpression.Evaluate(strExpression, strRating);
            dblCost = ApplyRestrictedForbiddenCostMultiplier(dblCost, strAvail);
            double dblNuyen = double.TryParse(Nuyen, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblParsed)
                ? dblParsed : 0;
            Nuyen = (dblNuyen - dblCost).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Adds a root-level Weapon in the minimal saved-character tree shape used by
        /// <see cref="Weapons"/>/<see cref="WeaponTrees"/> - <paramref name="strDamage"/>/
        /// <paramref name="strAp"/>/<paramref name="strMode"/>/<paramref name="strRc"/>/
        /// <paramref name="strAmmo"/> are copied as-is from weapons.xml (no STR-substitution or
        /// underbarrel/accessory bonus math is ported for the damage code).</summary>
        public void AddWeapon(string strName, string strCategory, string strDamage, string strAp, string strMode,
            string strRc, string strAmmo, string strCost, string strAvail, string strSource, string strPage,
            string strUseSkill = "", string strReach = "0")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A weapon name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objWeapons = objRoot.SelectSingleNode("weapons");
            if (objWeapons == null)
            {
                objWeapons = Document.CreateElement("weapons");
                objRoot.AppendChild(objWeapons);
            }

            var objWeapon = Document.CreateElement("weapon");
            // Legacy characters (and this port's own weapons before this feature) don't necessarily
            // have a <guid> - name+category matching (RemoveWeapon/SetWeaponEquipped/
            // SetWeaponLocation) still works for those. Only accessory/mod add/remove needs a
            // stable per-instance identity, so it's the one operation that requires a guid.
            AppendElement(objWeapon, "guid", Guid.NewGuid().ToString());
            AppendElement(objWeapon, "name", strName.Trim());
            AppendElement(objWeapon, "category", strCategory);
            AppendElement(objWeapon, "reach", strReach);
            AppendElement(objWeapon, "damage", strDamage);
            AppendElement(objWeapon, "ap", strAp);
            AppendElement(objWeapon, "mode", strMode);
            AppendElement(objWeapon, "rc", strRc);
            AppendElement(objWeapon, "ammo", strAmmo);
            AppendElement(objWeapon, "cost", strCost);
            AppendElement(objWeapon, "avail", strAvail);
            AppendElement(objWeapon, "useskill", strUseSkill);
            AppendElement(objWeapon, "source", strSource);
            AppendElement(objWeapon, "page", strPage);
            AppendElement(objWeapon, "location", string.Empty);
            AppendElement(objWeapon, "equipped", "True");
            AppendElement(objWeapon, "ammoloaded", "-1");
            AppendElement(objWeapon, "ammoremaining", "0");
            objWeapon.AppendChild(Document.CreateElement("accessories"));
            objWeapon.AppendChild(Document.CreateElement("weaponmods"));
            objWeapon.AppendChild(Document.CreateElement("gears"));
            objWeapon.AppendChild(Document.CreateElement("ammos"));
            objWeapons.AppendChild(objWeapon);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
        }

        /// <summary>Ported from frmReload.cs/frmCareer.cs's "Buy Ammo"/reload flow: which Gear item
        /// (by <see cref="GearId"/>) is currently loaded into a Weapon, and how many rounds remain
        /// in it - the missing concept `RestrictStickNShock`'s own note previously called out as
        /// blocking loaded-ammo dice-pool bonuses. Unlike legacy's Extra-field weapon-category
        /// matching (which needs a whole separate "restrict this purchase to one category" gear-
        /// picker mode this port doesn't have), compatibility is checked purely by ammo name
        /// against the weapon's AmmoCategory - ported directly from frmCareer.cs's
        /// IsAmmunitionCompatible, which already works exactly this way for every non-generic ammo
        /// type (Arrows/Bolts/Grenades/Missiles/Mortars/etc.) and falls back to "any non-exotic
        /// category" for plain Ammo: Regular/Stick-n-Shock/etc.</summary>
        public sealed class WeaponAmmoOption
        {
            internal WeaponAmmoOption(int intGearId, string strName, int intQuantity)
            {
                GearId = intGearId;
                Name = strName;
                Quantity = intQuantity;
            }

            public int GearId { get; }
            public string Name { get; }
            public int Quantity { get; }
        }

        private static bool IsAmmunitionCompatible(string strAmmoName, string strAmmoCategory)
        {
            if (string.IsNullOrEmpty(strAmmoCategory))
                return false;
            if (strAmmoName.Contains("Arrow", StringComparison.Ordinal))
                return strAmmoCategory == "Bows";
            if (strAmmoName.Contains("Bolt", StringComparison.Ordinal))
                return strAmmoCategory == "Crossbows";
            if (strAmmoName.Contains("Assault Cannon", StringComparison.Ordinal))
                return strAmmoCategory == "Assault Cannons";
            if (strAmmoName.Contains("Taser Dart", StringComparison.Ordinal))
                return strAmmoCategory == "Tasers";
            if (strAmmoName.Contains("Gauss Rifle", StringComparison.Ordinal))
                return strAmmoCategory == "Gauss Rifles";
            if (strAmmoName.Contains("Grenade", StringComparison.Ordinal) || strAmmoName.Contains("Minigrenade", StringComparison.Ordinal))
                return strAmmoCategory == "Grenade Launchers";
            if (strAmmoName.Contains("Missile", StringComparison.Ordinal) || strAmmoName.Contains("Rocket", StringComparison.Ordinal))
                return strAmmoCategory == "Missile Launchers";
            if (strAmmoName.Contains("Mortar", StringComparison.Ordinal))
                return strAmmoCategory == "Mortar Launchers";
            return strAmmoCategory != "Bows" && strAmmoCategory != "Crossbows" && strAmmoCategory != "Grenade Launchers"
                && strAmmoCategory != "Missile Launchers" && strAmmoCategory != "Mortar Launchers";
        }

        /// <summary>The rules-data AmmoCategory (weapons.xml's &lt;ammocategory&gt; override,
        /// falling back to the weapon's own Category) a Weapon's ammo must be compatible with -
        /// ported from clsEquipment.cs's Weapon.AmmoCategory.</summary>
        private string GetWeaponAmmoCategory(string strWeaponName, string strFallbackCategory)
        {
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");
            XmlNode? objXmlWeapon = objWeaponsDoc.SelectSingleNode($"/chummer/weapons/weapon[name = '{strWeaponName}']");
            string strOverride = objXmlWeapon?["ammocategory"]?.InnerText ?? string.Empty;
            return string.IsNullOrEmpty(strOverride) ? strFallbackCategory : strOverride;
        }

        /// <summary>Every owned Ammunition Gear item (root-level or nested one level, e.g. inside a
        /// Spare Clip - same depth legacy's own search covers) compatible with this Weapon, for a
        /// reload picker. Excludes Stick-n-Shock when RestrictStickNShock excludes this weapon's
        /// AmmoCategory, matching the same house rule <see cref="AddGear"/> already enforces at
        /// purchase time.</summary>
        public IReadOnlyList<WeaponAmmoOption> GetWeaponAmmoOptions(Guid guiWeaponId)
        {
            XmlNode? objWeapon = FindWeaponNodeByGuid(guiWeaponId);
            if (objWeapon == null)
                return Array.Empty<WeaponAmmoOption>();

            string strAmmoCategory = GetWeaponAmmoCategory(
                GetValue(objWeapon, "name", string.Empty), GetValue(objWeapon, "category", string.Empty));

            CharacterOptions objOptions = GetCharacterOptions();
            bool blnExcludeStickNShock = objOptions.RestrictStickNShock
                && objOptions.StickNShockExcludedWeaponCategories.Contains(strAmmoCategory);

            var lstOptions = new List<WeaponAmmoOption>();
            int intGearId = 0;
            foreach (XmlNode objGearNode in EnumerateGearNodesDfs())
            {
                int intThisId = intGearId++;
                string strCategory = GetValue(objGearNode, "category", string.Empty);
                int intQty = int.TryParse(GetValue(objGearNode, "qty", "0"), out var q) ? q : 0;
                if (strCategory != "Ammunition" || intQty <= 0)
                    continue;

                string strName = GetValue(objGearNode, "name", string.Empty);
                if (blnExcludeStickNShock && string.Equals(strName, "Ammo: Stick-n-Shock", StringComparison.Ordinal))
                    continue;
                if (!IsAmmunitionCompatible(strName, strAmmoCategory))
                    continue;

                lstOptions.Add(new WeaponAmmoOption(intThisId, strName, intQty));
            }

            return lstOptions;
        }

        /// <summary>Parses a weapon's raw rules-data-derived Ammo string (e.g. "30(c)", "10(c) or
        /// external source", "6(cy)/2(belt)") into the whole-round-count choices it offers, same as
        /// frmCareer.cs's own cboType population - "external source" alternatives are dropped since
        /// this port has no External Source concept.</summary>
        public IReadOnlyList<int> GetWeaponAmmoCapacityChoices(Guid guiWeaponId)
        {
            XmlNode? objWeapon = FindWeaponNodeByGuid(guiWeaponId);
            var lstChoices = new List<int>();
            if (objWeapon == null)
                return lstChoices;

            string strAmmo = GetValue(objWeapon, "ammo", string.Empty);
            if (string.IsNullOrEmpty(strAmmo))
                return lstChoices;

            foreach (string strPart in strAmmo.Split(new[] { " or ", "/" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string strTrimmed = strPart.Trim();
                int intParenIndex = strTrimmed.IndexOf('(');
                if (intParenIndex >= 0)
                    strTrimmed = strTrimmed.Substring(0, intParenIndex);
                if (int.TryParse(strTrimmed, out int intCount) && intCount > 0)
                    lstChoices.Add(intCount);
            }

            return lstChoices;
        }

        /// <summary>Loads a Weapon with a chosen amount of a chosen Ammo Gear item - ported from
        /// frmCareer.cs's "Buy Ammo"/reload click handlers: any rounds still in the Weapon's
        /// previous load are returned to that Gear item's Quantity first (matching legacy's "return
        /// unspent rounds to the Ammo" step), then <paramref name="intCount"/> rounds are consumed
        /// from <paramref name="intAmmoGearId"/>'s Quantity - clamped to whatever's actually left if
        /// that's less than requested, same as legacy ("use whatever is left") rather than failing.</summary>
        public bool ReloadWeapon(Guid guiWeaponId, int intAmmoGearId, int intCount)
        {
            XmlNode? objWeapon = FindWeaponNodeByGuid(guiWeaponId);
            XmlNode? objAmmoGear = GetGearNodeById(intAmmoGearId);
            if (objWeapon == null || objAmmoGear == null || intCount <= 0)
                return false;

            // Return any rounds unspent from the weapon's current load first - if that's the same
            // Gear item being reloaded from, its just-restored Quantity is what the clamp below sees.
            ReturnUnspentAmmoToItsSource(objWeapon);

            int intAmmoQty = int.TryParse(GetValue(objAmmoGear, "qty", "0"), out var qAvail) ? qAvail : 0;
            int intLoaded = Math.Min(intCount, intAmmoQty);
            if (intLoaded <= 0)
                return false;

            SetChildValue(objAmmoGear, "qty", (intAmmoQty - intLoaded).ToString(CultureInfo.InvariantCulture));
            SetChildValue(objWeapon, "ammoloaded", intAmmoGearId.ToString(CultureInfo.InvariantCulture));
            SetChildValue(objWeapon, "ammoremaining", intLoaded.ToString(CultureInfo.InvariantCulture));
            Changed?.Invoke();
            return true;
        }

        private void ReturnUnspentAmmoToItsSource(XmlNode objWeapon)
        {
            int intPreviousGearId = int.TryParse(GetValue(objWeapon, "ammoloaded", "-1"), out var g) ? g : -1;
            int intUnspent = int.TryParse(GetValue(objWeapon, "ammoremaining", "0"), out var r) ? r : 0;
            if (intPreviousGearId < 0 || intUnspent <= 0)
                return;

            XmlNode? objPreviousGear = GetGearNodeById(intPreviousGearId);
            if (objPreviousGear == null)
                return;

            int intPreviousQty = int.TryParse(GetValue(objPreviousGear, "qty", "0"), out var q) ? q : 0;
            SetChildValue(objPreviousGear, "qty", (intPreviousQty + intUnspent).ToString(CultureInfo.InvariantCulture));
        }

        private string ComputeAmmoStatus(XmlNode objWeapon)
        {
            int intGearId = int.TryParse(GetValue(objWeapon, "ammoloaded", "-1"), out var g) ? g : -1;
            int intRemaining = int.TryParse(GetValue(objWeapon, "ammoremaining", "0"), out var r) ? r : 0;
            if (intGearId < 0)
                return string.Empty;

            XmlNode? objGear = GetGearNodeById(intGearId);
            string strName = objGear != null ? GetValue(objGear, "name", string.Empty) : string.Empty;
            return string.IsNullOrEmpty(strName) ? string.Empty : intRemaining + " (" + strName + ")";
        }

        /// <summary>Ported from frmNaturalWeapon.cs: manually defines a melee Weapon for an adept/
        /// critter power (e.g. Claws) instead of picking one from weapons.xml - assembles the
        /// Damage Value from a base (a fixed rating or "(STR/2)"), an optional +/- modifier, and a
        /// P/S type, an AP value, and links it to a player-chosen Combat Active Skill (persisted as
        /// the new UseSkill override <see cref="ComputeWeaponDicePool"/> now understands) instead
        /// of relying on the weapon's Category. Source/page are copied from critterpowers.xml's
        /// "Natural Weapon" power entry, matching legacy. Always Avail 0/Cost 0, like legacy.</summary>
        public IReadOnlyList<string> GetCombatActiveSkillNames()
        {
            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            var lstNames = new List<string>();
            XmlNodeList? objNodes = objSkillsDoc.SelectNodes("/chummer/skills/skill[category = \"Combat Active\"]");
            if (objNodes != null)
                foreach (XmlNode objNode in objNodes)
                {
                    string strName = objNode["name"]?.InnerText ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(strName))
                        lstNames.Add(strName);
                }
            return lstNames;
        }

        public bool AddNaturalWeapon(string strName, string strUseSkill, string strDvBase, int intDvMod,
            string strDvType, int intAp, int intReach)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            string strDamage = strDvBase;
            if (intDvMod > 0)
                strDamage += "+" + intDvMod;
            else if (intDvMod < 0)
                strDamage += intDvMod.ToString(CultureInfo.InvariantCulture);
            strDamage += strDvType;

            string strAp = intAp switch
            {
                0 => "0",
                > 0 => "+" + intAp.ToString(CultureInfo.InvariantCulture),
                _ => intAp.ToString(CultureInfo.InvariantCulture)
            };

            XmlDocument objPowersDoc = XmlManager.Instance.Load("critterpowers.xml");
            XmlNode? objPower = objPowersDoc.SelectSingleNode("/chummer/powers/power[name = \"Natural Weapon\"]");
            string strSource = objPower?["source"]?.InnerText ?? string.Empty;
            string strPage = objPower?["page"]?.InnerText ?? string.Empty;

            AddWeapon(strName, "Natürliche Waffe", strDamage, strAp, "0", "0", string.Empty, "0", "0",
                strSource, strPage, strUseSkill, intReach.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>Named weapon locations remain persisted even while empty.</summary>
        public IReadOnlyList<string> WeaponLocations => (IReadOnlyList<string>?)Document.SelectNodes("/character/weaponlocations/weaponlocation")?
            .Cast<XmlNode>().Select(objNode => objNode.InnerText).Where(strName => !string.IsNullOrWhiteSpace(strName))
            .Distinct(StringComparer.Ordinal).ToList() ?? Array.Empty<string>();

        public bool AddWeaponLocation(string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0 || WeaponLocations.Contains(strName, StringComparer.Ordinal)) return false;
            var objRoot = Document.DocumentElement;
            if (objRoot == null) return false;
            XmlElement? objLocations = objRoot.SelectSingleNode("weaponlocations") as XmlElement;
            if (objLocations == null)
            {
                objLocations = Document.CreateElement("weaponlocations");
                objRoot.AppendChild(objLocations);
            }
            AppendElement(objLocations, "weaponlocation", strName);
            Changed?.Invoke();
            return true;
        }

        public bool SetWeaponLocation(string strName, string strCategory, string strLocation)
        {
            strLocation = strLocation.Trim();
            if (!string.IsNullOrEmpty(strLocation) && !WeaponLocations.Contains(strLocation, StringComparer.Ordinal)) return false;
            XmlNodeList? objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return false;
            foreach (XmlNode objWeapon in objNodes)
            {
                if (!string.Equals(GetValue(objWeapon, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objWeapon, "category", string.Empty), strCategory, StringComparison.Ordinal)) continue;
                if (string.Equals(GetValue(objWeapon, "location", string.Empty), strLocation, StringComparison.Ordinal)) return false;
                SetChildValue(objWeapon, "location", strLocation);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public bool RemoveWeaponLocation(string strName)
        {
            strName = strName.Trim();
            XmlNode? objLocation = Document.SelectNodes("/character/weaponlocations/weaponlocation")?.Cast<XmlNode>()
                .FirstOrDefault(objNode => string.Equals(objNode.InnerText, strName, StringComparison.Ordinal));
            if (objLocation?.ParentNode == null) return false;
            objLocation.ParentNode.RemoveChild(objLocation);
            XmlNodeList? objWeapons = Document.SelectNodes("/character/weapons/weapon");
            if (objWeapons != null)
                foreach (XmlNode objWeapon in objWeapons)
                    if (string.Equals(GetValue(objWeapon, "location", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objWeapon, "location", string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first root-level saved weapon matching its name/category.</summary>
        public bool RemoveWeapon(string strName, string strCategory)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null)
                return false;

            foreach (XmlNode objWeapon in objNodes)
            {
                if (!string.Equals(GetValue(objWeapon, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objWeapon, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;

                objWeapon.ParentNode?.RemoveChild(objWeapon);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root Weapon
        /// (including its own installed accessories/mods/loaded gear cost).</summary>
        public bool SellWeapon(string strName, string strCategory, double dblSellPercent)
        {
            XmlNode? objNode = Document.SelectNodes("/character/weapons/weapon")?.Cast<XmlNode>()
                .FirstOrDefault(n => string.Equals(GetValue(n, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(n, "category", string.Empty), strCategory, StringComparison.Ordinal));
            if (objNode == null)
                return false;

            int intRefund = ComputeSellRefund(
                ReadTreeItem(objNode, "accessories/accessory", "weaponmods/weaponmod", "gears/gear", "ammos/ammo").CalculatedCost,
                dblSellPercent);
            if (!RemoveWeapon(strName, strCategory))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        /// <summary>Sets the equipped state of the first root-level weapon matching name and category.</summary>
        public bool SetWeaponEquipped(string strName, string strCategory, bool blnEquipped)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return false;
            foreach (XmlNode objWeapon in objNodes)
            {
                if (!string.Equals(GetValue(objWeapon, "name", string.Empty), strName, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objWeapon, "category", string.Empty), strCategory, StringComparison.Ordinal)) continue;
                SetChildValue(objWeapon, "equipped", blnEquipped ? "True" : "False");
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        private XmlNode? GetWeaponNodeByGuid(Guid guiWeaponId)
            => Document.SelectSingleNode($"/character/weapons/weapon[guid = '{guiWeaponId}']");

        /// <summary>Same as <see cref="GetWeaponNodeByGuid"/> but also matches vehicle-mounted
        /// Weapons - used by the ammo-tracking methods (ReloadWeapon/GetWeaponAmmoOptions/
        /// GetWeaponAmmoCapacityChoices), which work the same way for either.</summary>
        private XmlNode? FindWeaponNodeByGuid(Guid guiWeaponId)
            => GetWeaponNodeByGuid(guiWeaponId)
                ?? Document.SelectSingleNode($"/character/vehicles/vehicle/weapons/weapon[guid = '{guiWeaponId}']");

        /// <summary>Adds a Weapon Accessory (ported from clsEquipment.cs's WeaponAccessory.Save) to
        /// a root-level weapon, deducting its cost. Rejects if the weapon's mount-slot eligibility
        /// check (<see cref="WeaponAllowsAccessoryMount"/>) fails.</summary>
        public bool AddWeaponAccessory(Guid guiWeaponId, string strName, string strMount, string strRc,
            string strAvail, string strCost, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A weapon accessory name is required.", nameof(strName));

            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objAccessories = objWeapon?.SelectSingleNode("accessories");
            if (objWeapon == null || objAccessories == null || !WeaponAllowsAccessoryMount(objWeapon, strMount))
                return false;

            var objAccessory = Document.CreateElement("accessory");
            AppendElement(objAccessory, "guid", Guid.NewGuid().ToString());
            AppendElement(objAccessory, "name", strName.Trim());
            AppendElement(objAccessory, "mount", strMount);
            AppendElement(objAccessory, "rc", strRc);
            AppendElement(objAccessory, "avail", strAvail);
            AppendElement(objAccessory, "cost", strCost);
            AppendElement(objAccessory, "included", "False");
            AppendElement(objAccessory, "installed", "True");
            AppendElement(objAccessory, "source", strSource);
            AppendElement(objAccessory, "page", strPage);
            objAccessories.AppendChild(objAccessory);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveWeaponAccessory(Guid guiWeaponId, Guid guiAccessoryId)
        {
            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objAccessory = objWeapon?.SelectSingleNode($"accessories/accessory[guid = '{guiAccessoryId}']");
            if (objAccessory?.ParentNode == null)
                return false;
            objAccessory.ParentNode.RemoveChild(objAccessory);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from frmCareer.cs's tsWeaponAddAccessory_Click's mount-list check: looks
        /// up the weapon's own rules-data entry in weapons.xml for &lt;allowaccessory&gt; and its
        /// &lt;accessorymounts&gt; list, and requires the accessory's own (possibly "/"-separated)
        /// mount to intersect with it. An accessory with an empty mount is always allowed (matches
        /// legacy's "mount = ''" fallback in its own picker filter). Doesn't enforce exclusivity
        /// between multiple accessories sharing the same mount - legacy doesn't either.</summary>
        private static bool WeaponAllowsAccessoryMount(XmlNode objWeapon, string strAccessoryMount)
        {
            string strWeaponName = GetValue(objWeapon, "name", string.Empty);
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");
            XmlNode? objXmlWeapon = objWeaponsDoc.SelectSingleNode(
                $"/chummer/weapons/weapon[name = '{strWeaponName}']");
            if (objXmlWeapon == null)
                return true; // Unknown to rules data (e.g. a hand-entered weapon) - don't block it.

            if (string.Equals(GetValue(objXmlWeapon, "allowaccessory", "True"), "False",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(strAccessoryMount))
                return true;

            var setAllowedMounts = new HashSet<string>(StringComparer.Ordinal);
            XmlNodeList? objMountNodes = objXmlWeapon.SelectNodes("accessorymounts/mount");
            if (objMountNodes != null)
                foreach (XmlNode objMountNode in objMountNodes)
                    setAllowedMounts.Add(objMountNode.InnerText);

            return strAccessoryMount.Split('/').Any(setAllowedMounts.Contains);
        }

        /// <summary>Adds a Weapon Modification (ported from clsEquipment.cs's WeaponMod.Save) to a
        /// root-level weapon, deducting its cost - <paramref name="strCost"/> may reference "Weapon
        /// Cost" (substituted with the weapon's own saved cost, matching clsEquipment.cs's mod cost
        /// formulas) and/or "Rating" (substituted by <see cref="RatingExpression"/>). Ported from
        /// frmCareer.cs's tsWeaponAddModification_Click: rejects if the weapon's own rules-data
        /// entry sets &lt;allowmod&gt; to false, and - when EnforceCapacity is on - rejects if
        /// installed non-included mods' slots plus this one would exceed the fixed 6-slot cap every
        /// weapon has (clsEquipment.cs's Weapon.SlotsRemaining hardcodes this as a constant, not a
        /// per-weapon data field).</summary>
        public bool AddWeaponMod(Guid guiWeaponId, string strName, string strRating, string strSlots,
            string strAvail, string strCost, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A weapon mod name is required.", nameof(strName));

            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objMods = objWeapon?.SelectSingleNode("weaponmods");
            if (objWeapon == null || objMods == null || !WeaponAllowsMods(objWeapon))
                return false;
            if (GetCharacterOptions().EnforceCapacity && !WeaponHasModSlotsAvailable(objWeapon, strSlots))
                return false;

            var objMod = Document.CreateElement("weaponmod");
            AppendElement(objMod, "guid", Guid.NewGuid().ToString());
            AppendElement(objMod, "name", strName.Trim());
            AppendElement(objMod, "rating", strRating);
            AppendElement(objMod, "slots", strSlots);
            AppendElement(objMod, "avail", strAvail);
            AppendElement(objMod, "cost", strCost);
            AppendElement(objMod, "included", "False");
            AppendElement(objMod, "installed", "True");
            AppendElement(objMod, "source", strSource);
            AppendElement(objMod, "page", strPage);
            objMods.AppendChild(objMod);

            string strWeaponCost = GetValue(objWeapon, "cost", "0");
            DeductGearCost(strCost.Replace("Weapon Cost", strWeaponCost, StringComparison.OrdinalIgnoreCase),
                strRating, "1", strAvail);
            Changed?.Invoke();
            return true;
        }

        private static bool WeaponAllowsMods(XmlNode objWeapon)
        {
            string strWeaponName = GetValue(objWeapon, "name", string.Empty);
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");
            XmlNode? objXmlWeapon = objWeaponsDoc.SelectSingleNode(
                $"/chummer/weapons/weapon[name = '{strWeaponName}']");
            return objXmlWeapon == null || !string.Equals(GetValue(objXmlWeapon, "allowmod", "True"), "False",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool WeaponHasModSlotsAvailable(XmlNode objWeapon, string strNewSlots)
        {
            const int intMaxSlots = 6;
            double dblUsed = 0;
            XmlNodeList? objModNodes = objWeapon.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    if (GetValue(objModNode, "included", "False") != "True"
                        && double.TryParse(GetValue(objModNode, "slots", "0"), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var dblSlots))
                        dblUsed += dblSlots;

            double dblNew = double.TryParse(strNewSlots, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var dblParsedNew) ? dblParsedNew : 0;
            return dblUsed + dblNew <= intMaxSlots;
        }

        public bool RemoveWeaponMod(Guid guiWeaponId, Guid guiModId)
        {
            XmlNode? objWeapon = GetWeaponNodeByGuid(guiWeaponId);
            XmlNode? objMod = objWeapon?.SelectSingleNode($"weaponmods/weaponmod[guid = '{guiModId}']");
            if (objMod?.ParentNode == null)
                return false;
            objMod.ParentNode.RemoveChild(objMod);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Adds a root-level Armor in the minimal saved-character tree shape used by
        /// <see cref="Armor"/> and the armor-encumbrance calc - <paramref name="strB"/>/
        /// <paramref name="strI"/> are the ballistic/impact ratings copied as-is from armor.xml
        /// (including a leading "+" for stacking bonus armor, same as the encumbrance calc already
        /// handles via ParseArmorRating).</summary>
        public void AddArmor(string strName, string strCategory, string strB, string strI, string strCapacity,
            string strCost, string strAvail, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("An armor name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objArmors = objRoot.SelectSingleNode("armors");
            if (objArmors == null)
            {
                objArmors = Document.CreateElement("armors");
                objRoot.AppendChild(objArmors);
            }

            var objArmor = Document.CreateElement("armor");
            AppendElement(objArmor, "name", strName.Trim());
            AppendElement(objArmor, "category", strCategory);
            AppendElement(objArmor, "b", strB);
            AppendElement(objArmor, "i", strI);
            AppendElement(objArmor, "armorcapacity", strCapacity);
            AppendElement(objArmor, "cost", strCost);
            AppendElement(objArmor, "avail", strAvail);
            AppendElement(objArmor, "source", strSource);
            AppendElement(objArmor, "page", strPage);
            AppendElement(objArmor, "armorname", string.Empty);
            AppendElement(objArmor, "equipped", "True");
            objArmor.AppendChild(Document.CreateElement("armormods"));
            objArmor.AppendChild(Document.CreateElement("gears"));
            objArmors.AppendChild(objArmor);
            DeductGearCost(strCost, "0", "1", strAvail);
            Changed?.Invoke();
        }

        /// <summary>Creates a named armor bundle. A bundle is persisted independently so that an
        /// empty set remains visible and can receive armor later.</summary>
        public bool AddArmorSet(string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0 || ArmorSets.Contains(strName, StringComparer.Ordinal))
                return false;

            var objRoot = Document.DocumentElement;
            if (objRoot == null)
                return false;
            XmlElement? objSets = objRoot.SelectSingleNode("armorbundles") as XmlElement;
            if (objSets == null)
            {
                objSets = Document.CreateElement("armorbundles");
                objRoot.AppendChild(objSets);
            }
            AppendElement(objSets, "armorbundle", strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns a root-level armor item to a bundle, or to the ungrouped root when
        /// <paramref name="strSetName"/> is empty.</summary>
        public bool SetArmorSet(string strName, string strCategory, string strSetName)
        {
            strSetName = strSetName.Trim();
            if (!string.IsNullOrEmpty(strSetName) && !ArmorSets.Contains(strSetName, StringComparer.Ordinal))
                return false;
            XmlNodeList? objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return false;
            foreach (XmlNode objArmor in objNodes)
            {
                if (!string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;
                if (string.Equals(GetValue(objArmor, "armorname", string.Empty), strSetName, StringComparison.Ordinal))
                    return false;
                SetChildValue(objArmor, "armorname", strSetName);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Deletes a bundle and moves all of its armor back to the ungrouped root.</summary>
        public bool RemoveArmorSet(string strName)
        {
            strName = strName.Trim();
            XmlNode? objSet = Document.SelectNodes("/character/armorbundles/armorbundle")?
                .Cast<XmlNode>().FirstOrDefault(objNode => string.Equals(objNode.InnerText, strName, StringComparison.Ordinal));
            if (objSet?.ParentNode == null)
                return false;
            objSet.ParentNode.RemoveChild(objSet);
            XmlNodeList? objArmor = Document.SelectNodes("/character/armors/armor");
            if (objArmor != null)
                foreach (XmlNode objNode in objArmor)
                    if (string.Equals(GetValue(objNode, "armorname", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objNode, "armorname", string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first root-level saved armor matching its name/category.</summary>
        public bool RemoveArmor(string strName, string strCategory)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return false;

            foreach (XmlNode objArmor in objNodes)
            {
                if (!string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;

                objArmor.ParentNode?.RemoveChild(objArmor);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>See <see cref="SellGear"/> - same refund-then-remove pattern for a root Armor
        /// item (including its own installed mods/gear cost).</summary>
        public bool SellArmor(string strName, string strCategory, double dblSellPercent)
        {
            XmlNode? objNode = Document.SelectNodes("/character/armors/armor")?.Cast<XmlNode>()
                .FirstOrDefault(n => string.Equals(GetValue(n, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(n, "category", string.Empty), strCategory, StringComparison.Ordinal));
            if (objNode == null)
                return false;

            int intRefund = ComputeSellRefund(
                ReadTreeItem(objNode, "armormods/armormod", "gears/gear").CalculatedCost, dblSellPercent);
            if (!RemoveArmor(strName, strCategory))
                return false;

            ApplySellRefund(intRefund, strName);
            return true;
        }

        /// <summary>Equips or unequips the first root-level saved armor matching its name/category -
        /// only equipped armor counts toward <see cref="ArmorEncumbrance"/> and the worn ballistic/
        /// impact rating shown elsewhere.</summary>
        public bool SetArmorEquipped(string strName, string strCategory, bool blnEquipped)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return false;

            foreach (XmlNode objArmor in objNodes)
            {
                if (!string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    || !string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    continue;

                SetChildValue(objArmor, "equipped", blnEquipped ? "True" : "False");
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Adds an Armor Modification to a root-level Armor item, matched by name+category
        /// (this port's Armor items have no guid, same lookup approach as <see
        /// cref="RemoveArmor"/>/<see cref="SetArmorEquipped"/>) - deducts cost and applies the
        /// mod's own rules-data &lt;bonus&gt; block at the given Rating. Ported from
        /// clsEquipment.cs's ArmorMod.Create/Save. Not ported: Armor capacity enforcement (this
        /// port doesn't track Armor capacity remaining at all yet, unlike Gear/Weapon Mod slots).</summary>
        public bool AddArmorMod(string strArmorName, string strArmorCategory, string strName, string strRating,
            string strB, string strI, string strAvail, string strCost, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("An armor mod name is required.", nameof(strName));

            XmlNode? objArmor = FindArmorNode(strArmorName, strArmorCategory);
            XmlNode? objMods = objArmor?.SelectSingleNode("armormods");
            if (objArmor == null || objMods == null)
                return false;

            var objMod = Document.CreateElement("armormod");
            AppendElement(objMod, "guid", Guid.NewGuid().ToString());
            AppendElement(objMod, "name", strName.Trim());
            AppendElement(objMod, "rating", strRating);
            AppendElement(objMod, "b", strB);
            AppendElement(objMod, "i", strI);
            AppendElement(objMod, "avail", strAvail);
            AppendElement(objMod, "cost", strCost);
            AppendElement(objMod, "included", "False");
            AppendElement(objMod, "equipped", "True");
            AppendElement(objMod, "source", strSource);
            AppendElement(objMod, "page", strPage);
            objMods.AppendChild(objMod);

            DeductGearCost(strCost, strRating, "1", strAvail);

            XmlDocument objArmorDoc = XmlManager.Instance.Load("armor.xml");
            XmlNode? objXmlMod = objArmorDoc.SelectSingleNode($"/chummer/mods/mod[name = '{strName.Trim()}']");
            ApplyBonus(objXmlMod?.SelectSingleNode("bonus"), ImprovementSource.ArmorMod, strName.Trim(), strRating);

            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the first saved Armor Modification matching its name, wherever it's
        /// nested (searches every root-level Armor's &lt;armormods&gt;), along with any
        /// Improvements its own &lt;bonus&gt; block granted on add.</summary>
        public bool RemoveArmorMod(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/armors/armor/armormods/armormod");
            if (objNodes == null)
                return false;

            foreach (XmlNode objMod in objNodes)
            {
                if (!string.Equals(GetValue(objMod, "name", string.Empty), strName.Trim(), StringComparison.Ordinal))
                    continue;

                objMod.ParentNode?.RemoveChild(objMod);
                RemoveBonusImprovements(ImprovementSource.ArmorMod, strName.Trim());
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        private XmlNode? FindArmorNode(string strName, string strCategory)
        {
            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null)
                return null;

            foreach (XmlNode objArmor in objNodes)
            {
                if (string.Equals(GetValue(objArmor, "name", string.Empty), strName.Trim(), StringComparison.Ordinal)
                    && string.Equals(GetValue(objArmor, "category", string.Empty), strCategory, StringComparison.Ordinal))
                    return objArmor;
            }
            return null;
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

        /// <summary>Resolves <paramref name="nodBonus"/> via <see cref="BonusApplier"/> and persists
        /// each resulting effect as an &lt;improvement&gt; element, in the shape <see
        /// cref="Improvement.Load"/> expects. Ported from clsImprovement.cs's CreateImprovements
        /// (only the non-interactive subset BonusApplier covers) plus CreateImprovement's actual
        /// XML write. Does not fire <see cref="Changed"/> itself - callers already do that for the
        /// item being added.</summary>
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
            AppendElement(objImprovement, "custom", "False");
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

        /// <summary>Fires whenever a root-level value (Karma, Bp, Nuyen, ...) changes, so a host
        /// UI showing those totals (e.g. the main window's status bar) can refresh without needing
        /// to know about every individual mutator that might have spent points.</summary>
        public event Action? Changed;

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
            Changed?.Invoke();
        }

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

        public IReadOnlyList<CharacterTreeItemData> Gear => ReadGearTree();

        /// <summary>Named storage locations for root-level Gear (ported from frmCareer.cs's
        /// cmdAddLocation_Click) - existing while empty, same as WeaponLocations.</summary>
        public IReadOnlyList<string> GearLocations => (IReadOnlyList<string>?)Document.SelectNodes("/character/gearlocations/gearlocation")?
            .Cast<XmlNode>().Select(objNode => objNode.InnerText).Where(strName => !string.IsNullOrWhiteSpace(strName))
            .Distinct(StringComparer.Ordinal).ToList() ?? Array.Empty<string>();

        public bool AddGearLocation(string strName)
        {
            strName = strName.Trim();
            if (strName.Length == 0 || GearLocations.Contains(strName, StringComparer.Ordinal)) return false;
            var objRoot = Document.DocumentElement;
            if (objRoot == null) return false;
            XmlElement? objLocations = objRoot.SelectSingleNode("gearlocations") as XmlElement;
            if (objLocations == null)
            {
                objLocations = Document.CreateElement("gearlocations");
                objRoot.AppendChild(objLocations);
            }
            AppendElement(objLocations, "gearlocation", strName);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a named Gear location, clearing it from any root-level Gear that was
        /// assigned to it - mirrors RemoveWeaponLocation's behavior for weapons.</summary>
        public bool RemoveGearLocation(string strName)
        {
            strName = strName.Trim();
            XmlNode? objLocation = Document.SelectNodes("/character/gearlocations/gearlocation")?.Cast<XmlNode>()
                .FirstOrDefault(objNode => string.Equals(objNode.InnerText, strName, StringComparison.Ordinal));
            if (objLocation?.ParentNode == null) return false;
            objLocation.ParentNode.RemoveChild(objLocation);
            XmlNodeList? objGearNodes = Document.SelectNodes("/character/gears/gear");
            if (objGearNodes != null)
                foreach (XmlNode objGear in objGearNodes)
                    if (string.Equals(GetValue(objGear, "location", string.Empty), strName, StringComparison.Ordinal))
                        SetChildValue(objGear, "location", string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns a root-level Gear item (by its depth-first GearId) to one of the
        /// character's own named locations, or clears it back to unassigned with an empty
        /// <paramref name="strLocation"/>.</summary>
        public bool SetGearLocation(int intGearId, string strLocation)
        {
            strLocation = strLocation.Trim();
            if (strLocation.Length > 0 && !GearLocations.Contains(strLocation, StringComparer.Ordinal))
                return false;
            XmlNode? objGear = GetGearNodeById(intGearId);
            // Only root-level Gear (a direct child of <gears>, not nested under another item) can
            // have a location - matches the legacy tree, which only ever lets you drag a top-level
            // item into a location.
            if (objGear == null || objGear.ParentNode?.Name != "gears")
                return false;
            SetChildValue(objGear, "location", strLocation);
            Changed?.Invoke();
            return true;
        }

        private IReadOnlyList<CharacterTreeItemData> ReadGearTree()
        {
            var objNodes = Document.SelectNodes("/character/gears/gear");
            var lstItems = new List<CharacterTreeItemData>();
            if (objNodes == null) return lstItems;

            var dicLocations = new Dictionary<string, CharacterTreeItemData>(StringComparer.Ordinal);
            foreach (string strLocation in GearLocations)
            {
                var objLocationNode = new CharacterTreeItemData(strLocation, "Gear location");
                dicLocations.Add(strLocation, objLocationNode);
                lstItems.Add(objLocationNode);
            }

            int intNextId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                var objItem = ReadGearTreeItem(objNode, ref intNextId);
                string strLocation = GetValue(objNode, "location", string.Empty);
                objItem.SetLocation(strLocation);
                if (!string.IsNullOrEmpty(strLocation) && dicLocations.TryGetValue(strLocation, out var objLocationNode2))
                    objLocationNode2.Children.Add(objItem);
                else
                    lstItems.Add(objItem);
            }
            return lstItems;
        }

        // Assigns GearId in the same depth-first order EnumerateGearNodesDfs walks the raw XML in,
        // so an ID handed back from the UI always resolves to the same node via GetGearNodeById.
        private static Dictionary<string, string>? _dicGearNameTranslations;
        private static string? _strGearTranslationsLanguage;

        // Cached per-language, same "<translate> child element" mechanism the Avalonia gear
        // picker (GearDialogViewModel) uses - keeps the Ausrüstung tree's names in sync with
        // whatever language pack the user has selected, instead of always showing the raw
        // (English) name that's actually saved in the character file.
        private static string GetGearTranslatedName(string strName)
        {
            string strLanguage = GlobalOptions.Instance.Language;
            if (_dicGearNameTranslations == null || _strGearTranslationsLanguage != strLanguage)
            {
                _dicGearNameTranslations = BuildGearNameTranslations();
                _strGearTranslationsLanguage = strLanguage;
            }
            return _dicGearNameTranslations.TryGetValue(strName, out string? strTranslate) ? strTranslate : strName;
        }

        private static Dictionary<string, bool>? _dicSkillDefaulting;

        // Ported from clsUnique.cs's Skill.Default: only used for the Rating-0 "defaulting" pool
        // (Attribute - 1), cross-referenced from skills.xml since the character file itself
        // doesn't save whether a Skill allows it.
        private static bool SkillAllowsDefaulting(string strName)
        {
            if (_dicSkillDefaulting == null)
                _dicSkillDefaulting = BuildSkillDefaultingLookup();
            return _dicSkillDefaulting.TryGetValue(strName, out bool blnDefault) && blnDefault;
        }

        private static Dictionary<string, bool> BuildSkillDefaultingLookup()
        {
            var dicResult = new Dictionary<string, bool>();
            XmlDocument objDocument = XmlManager.Instance.Load("skills.xml");
            XmlNodeList? objNodes = objDocument.SelectNodes("/chummer/skills/skill");
            if (objNodes == null) return dicResult;

            foreach (XmlNode objNode in objNodes)
            {
                string strName = objNode["name"]?.InnerText ?? string.Empty;
                if (strName.Length == 0) continue;
                dicResult[strName] = objNode["default"]?.InnerText == "Yes";
            }

            return dicResult;
        }

        private static Dictionary<string, string> BuildGearNameTranslations()
        {
            var dicResult = new Dictionary<string, string>();
            XmlDocument objDocument = XmlManager.Instance.Load("gear.xml");
            XmlNodeList? objNodes = objDocument.SelectNodes("/chummer/gears/gear");
            if (objNodes == null) return dicResult;

            foreach (XmlNode objNode in objNodes)
            {
                string strName = objNode["name"]?.InnerText ?? string.Empty;
                string? strTranslate = objNode["translate"]?.InnerText;
                if (!string.IsNullOrEmpty(strName) && !string.IsNullOrEmpty(strTranslate))
                    dicResult[strName] = strTranslate!;
            }
            return dicResult;
        }

        private CharacterTreeItemData ReadGearTreeItem(XmlNode objNode, ref int intNextId)
        {
            var objItem = ReadTreeItem(objNode);
            objItem.SetTranslatedName(GetGearTranslatedName(objItem.Name));
            objItem.SetGearId(intNextId);
            objItem.SetGearDetails(GetValue(objNode, "capacity", string.Empty), GetValue(objNode, "response", string.Empty),
                GetValue(objNode, "signal", string.Empty), GetValue(objNode, "system", string.Empty),
                GetValue(objNode, "firewall", string.Empty), GetValue(objNode, "active", "False") == "True");
            intNextId++;

            var objChildren = objNode.SelectNodes("children/gear");
            if (objChildren == null) return objItem;

            foreach (XmlNode objChild in objChildren)
                objItem.Children.Add(ReadGearTreeItem(objChild, ref intNextId));
            return objItem;
        }

        // Cyberware and bioware are saved to the same <cyberwares> list and only distinguished by
        // <improvementsource> ("Cyberware" vs "Bioware") - split here so each gets its own tree.
        // Both read through the same ID-assigning walk (see ReadCyberwareOrBiowareTree) so a
        // CyberwareId handed back from either tree's UI always resolves to the same node via
        // GetCyberwareNodeById, matching the Gear tree's GearId/MoveGear pattern.
        public IReadOnlyList<CharacterTreeItemData> Cyberware => ReadCyberwareOrBiowareTree(blnBioware: false);

        public IReadOnlyList<CharacterTreeItemData> Bioware => ReadCyberwareOrBiowareTree(blnBioware: true);

        private IReadOnlyList<CharacterTreeItemData> ReadCyberwareOrBiowareTree(bool blnBioware)
        {
            var lstItems = new List<CharacterTreeItemData>();
            var objAllNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objAllNodes == null) return lstItems;

            int intNextId = 0;
            foreach (XmlNode objNode in objAllNodes)
            {
                bool blnIsBioware = GetValue(objNode, "improvementsource", string.Empty) == "Bioware";
                CharacterTreeItemData objItem = ReadCyberwareTreeItem(objNode, ref intNextId);
                if (blnIsBioware == blnBioware)
                    lstItems.Add(objItem);
            }
            return lstItems;
        }

        private CharacterTreeItemData ReadCyberwareTreeItem(XmlNode objNode, ref int intNextId)
        {
            var objItem = ReadTreeItem(objNode);
            objItem.SetCyberwareId(intNextId);
            intNextId++;

            var objChildren = objNode.SelectNodes("children/cyberware");
            if (objChildren == null) return objItem;
            foreach (XmlNode objChild in objChildren)
                objItem.Children.Add(ReadCyberwareTreeItem(objChild, ref intNextId));
            return objItem;
        }

        private XmlNode? GetCyberwareNodeById(int intCyberwareId)
        {
            var objTopNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objTopNodes == null) return null;

            int intCurrentId = 0;
            foreach (XmlNode objNode in objTopNodes)
            {
                XmlNode? objFound = FindCyberwareNodeById(objNode, intCyberwareId, ref intCurrentId);
                if (objFound != null) return objFound;
            }
            return null;
        }

        private XmlNode? FindCyberwareNodeById(XmlNode objNode, int intTargetId, ref int intCurrentId)
        {
            if (intCurrentId == intTargetId) return objNode;
            intCurrentId++;

            var objChildren = objNode.SelectNodes("children/cyberware");
            if (objChildren == null) return null;
            foreach (XmlNode objChild in objChildren)
            {
                XmlNode? objFound = FindCyberwareNodeById(objChild, intTargetId, ref intCurrentId);
                if (objFound != null) return objFound;
            }
            return null;
        }

        /// <summary>Moves a Cyberware/Bioware item within the &lt;cyberwares&gt; tree - either
        /// reordering it among its current siblings (inserted immediately before <paramref
        /// name="intTargetCyberwareId"/>) or, with <paramref name="blnReparent"/>, making it a
        /// child of the target instead. Same shape as <see cref="MoveGear"/> for the Gear tree -
        /// CyberwareIds are depth-first positions recomputed on every read, so callers must reload
        /// the tree after a successful move before issuing another one.</summary>
        public bool MoveCyberware(int intSourceCyberwareId, int intTargetCyberwareId, bool blnReparent)
        {
            XmlNode? objSource = GetCyberwareNodeById(intSourceCyberwareId);
            XmlNode? objTarget = GetCyberwareNodeById(intTargetCyberwareId);
            if (objSource == null || objTarget == null || objSource == objTarget || objSource.ParentNode == null)
                return false;

            // Refuse to move an item into its own subtree - see MoveGear's identical guard.
            for (XmlNode? objCursor = objTarget; objCursor != null; objCursor = objCursor.ParentNode)
                if (objCursor == objSource)
                    return false;

            objSource.ParentNode.RemoveChild(objSource);

            if (blnReparent)
            {
                XmlNode? objChildren = objTarget.SelectSingleNode("children");
                if (objChildren == null)
                {
                    objChildren = Document.CreateElement("children");
                    objTarget.AppendChild(objChildren);
                }
                objChildren.AppendChild(objSource);
            }
            else
            {
                if (objTarget.ParentNode == null)
                    return false;
                objTarget.ParentNode.InsertBefore(objSource, objTarget);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Armor is represented as a tree so installed armor modifications and optional
        /// saved armor sets remain visible instead of being flattened into a list.</summary>
        public IReadOnlyList<CharacterTreeItemData> Armor => ReadArmorTree();

        /// <summary>Persisted armor-bundle names, including empty bundles.</summary>
        public IReadOnlyList<string> ArmorSets => (IReadOnlyList<string>?)Document.SelectNodes("/character/armorbundles/armorbundle")?
            .Cast<XmlNode>().Select(objNode => objNode.InnerText).Where(strName => !string.IsNullOrWhiteSpace(strName))
            .Distinct(StringComparer.Ordinal).ToList() ?? Array.Empty<string>();

        /// <summary>Armor encumbrance penalty (a negative dice-pool modifier, 0 if under threshold),
        /// ported from clsCharacter.cs's BallisticArmorEncumbrance/ImpactArmorEncumbrance. Covers the
        /// vanilla rule (BOD*2, or *3 if any worn armor is Military Grade, Form-Fitting counted at
        /// half rating), the SoftWeave Improvement's STR-based reduction, the ArmorEncumbrancePenalty
        /// Improvement, and all three optional house rules (IgnoreArmorEncumbrance,
        /// AlternateArmorEncumbrance's BOD+STR threshold, NoSingleArmorEncumbrance - Helmets and
        /// Shields/SecureTech PPP System don't count as a "piece" for that last rule, matching
        /// clsCharacter.cs). Not yet ported: ArmorMod bonuses to ballistic/impact rating (base
        /// &lt;b&gt;/&lt;i&gt; values only).</summary>
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

        /// <summary>Dice-pool penalty from filled Physical/Stun condition-monitor boxes. SR4 applies
        /// -1 for every three filled boxes on each track; ConditionMonitor Improvements then adjust
        /// that result (for example, wound-penalty mitigation).</summary>
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

        /// <summary>Matrix Initiative, ported from clsCharacter.cs. Covers all legacy branches:
        /// legacy branches:
        ///  - A.I./technocritter/protosapient: INT + Response (checked first - it overrides
        ///    everything else, same order as the legacy version).
        ///  - Technomancer (and not A.I.): (INT x 2) + 1 + LivingPersonaResponse Improvements.
        ///  - Sprite: fixed INI metatype minimum (overrides the normal/Technomancer branches but
        ///    is itself overridden by the A.I. branch, matching the legacy evaluation order).
        ///  - Otherwise: INT + active Commlink's Response + MatrixInitiative Improvements (the
        ///    default human/non-awakened path) - see ActiveCommlinkResponse's doc comment for its
        ///    scoped-down Gear search.
        /// NOT ported: the TechnomancerAllowCommlink house rule
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
                else if (IsSprite)
                {
                    intBase = GetAttributeMinimum("INI");
                    sb.Append("Sprite-Metatype-Initiative: ").Append(intBase);
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

        /// <summary>Career mode: raises an active skill's rating by one, deducting Karma. False if
        /// not enough Karma, or the skill is currently grouped (raise the skill group instead).</summary>
        public bool RaiseActiveSkill(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True")
                return false;

            var objOptions = GetCharacterOptions();
            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            int intCost = intRating == 0
                ? objOptions.KarmaNewActiveSkill
                : (intRating + 1) * objOptions.KarmaImproveActiveSkill * (intRating >= 6 ? 2 : 1);

            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intCost > intKarma)
                return false;

            string strName = GetValue(objNode, "name", string.Empty);
            SetChildValue(objNode, "rating", (intRating + 1).ToString());
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveSkill, strName);
            AddExpense("Karma", -intCost, strName + " " + intRating + " -> " + (intRating + 1), null, objUndo);
            return true;
        }

        /// <summary>Create mode: sets an active skill's rating directly. False if the skill is
        /// currently grouped (set the group's rating instead).</summary>
        public bool SetActiveSkillRating(int intSkillId, int intRating)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True")
                return false;

            SetChildValue(objNode, "rating", intRating.ToString());
            return true;
        }

        /// <summary>Create mode: raises an active skill's rating by one, deducting from the Karma
        /// or BP pool depending on <see cref="BuildMethod"/>. False if not enough points, the skill
        /// is grouped, or the skill is already at its ratingmax.</summary>
        public bool RaiseActiveSkillCreate(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            int intRatingMax = int.TryParse(GetValue(objNode, "ratingmax", "6"), out var rm) ? rm : 6;
            if (intRating >= intRatingMax)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);

            if (blnKarmaBuild)
            {
                int intCost = intRating == 0
                    ? objOptions.KarmaNewActiveSkill
                    : (intRating + 1) * objOptions.KarmaImproveActiveSkill * (intRating >= 6 ? 2 : 1);
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intCost > intKarma)
                    return false;
                Karma = (intKarma - intCost).ToString();
            }
            else
            {
                int intCost = objOptions.BpActiveSkill;
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intCost > intBp)
                    return false;
                Bp = (intBp - intCost).ToString();
            }

            SetChildValue(objNode, "rating", (intRating + 1).ToString());
            return true;
        }

        /// <summary>Create mode: lowers an active skill's rating by one, refunding the Karma or BP
        /// that was spent to reach the current rank.</summary>
        public bool LowerActiveSkillCreate(int intSkillId)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null || GetValue(objNode, "grouped", "False") == "True")
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (intRating <= 0)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPreviousRating = intRating - 1;

            if (blnKarmaBuild)
            {
                int intRefund = intPreviousRating == 0
                    ? objOptions.KarmaNewActiveSkill
                    : intRating * objOptions.KarmaImproveActiveSkill * (intPreviousRating >= 6 ? 2 : 1);
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + intRefund).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + objOptions.BpActiveSkill).ToString();
            }

            SetChildValue(objNode, "rating", intPreviousRating.ToString());
            return true;
        }

        /// <summary>Whether a still-unlocked (grouped=False) skill group is safe to raise/set as a
        /// whole: true if every member skill already shares the same individual rating (including
        /// trivially true for a group with no member skills yet, or one already at its own stored
        /// rating). <paramref name="intCommonRating"/> is that shared rating when true.</summary>
        private bool CanRaiseSkillGroupAsAWhole(string strGroupName, int intGroupRating, out int intCommonRating)
        {
            intCommonRating = intGroupRating;
            bool blnFirst = true;
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null)
                return true;

            foreach (XmlNode objSkillNode in objNodes)
            {
                if (GetValue(objSkillNode, "knowledge", "False") == "True"
                    || GetValue(objSkillNode, "skillgroup", string.Empty) != strGroupName)
                    continue;

                int intRating = int.TryParse(GetValue(objSkillNode, "rating", "0"), out var r) ? r : 0;
                if (blnFirst)
                {
                    intCommonRating = intRating;
                    blnFirst = false;
                }
                else if (intRating != intCommonRating)
                {
                    return false;
                }
            }

            // A group whose member skills already agree with each other but not with the group's
            // own stored rating has been raised individually while ungrouped - only resumable as a
            // group (adopting that shared rating as the new baseline) with AllowSkillRegrouping.
            return intCommonRating == intGroupRating || GetCharacterOptions().AllowSkillRegrouping;
        }

        /// <summary>Career mode: raises a skill group's rating by one, deducting Karma, and keeps
        /// every grouped member skill's own rating in sync with the new group rating. False (in
        /// addition to insufficient Karma) if member skills have already diverged from each other,
        /// or from the group's own rating without <see cref="Options.AllowSkillRegrouping"/> - see
        /// <see cref="CanRaiseSkillGroupAsAWhole"/>.</summary>
        public bool RaiseSkillGroup(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null)
                return false;

            var objOptions = GetCharacterOptions();
            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intRating, out int intCommonRating))
                return false;
            intRating = intCommonRating;
            int intCost = intRating == 0 ? objOptions.KarmaNewSkillGroup : (intRating + 1) * objOptions.KarmaImproveSkillGroup;

            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intCost > intKarma)
                return false;

            int intNewRating = intRating + 1;
            SetChildValue(objNode, "rating", intNewRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intNewRating);
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveSkillGroup, strGroupName);
            AddExpense("Karma", -intCost, strGroupName + " " + intRating + " -> " + intNewRating, null, objUndo);
            return true;
        }

        /// <summary>Create mode: sets a skill group's rating directly and syncs member skills. False
        /// if member skills have already diverged - see <see cref="CanRaiseSkillGroupAsAWhole"/>.</summary>
        public bool SetSkillGroupRating(string strGroupName, int intRating)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null)
                return false;

            int intCurrentRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intCurrentRating, out _))
                return false;

            SetChildValue(objNode, "rating", intRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intRating);
            return true;
        }

        /// <summary>Create mode: raises a skill group's rating by one, deducting from the Karma or
        /// BP pool depending on <see cref="BuildMethod"/>, and syncs member skills. False if member
        /// skills have already diverged - see <see cref="CanRaiseSkillGroupAsAWhole"/>.</summary>
        public bool RaiseSkillGroupCreate(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null)
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            // Skill groups may only be raised to 4 during character creation - the 5/6 ranks
            // are career-mode-only (frmCreate.cs caps nudActiveSkillGroup at 4).
            if (intRating >= 4)
                return false;
            if (!CanRaiseSkillGroupAsAWhole(strGroupName, intRating, out int intCommonRating))
                return false;
            intRating = intCommonRating;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);

            if (blnKarmaBuild)
            {
                int intCost = intRating == 0 ? objOptions.KarmaNewSkillGroup : (intRating + 1) * objOptions.KarmaImproveSkillGroup;
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intCost > intKarma)
                    return false;
                Karma = (intKarma - intCost).ToString();
            }
            else
            {
                int intCost = objOptions.BpSkillGroup;
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intCost > intBp)
                    return false;
                Bp = (intBp - intCost).ToString();
            }

            int intNewRating = intRating + 1;
            SetChildValue(objNode, "rating", intNewRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intNewRating);
            return true;
        }

        /// <summary>Create mode: lowers a skill group's rating by one, refunding the Karma or BP
        /// that was spent to reach the current rank, and syncs member skills.</summary>
        public bool LowerSkillGroupCreate(string strGroupName)
        {
            XmlNode? objNode = GetSkillGroupNode(strGroupName);
            if (objNode == null)
                return false;

            int intRating = int.TryParse(GetValue(objNode, "rating", "0"), out var r) ? r : 0;
            if (intRating <= 0)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intPreviousRating = intRating - 1;

            if (blnKarmaBuild)
            {
                int intRefund = intPreviousRating == 0 ? objOptions.KarmaNewSkillGroup : intRating * objOptions.KarmaImproveSkillGroup;
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + intRefund).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + objOptions.BpSkillGroup).ToString();
            }

            SetChildValue(objNode, "rating", intPreviousRating.ToString());
            SyncGroupedSkillRatings(strGroupName, intPreviousRating);
            return true;
        }

        /// <summary>Keeps every skill in <paramref name="strGroupName"/> locked to the group's own
        /// rating and cost. Once the group has a rating above 0, its member skills are marked
        /// "grouped" (locking them - see <see cref="RaiseActiveSkillCreate"/>/<see cref="RaiseActiveSkill"/>)
        /// and their own rating is overwritten to match, so the group's cost is what governs them
        /// and any individual skill cost no longer applies. Dropping the group back to 0 unlocks them.</summary>
        private void SyncGroupedSkillRatings(string strGroupName, int intRating)
        {
            var objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null) return;
            foreach (XmlNode objSkillNode in objNodes)
            {
                if (GetValue(objSkillNode, "skillgroup", string.Empty) != strGroupName)
                    continue;

                SetChildValue(objSkillNode, "grouped", intRating > 0 ? "True" : "False");
                SetChildValue(objSkillNode, "rating", intRating.ToString());
            }
        }

        /// <summary>Create mode: sets an active skill's specialization directly, no Karma cost.</summary>
        public bool SetActiveSkillSpecialization(int intSkillId, string strSpecialization)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "spec", strSpecialization);
            return true;
        }

        /// <summary>Career mode: sets an active skill's specialization, deducting the flat
        /// KarmaSpecialization cost. False if not enough Karma.</summary>
        public bool AddActiveSkillSpecialization(int intSkillId, string strSpecialization)
        {
            XmlNode? objNode = GetActiveSkillNode(intSkillId);
            if (objNode == null)
                return false;

            var objOptions = GetCharacterOptions();
            int intCost = objOptions.KarmaSpecialization;
            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intCost > intKarma)
                return false;

            string strName = GetValue(objNode, "name", string.Empty);
            SetChildValue(objNode, "spec", strSpecialization);
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.SkillSpec, strName);
            AddExpense("Karma", -intCost, strName + " -> " + strSpecialization, null, objUndo);
            return true;
        }

        /// <summary>Adds a new Exotic Active Skill (e.g. "Exotic Ranged Weapon (Bow)" - the
        /// specialization holds the "(Bow)" sub-type). Starts at rating 0; the first point costs
        /// KarmaNewActiveSkill like any other new active skill when raised.</summary>
        public void AddExoticSkill(string strName, string strSpecialization, string strCategory, string strAttribute)
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
            AppendElement(objSkill, "name", strName.Trim());
            AppendElement(objSkill, "skillgroup", string.Empty);
            AppendElement(objSkill, "skillcategory", strCategory);
            AppendElement(objSkill, "grouped", "False");
            AppendElement(objSkill, "default", "False");
            AppendElement(objSkill, "rating", "0");
            AppendElement(objSkill, "ratingmax", "6");
            AppendElement(objSkill, "knowledge", "False");
            AppendElement(objSkill, "exotic", "True");
            AppendElement(objSkill, "spec", strSpecialization.Trim());
            AppendElement(objSkill, "allowdelete", "True");
            AppendElement(objSkill, "attribute", strAttribute);
            AppendElement(objSkill, "totalvalue", "0");
            objSkills.AppendChild(objSkill);
        }

        private XmlNode? GetActiveSkillNode(int intSkillId)
        {
            XmlNodeList? objNodes = Document.SelectNodes("/character/skills/skill");
            if (objNodes == null || intSkillId < 0 || intSkillId >= objNodes.Count)
                return null;

            XmlNode objNode = objNodes[intSkillId]!;
            return GetValue(objNode, "knowledge", "False") == "False" ? objNode : null;
        }

        private XmlNode? GetSkillGroupNode(string strGroupName)
            => Document.SelectSingleNode("/character/skillgroups/skillgroup[name = '" + strGroupName + "']");

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

        /// <summary>Pets are persisted as Contact entries with <c>type=Pet</c>, matching the legacy app.</summary>
        public IReadOnlyList<CharacterContactData> Pets => ReadPets();

        public void AddContact(string strName, string strConnection, string strLoyalty, bool blnEnemy,
            string strType = "")
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
            AppendElement(objContact, "type", string.IsNullOrEmpty(strType) ? (blnEnemy ? "Enemy" : "Contact") : strType);
            AppendElement(objContact, "file", string.Empty);
            AppendElement(objContact, "notes", string.Empty);
            AppendElement(objContact, "groupname", string.Empty);
            AppendElement(objContact, "colour", "0");
            AppendElement(objContact, "free", "False");
            objContacts.AppendChild(objContact);
            Changed?.Invoke();
        }

        /// <summary>Adds a Pet contact, the same representation used by the legacy PetControl.</summary>
        public void AddPet(string strName) => AddContact(strName, "0", "0", blnEnemy: false, strType: "Pet");

        public bool UpdateContact(int intContactId, string strName, string strConnection, string strLoyalty)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "name", strName);
            SetChildValue(objNode, "connection", strConnection);
            SetChildValue(objNode, "loyalty", strLoyalty);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveContact(int intContactId)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode?.ParentNode == null)
                return false;

            objNode.ParentNode.RemoveChild(objNode);
            Changed?.Invoke();
            return true;
        }

        public bool UpdateContactNotes(int intContactId, string strNotes)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "notes", strNotes);
            return true;
        }

        /// <summary>Associates a Contact/Pet with another saved character file.</summary>
        public bool UpdateContactFile(int intContactId, string strFileName, string strRelativeFileName)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "file", strFileName);
            SetChildValue(objNode, "relative", strRelativeFileName);
            Changed?.Invoke();
            return true;
        }

        public bool SetContactFree(int intContactId, bool blnFree)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "free", blnFree ? "True" : "False");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sets a Group contact's profession label and the four modifiers that sum into
        /// its Group Rating - ported from frmSelectContactConnection.cs.</summary>
        public bool UpdateContactGroup(int intContactId, string strGroupName, int intMembership,
            int intAreaOfInfluence, int intMagicalResources, int intMatrixResources)
        {
            XmlNode? objNode = GetContactNode(intContactId);
            if (objNode == null)
                return false;

            SetChildValue(objNode, "groupname", strGroupName);
            SetChildValue(objNode, "membership", intMembership.ToString());
            SetChildValue(objNode, "areaofinfluence", intAreaOfInfluence.ToString());
            SetChildValue(objNode, "magicalresources", intMagicalResources.ToString());
            SetChildValue(objNode, "matrixresources", intMatrixResources.ToString());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Karma/BP spent at chargen on Contacts, minus what Enemies refund, after the
        /// FreeContacts (CHA x multiplier) and FreeContactsFlat house rules - ported from
        /// frmCreate.cs. This is purely informational (unlike attribute/skill Karma, legacy doesn't
        /// deduct it from the Karma pool directly - it only feeds the BP/Karma summary panel).</summary>
        public int ContactPointsUsed
        {
            get
            {
                var objOptions = GetCharacterOptions();
                bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
                int intRate = blnKarmaBuild ? objOptions.KarmaContact : objOptions.BpContact;

                int intUsed = Contacts.Where(c => !c.Free)
                    .Sum(c => (ParseInt(c.Connection) + c.GroupRating + ParseInt(c.Loyalty)) * intRate);
                int intRefund = Enemies.Where(c => !c.Free)
                    .Sum(c => (ParseInt(c.Connection) + c.GroupRating + ParseInt(c.Loyalty)) * intRate);
                intUsed -= intRefund;

                if (objOptions.FreeContacts)
                {
                    int intFreePoints = GetAttributeInt("CHA") * objOptions.FreeContactsMultiplier;
                    if (blnKarmaBuild)
                        intFreePoints *= objOptions.KarmaContact;
                    intUsed = Math.Max(0, intUsed - intFreePoints);
                }

                if (objOptions.FreeContactsFlat)
                {
                    int intFreePoints = objOptions.FreeContactsFlatNumber;
                    if (blnKarmaBuild)
                        intFreePoints *= objOptions.KarmaContact;
                    intUsed = Math.Max(0, intUsed - intFreePoints);
                }

                return intUsed;
            }
        }

        private static int ParseInt(string strValue) => int.TryParse(strValue, out var intValue) ? intValue : 0;

        public IReadOnlyList<CharacterMartialArtData> MartialArts => ReadMartialArts();

        public IReadOnlyList<CharacterMartialArtManeuverData> MartialArtManeuvers => ReadMartialArtManeuvers();

        /// <summary>Ported from frmSelectMartialArt.cs: adds the Martial Art with its full set of
        /// rules-data advantages snapshotted in (matches how ReadMartialArts expects to find them
        /// nested under martialartadvantages, not re-resolved from martialarts.xml every load).</summary>
        public void AddMartialArt(string strName, IReadOnlyList<string> lstAdvantages, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A martial art name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objMartialArts = objRoot.SelectSingleNode("martialarts");
            if (objMartialArts == null)
            {
                objMartialArts = Document.CreateElement("martialarts");
                objRoot.AppendChild(objMartialArts);
            }

            var objMartialArt = Document.CreateElement("martialart");
            AppendElement(objMartialArt, "name", strName.Trim());
            AppendElement(objMartialArt, "rating", "1");
            AppendElement(objMartialArt, "source", strSource);
            AppendElement(objMartialArt, "page", strPage);
            var objAdvantages = Document.CreateElement("martialartadvantages");
            foreach (string strAdvantage in lstAdvantages)
            {
                var objAdvantage = Document.CreateElement("martialartadvantage");
                AppendElement(objAdvantage, "guid", Guid.NewGuid().ToString());
                AppendElement(objAdvantage, "name", strAdvantage);
                objAdvantages.AppendChild(objAdvantage);
            }
            objMartialArt.AppendChild(objAdvantages);
            objMartialArts.AppendChild(objMartialArt);
            Changed?.Invoke();
        }

        /// <summary>Ported from frmSelectMartialArt.cs's Maneuver tab.</summary>
        public void AddMartialArtManeuver(string strName, string strSource, string strPage)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A maneuver name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objManeuvers = objRoot.SelectSingleNode("martialartmaneuvers");
            if (objManeuvers == null)
            {
                objManeuvers = Document.CreateElement("martialartmaneuvers");
                objRoot.AppendChild(objManeuvers);
            }

            var objManeuver = Document.CreateElement("martialartmaneuver");
            AppendElement(objManeuver, "guid", Guid.NewGuid().ToString());
            AppendElement(objManeuver, "name", strName.Trim());
            AppendElement(objManeuver, "source", strSource);
            AppendElement(objManeuver, "page", strPage);
            objManeuvers.AppendChild(objManeuver);
            Changed?.Invoke();
        }

        /// <summary>Removes the first saved Martial Art matching its name (and any nested
        /// martialartadvantage entries with it).</summary>
        public bool RemoveMartialArt(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/martialarts/martialart");
            if (objNodes == null)
                return false;

            foreach (XmlNode objMartialArt in objNodes)
            {
                if (!string.Equals(GetValue(objMartialArt, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                objMartialArt.ParentNode?.RemoveChild(objMartialArt);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Removes the first saved Martial Art Maneuver matching its name.</summary>
        public bool RemoveMartialArtManeuver(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/martialartmaneuvers/martialartmaneuver");
            if (objNodes == null)
                return false;

            foreach (XmlNode objManeuver in objNodes)
            {
                if (!string.Equals(GetValue(objManeuver, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                objManeuver.ParentNode?.RemoveChild(objManeuver);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        public IReadOnlyList<CharacterPowerData> AdeptPowers => ReadAdeptPowers();

        /// <summary>Ported from frmSelectPower.cs's cmdOK_Click. Also applies the power's own
        /// rules-data &lt;bonus&gt; block (see <see cref="ApplyBonus"/>) at the power's Rating,
        /// matching legacy's CreateImprovements call on add - including &lt;selectskill&gt;/
        /// &lt;selectattribute&gt; (see <see cref="GetAdeptPowerSkillSelectionOptions"/>/<see
        /// cref="GetAdeptPowerAttributeSelectionOptions"/>), whose player-picked
        /// <paramref name="strSelected"/> becomes the corresponding Improvement. Note:
        /// &lt;selectsenseware&gt; (Improved Sense) isn't a <see cref="BonusApplier"/>-covered
        /// node - see <see cref="AddImprovedSensePower"/> for that dedicated flow.</summary>
        public void AddAdeptPower(string strName, string strRating, string strPointsPerLevel, string strSelected = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A power name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objPowers = objRoot.SelectSingleNode("powers");
            if (objPowers == null)
            {
                objPowers = Document.CreateElement("powers");
                objRoot.AppendChild(objPowers);
            }

            var objPower = Document.CreateElement("power");
            AppendElement(objPower, "name", strName.Trim());
            AppendElement(objPower, "extra", strSelected.Trim());
            AppendElement(objPower, "rating", strRating);
            AppendElement(objPower, "pointsperlevel", strPointsPerLevel);
            AppendElement(objPower, "discounted", "False");
            AppendElement(objPower, "discountedgeas", "False");
            objPowers.AppendChild(objPower);

            XmlDocument objPowersDoc = XmlManager.Instance.Load("powers.xml");
            XmlNode? objXmlPower = objPowersDoc.SelectSingleNode($"/chummer/powers/power[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlPower?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.Power, strName.Trim(), strRating);
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.Power, strName.Trim(), strSelected, strRating);

            Changed?.Invoke();
        }

        /// <summary>One senseware item ("Improved Sense" Adept Power etc.) the player can pick
        /// from - <see cref="SourceFile"/> lets the caller re-resolve the item's own rules-data
        /// node (cyberware.xml/bioware.xml/gear.xml) when the choice is applied.</summary>
        public sealed record SenseImprovementOption(string Name, string DisplayName, string SourceFile);

        /// <summary>Ported from clsImprovement.cs's AddSensewareSource/the selectsenseware picker
        /// setup: given an Adept Power whose rules-data &lt;bonus&gt; is a &lt;selectsenseware&gt;
        /// node (e.g. "Improved Sense"), lists every Cyberware/Bioware/Gear item in the mount's
        /// requested categories, filtered to &lt;senseimprovement&gt;yes&lt;/senseimprovement&gt;
        /// items when the node's requiresenseimprovement="yes". Empty if the power has no such
        /// bonus.</summary>
        public IReadOnlyList<SenseImprovementOption> GetSenseImprovementOptions(string strPowerName)
        {
            XmlNode? objXmlSelectSenseware = FindSelectSensewareNode(strPowerName);
            if (objXmlSelectSenseware == null)
                return Array.Empty<SenseImprovementOption>();

            bool blnRequireSenseImprovement = objXmlSelectSenseware.Attributes?["requiresenseimprovement"]?.InnerText == "yes";
            var lstOptions = new List<SenseImprovementOption>();
            var setSeenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (strAttribute, strFile, strBasePath) in new[]
                     {
                         ("cyberwarecategory", "cyberware.xml", "/chummer/cyberwares/cyberware"),
                         ("biowarecategory", "bioware.xml", "/chummer/biowares/bioware"),
                         ("gearcategory", "gear.xml", "/chummer/gears/gear"),
                     })
            {
                string? strCategories = objXmlSelectSenseware.Attributes?[strAttribute]?.InnerText;
                if (string.IsNullOrEmpty(strCategories))
                    continue;

                var setCategories = new HashSet<string>(strCategories.Split(','), StringComparer.Ordinal);
                XmlDocument objSourceDoc = XmlManager.Instance.Load(strFile);
                XmlNodeList? objNodes = objSourceDoc.SelectNodes(strBasePath);
                if (objNodes == null)
                    continue;

                foreach (XmlNode objXmlItem in objNodes)
                {
                    if (!setCategories.Contains(GetValue(objXmlItem, "category", string.Empty)))
                        continue;
                    if (blnRequireSenseImprovement && GetValue(objXmlItem, "senseimprovement", "no") != "yes")
                        continue;

                    string strName = GetValue(objXmlItem, "name", string.Empty);
                    if (string.IsNullOrEmpty(strName) || !setSeenNames.Add(strName))
                        continue;

                    lstOptions.Add(new SenseImprovementOption(strName,
                        GetValue(objXmlItem, "translate", strName), strFile));
                }
            }

            lstOptions.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return lstOptions;
        }

        /// <summary>Adds an Adept Power that grants a selectsenseware bonus (e.g. "Improved
        /// Sense"), then applies <paramref name="strSelectedSenseware"/>'s own rules-data
        /// &lt;bonus&gt; block - ported from clsImprovement.cs's selectsenseware handler.
        /// <paramref name="strSelectedSenseware"/> must be one of <see
        /// cref="GetSenseImprovementOptions"/>'s <see cref="SenseImprovementOption.Name"/> values.
        /// The applied Rating is 1, or the selected item's own &lt;rating&gt; under the
        /// ImprovedSenseFullRating house rule.</summary>
        public bool AddImprovedSensePower(string strName, string strRating, string strPointsPerLevel,
            string strSelectedSenseware)
        {
            XmlNode? objXmlSelectSenseware = FindSelectSensewareNode(strName);
            if (objXmlSelectSenseware == null || string.IsNullOrWhiteSpace(strSelectedSenseware))
                return false;

            XmlNode? objXmlSelected = GetSenseImprovementOptions(strName)
                .Where(o => o.Name == strSelectedSenseware)
                .Select(o => XmlManager.Instance.Load(o.SourceFile)
                    .SelectSingleNode($"/chummer/*/*[name = '{strSelectedSenseware}']"))
                .FirstOrDefault(n => n != null);
            if (objXmlSelected == null)
                return false;

            AddAdeptPower(strName, strRating, strPointsPerLevel);

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objImprovements = objRoot.SelectSingleNode("improvements") as XmlElement
                ?? (XmlElement)objRoot.AppendChild(Document.CreateElement("improvements"));
            var objSelection = Document.CreateElement("improvement");
            AppendElement(objSelection, "improvementttype", ImprovementType.SelectSenseware.ToString());
            AppendElement(objSelection, "improvedname", strSelectedSenseware);
            AppendElement(objSelection, "sourcename", strName.Trim());
            AppendElement(objSelection, "val", "0");
            AppendElement(objSelection, "rating", "1");
            AppendElement(objSelection, "min", "0");
            AppendElement(objSelection, "max", "0");
            AppendElement(objSelection, "aug", "0");
            AppendElement(objSelection, "augmax", "0");
            AppendElement(objSelection, "unique", string.Empty);
            AppendElement(objSelection, "improvementsource", ImprovementSource.Power.ToString());
            AppendElement(objSelection, "addtorating", "False");
            AppendElement(objSelection, "enabled", "True");
            AppendElement(objSelection, "custom", "False");
            objImprovements.AppendChild(objSelection);

            string strSensewareRating = GetCharacterOptions().ImprovedSenseFullRating
                ? GetValue(objXmlSelected, "rating", "1")
                : "1";
            ApplyBonus(objXmlSelected.SelectSingleNode("bonus"), ImprovementSource.Power, strName.Trim(), strSensewareRating);

            Changed?.Invoke();
            return true;
        }

        private static XmlNode? FindSelectSensewareNode(string strPowerName)
        {
            XmlDocument objPowersDoc = XmlManager.Instance.Load("powers.xml");
            XmlNode? objXmlPower = objPowersDoc.SelectSingleNode($"/chummer/powers/power[name = '{strPowerName.Trim()}']");
            return objXmlPower?.SelectSingleNode("bonus/selectsenseware");
        }

        public bool RemoveAdeptPower(string strName)
        {
            if (string.IsNullOrWhiteSpace(strName))
                return false;

            var objNodes = Document.SelectNodes("/character/powers/power");
            if (objNodes == null)
                return false;

            foreach (XmlNode objPower in objNodes)
            {
                if (!string.Equals(GetValue(objPower, "name", string.Empty), strName.Trim(),
                        StringComparison.Ordinal))
                    continue;

                objPower.ParentNode?.RemoveChild(objPower);
                RemoveBonusImprovements(ImprovementSource.Power, strName.Trim());
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        // Ported from frmCareer.cs/frmCreate.cs's CalculatePowerPoints().
        public CharacterDerivedValueData AdeptPowerPoints
        {
            get
            {
                var decUsed = AdeptPowers.Sum(p =>
                    decimal.TryParse(p.TotalPoints, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m);

                // Mystic Adepts split MAG between the Magician and Adept portions; a pure Adept
                // uses the character's full MAG.
                var intMag = MysticAdept ? MysticAdeptAdeptMagSplit : GetAttributeInt("MAG");
                var lstContributions = ImprovementManager
                    .DescribeAugmentedValueOf(Improvements, ImprovementType.AdeptPowerPoints)
                    .ToList();
                var intTotal = intMag + lstContributions.Sum(c => c.Value);

                var sb = new StringBuilder();
                sb.Append("MAG").Append(MysticAdept ? " (Adept-Anteil)" : string.Empty).Append(": ").Append(intMag);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Verfügbar: ").Append(intTotal);
                sb.Append('\n').Append("Verbraucht: ").Append(decUsed);
                var decRemaining = intTotal - decUsed;
                sb.Append('\n').Append("Übrig: ").Append(decRemaining);
                return new CharacterDerivedValueData((int)decimal.Truncate(decRemaining), sb.ToString());
            }
        }

        /// <summary>Ported from clsMainController.cs's CalculateFreeSpiritPowerPoints: a
        /// player-character Free Spirit's power points come from EDG (or MAG, under the
        /// FreeSpiritPowerPointsMag house rule) plus any FreeSpiritPowerPoints Improvement bonus;
        /// used points are the sum of owned Critter Powers' point costs. Null when the character
        /// isn't a PC Free Spirit (Metatype != "Free Spirit", or a critter rather than a PC -
        /// matches legacy's Metatype/IsCritter gate).</summary>
        public CharacterDerivedValueData? FreeSpiritPowerPoints
        {
            get
            {
                if (!string.Equals(Metatype, "Free Spirit", StringComparison.Ordinal) || IsCritter)
                    return null;

                var decUsed = CritterPowers.Sum(p =>
                    decimal.TryParse(p.Points, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m);

                bool blnUseMag = GetCharacterOptions().FreeSpiritPowerPointsMag;
                var intBase = GetAttributeInt(blnUseMag ? "MAG" : "EDG");
                var lstContributions = ImprovementManager
                    .DescribeAugmentedValueOf(Improvements, ImprovementType.FreeSpiritPowerPoints)
                    .ToList();
                var intTotal = intBase + lstContributions.Sum(c => c.Value);

                var sb = new StringBuilder();
                sb.Append(blnUseMag ? "MAG" : "EDG").Append(": ").Append(intBase);
                AppendContributions(sb, lstContributions);
                sb.Append('\n').Append("Verfügbar: ").Append(intTotal);
                sb.Append('\n').Append("Verbraucht: ").Append(decUsed);
                var decRemaining = intTotal - decUsed;
                sb.Append('\n').Append("Übrig: ").Append(decRemaining);
                return new CharacterDerivedValueData((int)decimal.Truncate(decRemaining), sb.ToString());
            }
        }

        public IReadOnlyList<CharacterSpellData> Spells => ReadSpells();

        public IReadOnlyList<CharacterSpiritData> Spirits => ReadSpirits();

        /// <summary>Ported from frmCareer.cs's cmdAddSpirit_Click, simplified to the fields the
        /// port's Spirits list actually displays - Spirits/Sprites are freely typed (no rules-data
        /// cross-reference like Gear/Cyberware), so this doesn't need a picker dialog.</summary>
        public void AddSpirit(string strName, string strCritterName, string strType, string strForce,
            string strServices, bool blnBound = false)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A spirit name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objSpirits = objRoot.SelectSingleNode("spirits");
            if (objSpirits == null)
            {
                objSpirits = Document.CreateElement("spirits");
                objRoot.AppendChild(objSpirits);
            }

            var objSpirit = Document.CreateElement("spirit");
            AppendElement(objSpirit, "name", strName.Trim());
            AppendElement(objSpirit, "crittername", strCritterName);
            AppendElement(objSpirit, "services", strServices);
            AppendElement(objSpirit, "force", strForce);
            AppendElement(objSpirit, "bound", blnBound ? "True" : "False");
            AppendElement(objSpirit, "type", strType);
            objSpirits.AppendChild(objSpirit);
            Changed?.Invoke();
        }

        /// <summary>Removes the first saved Spirit/Sprite matching name+type+force - legacy has no
        /// stable per-entry identity for Spirits either, so this matches the same
        /// first-occurrence-by-fields approach RemoveLifestyle/RemoveCyberware already use.</summary>
        public bool RemoveSpirit(string strName, string strType, string strForce)
        {
            var objNodes = Document.SelectNodes("/character/spirits/spirit");
            if (objNodes == null) return false;

            foreach (XmlNode objSpirit in objNodes)
            {
                if (!string.Equals(GetValue(objSpirit, "name", string.Empty), strName, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objSpirit, "type", "Spirit"), strType, StringComparison.Ordinal)
                    || !string.Equals(GetValue(objSpirit, "force", "0"), strForce, StringComparison.Ordinal))
                    continue;

                objSpirit.ParentNode?.RemoveChild(objSpirit);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        public IReadOnlyList<CharacterInitiationGradeData> InitiationGrades => ReadInitiationGrades();

        /// <summary>Current Initiate (Magician) or Submersion (Technomancer) Grade - the count of
        /// saved InitiationGrades entries.</summary>
        public int InitiateGrade => InitiationGrades.Count;

        /// <summary>Raises the character's Initiate/Submersion Grade by one, deducting Karma -
        /// ported from frmCareer.cs's cmdImproveInitiation_Click, including the MAG/RES-boosting
        /// Improvement (replaced wholesale each raise, matching legacy's
        /// RemoveImprovements(Initiation, "Initiation") + CreateImprovement pair) and the
        /// Metamagic Improvement refresh (any owned Metamagic whose rules-data &lt;bonus&gt;
        /// references "Rating" gets its Improvements rebuilt at the new Grade as the Rating).</summary>
        public bool RaiseInitiateGrade(bool blnGroup, bool blnOrdeal)
        {
            if (!Magician && !Technomancer)
                return false;

            int intCurrentGrade = InitiateGrade;
            int intMagOrRes = GetAttributeInt(Technomancer ? "RES" : "MAG");
            if (intCurrentGrade + 1 > intMagOrRes)
                return false;

            var objOptions = GetCharacterOptions();
            double dblMultiplier = 1.0;
            if (blnGroup) dblMultiplier -= 0.2;
            if (blnOrdeal) dblMultiplier -= 0.2;
            dblMultiplier = Math.Round(dblMultiplier, 2);
            int intKarmaCost = (int)Math.Ceiling((10 + (intCurrentGrade + 1) * objOptions.KarmaInitiation) * dblMultiplier);

            int intKarma = int.TryParse(Karma, out var k) ? k : 0;
            if (intKarmaCost > intKarma)
                return false;

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objGrades = objRoot.SelectSingleNode("initiationgrades");
            if (objGrades == null)
            {
                objGrades = Document.CreateElement("initiationgrades");
                objRoot.AppendChild(objGrades);
            }

            var objGrade = Document.CreateElement("initiationgrade");
            AppendElement(objGrade, "grade", (intCurrentGrade + 1).ToString());
            AppendElement(objGrade, "group", blnGroup.ToString());
            AppendElement(objGrade, "ordeal", blnOrdeal.ToString());
            AppendElement(objGrade, "res", Technomancer.ToString());
            objGrades.AppendChild(objGrade);

            Karma = (intKarma - intKarmaCost).ToString();

            int intNewGrade = intCurrentGrade + 1;
            RemoveBonusImprovements(ImprovementSource.Initiation, "Initiation");
            AppendImprovement(new ImprovementSpec(ImprovementType.Attribute, Technomancer ? "RES" : "MAG",
                Maximum: intNewGrade), ImprovementSource.Initiation, "Initiation");
            RefreshRatingScaledMetamagicImprovements(intNewGrade);

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveInitiateGrade, (intCurrentGrade + 1).ToString());
            AddExpense("Karma", -intKarmaCost, "Initiate Grade " + intCurrentGrade + " -> " + (intCurrentGrade + 1), null, objUndo);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ported from frmCareer.cs's cmdImproveInitiation_Click's Metamagic-refresh loop:
        /// any owned Metamagic whose rules-data &lt;bonus&gt; XML references "Rating" (a simple
        /// substring check, matching legacy) has its Improvements rebuilt with the new Initiation
        /// Grade as the Rating - e.g. a Metamagic granting "+Rating to some pool" gets stronger as
        /// the character initiates further.</summary>
        private void RefreshRatingScaledMetamagicImprovements(int intNewGrade)
        {
            if (Metamagics.Count == 0)
                return;

            XmlDocument objMetamagicsDoc = XmlManager.Instance.Load("metamagic.xml");
            foreach (CharacterMetamagicData objMetamagic in Metamagics)
            {
                XmlNode? objXmlMetamagic = objMetamagicsDoc.SelectSingleNode(
                    $"/chummer/metamagics/metamagic[name = '{objMetamagic.Name}']");
                XmlNode? objXmlBonus = objXmlMetamagic?.SelectSingleNode("bonus");
                if (objXmlBonus == null || !objXmlBonus.InnerXml.Contains("Rating"))
                    continue;

                RemoveBonusImprovements(ImprovementSource.Metamagic, objMetamagic.Name);
                ApplyBonus(objXmlBonus, ImprovementSource.Metamagic, objMetamagic.Name, intNewGrade.ToString());
            }
        }

        public IReadOnlyList<CharacterMetamagicData> Metamagics => ReadMetamagics();

        /// <summary>A Technomancer's Complex Forms - ported from clsUnique.cs's TechProgram class,
        /// data drawn from programs.xml (see frmSelectProgram.cs).</summary>
        public IReadOnlyList<CharacterComplexFormData> ComplexForms => ReadComplexForms();

        private IReadOnlyList<CharacterComplexFormData> ReadComplexForms()
        {
            var lstForms = new List<CharacterComplexFormData>();
            var objNodes = Document.SelectNodes("/character/techprograms/techprogram");
            if (objNodes == null) return lstForms;
            foreach (XmlNode objNode in objNodes)
            {
                var lstOptions = new List<(string Name, string Rating)>();
                var objOptionNodes = objNode.SelectNodes("programoptions/programoption");
                if (objOptionNodes != null)
                    foreach (XmlNode objOptionNode in objOptionNodes)
                        lstOptions.Add((GetValue(objOptionNode, "name", string.Empty),
                            GetValue(objOptionNode, "rating", "0")));

                lstForms.Add(new CharacterComplexFormData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "category", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "rating", "0"), lstOptions));
            }

            return lstForms;
        }

        /// <summary>Ported from clsUnique.cs's TechProgram.Create/Save. Also applies the program's
        /// own rules-data &lt;bonus&gt; block at Rating 1 (matching the always-1 saved Rating), and,
        /// when the bonus is a &lt;selecttext&gt;/&lt;selectskill&gt;/&lt;selectattribute&gt; node,
        /// <paramref name="strExtra"/> becomes the corresponding Improvement (see <see
        /// cref="ApplySelectedImprovement"/>).</summary>
        public void AddComplexForm(string strName, string strCategory, string strSource, string strPage,
            string strExtra = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A complex form name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objForms = objRoot.SelectSingleNode("techprograms");
            if (objForms == null)
            {
                objForms = Document.CreateElement("techprograms");
                objRoot.AppendChild(objForms);
            }

            var objForm = Document.CreateElement("techprogram");
            AppendElement(objForm, "guid", Guid.NewGuid().ToString());
            AppendElement(objForm, "name", strName.Trim());
            AppendElement(objForm, "category", strCategory);
            AppendElement(objForm, "rating", "1");
            AppendElement(objForm, "extra", strExtra.Trim());
            AppendElement(objForm, "source", strSource);
            AppendElement(objForm, "page", strPage);
            objForms.AppendChild(objForm);

            XmlDocument objProgramsDoc = XmlManager.Instance.Load("programs.xml");
            XmlNode? objXmlProgram = objProgramsDoc.SelectSingleNode(
                $"/chummer/programs/program[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlProgram?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.ComplexForm, strName.Trim());
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.ComplexForm, strName.Trim(), strExtra, "1");

            Changed?.Invoke();
        }

        /// <summary>Program Options offered for a Complex Form of the given category - ported
        /// from frmSelectProgramOption.cs's Load handler: an option from programs.xml's
        /// /chummer/options/option is offered if it has no &lt;programtypes&gt; restriction, or one
        /// of its &lt;programtype&gt; entries matches <paramref name="strCategory"/> (the Complex
        /// Form's own &lt;category&gt;, e.g. "Common Use"/"Hacking").</summary>
        public IReadOnlyList<string> GetComplexFormOptionChoices(string strCategory)
        {
            XmlDocument objProgramsDoc = XmlManager.Instance.Load("programs.xml");
            var lstOptions = new List<string>();
            foreach (XmlNode objOption in objProgramsDoc.SelectNodes("/chummer/options/option")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objOption["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(strName))
                    continue;

                XmlNodeList? objTypeNodes = objOption.SelectNodes("programtypes/programtype");
                bool blnAdd = objTypeNodes == null || objTypeNodes.Count == 0
                    || objTypeNodes.Cast<XmlNode>().Any(objType => objType.InnerText == strCategory);
                if (blnAdd)
                    lstOptions.Add(strName);
            }

            return lstOptions;
        }

        /// <summary>Adds a Program Option to an existing Complex Form - ported from
        /// clsUnique.cs's TechProgramOption.Create. Rating starts at 1 unless the option's
        /// &lt;maxrating&gt; is explicitly "0" (legacy defaults maxrating to 6 otherwise). Not
        /// ported: legacy's rare per-option
        /// &lt;bonus&gt; application (only 1 of 25 real programs.xml options has one) and the
        /// Complex Form capacity gating in frmCreate.cs (moot here since saved Complex Forms are
        /// always Rating 1, i.e. always CalculatedCapacity 0 under the "Rating/2" formula).</summary>
        public bool AddComplexFormOption(string strGuid, string strOptionName)
        {
            if (string.IsNullOrWhiteSpace(strGuid) || string.IsNullOrWhiteSpace(strOptionName))
                return false;

            var objNodes = Document.SelectNodes("/character/techprograms/techprogram");
            if (objNodes == null)
                return false;

            XmlNode? objForm = objNodes.Cast<XmlNode>().FirstOrDefault(
                n => string.Equals(GetValue(n, "guid", string.Empty), strGuid, StringComparison.Ordinal));
            if (objForm is not XmlElement objFormElement)
                return false;

            XmlDocument objProgramsDoc = XmlManager.Instance.Load("programs.xml");
            XmlNode? objXmlOption = objProgramsDoc.SelectSingleNode(
                $"/chummer/options/option[name = '{strOptionName.Trim()}']");
            if (objXmlOption == null)
                return false;

            var objOptions = objFormElement.SelectSingleNode("programoptions");
            if (objOptions is not XmlElement objOptionsElement)
            {
                objOptionsElement = Document.CreateElement("programoptions");
                objFormElement.AppendChild(objOptionsElement);
            }

            // Legacy defaults maxrating to 6 (>0) unless the node's InnerText is non-empty and says
            // otherwise, so an explicit "0" is the only value that keeps the starting Rating at 0.
            string strMaxRating = objXmlOption["maxrating"]?.InnerText ?? string.Empty;
            var objOptionElement = Document.CreateElement("programoption");
            AppendElement(objOptionElement, "name", strOptionName.Trim());
            AppendElement(objOptionElement, "rating", strMaxRating == "0" ? "0" : "1");
            AppendElement(objOptionElement, "source", objXmlOption["source"]?.InnerText ?? string.Empty);
            AppendElement(objOptionElement, "page", objXmlOption["page"]?.InnerText ?? string.Empty);
            objOptionsElement.AppendChild(objOptionElement);

            Changed?.Invoke();
            return true;
        }

        public bool RemoveComplexForm(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            var objNodes = Document.SelectNodes("/character/techprograms/techprogram");
            if (objNodes == null)
                return false;

            foreach (XmlNode objForm in objNodes)
            {
                if (!string.Equals(GetValue(objForm, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    continue;

                string strName = GetValue(objForm, "name", string.Empty);
                objForm.ParentNode?.RemoveChild(objForm);
                RemoveBonusImprovements(ImprovementSource.ComplexForm, strName);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>A Critter/Free Spirit's innate powers - ported (read-only) from clsUnique.cs's
        /// CritterPower class. No add/remove/picker yet, only read+print support.</summary>
        public IReadOnlyList<CharacterCritterPowerData> CritterPowers => ReadCritterPowers();

        private IReadOnlyList<CharacterCritterPowerData> ReadCritterPowers()
        {
            var lstPowers = new List<CharacterCritterPowerData>();
            var objNodes = Document.SelectNodes("/character/critterpowers/critterpower");
            if (objNodes == null) return lstPowers;
            foreach (XmlNode objNode in objNodes)
                lstPowers.Add(new CharacterCritterPowerData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "extra", string.Empty), GetValue(objNode, "points", "0"),
                    GetValue(objNode, "rating", "0")));
            return lstPowers;
        }

        /// <summary>Ported from clsUnique.cs's CritterPower.Create/Save, simplified to skip the
        /// &lt;bonus&gt; Improvement-creation path (matches AddMetamagic/AddCyberware etc.).</summary>
        /// <summary>Ported from clsUnique.cs's CritterPower.Create/Save. Also applies the power's
        /// own rules-data &lt;bonus&gt; block, scaled by <paramref name="strRating"/> for powers
        /// whose rules-data entry sets &lt;rating&gt;yes&lt;/rating&gt; (e.g. Armor (Ballistic));
        /// every other power ignores it and applies at a fixed Rating of 1 (matching legacy's
        /// nudCritterPowerRating being disabled/irrelevant for those). When the bonus is a
        /// &lt;selecttext&gt;/&lt;selectskill&gt;/&lt;selectattribute&gt; node, <paramref
        /// name="strExtra"/> becomes the corresponding Improvement (see <see
        /// cref="ApplySelectedImprovement"/>).</summary>
        public void AddCritterPower(string strName, string strPoints, string strSource, string strPage,
            string strExtra = "", string strRating = "1")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A critter power name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objPowers = objRoot.SelectSingleNode("critterpowers");
            if (objPowers == null)
            {
                objPowers = Document.CreateElement("critterpowers");
                objRoot.AppendChild(objPowers);
            }

            XmlDocument objPowersDoc = XmlManager.Instance.Load("critterpowers.xml");
            XmlNode? objXmlPower = objPowersDoc.SelectSingleNode(
                $"/chummer/powers/power[name = '{strName.Trim()}']");
            bool blnHasRating = objXmlPower?.SelectSingleNode("rating")?.InnerText == "yes";

            var objPower = Document.CreateElement("critterpower");
            AppendElement(objPower, "guid", Guid.NewGuid().ToString());
            AppendElement(objPower, "name", strName.Trim());
            AppendElement(objPower, "extra", strExtra.Trim());
            AppendElement(objPower, "points", strPoints);
            AppendElement(objPower, "rating", blnHasRating ? strRating : "0");
            AppendElement(objPower, "source", strSource);
            AppendElement(objPower, "page", strPage);
            objPowers.AppendChild(objPower);

            XmlNode? objXmlBonus = objXmlPower?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.CritterPower, strName.Trim(),
                blnHasRating ? strRating : "1");
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.CritterPower, strName.Trim(), strExtra,
                blnHasRating ? strRating : "1");

            Changed?.Invoke();
        }

        public bool RemoveCritterPower(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            var objNodes = Document.SelectNodes("/character/critterpowers/critterpower");
            if (objNodes == null)
                return false;

            foreach (XmlNode objPower in objNodes)
            {
                if (!string.Equals(GetValue(objPower, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    continue;

                string strName = GetValue(objPower, "name", string.Empty);
                objPower.ParentNode?.RemoveChild(objPower);
                RemoveBonusImprovements(ImprovementSource.CritterPower, strName);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>Whether adding this Metamagic prompts for a free-text detail - ported from
        /// clsImprovement.cs's selecttext bonus node (e.g. Attunement (Animal/Item)'s target).</summary>
        public bool MetamagicRequiresTextSelection(string strName) =>
            FindBonusChild("metamagic.xml", "metamagics", "metamagic", strName, "selecttext") != null;

        /// <summary>Ported from clsUnique.cs's Metamagic.Create/Save, also applying the
        /// metamagic's own rules-data &lt;bonus&gt; block (see <see cref="ApplyBonus"/>) and,
        /// when the bonus is a &lt;selecttext&gt; node (see <see
        /// cref="MetamagicRequiresTextSelection"/>), <paramref name="strSelected"/> becomes a Text
        /// Improvement (see <see cref="ApplySelectedImprovement"/>).</summary>
        public void AddMetamagic(string strName, string strSource, string strPage, string strSelected = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A metamagic name is required.", nameof(strName));

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objMetamagics = objRoot.SelectSingleNode("metamagics");
            if (objMetamagics == null)
            {
                objMetamagics = Document.CreateElement("metamagics");
                objRoot.AppendChild(objMetamagics);
            }

            var objMetamagic = Document.CreateElement("metamagic");
            AppendElement(objMetamagic, "guid", Guid.NewGuid().ToString());
            AppendElement(objMetamagic, "name", strName.Trim());
            AppendElement(objMetamagic, "source", strSource);
            AppendElement(objMetamagic, "paidwithkarma", "False");
            AppendElement(objMetamagic, "page", strPage);
            AppendElement(objMetamagic, "improvementsource", "Metamagic");
            objMetamagics.AppendChild(objMetamagic);

            XmlDocument objMetamagicsDoc = XmlManager.Instance.Load("metamagic.xml");
            XmlNode? objXmlMetamagic = objMetamagicsDoc.SelectSingleNode(
                $"/chummer/metamagics/metamagic[name = '{strName.Trim()}']");
            XmlNode? objXmlBonus = objXmlMetamagic?.SelectSingleNode("bonus");
            ApplyBonus(objXmlBonus, ImprovementSource.Metamagic, strName.Trim());
            ApplySelectedImprovement(objXmlBonus, ImprovementSource.Metamagic, strName.Trim(), strSelected, "1");

            Changed?.Invoke();
        }

        public bool RemoveMetamagic(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            var objNodes = Document.SelectNodes("/character/metamagics/metamagic");
            if (objNodes == null)
                return false;

            foreach (XmlNode objMetamagic in objNodes)
            {
                if (!string.Equals(GetValue(objMetamagic, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    continue;

                string strName = GetValue(objMetamagic, "name", string.Empty);
                objMetamagic.ParentNode?.RemoveChild(objMetamagic);
                RemoveBonusImprovements(ImprovementSource.Metamagic, strName);
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        private IReadOnlyList<CharacterMetamagicData> ReadMetamagics()
        {
            var lstMetamagics = new List<CharacterMetamagicData>();
            var objNodes = Document.SelectNodes("/character/metamagics/metamagic");
            if (objNodes == null) return lstMetamagics;
            foreach (XmlNode objNode in objNodes)
                lstMetamagics.Add(new CharacterMetamagicData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "source", string.Empty),
                    GetValue(objNode, "page", string.Empty)));
            return lstMetamagics;
        }

        public IReadOnlyList<CharacterLifestyleData> Lifestyles => ReadLifestyles();

        /// <summary>Adds a base lifestyle using the character-file shape written by the legacy application.
        /// <paramref name="strDice"/>/<paramref name="strMultiplier"/> are optional (empty for
        /// existing callers) - when supplied they're used directly by <see
        /// cref="GetLifestyleNuyenRollInfo"/> instead of that method's by-name lifestyles.xml
        /// re-lookup, which only works for plain Lifestyles whose saved name still matches a
        /// lifestyles.xml entry (see <see cref="AddAdvancedLifestyle"/>, whose custom player-chosen
        /// name never would).</summary>
        public void AddLifestyle(string strName, string strCost, string strMonths = "1", string strDice = "",
            string strMultiplier = "")
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A lifestyle name is required.", nameof(strName));
            var objRoot = Document.DocumentElement ?? throw new InvalidOperationException("Character document has no root element.");
            var objLifestyles = objRoot.SelectSingleNode("lifestyles") as XmlElement;
            if (objLifestyles == null) { objLifestyles = Document.CreateElement("lifestyles"); objRoot.AppendChild(objLifestyles); }
            var objLifestyle = Document.CreateElement("lifestyle");
            AppendElement(objLifestyle, "lifestylename", strName);
            AppendElement(objLifestyle, "cost", strCost);
            AppendElement(objLifestyle, "months", strMonths);
            if (!string.IsNullOrEmpty(strDice)) AppendElement(objLifestyle, "dice", strDice);
            if (!string.IsNullOrEmpty(strMultiplier)) AppendElement(objLifestyle, "multiplier", strMultiplier);
            objLifestyles.AppendChild(objLifestyle);
            Changed?.Invoke();
        }

        /// <summary>Names offered for one of an Advanced Lifestyle's five aspects - ported from
        /// frmSelectAdvancedLifestyle.cs's Load handler's Comforts/Entertainment/Necessities/
        /// Neighborhood/Security ComboBox population (Advanced type only - this port doesn't
        /// model Safehouse/BoltHole's separate &lt;slp&gt; LP override).</summary>
        public IReadOnlyList<string> GetLifestyleAspectOptions(string strAspectTag)
        {
            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            var lstNames = new List<string>();
            foreach (XmlNode objAspect in objLifestylesDoc.SelectNodes(
                         $"/chummer/{AspectContainerTag(strAspectTag)}/{strAspectTag}")?.Cast<XmlNode>()
                     ?? Enumerable.Empty<XmlNode>())
            {
                string strName = objAspect["name"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(strName))
                    lstNames.Add(strName);
            }

            return lstNames;
        }

        /// <summary>One Positive/Negative Quality an Advanced Lifestyle can take - ported from
        /// frmSelectAdvancedLifestyle.cs's quality tree population (lifestyles.xml has its own
        /// quality catalog, separate from qualities.xml's character Qualities), filtered to
        /// entries whose &lt;allowed&gt; list includes "Advanced".</summary>
        public sealed record LifestyleQualityOption(string Name, string Category, int Lp, string Source, string Page);

        public IReadOnlyList<LifestyleQualityOption> GetLifestyleQualityOptions()
        {
            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            var lstOptions = new List<LifestyleQualityOption>();
            foreach (XmlNode objQuality in objLifestylesDoc.SelectNodes("/chummer/qualities/quality")?.Cast<XmlNode>()
                         ?? Enumerable.Empty<XmlNode>())
            {
                string strAllowed = objQuality["allowed"]?.InnerText ?? string.Empty;
                if (!strAllowed.Split(',').Contains("Advanced"))
                    continue;

                string strName = objQuality["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(strName))
                    continue;

                int intLp = int.TryParse(objQuality["lp"]?.InnerText, out var lp) ? lp : 0;
                lstOptions.Add(new LifestyleQualityOption(strName, objQuality["category"]?.InnerText ?? string.Empty,
                    intLp, objQuality["source"]?.InnerText ?? string.Empty, objQuality["page"]?.InnerText ?? string.Empty));
            }

            return lstOptions;
        }

        /// <summary>Adds an Advanced Lifestyle - ported from frmSelectAdvancedLifestyle.cs's
        /// AcceptForm/CalculateValues. Total LP is the five aspects' own &lt;lp&gt; plus each
        /// chosen Quality's &lt;lp&gt; (Negative Qualities carry negative values, matching real
        /// data); Nuyen cost comes from lifestyles.xml's &lt;costs&gt; table (linear extrapolation
        /// past LP 30, exactly like legacy), then scaled by Roommates% and the overall Percentage
        /// slider. Dice/Multiplier for the starting-Nuyen roll are looked up from whichever
        /// standard Lifestyle (Street..Luxury) the total LP maps onto - not ported: legacy's
        /// separate "effective LP" (aspects-only, no Qualities) used only to pick Dice/Multiplier
        /// independently of the Nuyen-cost LP; this port uses the same total LP for both, which
        /// only differs from legacy when Qualities push the character across a LP tier boundary.</summary>
        public void AddAdvancedLifestyle(string strName, string strComforts, string strEntertainment,
            string strNecessities, string strNeighborhood, string strSecurity, int intRoommates, int intPercentage,
            IReadOnlyList<string> lstPositiveQualities, IReadOnlyList<string> lstNegativeQualities)
        {
            if (string.IsNullOrWhiteSpace(strName))
                throw new ArgumentException("A lifestyle name is required.", nameof(strName));

            AdvancedLifestylePreview objPreview = PreviewAdvancedLifestyle(strComforts, strEntertainment,
                strNecessities, strNeighborhood, strSecurity, intRoommates, intPercentage, lstPositiveQualities,
                lstNegativeQualities);

            AddLifestyle(strName, objPreview.Cost.ToString(CultureInfo.InvariantCulture), "1",
                objPreview.Dice.ToString(CultureInfo.InvariantCulture),
                objPreview.Multiplier.ToString(CultureInfo.InvariantCulture));

            var objNodes = Document.SelectNodes("/character/lifestyles/lifestyle");
            if (objNodes?[objNodes.Count - 1] is XmlElement objLastLifestyle)
            {
                AppendElement(objLastLifestyle, "comforts", strComforts);
                AppendElement(objLastLifestyle, "entertainment", strEntertainment);
                AppendElement(objLastLifestyle, "necessities", strNecessities);
                AppendElement(objLastLifestyle, "neighborhood", strNeighborhood);
                AppendElement(objLastLifestyle, "security", strSecurity);
                AppendElement(objLastLifestyle, "roommates", intRoommates.ToString(CultureInfo.InvariantCulture));
                AppendElement(objLastLifestyle, "percentage", intPercentage.ToString(CultureInfo.InvariantCulture));
                AppendElement(objLastLifestyle, "type", "Advanced");
                var objQualities = Document.CreateElement("qualities");
                foreach (string strQuality in lstPositiveQualities.Concat(lstNegativeQualities))
                    AppendElement(objQualities, "quality", strQuality);
                objLastLifestyle.AppendChild(objQualities);
            }
        }

        public sealed record AdvancedLifestylePreview(int Lp, int Cost, int Dice, int Multiplier);

        /// <summary>Non-mutating LP/Cost/Dice/Multiplier computation shared by <see
        /// cref="AddAdvancedLifestyle"/> and the picker dialog's live preview, so the two can
        /// never drift out of sync.</summary>
        public AdvancedLifestylePreview PreviewAdvancedLifestyle(string strComforts, string strEntertainment,
            string strNecessities, string strNeighborhood, string strSecurity, int intRoommates, int intPercentage,
            IReadOnlyList<string> lstPositiveQualities, IReadOnlyList<string> lstNegativeQualities)
        {
            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            int intLp = 0;
            intLp += AspectLp(objLifestylesDoc, "comfort", strComforts);
            intLp += AspectLp(objLifestylesDoc, "entertainment", strEntertainment);
            intLp += AspectLp(objLifestylesDoc, "necessity", strNecessities);
            intLp += AspectLp(objLifestylesDoc, "neighborhood", strNeighborhood);
            intLp += AspectLp(objLifestylesDoc, "security", strSecurity);

            foreach (string strQuality in lstPositiveQualities.Concat(lstNegativeQualities))
            {
                XmlNode? objXmlQuality = objLifestylesDoc.SelectSingleNode($"/chummer/qualities/quality[name = '{strQuality}']");
                if (objXmlQuality != null && int.TryParse(objXmlQuality["lp"]?.InnerText, out var intQualityLp))
                    intLp += intQualityLp;
            }

            intLp = Math.Max(0, intLp);

            int intNuyen;
            if (intLp < 29)
            {
                intNuyen = int.TryParse(objLifestylesDoc.SelectSingleNode(
                    $"/chummer/costs/cost[lp = '{intLp}']")?["cost"]?.InnerText, out var c) ? c : 0;
            }
            else
            {
                int intBase = int.TryParse(objLifestylesDoc.SelectSingleNode(
                    "/chummer/costs/cost[lp = '30']")?["cost"]?.InnerText, out var b) ? b : 0;
                int intPerLp = int.TryParse(objLifestylesDoc.SelectSingleNode(
                    "/chummer/costs/cost[lp = '31+']")?["cost"]?.InnerText, out var p) ? p : 0;
                intNuyen = intBase + (intLp - 30) * intPerLp;
            }

            intNuyen = (int)(intNuyen * (1.0 + intRoommates / 10.0));
            intNuyen = (int)(intNuyen * (intPercentage / 100.0));

            string strTierName = intLp switch
            {
                >= 21 => "Luxury",
                >= 16 => "High",
                >= 11 => "Middle",
                >= 6 => "Low",
                >= 1 => "Squatter",
                _ => "Street",
            };
            XmlNode? objXmlTier = objLifestylesDoc.SelectSingleNode($"/chummer/lifestyles/lifestyle[name = '{strTierName}']");
            int intDice = int.TryParse(objXmlTier?["dice"]?.InnerText, out var d) ? d : 1;
            int intMultiplier = int.TryParse(objXmlTier?["multiplier"]?.InnerText, out var m) ? m : 0;

            return new AdvancedLifestylePreview(intLp, intNuyen, intDice, intMultiplier);
        }

        private static int AspectLp(XmlDocument objLifestylesDoc, string strAspectTag, string strValue)
        {
            XmlNode? objXmlAspect = objLifestylesDoc.SelectSingleNode(
                $"/chummer/{AspectContainerTag(strAspectTag)}/{strAspectTag}[name = '{strValue}']");
            return int.TryParse(objXmlAspect?["lp"]?.InnerText, out var lp) ? lp : 0;
        }

        /// <summary>lifestyles.xml's five aspect list container names don't all follow simple
        /// "+s" pluralization ("necessity"/"security" -&gt; "necessities"/"securities").</summary>
        private static string AspectContainerTag(string strAspectTag) => strAspectTag switch
        {
            "necessity" => "necessities",
            "security" => "securities",
            _ => strAspectTag + "s",
        };

        public bool RemoveLifestyle(string strName)
        {
            XmlNodeList? objLifestyles = Document.SelectNodes("/character/lifestyles/lifestyle");
            if (objLifestyles == null) return false;
            foreach (XmlNode objLifestyle in objLifestyles)
            {
                string strSavedName = GetValue(objLifestyle, "lifestylename", GetValue(objLifestyle, "name", string.Empty));
                if (!string.Equals(strSavedName, strName, StringComparison.Ordinal)) continue;
                objLifestyle.ParentNode?.RemoveChild(objLifestyle);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>The dice/multiplier/bonus needed to prompt for a starting-Nuyen roll, ported
        /// from frmCreate.cs's ConfirmSaveCreatedCharacter.</summary>
        public sealed record LifestyleNuyenRollInfo(int Dice, int Multiplier, int Extra);

        /// <summary>Ported from frmCreate.cs's ConfirmSaveCreatedCharacter: auto-adds a Street
        /// Lifestyle if the character has none yet, then computes the dice/multiplier for the
        /// highest-multiplier Lifestyle owned and the "+1 per 100 leftover Nuyen, capped at 3x
        /// dice" bonus. Returns null once the character is already Created.</summary>
        public LifestyleNuyenRollInfo? GetLifestyleNuyenRollInfo()
        {
            if (Created)
                return null;
            if (Lifestyles.Count == 0)
                AddLifestyle("Street", "0");

            XmlDocument objLifestylesDoc = XmlManager.Instance.Load("lifestyles.xml");
            int intDice = 0, intMultiplier = 0;
            foreach (CharacterLifestyleData objLifestyle in Lifestyles)
            {
                int intLifestyleMultiplier;
                int intLifestyleDice;
                if (!string.IsNullOrEmpty(objLifestyle.Multiplier))
                {
                    // Own persisted Dice/Multiplier (Advanced Lifestyles, or any Lifestyle added
                    // since this field started being written) - see AddLifestyle's doc comment.
                    intLifestyleMultiplier = int.TryParse(objLifestyle.Multiplier, out var lm) ? lm : 0;
                    intLifestyleDice = int.TryParse(objLifestyle.Dice, out var ld) ? ld : 0;
                }
                else
                {
                    XmlNode? objXmlLifestyle = objLifestylesDoc.SelectSingleNode(
                        $"/chummer/lifestyles/lifestyle[name = '{objLifestyle.Name}']");
                    if (objXmlLifestyle == null)
                        continue;
                    intLifestyleMultiplier = int.TryParse(GetValue(objXmlLifestyle, "multiplier", "0"), out var m) ? m : 0;
                    intLifestyleDice = int.TryParse(GetValue(objXmlLifestyle, "dice", "0"), out var d) ? d : 0;
                }

                if (intLifestyleMultiplier > intMultiplier)
                {
                    intMultiplier = intLifestyleMultiplier;
                    intDice = intLifestyleDice;
                }
            }

            int intNuyen = int.TryParse(Nuyen, out var n) ? n : 0;
            int intExtra = Math.Min((int)Math.Floor(intNuyen / 100.0), intDice * 3);
            return new LifestyleNuyenRollInfo(intDice, intMultiplier, intExtra);
        }

        /// <summary>Applies a manually-entered starting-Nuyen dice-roll result (see
        /// <see cref="GetLifestyleNuyenRollInfo"/>), then finalizes creation - ported from
        /// frmCreate.cs's ConfirmSaveCreatedCharacter.</summary>
        public bool FinalizeCreationWithLifestyleNuyenRoll(int intDiceResult)
        {
            LifestyleNuyenRollInfo? objInfo = GetLifestyleNuyenRollInfo();
            if (objInfo == null)
                return false;

            int intStartingNuyen = Math.Max(0, (intDiceResult + objInfo.Extra) * objInfo.Multiplier);
            Nuyen = intStartingNuyen.ToString();
            return FinalizeCreation();
        }

        public IReadOnlyList<CharacterVehicleData> Vehicles => ReadVehicles();

        // Karma and Nuyen history entries are saved into the same <expenses> list and only
        // distinguished by <type> - split here to feed the two separate history lists/charts.
        public IReadOnlyList<CharacterExpenseData> KarmaExpenses => ReadExpenses(strType: "Karma");

        public IReadOnlyList<CharacterExpenseData> NuyenExpenses => ReadExpenses(strType: "Nuyen");

        /// <summary>
        /// Appends a Karma or Nuyen history entry in the same XML shape as the legacy career mode.
        /// Positive amounts are earnings; negative amounts are expenditures. The caller supplies
        /// the signed amount so refunds can be represented without a second write API.
        /// </summary>
        public void AddExpense(string strType, decimal decAmount, string strReason, DateTime? datDate = null,
            ExpenseUndo objUndo = null)
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

            if (objUndo != null)
            {
                var objUndoElement = Document.CreateElement("undo");
                AppendElement(objUndoElement, "karmatype", objUndo.KarmaType.ToString());
                AppendElement(objUndoElement, "nuyentype", objUndo.NuyenType.ToString());
                AppendElement(objUndoElement, "objectid", objUndo.ObjectId);
                AppendElement(objUndoElement, "qty", objUndo.Qty.ToString());
                AppendElement(objUndoElement, "extra", objUndo.Extra);
                objExpense.AppendChild(objUndoElement);
            }
        }

        /// <summary>Ported from frmCareer.cs's Karma/Nuyen expense edit dialog: Reason and Date are
        /// always editable; Amount is included here too (legacy locks it for non-manual entries via
        /// Undo.KarmaType/NuyenType, which this port's AddExpense never attaches, so every entry the
        /// Avalonia UI creates is effectively "manual" and editable).</summary>
        public bool UpdateExpense(string strGuid, string strReason, decimal decAmount, DateTime datDate)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            XmlNode? objExpense = FindExpenseNode(strGuid);
            if (objExpense == null)
                return false;

            SetChildValue(objExpense, "reason", strReason.Trim());
            SetChildValue(objExpense, "amount", decAmount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetChildValue(objExpense, "date", datDate.ToString("O"));
            Changed?.Invoke();
            return true;
        }

        public bool RemoveExpense(string strGuid)
        {
            if (string.IsNullOrWhiteSpace(strGuid))
                return false;

            XmlNode? objExpense = FindExpenseNode(strGuid);
            if (objExpense == null)
                return false;

            objExpense.ParentNode?.RemoveChild(objExpense);
            Changed?.Invoke();
            return true;
        }

        private XmlNode? FindExpenseNode(string strGuid)
        {
            var objNodes = Document.SelectNodes("/character/expenses/expense");
            if (objNodes == null) return null;
            foreach (XmlNode objExpense in objNodes)
            {
                if (string.Equals(GetValue(objExpense, "guid", string.Empty), strGuid, StringComparison.Ordinal))
                    return objExpense;
            }

            return null;
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

        /// <summary>Portrait image, base64-encoded - matches the legacy &lt;mugshot&gt; element
        /// (clsCharacter.cs's Save()), which stores the image inline rather than as a file path.</summary>
        public string Mugshot
        {
            get => GetValue("/character/mugshot", string.Empty);
            set => SetRootValue("mugshot", value);
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

        /// <summary>The natural (unaugmented) attribute Value, as opposed to GetAttributeInt's
        /// TotalValue - only used by the CapSkillRating house rule, which caps against the
        /// character's "real" attribute rather than its cyberware/magic-boosted total.</summary>
        private int GetAttributeBaseInt(string strCode)
        {
            var objNode = Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']/value");
            return int.TryParse(objNode?.InnerText, out var intValue) ? intValue : 0;
        }

        private int GetAttributeMinimum(string strCode)
        {
            var objNode = Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']/metatypemin");
            return int.TryParse(objNode?.InnerText, out var intValue) ? intValue : 0;
        }

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
            var objOptions = GetCharacterOptions();
            var objNodes = Document.SelectNodes("/character/armors/armor[equipped = 'True']");
            var dblBod = double.TryParse(GetAttributeValue("BOD"), out var d) ? d : 0;
            var dblStr = double.TryParse(GetAttributeValue("STR"), out var dStr) ? dStr : 0;

            var intMultiplier = 2;
            var intTotalBallistic = 0;
            var intTotalImpact = 0;
            var intArmorCount = 0;
            var lstWorn = new List<string>();
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    var strCategory = GetValue(objNode, "category", string.Empty);
                    if (strCategory == "Military Grade Armor")
                        intMultiplier = 3;

                    // Helmets/Shields/SecureTech PPP System don't count as a "piece" for the
                    // NoSingleArmorEncumbrance house rule (you can wear one plus a base suit
                    // without it counting as "two pieces").
                    if (strCategory != "Helmets and Shields" && strCategory != "SecureTech PPP System")
                        intArmorCount++;

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

            var intBallisticRating = ComputeArmorRating(objNodes, "b", ImprovementType.BallisticArmor);
            var intImpactRating = ComputeArmorRating(objNodes, "i", ImprovementType.ImpactArmor);

            // SoftWeave reduces the highest worn Ballistic/Impact rating (capped at STR) out of
            // the encumbrance total only - the displayed rating itself is unaffected.
            var blnSoftWeave = Improvements.Any(i => i.Type == ImprovementType.SoftWeave && i.Enabled);
            if (blnSoftWeave)
            {
                intTotalBallistic -= (int)Math.Min(intBallisticRating, dblStr);
                intTotalImpact -= (int)Math.Min(intImpactRating, dblStr);
            }

            // Alternate Armor Encumbrance house rule: threshold is BOD*(X-1) + STR instead of BOD*X.
            if (objOptions.AlternateArmorEncumbrance)
                intMultiplier--;
            var intThreshold = objOptions.AlternateArmorEncumbrance
                ? (int)(dblBod * intMultiplier + dblStr)
                : (int)(dblBod * intMultiplier);
            var strThresholdNote = objOptions.AlternateArmorEncumbrance
                ? "Schwelle: Konstitution " + dblBod + " x " + intMultiplier + " + Stärke " + dblStr + " = " + intThreshold
                : "Schwelle: Konstitution " + dblBod + " x " + intMultiplier + " = " + intThreshold
                    + (intMultiplier == 3 ? " (Militärgraderüstung getragen)" : "");

            int intArmorEncumbrancePenaltyBonus = ImprovementManager.ValueOf(Improvements, ImprovementType.ArmorEncumbrancePenalty);
            bool blnIgnoreEncumbrance = objOptions.IgnoreArmorEncumbrance;
            bool blnNoSinglePiecePenalty = objOptions.NoSingleArmorEncumbrance && intArmorCount == 1;

            return new CharacterEncumbranceData(
                BuildArmorRatingValue(intBallisticRating, "b", "ballistisch", objNodes, ImprovementType.BallisticArmor),
                BuildArmorRatingValue(intImpactRating, "i", "Stoß", objNodes, ImprovementType.ImpactArmor),
                BuildEncumbranceValue(intTotalBallistic, intThreshold, "ballistisch", strThresholdNote, lstWorn,
                    blnIgnoreEncumbrance, blnNoSinglePiecePenalty, intArmorEncumbrancePenaltyBonus),
                BuildEncumbranceValue(intTotalImpact, intThreshold, "Stoß", strThresholdNote, lstWorn,
                    blnIgnoreEncumbrance, blnNoSinglePiecePenalty, intArmorEncumbrancePenaltyBonus));
        }

        // Ported from clsCharacter.cs's BallisticArmorRating/ImpactArmorRating: a "+"-prefixed
        // rating (e.g. "+3") is stacking bonus armor, not a suit that supersedes others. Non-"+"
        // items only the single highest counts; "+" Clothing items sum among themselves and that
        // sum competes with the highest as an alternative base; "+" non-Clothing items always add
        // on top of whichever base wins.
        private int ComputeArmorRating(XmlNodeList? objNodes, string strElement, ImprovementType eImprovementType)
        {
            var intHighest = 0;
            var intStacking = 0;
            var intClothing = 0;
            if (objNodes != null)
            {
                foreach (XmlNode objNode in objNodes)
                {
                    var strRating = GetValue(objNode, strElement, "0");
                    var intRating = ParseArmorRating(strRating);
                    if (!strRating.StartsWith("+"))
                    {
                        intHighest = Math.Max(intHighest, intRating);
                        continue;
                    }

                    if (GetValue(objNode, "category", string.Empty) == "Clothing")
                        intClothing += intRating;
                    else
                        intStacking += intRating;
                }
            }

            var intArmor = Math.Max(intHighest, intClothing);
            return intArmor + intStacking + ImprovementManager.ValueOf(Improvements, eImprovementType);
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
        private static int ParseArmorRating(string strRating)
        {
            var strLeading = new string(strRating.TakeWhile(c => char.IsDigit(c) || c == '-' || c == '+').ToArray());
            return int.TryParse(strLeading, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var intValue) ? intValue : 0;
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

        private XmlNode? FindCalendarWeek(string strInternalId)
        {
            if (!Guid.TryParse(strInternalId, out _))
                return null;
            XmlNodeList? objWeeks = Document.SelectNodes("/character/calendar/week");
            if (objWeeks == null)
                return null;
            foreach (XmlNode objWeek in objWeeks)
                if (string.Equals(GetValue(objWeek, "guid", string.Empty), strInternalId, StringComparison.OrdinalIgnoreCase))
                    return objWeek;
            return null;
        }

        private IReadOnlyList<CharacterAttributeData> ReadAttributes()
        {
            var lstAttributes = new List<CharacterAttributeData>();
            var objNodes = Document.SelectNodes("/character/attributes/attribute");
            if (objNodes == null) return lstAttributes;
            foreach (XmlNode objNode in objNodes)
            {
                var strCode = GetValue(objNode, "name", string.Empty);
                var strValue = GetValue(objNode, "value", "0");
                var strTotalValue = GetValue(objNode, "totalvalue", strValue);
                if (strCode == "MAG" || strCode == "RES")
                    strTotalValue = ApplyEssencePenaltyToAttribute(strCode, strTotalValue).ToString(CultureInfo.InvariantCulture);
                else if (strCode == "AGI" || strCode == "BOD" || strCode == "STR")
                {
                    int intMeatValue = int.TryParse(strTotalValue, out var intParsedMeat) ? intParsedMeat : 0;
                    strTotalValue = ApplyCyberlimbAveraging(strCode, intMeatValue).ToString(CultureInfo.InvariantCulture);
                }
                var strMinimum = GetValue(objNode, "metatypemin", "0");
                lstAttributes.Add(new CharacterAttributeData(
                    strCode, strValue, strTotalValue,
                    strMinimum, GetValue(objNode, "metatypemax", "0"),
                    GetValue(objNode, "metatypeaugmax", GetValue(objNode, "metatypemax", "0")),
                    ComputeAttributeAugmented(strCode, strTotalValue),
                    ComputeAttributeKarmaCostToIncrease(strValue, strMinimum)));
            }

            return lstAttributes;
        }

        /// <summary>
        /// Ported from clsUnique.cs's Attribute.TotalValue's Cyberlimb-averaging block: for AGI/
        /// BOD/STR, replaces the "meat" total with the average across all of the character's
        /// Cyberlimbs' own stats (any limb slots not replaced by a Cyberlimb still contribute the
        /// meat value, padded out to Options.LimbCount). Limbs whose &lt;limbslot&gt; matches
        /// Options.ExcludeLimbSlot (e.g. excluding the skull from a torso-cyberlimb-only build)
        /// are skipped entirely, same as legacy.
        /// </summary>
        private int ApplyCyberlimbAveraging(string strCode, int intMeatValue)
        {
            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes == null) return intMeatValue;

            CharacterOptions objOptions = GetCharacterOptions();
            int intLimbTotal = 0;
            int intLimbCount = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "category", string.Empty) != "Cyberlimb")
                    continue;

                bool blnBioware = GetValue(objNode, "improvementsource", string.Empty) == "Bioware";
                string strLimbSlot = GetCyberwareLimbSlot(GetValue(objNode, "name", string.Empty), blnBioware);
                if (string.IsNullOrEmpty(strLimbSlot) || strLimbSlot == objOptions.ExcludeLimbSlot)
                    continue;

                intLimbCount++;
                (int intBody, int intStrength, int intAgility) = ComputeCyberlimbStats(objNode);
                intLimbTotal += strCode switch
                {
                    "BOD" => intBody,
                    "STR" => intStrength,
                    _ => intAgility
                };
            }

            if (intLimbCount == 0)
                return intMeatValue;

            if (intLimbCount < objOptions.LimbCount)
            {
                // Not all of the limbs have been replaced - fill the rest with the meat value to
                // get the average.
                intLimbTotal += intMeatValue * (objOptions.LimbCount - intLimbCount);
                intLimbCount = objOptions.LimbCount;
            }

            return (int)Math.Floor(intLimbTotal / (double)intLimbCount);
        }

        /// <summary>The rules-data &lt;limbslot&gt; (arm/leg/torso/skull) for a named Cyberlimb -
        /// not persisted per-item in the save file, so looked up by name the same way other
        /// rules-only metadata (e.g. FindBonusChild) is resolved on demand.</summary>
        private string GetCyberwareLimbSlot(string strName, bool blnBioware)
        {
            XmlDocument objDoc = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
            XmlNode? objXmlWare = objDoc.SelectSingleNode(
                $"/chummer/{(blnBioware ? "biowares/bioware" : "cyberwares/cyberware")}[name = '{strName}']");
            return objXmlWare?["limbslot"]?.InnerText ?? string.Empty;
        }

        /// <summary>Ported from clsEquipment.cs's Cyberware.TotalBody/TotalStrength/TotalAgility -
        /// a Cyberlimb's own physical stats start at a base of 3 and can be overridden/boosted by
        /// its "Customized X"/"Enhanced X" child plugins.</summary>
        private (int Body, int Strength, int Agility) ComputeCyberlimbStats(XmlNode objCyberwareNode)
        {
            int intBody = 3, intStrength = 3, intAgility = 3;
            int intBodyBonus = 0, intStrengthBonus = 0, intAgilityBonus = 0;

            XmlNodeList? objChildren = objCyberwareNode.SelectNodes("children/cyberware");
            if (objChildren != null)
            {
                foreach (XmlNode objChild in objChildren)
                {
                    string strName = GetValue(objChild, "name", string.Empty);
                    int intRating = int.TryParse(GetValue(objChild, "rating", "0"), out var intParsed) ? intParsed : 0;
                    switch (strName)
                    {
                        case "Customized Body": intBody = intRating; break;
                        case "Enhanced Body": intBodyBonus = intRating; break;
                        case "Customized Strength": intStrength = intRating; break;
                        case "Enhanced Strength": intStrengthBonus = intRating; break;
                        case "Customized Agility": intAgility = intRating; break;
                        case "Enhanced Agility": intAgilityBonus = intRating; break;
                    }
                }
            }

            return (intBody + intBodyBonus, intStrength + intStrengthBonus, intAgility + intAgilityBonus);
        }

        // Ported from clsUnique.cs's Attribute.AttributeModifiers/TotalValue.
        private CharacterDerivedValueData ComputeAttributeAugmented(string strCode, string strTotalValue)
        {
            var intBase = int.TryParse(strTotalValue, out var intParsed) ? intParsed : 0;
            var lstContributions = ImprovementManager.DescribeAugmentedValueOf(Improvements, ImprovementType.Attribute, strCode)
                .ToList();

            // Ported from frmCareer.cs/frmCreate.cs: a negative ballistic/impact armor encumbrance
            // penalty is dynamically applied as an AGI and REA reduction (not just a skill dice
            // pool note), each source counted separately if both are negative.
            if (strCode == "AGI" || strCode == "REA")
            {
                CharacterEncumbranceData encumbrance = ArmorEncumbrance;
                if (encumbrance.BallisticPenalty.Value < 0)
                    lstContributions.Add(("Rüstungsbehinderung (ballistisch)", encumbrance.BallisticPenalty.Value));
                if (encumbrance.ImpactPenalty.Value < 0)
                    lstContributions.Add(("Rüstungsbehinderung (Stoß)", encumbrance.ImpactPenalty.Value));
            }

            var intTotal = intBase + lstContributions.Sum(c => c.Value);

            var sb = new StringBuilder();
            sb.Append("Basis: ").Append(intBase);
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Gesamt: ").Append(intTotal);
            return new CharacterDerivedValueData(intTotal, sb.ToString());
        }

        // Ported from frmCareer.cs's cmdImprove<Attribute>_Click handlers.
        private int ComputeAttributeKarmaCostToIncrease(string strValue, string strMinimum)
        {
            var intValue = int.TryParse(strValue, out var intParsedValue) ? intParsedValue : 0;
            var intMinimum = int.TryParse(strMinimum, out var intParsedMinimum) ? intParsedMinimum : 0;
            var objOptions = GetCharacterOptions();

            var intCost = (intValue + 1) * objOptions.KarmaAttribute;
            if (objOptions.AlternateMetatypeAttributeKarma)
                intCost -= (intMinimum - 1) * objOptions.KarmaAttribute;
            return intCost;
        }

        /// <summary>Career mode: raises an attribute's base Value by one, deducting Karma and logging an expense+undo. False if not enough Karma.</summary>
        public bool RaiseAttribute(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;

            var strValue = GetValue(objNode, "value", "0");
            var strMinimum = GetValue(objNode, "metatypemin", "0");
            var intCost = ComputeAttributeKarmaCostToIncrease(strValue, strMinimum);
            var intKarma = int.TryParse(Karma, out var intParsedKarma) ? intParsedKarma : 0;
            if (intCost > intKarma) return false;

            var intValue = int.TryParse(strValue, out var intParsedValue) ? intParsedValue : 0;
            SetChildValue(objNode, "value", (intValue + 1).ToString());
            SetChildValue(objNode, "totalvalue", (intValue + 1).ToString());
            Karma = (intKarma - intCost).ToString();

            var objUndo = new ExpenseUndo();
            objUndo.CreateKarma(KarmaExpenseType.ImproveAttribute, strCode);
            AddExpense("Karma", -intCost, strCode + " " + intValue + " -> " + (intValue + 1), null, objUndo);
            return true;
        }

        /// <summary>Create mode: sets an attribute's base Value directly - cost is derived from the whole build's point pool, not charged per call.</summary>
        public bool SetAttributeValue(string strCode, int intValue)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;
            SetChildValue(objNode, "value", intValue.ToString());
            SetChildValue(objNode, "totalvalue", intValue.ToString());
            return true;
        }

        /// <summary>Create mode: raises an attribute's base Value by one, deducting from the Karma
        /// or BP pool depending on <see cref="BuildMethod"/>. Enforces the metatype maximum and the
        /// SR4 chargen rule that only one attribute may be raised to its natural maximum.</summary>
        public bool RaiseAttributeCreate(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;

            var strValue = GetValue(objNode, "value", "0");
            var strMinimum = GetValue(objNode, "metatypemin", "0");
            var strMaximum = GetValue(objNode, "metatypemax", "0");
            int intValue = int.TryParse(strValue, out var v) ? v : 0;
            int intMaximum = int.TryParse(strMaximum, out var mx) ? mx : 0;
            if (intValue >= intMaximum)
                return false;

            bool blnReachesMax = intValue + 1 == intMaximum;
            if (blnReachesMax && s_astrPrimaryAttributeCodes.Contains(strCode) && AnyOtherAttributeAtMax(strCode))
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);

            int intCost;
            if (blnKarmaBuild)
                intCost = ComputeAttributeKarmaCostToIncrease(strValue, strMinimum);
            else
                intCost = objOptions.BpAttribute + (blnReachesMax ? objOptions.BpAttributeMax : 0);

            if (!objOptions.AllowExceedAttributeBp && s_astrPrimaryAttributeCodes.Contains(strCode)
                && !ExceedAttributeBpAllowedForThisRaise(intCost))
                return false;

            if (blnKarmaBuild)
            {
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                if (intCost > intKarma)
                    return false;
                Karma = (intKarma - intCost).ToString();
            }
            else
            {
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                if (intCost > intBp)
                    return false;
                Bp = (intBp - intCost).ToString();
            }

            SetChildValue(objNode, "value", (intValue + 1).ToString());
            SetChildValue(objNode, "totalvalue", (intValue + 1).ToString());
            return true;
        }

        /// <summary>Ported from frmCreate.cs's nud&lt;Attribute&gt;_ValueChanged handlers' "no more
        /// than half your starting BP/Karma on primary attributes" check (gated by the
        /// AllowExceedAttributeBp house rule). Returns true (no restriction) if the character has
        /// no persisted starting total - older save files predating this field, or characters not
        /// created through <see cref="NewCharacterFactory"/> - since there's nothing to check
        /// against. <paramref name="intAdditionalCost"/> is the specific raise being attempted,
        /// added on top of everything already spent on the 8 primary attributes so far.</summary>
        private bool ExceedAttributeBpAllowedForThisRaise(int intAdditionalCost)
        {
            int intStartingTotal = int.TryParse(GetValue("/character/startingbuildpoints", "0"), out var s) ? s : 0;
            if (intStartingTotal <= 0)
                return true;

            int intSpent = s_astrPrimaryAttributeCodes.Sum(ComputeAttributeCreatePointsSpent);
            return intSpent + intAdditionalCost <= intStartingTotal / 2;
        }

        /// <summary>Recomputes how many Karma/BP points have already been spent raising this
        /// attribute from its metatype minimum to its current value, using the exact same per-step
        /// cost formulas <see cref="RaiseAttributeCreate"/>/<see cref="LowerAttributeCreate"/>
        /// already charge/refund, so it can never drift from what was actually charged.</summary>
        private int ComputeAttributeCreatePointsSpent(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return 0;

            int intValue = int.TryParse(GetValue(objNode, "value", "0"), out var v) ? v : 0;
            int intMinimum = int.TryParse(GetValue(objNode, "metatypemin", "0"), out var mn) ? mn : 0;
            int intMaximum = int.TryParse(GetValue(objNode, "metatypemax", "0"), out var mx) ? mx : 0;

            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            if (blnKarmaBuild)
            {
                int intSpent = 0;
                for (int i = intMinimum; i < intValue; i++)
                    intSpent += ComputeAttributeKarmaCostToIncrease(i.ToString(), intMinimum.ToString());
                return intSpent;
            }

            var objOptions = GetCharacterOptions();
            int intBpSpent = objOptions.BpAttribute * (intValue - intMinimum);
            if (intValue == intMaximum && intMaximum > intMinimum)
                intBpSpent += objOptions.BpAttributeMax;
            return intBpSpent;
        }

        /// <summary>Create mode: lowers an attribute's base Value by one, refunding the Karma or BP
        /// that was spent to reach the current rank.</summary>
        public bool LowerAttributeCreate(string strCode)
        {
            var objNode = GetAttributeNode(strCode);
            if (objNode == null) return false;

            var strValue = GetValue(objNode, "value", "0");
            var strMinimum = GetValue(objNode, "metatypemin", "0");
            int intValue = int.TryParse(strValue, out var v) ? v : 0;
            int intMinimum = int.TryParse(strMinimum, out var mn) ? mn : 0;
            if (intValue <= intMinimum)
                return false;

            var objOptions = GetCharacterOptions();
            bool blnKarmaBuild = string.Equals(BuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            int intMaximum = int.TryParse(GetValue(objNode, "metatypemax", "0"), out var mx) ? mx : 0;
            bool blnWasAtMax = intValue == intMaximum;

            if (blnKarmaBuild)
            {
                int intRefund = ComputeAttributeKarmaCostToIncrease((intValue - 1).ToString(), strMinimum);
                int intKarma = int.TryParse(Karma, out var k) ? k : 0;
                Karma = (intKarma + intRefund).ToString();
            }
            else
            {
                int intRefund = objOptions.BpAttribute + (blnWasAtMax ? objOptions.BpAttributeMax : 0);
                int intBp = int.TryParse(Bp, out var b) ? b : 0;
                Bp = (intBp + intRefund).ToString();
            }

            SetChildValue(objNode, "value", (intValue - 1).ToString());
            SetChildValue(objNode, "totalvalue", (intValue - 1).ToString());
            return true;
        }

        // The SR4 chargen rule "only one attribute may reach its natural maximum" applies only to
        // the 8 primary physical/mental attributes - Edge, Magic, and Resonance each have their
        // own separate cost/cap rules and neither trigger nor are blocked by this rule. Essence
        // isn't a chargen attribute at all (it only decreases from cyber/bioware).
        private static readonly string[] s_astrPrimaryAttributeCodes =
        {
            "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL"
        };

        private bool AnyOtherAttributeAtMax(string strExcludeCode)
        {
            var objNodes = Document.SelectNodes("/character/attributes/attribute");
            if (objNodes == null) return false;
            foreach (XmlNode objNode in objNodes)
            {
                string strCode = GetValue(objNode, "name", string.Empty);
                if (strCode == strExcludeCode || !s_astrPrimaryAttributeCodes.Contains(strCode))
                    continue;
                int intValue = int.TryParse(GetValue(objNode, "value", "0"), out var v) ? v : 0;
                int intMaximum = int.TryParse(GetValue(objNode, "metatypemax", "0"), out var mx) ? mx : 0;
                if (intMaximum > 0 && intValue >= intMaximum)
                    return true;
            }
            return false;
        }

        private XmlNode GetAttributeNode(string strCode)
            => Document.SelectSingleNode("/character/attributes/attribute[name = '" + strCode + "']");

        private CharacterOptions _objCharacterOptionsOverride;

        // Deliberately not cached: house rules/karma-BP costs can change from the Options dialog
        // while a character stays open, and a stale cached CharacterOptions would silently ignore
        // that (a real bug this fixes - see PORTING_PLAN.md).
        private CharacterOptions GetCharacterOptions()
        {
            if (_objCharacterOptionsOverride != null)
                return _objCharacterOptionsOverride;

            var objOptions = new CharacterOptions();
            objOptions.Load(GetValue("/character/settings", "default.xml"));
            return objOptions;
        }

        /// <summary>
        /// True if the given rules-data &lt;source&gt; book code is enabled in this character's
        /// game settings (ported from legacy pickers' use of Options.BookEnabled()/BookXPath()).
        /// A blank/missing source is always allowed, matching legacy's behavior of only filtering
        /// items that actually declare a source book.
        /// </summary>
        public bool IsBookEnabled(string strSourceCode) =>
            string.IsNullOrEmpty(strSourceCode) || GetCharacterOptions().BookEnabled(strSourceCode);

        /// <summary>Test-only hook to exercise settings-driven behavior (house rules, karma/BP
        /// costs, ...) without needing a real settings/*.xml file on disk.</summary>
        internal void SetCharacterOptionsForTesting(CharacterOptions objOptions) => _objCharacterOptionsOverride = objOptions;

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
            foreach (string strSetName in ArmorSets)
            {
                var objSet = new CharacterTreeItemData(strSetName, "Armor set");
                dicSets.Add(strSetName, objSet);
                lstArmor.Add(objSet);
            }
            var objNodes = Document.SelectNodes("/character/armors/armor");
            if (objNodes == null) return lstArmor;

            foreach (XmlNode objNode in objNodes)
            {
                var objArmor = ReadTreeItem(objNode, "armormods/armormod", "gears/gear");
                objArmor.SetArmorRatings(GetValue(objNode, "b", "0"), GetValue(objNode, "i", "0"));
                string strSetName = GetValue(objNode, "armorname", string.Empty);
                objArmor.SetArmorSetName(strSetName);
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
            var dicLocations = new Dictionary<string, CharacterTreeItemData>(StringComparer.Ordinal);
            foreach (string strLocation in WeaponLocations)
            {
                var objLocation = new CharacterTreeItemData(strLocation, "Weapon location");
                dicLocations.Add(strLocation, objLocation);
                lstWeapons.Add(objLocation);
            }
            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return lstWeapons;
            foreach (XmlNode objNode in objNodes)
            {
                var objWeapon = ReadTreeItem(objNode, "accessories/accessory", "weaponmods/weaponmod", "gears/gear", "ammos/ammo");
                MarkWeaponAccessoriesAndMods(objWeapon, objNode);
                string strLocation = GetValue(objNode, "location", string.Empty);
                objWeapon.SetLocation(strLocation);
                (string strPoolDisplay, string strTooltip) = ComputeWeaponDicePool(
                    GetValue(objNode, "category", string.Empty), GetValue(objNode, "name", string.Empty),
                    WeaponNodeHasSmartgun(objNode), objNode);
                objWeapon.SetWeaponDicePool(strPoolDisplay, strTooltip);
                objWeapon.SetAmmoStatus(ComputeAmmoStatus(objNode));
                if (!string.IsNullOrEmpty(strLocation) && dicLocations.TryGetValue(strLocation, out var objLocation))
                    objLocation.Children.Add(objWeapon);
                else
                    lstWeapons.Add(objWeapon);
            }
            return lstWeapons;
        }

        /// <summary>Flags <paramref name="objWeapon"/>'s direct children that came from
        /// &lt;accessories&gt;/&lt;weaponmods&gt; (matched by guid against the raw XML, since
        /// ReadTreeItem's Children collection loses which xpath each one was read from) so the UI
        /// can tell them apart from the weapon's Gear/Ammo children.</summary>
        private static void MarkWeaponAccessoriesAndMods(CharacterTreeItemData objWeapon, XmlNode objWeaponNode)
        {
            var setAccessoryGuids = new HashSet<string>(StringComparer.Ordinal);
            var objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                    setAccessoryGuids.Add(GetValue(objAccessoryNode, "guid", string.Empty));

            var setModGuids = new HashSet<string>(StringComparer.Ordinal);
            var objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    setModGuids.Add(GetValue(objModNode, "guid", string.Empty));

            foreach (CharacterTreeItemData objChild in objWeapon.Children)
            {
                if (!string.IsNullOrEmpty(objChild.ItemGuid) && setAccessoryGuids.Contains(objChild.ItemGuid))
                    objChild.IsWeaponAccessory = true;
                else if (!string.IsNullOrEmpty(objChild.ItemGuid) && setModGuids.Contains(objChild.ItemGuid))
                    objChild.IsWeaponMod = true;
            }
        }

        private bool WeaponNodeHasSmartgun(XmlNode objWeaponNode)
        {
            var objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                    if (GetValue(objAccessoryNode, "name", string.Empty).StartsWith("Smartgun System", StringComparison.Ordinal)
                        && GetValue(objAccessoryNode, "installed", "True") == "True")
                        return true;
            var objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                    if (GetValue(objModNode, "name", string.Empty).StartsWith("Smartgun System", StringComparison.Ordinal)
                        && GetValue(objModNode, "installed", "True") == "True")
                        return true;
            return false;
        }

        private IReadOnlyList<CharacterWeaponData> ReadWeapons()
        {
            var lstWeapons = new List<CharacterWeaponData>();
            var objNodes = Document.SelectNodes("/character/weapons/weapon");
            if (objNodes == null) return lstWeapons;
            foreach (XmlNode objNode in objNodes)
            {
                string strName = GetValue(objNode, "name", string.Empty);
                string strCategory = GetValue(objNode, "category", string.Empty);
                (string strPoolDisplay, string strTooltip) = ComputeWeaponDicePool(strCategory, strName,
                    WeaponNodeHasSmartgun(objNode), objNode);

                lstWeapons.Add(new CharacterWeaponData(strName, strCategory, GetValue(objNode, "damage", string.Empty),
                    GetValue(objNode, "ammo", string.Empty), GetValue(objNode, "ap", string.Empty),
                    GetValue(objNode, "rc", string.Empty), strPoolDisplay, strTooltip));
            }
            return lstWeapons;
        }

        // Ported from clsEquipment.cs's Weapon.DicePool: which Active Skill a weapon Category
        // rolls against. Not ported: the strRange-based "Special Weapons" disambiguation.
        private static readonly Dictionary<string, string> s_dicWeaponCategorySkills = new(StringComparer.Ordinal)
        {
            ["Bows"] = "Archery",
            ["Crossbows"] = "Archery",
            ["Assault Rifles"] = "Automatics",
            ["Machine Pistols"] = "Automatics",
            ["Submachine Guns"] = "Automatics",
            ["Battle Rifles"] = "Automatics",
            ["Blades"] = "Blades",
            ["Cyberware Blades"] = "Blades",
            ["Clubs"] = "Clubs",
            ["Assault Cannons"] = "Heavy Weapons",
            ["Grenade Launchers"] = "Heavy Weapons",
            ["Missile Launchers"] = "Heavy Weapons",
            ["Mortar Launchers"] = "Heavy Weapons",
            ["Light Machine Guns"] = "Heavy Weapons",
            ["Medium Machine Guns"] = "Heavy Weapons",
            ["Heavy Machine Guns"] = "Heavy Weapons",
            ["Shotguns"] = "Longarms",
            ["Sniper Rifles"] = "Longarms",
            ["Sports Rifles"] = "Longarms",
            ["Throwing Weapons"] = "Throwing Weapons",
            ["Cyberware Throwing Weapons"] = "Throwing Weapons",
            ["Unarmed"] = "Unarmed Combat",
            ["Cyberware Clubs"] = "Unarmed Combat",
            ["Cyberware"] = "Unarmed Combat"
        };

        // A Smartgun System only grants its bonus for skills SR4 actually pairs with smartguns.
        private static readonly HashSet<string> s_setSmartlinkEligibleSkills = new(StringComparer.Ordinal)
        {
            "Automatics", "Exotic Ranged Weapon", "Heavy Weapons", "Longarms", "Pistols"
        };

        private (string PoolDisplay, string Tooltip) ComputeWeaponDicePool(string strCategory, string strWeaponName,
            bool blnHasSmartgun, XmlNode? objWeaponNode = null)
        {
            // A per-weapon UseSkill override (e.g. a homebrew Natural Weapon linked to a chosen
            // Combat Active Skill, see AddNaturalWeapon) takes priority over the Category mapping.
            string strUseSkillOverride = objWeaponNode != null ? GetValue(objWeaponNode, "useskill", string.Empty) : string.Empty;
            string strSkillName = !string.IsNullOrEmpty(strUseSkillOverride)
                ? strUseSkillOverride
                : s_dicWeaponCategorySkills.TryGetValue(strCategory, out var strMapped)
                    ? strMapped
                    : strCategory is "Exotic Melee Weapons" or "Exotic Ranged Weapons" or "Cyberware Exotic Melee Weapons"
                        or "Cyberware Exotic Ranged Weapons"
                        ? (strCategory.Contains("Melee") ? "Exotic Melee Weapon" : "Exotic Ranged Weapon")
                        : "Pistols";

            CharacterSkillData? objSkill = Skills.FirstOrDefault(s => s.Name == strSkillName
                && (!s.Exotic || s.Specialization == strWeaponName));
            if (objSkill == null)
                return (string.Empty, string.Empty);

            int intPool = int.TryParse(objSkill.TotalValue, out var intParsed) ? intParsed : 0;
            var sb = new StringBuilder();
            sb.Append("Fertigkeit: ").Append(objSkill.Name).Append(" (").Append(objSkill.TotalValue).Append(')');

            int intSmartlinkBonus = 0;
            if (blnHasSmartgun && s_setSmartlinkEligibleSkills.Contains(strSkillName))
            {
                intSmartlinkBonus = ImprovementManager.ValueOf(Improvements, ImprovementType.Smartlink);
                if (intSmartlinkBonus != 0)
                    sb.Append('\n').Append("Smartgun System: ").Append(FormatSigned(intSmartlinkBonus));
            }

            intPool += intSmartlinkBonus;

            if (objWeaponNode != null)
            {
                intPool += SumInstalledAccessoryAndModDicePool(objWeaponNode, sb);
                intPool += SumLoadedAmmoDicePoolBonus(objWeaponNode, sb);
            }

            string strDisplay = intPool.ToString();

            if (!string.IsNullOrEmpty(objSkill.Specialization)
                && (objSkill.Specialization == strWeaponName || objSkill.Specialization == strCategory))
            {
                strDisplay += " (" + (intPool + 2) + ")";
                sb.Append('\n').Append("Spezialisierung \"").Append(objSkill.Specialization).Append("\": +2");
            }

            sb.Append('\n').Append("Würfelpool: ").Append(strDisplay);
            return (strDisplay, sb.ToString());
        }

        /// <summary>Ported from clsEquipment.cs's Weapon.DicePool's Ammo-derived pool bonus: the
        /// currently loaded ammo Gear item's rules-data &lt;weaponbonus&gt;/&lt;pool&gt; value
        /// (e.g. Ammo: High-Power Rounds' -2, Ammo: Deathdealer's +1), if any.</summary>
        private int SumLoadedAmmoDicePoolBonus(XmlNode objWeaponNode, StringBuilder sb)
        {
            int intGearId = int.TryParse(GetValue(objWeaponNode, "ammoloaded", "-1"), out var g) ? g : -1;
            if (intGearId < 0)
                return 0;

            XmlNode? objAmmoGear = GetGearNodeById(intGearId);
            string strAmmoName = objAmmoGear != null ? GetValue(objAmmoGear, "name", string.Empty) : string.Empty;
            if (string.IsNullOrEmpty(strAmmoName))
                return 0;

            XmlDocument objGearDoc = XmlManager.Instance.Load("gear.xml");
            XmlNode? objXmlAmmo = objGearDoc.SelectSingleNode($"/chummer/gears/gear[name = '{strAmmoName}']");
            string strPool = objXmlAmmo?.SelectSingleNode("weaponbonus/pool")?.InnerText ?? string.Empty;
            if (!int.TryParse(strPool, out int intBonus) || intBonus == 0)
                return 0;

            sb.Append('\n').Append(strAmmoName).Append(": ").Append(FormatSigned(intBonus));
            return intBonus;
        }

        /// <summary>Ported from clsEquipment.cs's Weapon.DicePool: sums each installed Weapon
        /// Accessory's/Mod's own rules-data &lt;dicepool&gt; value (a plain integer, or "Rating"/
        /// "-Rating" for Mods whose bonus scales with their own Rating).</summary>
        private int SumInstalledAccessoryAndModDicePool(XmlNode objWeaponNode, StringBuilder sb)
        {
            int intTotal = 0;
            XmlDocument objWeaponsDoc = XmlManager.Instance.Load("weapons.xml");

            XmlNodeList? objAccessoryNodes = objWeaponNode.SelectNodes("accessories/accessory");
            if (objAccessoryNodes != null)
                foreach (XmlNode objAccessoryNode in objAccessoryNodes)
                {
                    if (GetValue(objAccessoryNode, "installed", "True") != "True")
                        continue;
                    string strName = GetValue(objAccessoryNode, "name", string.Empty);
                    XmlNode? objXmlAccessory = objWeaponsDoc.SelectSingleNode(
                        $"/chummer/accessories/accessory[name = '{strName}']");
                    int intBonus = (int)RatingExpression.Evaluate(
                        objXmlAccessory?.SelectSingleNode("dicepool")?.InnerText ?? string.Empty, "0");
                    if (intBonus == 0)
                        continue;
                    intTotal += intBonus;
                    sb.Append('\n').Append(strName).Append(": ").Append(FormatSigned(intBonus));
                }

            XmlNodeList? objModNodes = objWeaponNode.SelectNodes("weaponmods/weaponmod");
            if (objModNodes != null)
                foreach (XmlNode objModNode in objModNodes)
                {
                    if (GetValue(objModNode, "installed", "True") != "True")
                        continue;
                    string strName = GetValue(objModNode, "name", string.Empty);
                    string strRating = GetValue(objModNode, "rating", "0");
                    XmlNode? objXmlMod = objWeaponsDoc.SelectSingleNode($"/chummer/mods/mod[name = '{strName}']");
                    int intBonus = (int)RatingExpression.Evaluate(
                        objXmlMod?.SelectSingleNode("dicepool")?.InnerText ?? string.Empty, strRating);
                    if (intBonus == 0)
                        continue;
                    intTotal += intBonus;
                    sb.Append('\n').Append(strName).Append(": ").Append(FormatSigned(intBonus));
                }

            return intTotal;
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

        /// <summary>Ported from clsCharacter.cs's PrintToStream PrintLeadershipAlternates/
        /// PrintArcanaAlternates blocks: a synthetic print-only copy of <paramref name="skill"/>
        /// under a different name/linked Attribute (e.g. "Leadership, Command" uses LOG instead of
        /// Leadership's own CHA), sharing its Rating/Specialization/SkillGroup but with its own
        /// recomputed dice pool for the substitute Attribute.</summary>
        public CharacterSkillData BuildAlternateSkillForPrint(CharacterSkillData skill, string strSuffix, string strAttribute)
        {
            int intRating = int.TryParse(skill.BaseRating, out var r) ? r : 0;
            bool blnCanDefault = skill.KnowledgeSkill || (!skill.Exotic && SkillAllowsDefaulting(skill.Name));
            (string strRatingDisplay, int intPool, string strTooltip) = ComputeSkillDicePool(
                skill.Name, skill.SkillGroup, skill.Category, strAttribute, intRating, skill.Specialization, blnCanDefault);
            return new CharacterSkillData(skill.SkillId, skill.Name + ", " + strSuffix, strAttribute,
                skill.BaseRating, strRatingDisplay, intPool.ToString(CultureInfo.InvariantCulture), strTooltip,
                skill.Specialization, skill.Category, skill.IsGroupLocked, blnAllowDelete: false,
                blnKnowledgeSkill: false, skill.SkillGroup, skill.Exotic);
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

            bool blnExotic = GetValue(objNode, "exotic", "False") == "True";
            // Knowledge/Language Skills can always be used untrained (SR4 65); active Skills can
            // only default if skills.xml says so (exotic Active Skills never allow it).
            bool blnCanDefault = blnKnowledge || (!blnExotic && SkillAllowsDefaulting(strName));
            (string strRatingDisplay, int intPool, string strTooltip) = ComputeSkillDicePool(
                strName, strSkillGroup, strCategory, strAttribute, intRating, strSpecialization, blnCanDefault);

            return new CharacterSkillData(intSkillId, strName, strAttribute, intRating.ToString(), strRatingDisplay,
                intPool.ToString(), strTooltip, strSpecialization, strCategory, blnIsGroupLocked, blnAllowDelete,
                blnKnowledge, strSkillGroup, blnExotic);
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
        /// Articulation, and the metatype-talent MetaRatingModifier bonus.
        /// </summary>
        private (string RatingDisplay, int Pool, string Tooltip) ComputeSkillDicePool(string strName,
            string strSkillGroup, string strCategory, string strAttribute, int intRating, string strSpecialization,
            bool blnCanDefault)
        {
            var objOptions = GetCharacterOptions();
            var lstRatingContributions = SkillImprovementContributions(strName, strSpecialization, strSkillGroup, strCategory, blnAddToRating: true);
            var lstPoolContributions = SkillImprovementContributions(strName, strSpecialization, strSkillGroup, strCategory, blnAddToRating: false);
            int intRatingMod = lstRatingContributions.Sum(c => c.Value);
            int intPoolMod = lstPoolContributions.Sum(c => c.Value);
            int intAttributeValue = GetAttributeInt(strAttribute);
            int intWound = WoundModifiers;
            int intAugmentedRating = intRating + intRatingMod;

            string strRatingDisplay = intRatingMod == 0
                ? intRating.ToString()
                : intRating + " (" + intAugmentedRating + ")";

            var sb = new StringBuilder();
            sb.Append("Fertigkeitswert: ").Append(intRating);
            int intPool;
            if (intRating == 0 && blnCanDefault)
            {
                // Ported from clsUnique.cs's Skill.TotalRating defaulting branch: a Rating-0 Skill
                // that allows defaulting rolls Attribute - 1, optionally including the Rating/Pool
                // Improvements if the house rule is on.
                intPool = intAttributeValue - 1;
                sb.Append(" (default)\n").Append("Attribut (").Append(strAttribute).Append(") - 1: ").Append(intPool);
                if (objOptions.SkillDefaultingIncludesModifiers)
                {
                    int intModSum = intRatingMod + intPoolMod;
                    intPool += intModSum;
                    AppendContributions(sb, lstRatingContributions);
                    AppendContributions(sb, lstPoolContributions);
                }

                if (objOptions.CapSkillRating)
                {
                    int intMax = Math.Max(20, (GetAttributeBaseInt(strAttribute) + intRating) * 2);
                    intPool = Math.Min(intMax, intPool);
                }

                intPool += intWound;
            }
            else
            {
                // House rule: the modified Rating (before DicePoolModifiers/Attribute) may not
                // exceed 1.5x the base Rating, rounded down.
                int intPoolRatingContribution = intAugmentedRating;
                if (objOptions.EnforceMaximumSkillRatingModifier)
                {
                    int intMaxModified = (int)Math.Floor(intRating * 1.5);
                    if (intPoolRatingContribution > intMaxModified)
                        intPoolRatingContribution = intMaxModified;
                }

                intPool = intRating == 0
                    ? 0
                    : intPoolRatingContribution + intPoolMod + intAttributeValue + intWound;

                // House rule: cap the total pool to the greater of 20 or 2x (natural, unaugmented
                // attribute + base Rating).
                if (objOptions.CapSkillRating)
                {
                    int intMax = Math.Max(20, (GetAttributeBaseInt(strAttribute) + intRating) * 2);
                    intPool = Math.Min(intMax, intPool);
                }

                AppendContributions(sb, lstRatingContributions);
                if (objOptions.EnforceMaximumSkillRatingModifier && intPoolRatingContribution != intAugmentedRating)
                    sb.Append('\n').Append("(Hausregel: max. 1,5x Fertigkeitswert -> ").Append(intPoolRatingContribution).Append(')');
                sb.Append('\n').Append("Attribut (").Append(strAttribute).Append("): ").Append(intAttributeValue);
                AppendContributions(sb, lstPoolContributions);
            }

            intPool = Math.Max(0, intPool);

            if (intWound != 0)
                sb.Append('\n').Append("Verletzungsmodifikator: ").Append(FormatSigned(intWound));
            if (!string.IsNullOrEmpty(strSpecialization))
                sb.Append('\n').Append("Spezialisierung \"").Append(strSpecialization).Append("\": +2 bei Anwendung");
            sb.Append('\n').Append("Würfelpool: ").Append(intPool);

            return (strRatingDisplay, intPool, sb.ToString());
        }

        private IReadOnlyList<(string SourceName, int Value)> SkillImprovementContributions(string strName,
            string strSpecialization, string strSkillGroup, string strCategory, bool blnAddToRating)
        {
            var lstContributions = new List<(string SourceName, int Value)>(
                ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Skill, strName, blnAddToRating));
            // Ported from clsUnique.cs's Skill pool calc, which always checks both the bare Name
            // and "Name (Specialization)" forms - the latter is how selectskill Improvements on
            // Exotic Skills (which all share the same bare Name, only distinguished by
            // Specialization - e.g. "Exotic Ranged Weapon (Bow)" vs. "...(Grenade Launcher)") get
            // stored, so a bonus picked for one specific Exotic Skill instance doesn't bleed onto
            // every other skill sharing its bare Name.
            if (!string.IsNullOrEmpty(strSpecialization))
                lstContributions.AddRange(ImprovementManager.DescribeValueOf(Improvements, ImprovementType.Skill,
                    strName + " (" + strSpecialization + ")", blnAddToRating));
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
                        blnIsEnemy,
                        GetValue(objNode, "notes", string.Empty),
                        GetValue(objNode, "free", "False") == "True",
                        GetValue(objNode, "groupname", string.Empty),
                        ParseInt(GetValue(objNode, "membership", "0")),
                        ParseInt(GetValue(objNode, "areaofinfluence", "0")),
                        ParseInt(GetValue(objNode, "magicalresources", "0")),
                        ParseInt(GetValue(objNode, "matrixresources", "0")),
                        GetValue(objNode, "file", string.Empty),
                        GetValue(objNode, "relative", string.Empty)));
                }

                intContactId++;
            }

            return lstContacts;
        }

        private IReadOnlyList<CharacterContactData> ReadPets()
        {
            var lstPets = new List<CharacterContactData>();
            var objNodes = Document.SelectNodes("/character/contacts/contact");
            if (objNodes == null) return lstPets;
            int intContactId = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "type", "Contact") == "Pet")
                {
                    lstPets.Add(new CharacterContactData(intContactId, GetValue(objNode, "name", string.Empty),
                        GetValue(objNode, "connection", "0"), GetValue(objNode, "loyalty", "0"), false,
                        GetValue(objNode, "notes", string.Empty), GetValue(objNode, "free", "False") == "True",
                        GetValue(objNode, "groupname", string.Empty), ParseInt(GetValue(objNode, "membership", "0")),
                        ParseInt(GetValue(objNode, "areaofinfluence", "0")), ParseInt(GetValue(objNode, "magicalresources", "0")),
                        ParseInt(GetValue(objNode, "matrixresources", "0")),
                        GetValue(objNode, "file", string.Empty), GetValue(objNode, "relative", string.Empty)));
                }
                intContactId++;
            }
            return lstPets;
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
                    GetValue(objNode, "rating", "0"), GetValue(objNode, "source", string.Empty),
                    GetValue(objNode, "page", string.Empty), lstAdvantages));
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
            {
                string strCategory = GetValue(objNode, "category", string.Empty);
                (int intPool, string strTooltip) = ComputeSpellDicePool(strCategory);
                lstSpells.Add(new CharacterSpellData(GetValue(objNode, "name", string.Empty),
                    strCategory, GetValue(objNode, "type", string.Empty),
                    GetValue(objNode, "range", string.Empty), GetValue(objNode, "damage", string.Empty),
                    GetValue(objNode, "duration", string.Empty), GetValue(objNode, "dv", string.Empty),
                    GetValue(objNode, "source", string.Empty), GetValue(objNode, "page", string.Empty),
                    intPool.ToString(), strTooltip));
            }
            return lstSpells;
        }

        /// <summary>Ported from clsUnique.cs's Spell.DicePool/DicePoolTooltip: the Spellcasting
        /// skill's TotalRating, +2 if the skill's own Specialization matches the spell's Category,
        /// plus any SpellCategory Improvements targeting that Category.</summary>
        private (int Pool, string Tooltip) ComputeSpellDicePool(string strCategory)
        {
            CharacterSkillData? objSpellcasting = Skills.FirstOrDefault(s => s.Name == "Spellcasting");
            int intSkillRating = objSpellcasting != null && int.TryParse(objSpellcasting.TotalValue, out var r) ? r : 0;
            bool blnSpecializationMatches = objSpellcasting != null && objSpellcasting.Specialization == strCategory;

            var lstContributions = ImprovementManager.DescribeValueOf(Improvements, ImprovementType.SpellCategory, strCategory);
            int intCategoryBonus = lstContributions.Sum(c => c.Value);
            int intTotal = intSkillRating + (blnSpecializationMatches ? 2 : 0) + intCategoryBonus;

            var sb = new StringBuilder();
            if (objSpellcasting != null)
            {
                sb.Append("Zaubern: ").Append(intSkillRating);
                if (blnSpecializationMatches)
                    sb.Append('\n').Append("Spezialisierung (").Append(strCategory).Append("): +2");
            }
            AppendContributions(sb, lstContributions);
            sb.Append('\n').Append("Würfelpool: ").Append(intTotal);
            return (intTotal, sb.ToString());
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
                    GetValue(objNode, "cost", "0"), GetValue(objNode, "months", "0"),
                    GetValue(objNode, "dice", string.Empty), GetValue(objNode, "multiplier", string.Empty)));
            return lstLifestyles;
        }

        private IReadOnlyList<CharacterVehicleData> ReadVehicles()
        {
            var lstVehicles = new List<CharacterVehicleData>();
            var objNodes = Document.SelectNodes("/character/vehicles/vehicle");
            if (objNodes == null) return lstVehicles;
            bool blnUseCalculatedSensor = GetCharacterOptions().UseCalculatedVehicleSensorRatings;
            foreach (XmlNode objNode in objNodes)
            {
                var objVehicle = new CharacterVehicleData(
                    GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "name", string.Empty), GetValue(objNode, "category", string.Empty),
                    GetValue(objNode, "handling", string.Empty), GetValue(objNode, "accel", string.Empty),
                    GetValue(objNode, "speed", string.Empty), GetValue(objNode, "pilot", string.Empty),
                    GetValue(objNode, "body", string.Empty), GetValue(objNode, "armor", string.Empty),
                    GetValue(objNode, "sensor", string.Empty), GetValue(objNode, "devicerating", string.Empty),
                    GetValue(objNode, "avail", string.Empty), GetValue(objNode, "cost", string.Empty),
                    GetValue(objNode, "addslots", string.Empty), GetValue(objNode, "source", string.Empty),
                    GetValue(objNode, "page", string.Empty), GetValue(objNode, "physicalcmfilled", "0"));
                AddVehicleChildren(objVehicle.Children, objNode.SelectNodes("mods/mod"), "Vehicle Mod");
                AddVehicleChildren(objVehicle.Children, objNode.SelectNodes("gears/gear"), "Gear");
                AddVehicleChildren(objVehicle.Children, objNode.SelectNodes("weapons/weapon"), "Weapon");
                XmlNodeList? objLocations = objNode.SelectNodes("locations/location");
                if (objLocations != null)
                    foreach (XmlNode objLocation in objLocations)
                        if (!string.IsNullOrWhiteSpace(objLocation.InnerText)) objVehicle.Locations.Add(objLocation.InnerText);
                objVehicle.SetSensorDisplay(blnUseCalculatedSensor
                    ? objVehicle.CalculatedSensor.ToString(CultureInfo.InvariantCulture)
                    : objVehicle.Sensor);
                lstVehicles.Add(objVehicle);
            }
            return lstVehicles;
        }

        private void AddVehicleChildren(List<CharacterTreeItemData> lstChildren, XmlNodeList? objNodes,
            string strFallbackCategory)
        {
            if (objNodes == null)
                return;

            foreach (XmlNode objNode in objNodes)
            {
                var objItem = new CharacterTreeItemData(GetValue(objNode, "name", string.Empty),
                    GetValue(objNode, "category", strFallbackCategory), GetValue(objNode, "rating", "0"),
                    GetValue(objNode, "installed", GetValue(objNode, "equipped", "False")) == "True",
                    GetValue(objNode, "cost", string.Empty), GetValue(objNode, "avail", string.Empty),
                    GetValue(objNode, "qty", "1"));
                objItem.SetItemGuid(GetValue(objNode, "guid", string.Empty));
                if (strFallbackCategory == "Vehicle Mod")
                    objItem.SetModSlots(GetValue(objNode, "slots", "0"), GetValue(objNode, "included", "False") == "True");
                if (strFallbackCategory == "Weapon")
                {
                    objItem.IsVehicleWeapon = true;
                    objItem.SetAmmoStatus(ComputeAmmoStatus(objNode));
                }
                if (strFallbackCategory == "Gear")
                {
                    objItem.SetLocation(GetValue(objNode, "location", string.Empty));
                    // Only Signal is actually used (CalculatedSensor) - Capacity/Response/System/
                    // Firewall aren't meaningful for vehicle-mounted gear the way they are for the
                    // root Gear tree's Commlinks, so they're left blank here.
                    objItem.SetGearDetails(string.Empty, string.Empty,
                        GetValue(objNode, "signal", string.Empty), string.Empty, string.Empty, blnActive: false);
                }
                AddVehicleChildren(objItem.Children, objNode.SelectNodes("children/gear"), "Gear");
                AddVehicleChildren(objItem.Children, objNode.SelectNodes("weapons/weapon"), "Weapon");
                lstChildren.Add(objItem);
            }
        }

        private IReadOnlyList<CharacterExpenseData> ReadExpenses(string strType)
        {
            var lstExpenses = new List<CharacterExpenseData>();
            var objNodes = Document.SelectNodes("/character/expenses/expense");
            if (objNodes == null) return lstExpenses;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "type", string.Empty) != strType) continue;
                lstExpenses.Add(new CharacterExpenseData(GetValue(objNode, "guid", string.Empty),
                    GetValue(objNode, "date", string.Empty),
                    GetValue(objNode, "amount", "0"), GetValue(objNode, "reason", string.Empty),
                    GetValue(objNode, "refund", "False") == "True"));
            }

            return lstExpenses;
        }

        private IReadOnlyList<CharacterCommlinkData> ReadCommlinks()
        {
            var lstCommlinks = new List<CharacterCommlinkData>();
            XmlNodeList? objNodes = Document.SelectNodes("//gear[category = 'Commlink']");
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
                GetValue(objNode, "equipped", "False") == "True",
                GetValue(objNode, "cost", string.Empty), GetValue(objNode, "avail", string.Empty),
                GetValue(objNode, "qty", "1"));
            objItem.SetItemGuid(GetValue(objNode, "guid", string.Empty));
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
            string strMaximum, string strAugmentedMaximum, CharacterDerivedValueData augmented, int intKarmaCostToIncrease)
        {
            Code = strCode;
            Value = strValue;
            TotalValue = strTotalValue;
            Minimum = strMinimum;
            Maximum = strMaximum;
            AugmentedMaximum = strAugmentedMaximum;
            Augmented = augmented;
            KarmaCostToIncrease = intKarmaCostToIncrease;
        }

        public string Code { get; private set; }
        public string Value { get; private set; }
        public string TotalValue { get; private set; }
        public string Minimum { get; private set; }
        public string Maximum { get; private set; }
        public string AugmentedMaximum { get; private set; }

        /// <summary>TotalValue plus any Attribute-type Improvement bonuses (e.g. Wired Reflexes'
        /// Reaction boost) that raise the augmented value without changing TotalValue itself.</summary>
        public CharacterDerivedValueData Augmented { get; private set; }

        /// <summary>Karma cost to raise this attribute's base Value by one point, ported from
        /// frmCareer.cs's cmdImprove&lt;Attribute&gt;_Click handlers.</summary>
        public int KarmaCostToIncrease { get; private set; }
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

    /// <summary>Substitutes "Rating" into a rules-data cost/avail/essence formula (e.g.
    /// "Rating * 3000") and evaluates it, same technique as legacy clsEquipment.cs. Shared by
    /// <see cref="CharacterTreeItemData"/>'s CalculatedCost/CalculatedAvail (post-save, resolved
    /// from the saved character) and any picker UI that needs to preview the value for a rating
    /// the user hasn't committed to yet.</summary>
    public static class RatingExpression
    {
        public static double Evaluate(string strExpression, string strRating)
        {
            if (string.IsNullOrEmpty(strExpression)) return 0;
            var strSubstituted = strExpression.Replace("Rating", strRating);
            try
            {
                var objNavigator = new System.Xml.XPath.XPathDocument(new StringReader("<i/>")).CreateNavigator();
                var objExpression = objNavigator.Compile(strSubstituted);
                return Convert.ToDouble(objNavigator.Evaluate(objExpression), CultureInfo.InvariantCulture);
            }
            catch
            {
                return double.TryParse(strSubstituted, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblValue)
                    ? dblValue
                    : 0;
            }
        }
    }

    public sealed class CharacterTreeItemData
    {
        internal CharacterTreeItemData(string strName, string strCategory = "", string strRating = "0",
            bool blnEquipped = false, string strCost = "", string strAvail = "", string strQty = "1")
        {
            Name = strName;
            TranslatedName = strName;
            Category = strCategory;
            Rating = strRating;
            Equipped = blnEquipped;
            Cost = strCost;
            Avail = strAvail;
            Qty = strQty;
            Children = new List<CharacterTreeItemData>();
        }

        public string Name { get; private set; }

        /// <summary>Name to display in the UI - the &lt;translate&gt; value from the data file if
        /// the current language pack provides one for this item, otherwise the same as Name. Only
        /// set (non-default) for Gear tree nodes so far.</summary>
        public string TranslatedName { get; private set; }

        internal void SetTranslatedName(string strTranslatedName) => TranslatedName = strTranslatedName;

        /// <summary>Empty for item types that don't save one (e.g. Quality nodes don't reuse this
        /// class). Gear/Cyberware/Armor/Weapon all use the same &lt;category&gt; element name.</summary>
        public string Category { get; }

        public string Rating { get; }

        /// <summary>Whether the item is currently worn/active/installed - only meaningful for
        /// item types that track it (Gear, Armor, Cyberware all do; not everything does).</summary>
        public bool Equipped { get; }

        /// <summary>Raw saved cost - a plain number or a "Rating"-formula string. Use CalculatedCost.</summary>
        public string Cost { get; }

        /// <summary>Raw saved availability, e.g. "6R". Use CalculatedAvail.</summary>
        public string Avail { get; }

        public string Qty { get; }

        /// <summary>Raw saved ballistic/impact armor rating (e.g. "+3") - only set for Armor tree
        /// nodes, empty otherwise. See CharacterDocument.ArmorEncumbrance for the aggregate rules.</summary>
        public string Ballistic { get; private set; } = string.Empty;

        public string Impact { get; private set; } = string.Empty;
        public string ArmorSetName { get; private set; } = string.Empty;
        public string Location { get; private set; } = string.Empty;
        /// <summary>Persisted GUID for items that have one (including vehicle modifications).</summary>
        public string ItemGuid { get; private set; } = string.Empty;

        /// <summary>Raw saved slots ("Rating"-formula string) - only set for Vehicle Mod nodes.
        /// See CharacterVehicleData.SlotsUsed for the evaluated total.</summary>
        public string ModSlots { get; private set; } = string.Empty;

        /// <summary>Mods that come pre-installed with the vehicle don't consume purchased slots -
        /// only meaningful when <see cref="IsVehicleMod"/> is true.</summary>
        public bool IncludedInVehicle { get; private set; }

        /// <summary>True for nodes built from a vehicle's &lt;mods&gt;&lt;mod&gt; list - a saved
        /// Category isn't a reliable way to tell (mods keep their real rules category, e.g.
        /// "Standard", not a generic marker), so this is set explicitly instead.</summary>
        public bool IsVehicleMod { get; private set; }

        /// <summary>True for nodes built from a &lt;weapons&gt;&lt;weapon&gt; list nested under a
        /// vehicle or vehicle Mod - same rationale as IsVehicleMod (Category is the weapon's real
        /// rules category, not a marker).</summary>
        public bool IsVehicleWeapon { get; internal set; }

        /// <summary>True for a root weapon's &lt;accessories&gt;&lt;accessory&gt; children (used to
        /// tell them apart from the weapon's own WeaponMod/Gear/Ammo children for add/remove and
        /// display purposes) - accessory nodes have no Category of their own.</summary>
        public bool IsWeaponAccessory { get; internal set; }

        /// <summary>True for a root weapon's &lt;weaponmods&gt;&lt;weaponmod&gt; children - same
        /// rationale as IsWeaponAccessory.</summary>
        public bool IsWeaponMod { get; internal set; }

        internal void SetArmorRatings(string strBallistic, string strImpact)
        {
            Ballistic = strBallistic;
            Impact = strImpact;
        }

        internal void SetArmorSetName(string strSetName) => ArmorSetName = strSetName;
        internal void SetLocation(string strLocation) => Location = strLocation;
        internal void SetItemGuid(string strItemGuid) => ItemGuid = strItemGuid;
        internal void SetModSlots(string strSlots, bool blnIncluded)
        {
            ModSlots = strSlots;
            IncludedInVehicle = blnIncluded;
            IsVehicleMod = true;
        }

        /// <summary>Only set (non-empty) for Weapon root nodes - see
        /// CharacterDocument.ComputeWeaponDicePool.</summary>
        public string WeaponDicePool { get; private set; } = string.Empty;

        public string WeaponDicePoolTooltip { get; private set; } = string.Empty;

        internal void SetWeaponDicePool(string strDicePool, string strTooltip)
        {
            WeaponDicePool = strDicePool;
            WeaponDicePoolTooltip = strTooltip;
        }

        /// <summary>"12/30 (Ammo: Regular Ammo)" - style summary of what's currently loaded, empty
        /// when nothing is loaded. Only set for root Weapon nodes. See
        /// CharacterDocument.ReloadWeapon/GetWeaponAmmoOptions.</summary>
        public string AmmoStatus { get; private set; } = string.Empty;

        internal void SetAmmoStatus(string strAmmoStatus) => AmmoStatus = strAmmoStatus;

        /// <summary>Depth-first position within the whole &lt;gears&gt; tree - only set for Gear
        /// tree nodes (-1 otherwise). Stable identity for AddChildGear/RemoveGear/SetGearQuantity,
        /// since name+category+rating isn't unique once gear can nest under other gear.</summary>
        public int GearId { get; private set; } = -1;

        internal void SetGearId(int intGearId) => GearId = intGearId;

        /// <summary>Depth-first position within the whole &lt;cyberwares&gt; tree - only set for
        /// Cyberware/Bioware tree nodes (-1 otherwise). Same purpose as <see cref="GearId"/>, for
        /// drag/drop reorder/reparent.</summary>
        public int CyberwareId { get; private set; } = -1;

        internal void SetCyberwareId(int intCyberwareId) => CyberwareId = intCyberwareId;

        /// <summary>Raw saved capacity (e.g. "8" or "[2]") - only set for Gear tree nodes.</summary>
        public string Capacity { get; private set; } = string.Empty;

        /// <summary>Commlink stats - only set (non-empty) for Commlink-category Gear nodes.</summary>
        public string Response { get; private set; } = string.Empty;

        public string Signal { get; private set; } = string.Empty;
        public string System { get; private set; } = string.Empty;
        public string Firewall { get; private set; } = string.Empty;
        public bool Active { get; private set; }

        internal void SetGearDetails(string strCapacity, string strResponse, string strSignal, string strSystem,
            string strFirewall, bool blnActive)
        {
            Capacity = strCapacity;
            Response = strResponse;
            Signal = strSignal;
            System = strSystem;
            Firewall = strFirewall;
            Active = blnActive;
        }

        /// <summary>Own capacity minus the sum of children's own capacity (each child's Capacity is
        /// treated as how much of the parent's slots it consumes) - a simplified version of
        /// clsEquipment.cs's Gear.CapacityRemaining that doesn't handle bracketed "[x]" capacity
        /// styles or per-item capacity-consumption overrides.</summary>
        public string CapacityRemaining
        {
            get
            {
                double dblOwn = double.TryParse(Capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var d0) ? d0 : 0;
                double dblUsed = Children.Sum(c =>
                    double.TryParse(c.Capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0);
                return (dblOwn - dblUsed).ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>"capacity (remaining verbleibend)" - matches the legacy UI's always-shown
        /// capacity line, defaulting to 0 for items (e.g. Commlinks) that don't save a capacity.</summary>
        public string CapacityDisplay
        {
            get
            {
                double dblOwn = double.TryParse(Capacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var d0) ? d0 : 0;
                return $"{dblOwn.ToString("0.##", CultureInfo.InvariantCulture)} ({CapacityRemaining} verbleibend)";
            }
        }

        /// <summary>The highest Response/Signal/System/Firewall value found across this node and
        /// its descendants - e.g. a Commlink's base hardware supplies Response/Signal, an installed
        /// Operating System gear supplies System/Firewall, and Commlink/OS Upgrade gear (which save
        /// a flat replacement rating, not a bonus) can raise any of the four further.</summary>
        public string EffectiveResponse => EffectiveStat(g => g.Response);
        public string EffectiveSignal => EffectiveStat(g => g.Signal);
        public string EffectiveSystem => EffectiveStat(g => g.System);
        public string EffectiveFirewall => EffectiveStat(g => g.Firewall);

        // Legacy always saves a <response>/<signal>/<system>/<firewall> element on every Gear node
        // (defaulting to "0" for non-Commlink items), so a plain non-empty check here would treat
        // every piece of Gear as a Commlink - require a positive value instead.
        public bool HasCommlinkStats => IsPositive(EffectiveResponse) || IsPositive(EffectiveSignal)
            || IsPositive(EffectiveSystem) || IsPositive(EffectiveFirewall);

        private static bool IsPositive(string strValue)
            => double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0;

        private string EffectiveStat(Func<CharacterTreeItemData, string> funcSelector)
        {
            double? dblMax = null;
            CollectMaxStat(funcSelector, ref dblMax);
            return dblMax.HasValue ? dblMax.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        }

        private void CollectMaxStat(Func<CharacterTreeItemData, string> funcSelector, ref double? dblMax)
        {
            if (double.TryParse(funcSelector(this), NumberStyles.Float, CultureInfo.InvariantCulture, out var dblOwn))
                dblMax = dblMax.HasValue ? Math.Max(dblMax.Value, dblOwn) : dblOwn;
            foreach (CharacterTreeItemData objChild in Children)
                objChild.CollectMaxStat(funcSelector, ref dblMax);
        }

        public List<CharacterTreeItemData> Children { get; }

        /// <summary>Ported from clsEquipment.cs's Gear.TotalCost.</summary>
        public int CalculatedCost
        {
            get
            {
                var intQty = int.TryParse(Qty, out var intParsedQty) ? intParsedQty : 1;
                var intOwn = (int)Math.Ceiling(EvaluateRatingExpression(Cost, Rating)) * intQty;
                return intOwn + Children.Sum(objChild => objChild.CalculatedCost);
            }
        }

        /// <summary>Ported from clsEquipment.cs's Gear.TotalAvail.</summary>
        public string CalculatedAvail
        {
            get
            {
                if (string.IsNullOrEmpty(Avail)) return string.Empty;
                var strSuffix = string.Empty;
                var strExpression = Avail;
                var chLast = Avail[Avail.Length - 1];
                if (chLast == 'R' || chLast == 'F')
                {
                    strSuffix = chLast.ToString();
                    strExpression = Avail.Substring(0, Avail.Length - 1);
                }

                var intValue = (int)EvaluateRatingExpression(strExpression, Rating);
                return intValue + strSuffix;
            }
        }

        private static double EvaluateRatingExpression(string strExpression, string strRating)
            => RatingExpression.Evaluate(strExpression, strRating);
    }

    public sealed class CharacterWeaponData
    {
        internal CharacterWeaponData(string strName, string strCategory, string strDamage, string strAmmo,
            string strAp = "", string strRc = "", string strDicePool = "", string strDicePoolTooltip = "")
        {
            Name = strName;
            Category = strCategory;
            Damage = strDamage;
            Ammo = strAmmo;
            Ap = strAp;
            Rc = strRc;
            DicePool = strDicePool;
            DicePoolTooltip = strDicePoolTooltip;
        }

        public string Name { get; }
        public string Category { get; }
        public string Damage { get; }
        public string Ammo { get; private set; }
        public string Ap { get; }
        public string Rc { get; }

        /// <summary>Ported from clsEquipment.cs's Weapon.DicePool - the linked Active Skill's
        /// dice pool plus a Smartgun System bonus where applicable, formatted as "12" or
        /// "12 (14)" when a matching specialization applies. Empty if no matching Skill is found
        /// (e.g. a Skill the character never raised past a defaulting-disallowed 0).</summary>
        public string DicePool { get; }

        public string DicePoolTooltip { get; }

        public string DisplayName
        {
            get
            {
                var strDetails = string.IsNullOrEmpty(Damage) ? Category : Damage;
                return string.IsNullOrEmpty(strDetails) ? Name : Name + " (" + strDetails + ")";
            }
        }
    }

    /// <summary>Read-only vehicle record with its saved stats and installed mods, gear, and weapons.</summary>
    public sealed class CharacterVehicleData
    {
        internal CharacterVehicleData(string strGuid, string strName, string strCategory, string strHandling, string strAcceleration,
            string strSpeed, string strPilot, string strBody, string strArmor, string strSensor,
            string strDeviceRating, string strAvail, string strCost, string strSlots, string strSource,
            string strPage, string strPhysicalCmFilled)
        {
            Guid = strGuid;
            Name = strName;
            Category = strCategory;
            Handling = strHandling;
            Acceleration = strAcceleration;
            Speed = strSpeed;
            Pilot = strPilot;
            Body = strBody;
            Armor = strArmor;
            Sensor = strSensor;
            DeviceRating = strDeviceRating;
            Avail = strAvail;
            Cost = strCost;
            Slots = strSlots;
            Source = strSource;
            Page = strPage;
            PhysicalCmFilled = strPhysicalCmFilled;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Category { get; }
        public string Handling { get; }
        public string Acceleration { get; }
        public string Speed { get; }
        public string Pilot { get; }
        public string Body { get; }
        public string Armor { get; }
        public string Sensor { get; }
        public string DeviceRating { get; }
        public string Avail { get; }
        public string Cost { get; }
        public string Slots { get; }
        public string Source { get; }
        public string Page { get; }
        public string PhysicalCmFilled { get; }
        public List<string> Locations { get; } = new();
        public List<CharacterTreeItemData> Children { get; } = new();

        /// <summary>Ported from clsEquipment.cs's Vehicle.Slots: 4 or the vehicle's Body, whichever
        /// is higher (the AddSlots-from-mods refinement isn't ported - no shipped vehicle mod
        /// grants bonus slots via a plain flat &lt;addslots&gt; value in the data this port reads).</summary>
        public int TotalSlots => Math.Max(4, int.TryParse(Body, out var b) ? b : 0);

        /// <summary>Ported from clsEquipment.cs's Vehicle.SlotsUsed: sums each installed,
        /// not-included-by-default Mod's Rating-evaluated Slots cost.</summary>
        public int SlotsUsed => Children
            .Where(c => c.IsVehicleMod && !c.IncludedInVehicle)
            .Sum(c => (int)RatingExpression.Evaluate(c.ModSlots, c.Rating));

        public int SlotsRemaining => TotalSlots - SlotsUsed;

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalCost: the vehicle's own cost, plus
        /// every non-included Mod's own (Body-and-Rating-resolved) cost, plus - for Mods that came
        /// included with the vehicle - the cost of any Weapon/Gear attached to them (their own slot
        /// cost doesn't count, but what's mounted on them still does), plus every other direct
        /// onboard Gear/Weapon's own cost.</summary>
        public int TotalCost
        {
            get
            {
                double dblBody = double.TryParse(Body, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 0;
                double dblTotal = double.TryParse(Cost, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : 0;
                foreach (CharacterTreeItemData objChild in Children)
                {
                    if (!objChild.IsVehicleMod)
                    {
                        dblTotal += objChild.CalculatedCost;
                    }
                    else if (objChild.IncludedInVehicle)
                    {
                        dblTotal += objChild.Children.Sum(objGrandchild => objGrandchild.CalculatedCost);
                    }
                    else
                    {
                        string strCost = objChild.Cost.Replace("Body",
                            dblBody.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        dblTotal += RatingExpression.Evaluate(strCost, objChild.Rating);
                    }
                }

                return (int)Math.Ceiling(dblTotal);
            }
        }

        /// <summary>Ported from clsEquipment.cs's Vehicle.CalculatedSensor, faithfully including its
        /// "only ever looks at the first onboard Gear item" quirk (the loop's break is unconditional,
        /// outside the category check): averages the Rating of that first item's "Sensor Functions"
        /// category children (only if the first item is itself Category "Sensors" with a Signal
        /// value), rounded up; falls back to the saved Sensor value if there's no qualifying first
        /// item or it has no such children. Only used (see UseCalculatedVehicleSensorRatings) as an
        /// alternative to the saved value, never persisted over it.</summary>
        public int CalculatedSensor
        {
            get
            {
                CharacterTreeItemData? objFirst = Children.FirstOrDefault();
                if (objFirst is { Category: "Sensors" } && ParseInt(objFirst.Signal) > 0)
                {
                    var lstRatings = objFirst.Children.Where(c => c.Category == "Sensor Functions")
                        .Select(c => ParseInt(c.Rating)).Where(r => r > 0).ToList();
                    if (lstRatings.Count > 0)
                        return (int)Math.Ceiling(lstRatings.Average());
                }

                return ParseInt(Sensor);
            }
        }

        private static int ParseInt(string strValue) => int.TryParse(strValue, out var i) ? i : 0;

        /// <summary>What the UI/print export should actually show for Sensor - <see cref="Sensor"/>
        /// (the saved value) unless UseCalculatedVehicleSensorRatings is on, in which case it's
        /// <see cref="CalculatedSensor"/>. Set once by ReadVehicles, since only it has access to the
        /// character's settings profile.</summary>
        public string SensorDisplay { get; private set; } = string.Empty;

        internal void SetSensorDisplay(string strValue) => SensorDisplay = strValue;
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
            string strCategory, bool blnIsGroupLocked, bool blnAllowDelete, bool blnKnowledgeSkill,
            string strSkillGroup = "", bool blnExotic = false)
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
            SkillGroup = strSkillGroup;
            Exotic = blnExotic;
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
        public string SkillGroup { get; private set; }
        public bool Exotic { get; private set; }
    }

    public sealed class CharacterEdgeData
    {
        internal CharacterEdgeData(int intRemaining, int intMaximum)
        {
            Remaining = intRemaining;
            Maximum = intMaximum;
        }

        public int Remaining { get; }
        public int Maximum { get; }
    }

    public sealed class CharacterContactData
    {
        internal CharacterContactData(int intContactId, string strName, string strConnection, string strLoyalty,
            bool blnIsEnemy, string strNotes, bool blnFree, string strGroupName, int intMembership,
            int intAreaOfInfluence, int intMagicalResources, int intMatrixResources, string strFileName = "",
            string strRelativeFileName = "")
        {
            ContactId = intContactId;
            Name = strName;
            Connection = strConnection;
            Loyalty = strLoyalty;
            IsEnemy = blnIsEnemy;
            Notes = strNotes;
            Free = blnFree;
            GroupName = strGroupName;
            Membership = intMembership;
            AreaOfInfluence = intAreaOfInfluence;
            MagicalResources = intMagicalResources;
            MatrixResources = intMatrixResources;
            FileName = strFileName;
            RelativeFileName = strRelativeFileName;
        }

        public int ContactId { get; }
        public string Name { get; }
        public string Connection { get; }
        public string Loyalty { get; }
        public bool IsEnemy { get; }
        public string Notes { get; }

        /// <summary>Doesn't cost Karma/BP to add - matches the legacy "Free" checkbox.</summary>
        public bool Free { get; }

        /// <summary>Free-text profession/organization label for a Group contact (e.g. "Hacker").</summary>
        public string GroupName { get; }

        public int Membership { get; }
        public int AreaOfInfluence { get; }
        public int MagicalResources { get; }
        public int MatrixResources { get; }

        /// <summary>Absolute path of a character file linked to this contact, when available.</summary>
        public string FileName { get; }

        /// <summary>Path of the linked character relative to the application directory.</summary>
        public string RelativeFileName { get; }

        /// <summary>Sum of the four Group modifiers - adds to Connection+Loyalty in the cost
        /// formula, ported from frmSelectContactConnection.cs's Total Connection Modifier.</summary>
        public int GroupRating => Membership + AreaOfInfluence + MagicalResources + MatrixResources;
    }

    public sealed class CharacterMartialArtData
    {
        internal CharacterMartialArtData(string strName, string strRating, string strSource, string strPage,
            IReadOnlyList<string> lstAdvantages)
        {
            Name = strName;
            Rating = strRating;
            Source = strSource;
            Page = strPage;
            Advantages = lstAdvantages;
        }

        public string Name { get; }
        public string Rating { get; }
        public string Source { get; }
        public string Page { get; }
        public string SourcePage => string.IsNullOrWhiteSpace(Page) ? Source : Source + " " + Page;
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

    public sealed class CharacterMetamagicData
    {
        internal CharacterMetamagicData(string strGuid, string strName, string strSource, string strPage)
        {
            Guid = strGuid;
            Name = strName;
            Source = strSource;
            Page = strPage;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Source { get; }
        public string Page { get; }
        public string SourcePage => string.IsNullOrWhiteSpace(Page) ? Source : Source + " " + Page;
    }

    public sealed class CharacterComplexFormData
    {
        internal CharacterComplexFormData(string strGuid, string strName, string strCategory, string strExtra,
            string strRating, IReadOnlyList<(string Name, string Rating)> lstOptions)
        {
            Guid = strGuid;
            Name = strName;
            Category = strCategory;
            Extra = strExtra;
            Rating = strRating;
            Options = lstOptions;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Category { get; }
        public string Extra { get; }
        public string Rating { get; }
        public IReadOnlyList<(string Name, string Rating)> Options { get; }
        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";
    }

    public sealed class CharacterCritterPowerData
    {
        internal CharacterCritterPowerData(string strGuid, string strName, string strExtra, string strPoints,
            string strRating = "0")
        {
            Guid = strGuid;
            Name = strName;
            Extra = strExtra;
            Points = strPoints;
            Rating = strRating;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Extra { get; }
        public string Points { get; }

        /// <summary>Ported from clsUnique.cs's CritterPower.Rating - only meaningful for powers
        /// whose rules-data entry sets &lt;rating&gt;yes&lt;/rating&gt; (e.g. Armor (Ballistic));
        /// "0" for every other power.</summary>
        public string Rating { get; }
        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";
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
            string strDamage, string strDuration, string strDv, string strSource, string strPage,
            string strDicePool = "0", string strDicePoolTooltip = "")
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
            DicePool = strDicePool;
            DicePoolTooltip = strDicePoolTooltip;
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

        /// <summary>Ported from clsUnique.cs's Spell.DicePool: the Spellcasting skill's
        /// TotalRating, +2 if the skill's own Specialization matches this spell's Category, plus
        /// any SpellCategory Improvements. "0" if the character has no Spellcasting skill at all.</summary>
        public string DicePool { get; }
        public string DicePoolTooltip { get; }
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
        internal CharacterLifestyleData(string strName, string strCost, string strMonths, string strDice,
            string strMultiplier)
        {
            Name = strName;
            Cost = strCost;
            Months = strMonths;
            Dice = strDice;
            Multiplier = strMultiplier;
        }

        public string Name { get; }
        public string Cost { get; }
        public string Months { get; }

        /// <summary>Empty for saves predating this field - see
        /// CharacterDocument.GetLifestyleNuyenRollInfo's by-name lifestyles.xml fallback.</summary>
        public string Dice { get; }

        public string Multiplier { get; }
    }

    public sealed class CharacterExpenseData
    {
        internal CharacterExpenseData(string strGuid, string strDate, string strAmount, string strReason, bool blnRefund)
        {
            Guid = strGuid;
            Date = strDate;
            Amount = strAmount;
            Reason = strReason;
            Refund = blnRefund;
        }

        /// <summary>Stable identity for UpdateExpense/RemoveExpense - assigned by AddExpense at
        /// creation time. Empty for entries saved before this field existed.</summary>
        public string Guid { get; }

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
