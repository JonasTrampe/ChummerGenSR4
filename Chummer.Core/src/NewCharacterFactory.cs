using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace Chummer.Core
{
    public sealed class NewCharacterMetatype
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Bp { get; set; }
        public string Movement { get; set; } = string.Empty;
        public List<NewCharacterMetavariant> Metavariants { get; } = new();
        public Dictionary<string, (string Min, string Max, string Aug)> AttributeRanges { get; } = new();
        public string CategoryLabel => Category switch
        {
            "Metahuman" => "Metamenschen",
            "Sapient Critter" => "Intelligente Critter",
            "Shapeshifter" => "Gestaltwandler",
            "Special" => "Besondere",
            _ => Category
        };

        public string BodRange => FormatRange("BOD");
        public string AgiRange => FormatRange("AGI");
        public string ReaRange => FormatRange("REA");
        public string StrRange => FormatRange("STR");
        public string ChaRange => FormatRange("CHA");
        public string IntRange => FormatRange("INT");
        public string LogRange => FormatRange("LOG");
        public string WilRange => FormatRange("WIL");
        public string IniRange => FormatRange("INI");
        public string EdgRange => FormatRange("EDG");
        public string MagRange => FormatRange("MAG");
        public string ResRange => FormatRange("RES");
        public string EssRange => FormatRange("ESS");

        private string FormatRange(string strCode)
        {
            if (!AttributeRanges.TryGetValue(strCode, out (string Min, string Max, string Aug) objRange))
                return string.Empty;
            return objRange.Min + "/" + objRange.Max + " (" + objRange.Aug + ")";
        }
    }

    public sealed class NewCharacterMetavariant
    {
        public string Name { get; set; } = string.Empty;
        public int Bp { get; set; }
        public string Source { get; set; } = string.Empty;

        public override string ToString()
        {
            return Name;
        }
    }

    public static class NewCharacterFactory
    {
        private static readonly string[] s_astrAttributeCodes =
        {
            "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "INI", "EDG", "MAG", "RES", "ESS"
        };

        /// <summary>Loads metatypes for the new-character picker. Supplying the selected profile's
        /// options applies its enabled-sourcebook filter before a character document exists.</summary>
        public static List<NewCharacterMetatype> LoadMetatypes(CharacterOptions? objOptions = null)
        {
            XmlDocument objDocument = XmlManager.Instance.Load("metatypes.xml");
            List<NewCharacterMetatype> lstMetatypes = new List<NewCharacterMetatype>();
            XmlNodeList? objNodes = objDocument.SelectNodes("/chummer/metatypes/metatype");
            if (objNodes == null)
                return lstMetatypes;

            foreach (XmlNode objNode in objNodes)
            {
                NewCharacterMetatype objMetatype = new NewCharacterMetatype
                {
                    Name = GetValue(objNode, "name", string.Empty),
                    Category = GetValue(objNode, "category", string.Empty),
                    Movement = GetValue(objNode, "movement", string.Empty),
                    Source = GetValue(objNode, "source", string.Empty)
                };
                if (objOptions != null && !string.IsNullOrEmpty(objMetatype.Source)
                    && !objOptions.BookEnabled(objMetatype.Source))
                    continue;
                int.TryParse(GetValue(objNode, "bp", "0"), out int intBp);
                objMetatype.Bp = intBp;

                AddAttributeRange(objMetatype, objNode, "BOD", "bod");
                AddAttributeRange(objMetatype, objNode, "AGI", "agi");
                AddAttributeRange(objMetatype, objNode, "REA", "rea");
                AddAttributeRange(objMetatype, objNode, "STR", "str");
                AddAttributeRange(objMetatype, objNode, "CHA", "cha");
                AddAttributeRange(objMetatype, objNode, "INT", "int");
                AddAttributeRange(objMetatype, objNode, "LOG", "log");
                AddAttributeRange(objMetatype, objNode, "WIL", "wil");
                AddAttributeRange(objMetatype, objNode, "INI", "ini");
                AddAttributeRange(objMetatype, objNode, "EDG", "edg");
                AddAttributeRange(objMetatype, objNode, "MAG", "mag");
                AddAttributeRange(objMetatype, objNode, "RES", "res");
                AddAttributeRange(objMetatype, objNode, "ESS", "ess");
                AddMetavariants(objMetatype, objNode, objOptions);

                lstMetatypes.Add(objMetatype);
            }

            lstMetatypes.Sort((x, y) =>
            {
                int intCategory = string.Compare(x.Category, y.Category, StringComparison.OrdinalIgnoreCase);
                return intCategory != 0 ? intCategory : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            return lstMetatypes;
        }

        /// <summary>Loads critters.xml's own &lt;metatype&gt; entries for the critter-creation
        /// picker - ported from frmMain.cs's "Create Critter" flow opening frmMetatype pointed at
        /// critters.xml instead of metatypes.xml. Shares NewCharacterMetatype's shape with
        /// LoadMetatypes since both files use an identical schema; Force-based critters (Spirits/
        /// Sprites) simply carry "F"/"F-2"/"F+3"-style formula strings in their attribute ranges
        /// instead of plain integers - resolved by CreateCritterCharacter via a supplied Force.</summary>
        public static List<NewCharacterMetatype> LoadCritterMetatypes(CharacterOptions? objOptions = null)
        {
            XmlDocument objDocument = XmlManager.Instance.Load("critters.xml");
            List<NewCharacterMetatype> lstMetatypes = new List<NewCharacterMetatype>();
            XmlNodeList? objNodes = objDocument.SelectNodes("/chummer/metatypes/metatype");
            if (objNodes == null)
                return lstMetatypes;

            foreach (XmlNode objNode in objNodes)
            {
                NewCharacterMetatype objMetatype = new NewCharacterMetatype
                {
                    Name = GetValue(objNode, "name", string.Empty),
                    Category = GetValue(objNode, "category", string.Empty),
                    Movement = GetValue(objNode, "movement", string.Empty),
                    Source = GetValue(objNode, "source", string.Empty)
                };
                if (objOptions != null && !string.IsNullOrEmpty(objMetatype.Source)
                    && !objOptions.BookEnabled(objMetatype.Source))
                    continue;

                AddAttributeRange(objMetatype, objNode, "BOD", "bod");
                AddAttributeRange(objMetatype, objNode, "AGI", "agi");
                AddAttributeRange(objMetatype, objNode, "REA", "rea");
                AddAttributeRange(objMetatype, objNode, "STR", "str");
                AddAttributeRange(objMetatype, objNode, "CHA", "cha");
                AddAttributeRange(objMetatype, objNode, "INT", "int");
                AddAttributeRange(objMetatype, objNode, "LOG", "log");
                AddAttributeRange(objMetatype, objNode, "WIL", "wil");
                AddAttributeRange(objMetatype, objNode, "INI", "ini");
                AddAttributeRange(objMetatype, objNode, "EDG", "edg");
                AddAttributeRange(objMetatype, objNode, "MAG", "mag");
                AddAttributeRange(objMetatype, objNode, "RES", "res");
                AddAttributeRange(objMetatype, objNode, "ESS", "ess");

                lstMetatypes.Add(objMetatype);
            }

            lstMetatypes.Sort((x, y) =>
            {
                int intCategory = string.Compare(x.Category, y.Category, StringComparison.OrdinalIgnoreCase);
                return intCategory != 0 ? intCategory : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            return lstMetatypes;
        }

        /// <summary>Ported from frmMain.cs's "Create Critter" flow (IsCritter=true, IgnoreRules=true,
        /// BuildMethod=Bp/BuildPoints=0, opening frmMetatype pointed at critters.xml) plus
        /// frmMetatype.cs's critter-specific branch: unlike a normal metatype's point-buy ranges,
        /// a critter's attributes are all set directly to their (optionally Force-scaled, e.g.
        /// "F-2"/"F+3") metatype values, and its Skills/Skill Groups/Knowledge Skills/Critter
        /// Powers are all seeded straight from the critters.xml entry rather than chosen by the
        /// player.</summary>
        public static CharacterDocument CreateCritterCharacter(string strDisplayName, string strSettingsFileName,
            NewCharacterMetatype objMetatype, int intForce = 0, CharacterOptions? objCharacterOptions = null)
        {
            XmlDocument objCrittersDoc = XmlManager.Instance.Load("critters.xml");
            XmlNode? objCritterNode = objCrittersDoc.SelectSingleNode(
                $"/chummer/metatypes/metatype[name = '{objMetatype.Name}']");

            CharacterOptions objOptions = objCharacterOptions ?? new CharacterOptions();
            if (objCharacterOptions == null)
                objOptions.Load(string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);

            XmlDocument objDocument = new XmlDocument();
            XmlElement objRoot = objDocument.CreateElement("character");
            objDocument.AppendChild(objRoot);

            AppendElement(objDocument, objRoot, "settings", string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);
            AppendElement(objDocument, objRoot, "metatype", objMetatype.Name);
            AppendElement(objDocument, objRoot, "metatypebp", "0");
            AppendElement(objDocument, objRoot, "metavariant", string.Empty);
            AppendElement(objDocument, objRoot, "metatypecategory", objMetatype.Category);
            AppendElement(objDocument, objRoot, "movement", objMetatype.Movement);
            AppendElement(objDocument, objRoot, "movementwalk", ExtractMovementPart(objMetatype.Movement, 0));
            AppendElement(objDocument, objRoot, "movementswim", ExtractSwimMovement(objMetatype.Movement));
            AppendElement(objDocument, objRoot, "movementfly", string.Empty);
            AppendElement(objDocument, objRoot, "mutantcritterbaseskills", "0");
            AppendElement(objDocument, objRoot, "name", strDisplayName);
            AppendElement(objDocument, objRoot, "mugshot", string.Empty);
            AppendElement(objDocument, objRoot, "sex", string.Empty);
            AppendElement(objDocument, objRoot, "age", string.Empty);
            AppendElement(objDocument, objRoot, "eyes", string.Empty);
            AppendElement(objDocument, objRoot, "height", string.Empty);
            AppendElement(objDocument, objRoot, "weight", string.Empty);
            AppendElement(objDocument, objRoot, "skin", string.Empty);
            AppendElement(objDocument, objRoot, "hair", string.Empty);
            AppendElement(objDocument, objRoot, "description", string.Empty);
            AppendElement(objDocument, objRoot, "background", string.Empty);
            AppendElement(objDocument, objRoot, "concept", string.Empty);
            AppendElement(objDocument, objRoot, "notes", string.Empty);
            AppendElement(objDocument, objRoot, "alias", string.Empty);
            AppendElement(objDocument, objRoot, "playername", string.Empty);
            AppendElement(objDocument, objRoot, "karma", "0");
            AppendElement(objDocument, objRoot, "totalkarma", "0");
            AppendElement(objDocument, objRoot, "streetcred", "0");
            AppendElement(objDocument, objRoot, "notoriety", "0");
            AppendElement(objDocument, objRoot, "publicawareness", "0");
            AppendElement(objDocument, objRoot, "ignorerules", "True");
            AppendElement(objDocument, objRoot, "created", "False");
            AppendElement(objDocument, objRoot, "maxavail", "0");
            AppendElement(objDocument, objRoot, "nuyen", "0");
            AppendElement(objDocument, objRoot, "bp", "0");
            AppendElement(objDocument, objRoot, "buildkarma", "0");
            AppendElement(objDocument, objRoot, "startingbuildpoints", "0");
            AppendElement(objDocument, objRoot, "buildmethod", "Bp");
            AppendElement(objDocument, objRoot, "knowpts", "0");
            AppendElement(objDocument, objRoot, "nuyenbp", "0");
            AppendElement(objDocument, objRoot, "nuyenmaxbp", "50");
            AppendElement(objDocument, objRoot, "adept", "False");
            AppendElement(objDocument, objRoot, "magician", "False");
            AppendElement(objDocument, objRoot, "technomancer", "False");
            AppendElement(objDocument, objRoot, "initiationoverride", "False");
            AppendElement(objDocument, objRoot, "critter", "True");
            AppendElement(objDocument, objRoot, "uneducated", "False");
            AppendElement(objDocument, objRoot, "uncouth", "False");
            AppendElement(objDocument, objRoot, "infirm", "False");

            XmlElement objAttributes = objDocument.CreateElement("attributes");
            objRoot.AppendChild(objAttributes);
            int intMagMax = 0, intResMax = 0;
            foreach (string strAttributeCode in s_astrAttributeCodes)
            {
                (string Min, string Max, string Aug) objRange = objMetatype.AttributeRanges.ContainsKey(strAttributeCode)
                    ? objMetatype.AttributeRanges[strAttributeCode]
                    : ("0", "0", "0");
                int intMin = EvaluateForceExpression(objRange.Min, intForce);
                int intMax = EvaluateForceExpression(objRange.Max, intForce);
                int intAug = EvaluateForceExpression(objRange.Aug, intForce);
                if (strAttributeCode == "MAG") intMagMax = intMax;
                if (strAttributeCode == "RES") intResMax = intMax;

                XmlElement objAttribute = objDocument.CreateElement("attribute");
                objAttributes.AppendChild(objAttribute);
                AppendElement(objDocument, objAttribute, "name", strAttributeCode);
                AppendElement(objDocument, objAttribute, "metatypemin", intMin.ToString(CultureInfo.InvariantCulture));
                AppendElement(objDocument, objAttribute, "metatypemax", intMax.ToString(CultureInfo.InvariantCulture));
                AppendElement(objDocument, objAttribute, "metatypeaugmax", intAug.ToString(CultureInfo.InvariantCulture));
                AppendElement(objDocument, objAttribute, "value", intMin.ToString(CultureInfo.InvariantCulture));
                AppendElement(objDocument, objAttribute, "augmodifier", "0");
                AppendElement(objDocument, objAttribute, "totalvalue", intMin.ToString(CultureInfo.InvariantCulture));
            }

            AppendElement(objDocument, objRoot, "magenabled", (intMagMax > 0).ToString());
            AppendElement(objDocument, objRoot, "initiategrade", "0");
            AppendElement(objDocument, objRoot, "resenabled", (intResMax > 0).ToString());
            AppendElement(objDocument, objRoot, "submersiongrade", "0");
            AppendElement(objDocument, objRoot, "groupmember", "False");
            AppendElement(objDocument, objRoot, "totaless", "6");
            AppendElement(objDocument, objRoot, "tradition", string.Empty);
            AppendElement(objDocument, objRoot, "stream", string.Empty);
            AppendElement(objDocument, objRoot, "physicalcmfilled", "0");
            AppendElement(objDocument, objRoot, "stuncmfilled", "0");

            AppendSkillGroups(objDocument, objRoot);
            AppendActiveSkills(objDocument, objRoot);
            ApplyCritterSkills(objDocument, objRoot, objCritterNode, intForce);
            AppendEmptyContainer(objDocument, objRoot, "martialarts");
            AppendEmptyContainer(objDocument, objRoot, "martialartmaneuvers");
            AppendEmptyContainer(objDocument, objRoot, "powers");
            AppendEmptyContainer(objDocument, objRoot, "spells");
            AppendEmptyContainer(objDocument, objRoot, "spirits");
            AppendEmptyContainer(objDocument, objRoot, "initiationgrades");
            AppendEmptyContainer(objDocument, objRoot, "cyberwares");
            AppendEmptyContainer(objDocument, objRoot, "biowares");
            AppendEmptyContainer(objDocument, objRoot, "armors");
            AppendEmptyContainer(objDocument, objRoot, "weapons");
            AppendEmptyContainer(objDocument, objRoot, "gears");
            AppendEmptyContainer(objDocument, objRoot, "vehicles");
            AppendEmptyContainer(objDocument, objRoot, "contacts");
            AppendEmptyContainer(objDocument, objRoot, "qualities");
            AppendEmptyContainer(objDocument, objRoot, "expenses");
            AppendEmptyContainer(objDocument, objRoot, "calendar");
            AppendEmptyContainer(objDocument, objRoot, "improvements");
            AppendEmptyContainer(objDocument, objRoot, "lifestyles");

            var objCharacter = new CharacterDocument(objDocument, strDisplayName);

            XmlNodeList? objPowerNodes = objCritterNode?.SelectNodes("powers/power");
            if (objPowerNodes != null)
                foreach (XmlNode objPowerNode in objPowerNodes)
                {
                    string strPowerName = objPowerNode.InnerText;
                    if (string.IsNullOrWhiteSpace(strPowerName))
                        continue;
                    string strSelect = objPowerNode.Attributes?["select"]?.InnerText ?? string.Empty;
                    objCharacter.AddCritterPower(strPowerName, "0", objMetatype.Source, string.Empty, strSelect,
                        blnCountTowardsLimit: false);
                }

            return objCharacter;
        }

        /// <summary>Evaluates a critter attribute's "F"/"F-2"/"F+3"-style Force formula (or a plain
        /// integer for non-Force critters) against the supplied Force, floored at 0.</summary>
        private static int EvaluateForceExpression(string strExpression, int intForce)
        {
            if (string.IsNullOrWhiteSpace(strExpression))
                return 0;
            string strSubstituted = strExpression.Replace("F", intForce.ToString(CultureInfo.InvariantCulture));
            return Math.Max(0, (int)RatingExpression.Evaluate(strSubstituted, "0"));
        }

        /// <summary>Seeds a critter's Active Skills/Skill Groups/Knowledge Skills straight from its
        /// critters.xml entry - ported from frmMetatype.cs's critter-specific skill population.
        /// Ratings may themselves be Force formulas (e.g. Ally Spirit's `rating="F"`).</summary>
        private static void ApplyCritterSkills(XmlDocument objDocument, XmlElement objRoot, XmlNode? objCritterNode,
            int intForce)
        {
            if (objCritterNode == null)
                return;

            XmlNodeList? objSkillNodes = objCritterNode.SelectNodes("skills/skill");
            if (objSkillNodes != null)
                foreach (XmlNode objSkillNode in objSkillNodes)
                {
                    string strName = objSkillNode.InnerText;
                    string strRating = EvaluateForceExpression(
                        objSkillNode.Attributes?["rating"]?.InnerText ?? "0", intForce).ToString(CultureInfo.InvariantCulture);
                    XmlNode? objMatch = objRoot.SelectSingleNode($"skills/skill[name = '{strName}']");
                    if (objMatch?["rating"] != null)
                        objMatch["rating"]!.InnerText = strRating;
                }

            XmlNodeList? objGroupNodes = objCritterNode.SelectNodes("skills/group");
            if (objGroupNodes != null)
                foreach (XmlNode objGroupNode in objGroupNodes)
                {
                    string strName = objGroupNode.InnerText;
                    string strRating = EvaluateForceExpression(
                        objGroupNode.Attributes?["rating"]?.InnerText ?? "0", intForce).ToString(CultureInfo.InvariantCulture);
                    XmlNode? objMatch = objRoot.SelectSingleNode($"skillgroups/skillgroup[name = '{strName}']");
                    if (objMatch?["rating"] != null)
                        objMatch["rating"]!.InnerText = strRating;
                }

            XmlElement objSkills = (XmlElement?)objRoot.SelectSingleNode("skills")
                ?? (XmlElement)objRoot.AppendChild(objDocument.CreateElement("skills"))!;
            XmlNodeList? objKnowledgeNodes = objCritterNode.SelectNodes("skills/knowledge");
            if (objKnowledgeNodes != null)
                foreach (XmlNode objKnowledgeNode in objKnowledgeNodes)
                {
                    string strRating = EvaluateForceExpression(
                        objKnowledgeNode.Attributes?["rating"]?.InnerText ?? "0", intForce).ToString(CultureInfo.InvariantCulture);
                    string strCategory = objKnowledgeNode.Attributes?["category"]?.InnerText ?? string.Empty;

                    XmlElement objSkill = objDocument.CreateElement("skill");
                    objSkills.AppendChild(objSkill);
                    AppendElement(objDocument, objSkill, "name", objKnowledgeNode.InnerText);
                    AppendElement(objDocument, objSkill, "skillgroup", string.Empty);
                    AppendElement(objDocument, objSkill, "skillcategory", strCategory);
                    AppendElement(objDocument, objSkill, "grouped", "False");
                    AppendElement(objDocument, objSkill, "default", "False");
                    AppendElement(objDocument, objSkill, "rating", strRating);
                    AppendElement(objDocument, objSkill, "ratingmax", "6");
                    AppendElement(objDocument, objSkill, "knowledge", "True");
                    AppendElement(objDocument, objSkill, "exotic", "False");
                    AppendElement(objDocument, objSkill, "spec", string.Empty);
                    AppendElement(objDocument, objSkill, "allowdelete", "True");
                    AppendElement(objDocument, objSkill, "attribute", "LOG");
                    AppendElement(objDocument, objSkill, "totalvalue", "0");
                }
        }

        public static CharacterDocument CreateNewCharacter(string strDisplayName, string strSettingsFileName,
            string strBuildMethod, int intBuildPoints, int intMaxAvailability, NewCharacterMetatype objMetatype,
            string strMetavariantName = "", bool blnIgnoreRules = false, string strMagicType = "None",
            CharacterOptions? objCharacterOptions = null)
        {
            bool blnAdept = strMagicType == "Adept" || strMagicType == "MysticAdept";
            bool blnMagician = strMagicType == "Magician" || strMagicType == "MysticAdept";
            bool blnTechnomancer = strMagicType == "Technomancer";
            bool blnKarmaBuild = string.Equals(strBuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            CharacterOptions objOptions = objCharacterOptions ?? new CharacterOptions();
            if (objCharacterOptions == null)
                objOptions.Load(string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);
            NewCharacterMetavariant? objMetavariant = objMetatype.Metavariants.FirstOrDefault(m =>
                string.Equals(m.Name, strMetavariantName, StringComparison.Ordinal));
            // Legacy stores the selected metavariant's listed cost in metatypebp; it replaces
            // the base metatype cost rather than being added to it (e.g. Dryad is 45 BP, not
            // Elf 30 + Dryad 45).
            int intMetatypeBp = objMetavariant?.Bp ?? objMetatype.Bp;
            int intMetatypeCost = blnKarmaBuild
                ? (objOptions.MetatypeCostsKarma ? intMetatypeBp * objOptions.MetatypeCostsKarmaMultiplier : 0)
                : intMetatypeBp;
            if (!blnIgnoreRules && intMetatypeCost > intBuildPoints)
                throw new ArgumentOutOfRangeException(nameof(objMetatype), "The selected metatype exceeds the creation budget.");
            int intRemainingBuildPoints = intBuildPoints - intMetatypeCost;
            XmlDocument objDocument = new XmlDocument();
            XmlElement objRoot = objDocument.CreateElement("character");
            objDocument.AppendChild(objRoot);

            AppendElement(objDocument, objRoot, "settings", string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);
            AppendElement(objDocument, objRoot, "metatype", objMetatype.Name);
            AppendElement(objDocument, objRoot, "metatypebp", intMetatypeBp.ToString());
            AppendElement(objDocument, objRoot, "metavariant", strMetavariantName);
            AppendElement(objDocument, objRoot, "metatypecategory", objMetatype.Category);
            AppendElement(objDocument, objRoot, "movement", objMetatype.Movement);
            AppendElement(objDocument, objRoot, "movementwalk", ExtractMovementPart(objMetatype.Movement, 0));
            AppendElement(objDocument, objRoot, "movementswim", ExtractSwimMovement(objMetatype.Movement));
            AppendElement(objDocument, objRoot, "movementfly", string.Empty);
            AppendElement(objDocument, objRoot, "mutantcritterbaseskills", "0");
            AppendElement(objDocument, objRoot, "name", strDisplayName);
            AppendElement(objDocument, objRoot, "mugshot", string.Empty);
            AppendElement(objDocument, objRoot, "sex", string.Empty);
            AppendElement(objDocument, objRoot, "age", string.Empty);
            AppendElement(objDocument, objRoot, "eyes", string.Empty);
            AppendElement(objDocument, objRoot, "height", string.Empty);
            AppendElement(objDocument, objRoot, "weight", string.Empty);
            AppendElement(objDocument, objRoot, "skin", string.Empty);
            AppendElement(objDocument, objRoot, "hair", string.Empty);
            AppendElement(objDocument, objRoot, "description", string.Empty);
            AppendElement(objDocument, objRoot, "background", string.Empty);
            AppendElement(objDocument, objRoot, "concept", string.Empty);
            AppendElement(objDocument, objRoot, "notes", string.Empty);
            AppendElement(objDocument, objRoot, "alias", string.Empty);
            AppendElement(objDocument, objRoot, "playername", string.Empty);
            AppendElement(objDocument, objRoot, "karma", blnKarmaBuild ? intRemainingBuildPoints.ToString() : "0");
            AppendElement(objDocument, objRoot, "totalkarma", "0");
            AppendElement(objDocument, objRoot, "streetcred", "0");
            AppendElement(objDocument, objRoot, "notoriety", "0");
            AppendElement(objDocument, objRoot, "publicawareness", "0");
            if (blnIgnoreRules)
                AppendElement(objDocument, objRoot, "ignorerules", "True");
            AppendElement(objDocument, objRoot, "created", "False");
            AppendElement(objDocument, objRoot, "maxavail", intMaxAvailability.ToString());
            AppendElement(objDocument, objRoot, "nuyen", "0");
            AppendElement(objDocument, objRoot, "bp", blnKarmaBuild ? "0" : intRemainingBuildPoints.ToString());
            AppendElement(objDocument, objRoot, "buildkarma", blnKarmaBuild ? intBuildPoints.ToString() : "0");
            // The starting total, unlike bp/buildkarma above which shrink as points are spent -
            // needed by AllowExceedAttributeBp's "max 50% of the starting total on primary
            // attributes" cap (see CharacterDocument.StartingBuildPoints).
            AppendElement(objDocument, objRoot, "startingbuildpoints", intBuildPoints.ToString());
            AppendElement(objDocument, objRoot, "buildmethod", strBuildMethod);
            AppendElement(objDocument, objRoot, "knowpts", "0");
            AppendElement(objDocument, objRoot, "nuyenbp", "0");
            AppendElement(objDocument, objRoot, "nuyenmaxbp", blnKarmaBuild ? "100" : "50");
            AppendElement(objDocument, objRoot, "adept", blnAdept.ToString());
            AppendElement(objDocument, objRoot, "magician", blnMagician.ToString());
            AppendElement(objDocument, objRoot, "technomancer", blnTechnomancer.ToString());
            AppendElement(objDocument, objRoot, "initiationoverride", "False");
            AppendElement(objDocument, objRoot, "critter", "False");
            AppendElement(objDocument, objRoot, "uneducated", "False");
            AppendElement(objDocument, objRoot, "uncouth", "False");
            AppendElement(objDocument, objRoot, "infirm", "False");

            XmlElement objAttributes = objDocument.CreateElement("attributes");
            objRoot.AppendChild(objAttributes);
            foreach (string strAttributeCode in s_astrAttributeCodes)
            {
                (string Min, string Max, string Aug) objRange = objMetatype.AttributeRanges.ContainsKey(strAttributeCode)
                    ? objMetatype.AttributeRanges[strAttributeCode]
                    : ("0", "0", "0");
                XmlElement objAttribute = objDocument.CreateElement("attribute");
                objAttributes.AppendChild(objAttribute);
                AppendElement(objDocument, objAttribute, "name", strAttributeCode);
                AppendElement(objDocument, objAttribute, "metatypemin", objRange.Min);
                AppendElement(objDocument, objAttribute, "metatypemax", objRange.Max);
                AppendElement(objDocument, objAttribute, "metatypeaugmax", objRange.Aug);
                AppendElement(objDocument, objAttribute, "value", strAttributeCode == "ESS" ? "6" : objRange.Min);
                AppendElement(objDocument, objAttribute, "augmodifier", "0");
                AppendElement(objDocument, objAttribute, "totalvalue", strAttributeCode == "ESS" ? "6" : objRange.Min);
            }

            AppendElement(objDocument, objRoot, "magenabled", (blnAdept || blnMagician).ToString());
            AppendElement(objDocument, objRoot, "initiategrade", "0");
            AppendElement(objDocument, objRoot, "resenabled", blnTechnomancer.ToString());
            AppendElement(objDocument, objRoot, "submersiongrade", "0");
            AppendElement(objDocument, objRoot, "groupmember", "False");
            AppendElement(objDocument, objRoot, "totaless", "6");
            AppendElement(objDocument, objRoot, "tradition", string.Empty);
            AppendElement(objDocument, objRoot, "stream", string.Empty);
            AppendElement(objDocument, objRoot, "physicalcmfilled", "0");
            AppendElement(objDocument, objRoot, "stuncmfilled", "0");

            AppendSkillGroups(objDocument, objRoot);
            AppendActiveSkills(objDocument, objRoot);
            AppendEmptyContainer(objDocument, objRoot, "martialarts");
            AppendEmptyContainer(objDocument, objRoot, "martialartmaneuvers");
            AppendEmptyContainer(objDocument, objRoot, "powers");
            AppendEmptyContainer(objDocument, objRoot, "spells");
            AppendEmptyContainer(objDocument, objRoot, "spirits");
            AppendEmptyContainer(objDocument, objRoot, "initiationgrades");
            AppendEmptyContainer(objDocument, objRoot, "cyberwares");
            AppendEmptyContainer(objDocument, objRoot, "biowares");
            AppendEmptyContainer(objDocument, objRoot, "armors");
            AppendEmptyContainer(objDocument, objRoot, "weapons");
            AppendEmptyContainer(objDocument, objRoot, "gears");
            AppendEmptyContainer(objDocument, objRoot, "vehicles");
            AppendEmptyContainer(objDocument, objRoot, "contacts");
            AppendEmptyContainer(objDocument, objRoot, "qualities");
            AppendEmptyContainer(objDocument, objRoot, "expenses");
            AppendEmptyContainer(objDocument, objRoot, "calendar");
            AppendEmptyContainer(objDocument, objRoot, "improvements");
            AppendEmptyContainer(objDocument, objRoot, "lifestyles");

            return new CharacterDocument(objDocument, strDisplayName);
        }

        private static void AddAttributeRange(NewCharacterMetatype objMetatype, XmlNode objNode, string strCode, string strPrefix)
        {
            objMetatype.AttributeRanges[strCode] = (
                GetValue(objNode, strPrefix + "min", "0"),
                GetValue(objNode, strPrefix + "max", "0"),
                GetValue(objNode, strPrefix + "aug", GetValue(objNode, strPrefix + "max", "0")));
        }

        private static void AddMetavariants(NewCharacterMetatype objMetatype, XmlNode objNode,
            CharacterOptions? objOptions)
        {
            XmlNodeList? objMetavariantNodes = objNode.SelectNodes("metavariants/metavariant");
            if (objMetavariantNodes == null)
                return;

            foreach (XmlNode objMetavariantNode in objMetavariantNodes)
            {
                NewCharacterMetavariant objMetavariant = new NewCharacterMetavariant
                {
                    Name = GetValue(objMetavariantNode, "name", string.Empty),
                    Source = GetValue(objMetavariantNode, "source", string.Empty)
                };
                if (objOptions != null && !string.IsNullOrEmpty(objMetavariant.Source)
                    && !objOptions.BookEnabled(objMetavariant.Source))
                    continue;
                int.TryParse(GetValue(objMetavariantNode, "bp", "0"), out int intBp);
                objMetavariant.Bp = intBp;
                if (!string.IsNullOrWhiteSpace(objMetavariant.Name))
                    objMetatype.Metavariants.Add(objMetavariant);
            }

            objMetatype.Metavariants.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static void AppendSkillGroups(XmlDocument objDocument, XmlElement objRoot)
        {
            XmlElement objGroups = objDocument.CreateElement("skillgroups");
            objRoot.AppendChild(objGroups);

            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            XmlNodeList? objGroupNodes = objSkillsDoc.SelectNodes("/chummer/skillgroups/name");
            if (objGroupNodes == null)
                return;

            foreach (XmlNode objGroupNode in objGroupNodes)
            {
                XmlElement objGroup = objDocument.CreateElement("skillgroup");
                objGroups.AppendChild(objGroup);
                AppendElement(objDocument, objGroup, "name", objGroupNode.InnerText);
                AppendElement(objDocument, objGroup, "rating", "0");
                AppendElement(objDocument, objGroup, "broken", "False");
            }
        }

        private static void AppendActiveSkills(XmlDocument objDocument, XmlElement objRoot)
        {
            XmlElement objSkills = objDocument.CreateElement("skills");
            objRoot.AppendChild(objSkills);

            XmlDocument objSkillsDoc = XmlManager.Instance.Load("skills.xml");
            XmlNodeList? objSkillNodes = objSkillsDoc.SelectNodes("/chummer/skills/skill");
            if (objSkillNodes == null)
                return;

            foreach (XmlNode objSkillNode in objSkillNodes)
            {
                // Exotic skills (Exotic Melee/Ranged Weapon, Pilot Exotic Vehicle) are added
                // per-instance via AddExoticSkill, not seeded here.
                if (GetValue(objSkillNode, "exotic", "No") == "Yes")
                    continue;

                string strSkillGroup = GetValue(objSkillNode, "skillgroup", string.Empty);
                XmlElement objSkill = objDocument.CreateElement("skill");
                objSkills.AppendChild(objSkill);
                AppendElement(objDocument, objSkill, "name", GetValue(objSkillNode, "name", string.Empty));
                AppendElement(objDocument, objSkill, "skillgroup", strSkillGroup);
                AppendElement(objDocument, objSkill, "skillcategory", GetValue(objSkillNode, "category", string.Empty));
                AppendElement(objDocument, objSkill, "grouped", "False");
                AppendElement(objDocument, objSkill, "default", GetValue(objSkillNode, "default", "No"));
                AppendElement(objDocument, objSkill, "rating", "0");
                AppendElement(objDocument, objSkill, "ratingmax", "6");
                AppendElement(objDocument, objSkill, "knowledge", "False");
                AppendElement(objDocument, objSkill, "exotic", "False");
                AppendElement(objDocument, objSkill, "spec", string.Empty);
                AppendElement(objDocument, objSkill, "allowdelete", "False");
                AppendElement(objDocument, objSkill, "attribute", GetValue(objSkillNode, "attribute", string.Empty));
                AppendElement(objDocument, objSkill, "totalvalue", "0");

                // Meta skills (e.g. "Perception (Visual)") mirror a base skill's Rating and get a
                // +2 bonus when that base skill's Specialization matches - see
                // CharacterFileService.Skills.cs's BuildSkillData. skills.xml uses camelCase
                // (isMeta/metaBase/metaSpec) and a lowercase "true"/"false" boolean, unlike this
                // save format's own "grouped"/"exotic" (lowercase element, capitalized boolean)
                // convention - normalized here to match.
                bool blnIsMeta = string.Equals(GetValue(objSkillNode, "isMeta", "false"), "true", StringComparison.OrdinalIgnoreCase);
                if (blnIsMeta)
                {
                    AppendElement(objDocument, objSkill, "isMeta", "True");
                    AppendElement(objDocument, objSkill, "metaBase", GetValue(objSkillNode, "metaBase", string.Empty));
                    AppendElement(objDocument, objSkill, "metaSpec", GetValue(objSkillNode, "metaSpec", string.Empty));
                }
            }
        }

        private static void AppendEmptyContainer(XmlDocument objDocument, XmlElement objRoot, string strName)
        {
            objRoot.AppendChild(objDocument.CreateElement(strName));
        }

        private static void AppendElement(XmlDocument objDocument, XmlElement objParent, string strName, string strValue)
        {
            XmlElement objChild = objDocument.CreateElement(strName);
            objChild.InnerText = strValue;
            objParent.AppendChild(objChild);
        }

        private static string GetValue(XmlNode objNode, string strChildName, string strDefaultValue)
        {
            return objNode[strChildName]?.InnerText ?? strDefaultValue;
        }

        private static string ExtractMovementPart(string strMovement, int intIndex)
        {
            if (string.IsNullOrWhiteSpace(strMovement))
                return string.Empty;
            string[] astrParts = strMovement.Split(',');
            return intIndex < astrParts.Length ? astrParts[intIndex].Trim() : string.Empty;
        }

        private static string ExtractSwimMovement(string strMovement)
        {
            string strSwimPart = ExtractMovementPart(strMovement, 1);
            return strSwimPart.StartsWith("Swim ", StringComparison.OrdinalIgnoreCase)
                ? strSwimPart.Substring(5).Trim()
                : strSwimPart;
        }
    }
}
