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
        /// TechnomancerAllowCommlink switches a Technomancer into the normal active-Commlink
        /// branch, matching the legacy house-rule behavior.</summary>
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
                else if (Technomancer && !GetCharacterOptions().TechnomancerAllowCommlink)
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

        private IReadOnlyList<CharacterInitiationGradeData>? _cachedInitiationGrades;
        public IReadOnlyList<CharacterInitiationGradeData> InitiationGrades
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedInitiationGrades short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedInitiationGrades ??= ReadInitiationGrades();
            }
        }

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

            int? intKarmaCostPreview = GetInitiateKarmaCostToIncrease(blnGroup, blnOrdeal);
            if (intKarmaCostPreview == null)
                return false;
            int intKarmaCost = intKarmaCostPreview.Value;

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

        /// <summary>Returns the Karma cost for the next Initiation/Submersion grade, including
        /// Group and Ordeal discounts, or <see langword="null"/> when the character is ineligible.</summary>
        public int? GetInitiateKarmaCostToIncrease(bool blnGroup, bool blnOrdeal)
        {
            if (!Magician && !Technomancer)
                return null;

            int intCurrentGrade = InitiateGrade;
            int intMagOrRes = GetAttributeInt(Technomancer ? "RES" : "MAG");
            if (intCurrentGrade + 1 > intMagOrRes)
                return null;

            double dblMultiplier = 1.0;
            if (blnGroup) dblMultiplier -= 0.2;
            if (blnOrdeal) dblMultiplier -= 0.2;
            dblMultiplier = Math.Round(dblMultiplier, 2);
            return (int)Math.Ceiling((10 + (intCurrentGrade + 1) * GetCharacterOptions().KarmaInitiation)
                * dblMultiplier);
        }

        /// <summary>Ported from frmCareer.cs's cmdImproveInitiation_Click's Metamagic-refresh loop:
        /// any owned Metamagic whose rules-data &lt;bonus&gt; XML references "Rating" (a simple
        /// substring check, matching legacy) has its Improvements rebuilt with the new Initiation
        /// Grade as the Rating - e.g. a Metamagic granting "+Rating to some pool" gets stronger as
        /// the character initiates further.</summary>
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

    }
}
