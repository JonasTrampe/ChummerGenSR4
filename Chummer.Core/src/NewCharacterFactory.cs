using System;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Core
{
    public sealed class NewCharacterMetatype
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
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

        public static List<NewCharacterMetatype> LoadMetatypes()
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
                    Movement = GetValue(objNode, "movement", string.Empty)
                };
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
                AddMetavariants(objMetatype, objNode);

                lstMetatypes.Add(objMetatype);
            }

            lstMetatypes.Sort((x, y) =>
            {
                int intCategory = string.Compare(x.Category, y.Category, StringComparison.OrdinalIgnoreCase);
                return intCategory != 0 ? intCategory : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            return lstMetatypes;
        }

        public static CharacterDocument CreateNewCharacter(string strDisplayName, string strSettingsFileName,
            string strBuildMethod, int intBuildPoints, int intMaxAvailability, NewCharacterMetatype objMetatype,
            string strMetavariantName = "", bool blnIgnoreRules = false)
        {
            bool blnKarmaBuild = string.Equals(strBuildMethod, "Karma", StringComparison.OrdinalIgnoreCase);
            XmlDocument objDocument = new XmlDocument();
            XmlElement objRoot = objDocument.CreateElement("character");
            objDocument.AppendChild(objRoot);

            AppendElement(objDocument, objRoot, "settings", string.IsNullOrWhiteSpace(strSettingsFileName) ? "default.xml" : strSettingsFileName);
            AppendElement(objDocument, objRoot, "metatype", objMetatype.Name);
            AppendElement(objDocument, objRoot, "metatypebp", objMetatype.Bp.ToString());
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
            AppendElement(objDocument, objRoot, "karma", blnKarmaBuild ? intBuildPoints.ToString() : "0");
            AppendElement(objDocument, objRoot, "totalkarma", "0");
            AppendElement(objDocument, objRoot, "streetcred", "0");
            AppendElement(objDocument, objRoot, "notoriety", "0");
            AppendElement(objDocument, objRoot, "publicawareness", "0");
            if (blnIgnoreRules)
                AppendElement(objDocument, objRoot, "ignorerules", "True");
            AppendElement(objDocument, objRoot, "created", "False");
            AppendElement(objDocument, objRoot, "maxavail", intMaxAvailability.ToString());
            AppendElement(objDocument, objRoot, "nuyen", "0");
            AppendElement(objDocument, objRoot, "bp", blnKarmaBuild ? "0" : intBuildPoints.ToString());
            AppendElement(objDocument, objRoot, "buildkarma", blnKarmaBuild ? intBuildPoints.ToString() : "0");
            AppendElement(objDocument, objRoot, "buildmethod", strBuildMethod);
            AppendElement(objDocument, objRoot, "knowpts", "0");
            AppendElement(objDocument, objRoot, "nuyenbp", "0");
            AppendElement(objDocument, objRoot, "nuyenmaxbp", blnKarmaBuild ? "100" : "50");
            AppendElement(objDocument, objRoot, "adept", "False");
            AppendElement(objDocument, objRoot, "magician", "False");
            AppendElement(objDocument, objRoot, "technomancer", "False");
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

            AppendElement(objDocument, objRoot, "magenabled", "False");
            AppendElement(objDocument, objRoot, "initiategrade", "0");
            AppendElement(objDocument, objRoot, "resenabled", "False");
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

        private static void AddMetavariants(NewCharacterMetatype objMetatype, XmlNode objNode)
        {
            XmlNodeList? objMetavariantNodes = objNode.SelectNodes("metavariants/metavariant");
            if (objMetavariantNodes == null)
                return;

            foreach (XmlNode objMetavariantNode in objMetavariantNodes)
            {
                NewCharacterMetavariant objMetavariant = new NewCharacterMetavariant
                {
                    Name = GetValue(objMetavariantNode, "name", string.Empty)
                };
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
