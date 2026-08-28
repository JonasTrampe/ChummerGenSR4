using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chummer.Core
{
    public static class CharacterDiff
    {
        public static CharacterDiffResult Compare(CharacterDocument objLocal, CharacterDocument objServer)
        {
            CharacterDiffResult objResult = new CharacterDiffResult();

            CompareScalar(objResult, "Alias", objLocal.Alias, objServer.Alias);
            CompareScalar(objResult, "Karma", objLocal.Karma, objServer.Karma);
            CompareScalar(objResult, "Career Karma", objLocal.CareerKarma.ToString(), objServer.CareerKarma.ToString());
            CompareScalar(objResult, "Nuyen", objLocal.Nuyen, objServer.Nuyen);
            CompareScalar(objResult, "Career Nuyen", objLocal.CareerNuyen.ToString(), objServer.CareerNuyen.ToString());
            CompareScalar(objResult, "Metatype", objLocal.Metatype, objServer.Metatype);
            CompareScalar(objResult, "Notes", objLocal.Notes, objServer.Notes);
            CompareScalar(objResult, "Mugshot", objLocal.Mugshot, objServer.Mugshot);

            CompareInitiative(objResult, "Initiative", objLocal.Initiative, objServer.Initiative);
            CompareInitiative(objResult, "Initiative Passes", objLocal.InitiativePasses, objServer.InitiativePasses);
            CompareInitiative(objResult, "Astral Initiative", objLocal.AstralInitiative, objServer.AstralInitiative);
            CompareInitiative(objResult, "Matrix Initiative", objLocal.MatrixInitiative, objServer.MatrixInitiative);
            CompareInitiative(objResult, "Matrix Initiative Passes", objLocal.MatrixInitiativePasses, objServer.MatrixInitiativePasses);

            CompareScalar(objResult, "Physical Damage", objLocal.Condition.PhysicalDamage, objServer.Condition.PhysicalDamage);
            CompareScalar(objResult, "Stun Damage", objLocal.Condition.StunDamage, objServer.Condition.StunDamage);
            CompareScalar(objResult, "Physical Condition Monitor", objLocal.Condition.PhysicalCm.Value.ToString(),
                objServer.Condition.PhysicalCm.Value.ToString());
            CompareScalar(objResult, "Stun Condition Monitor", objLocal.Condition.StunCm.Value.ToString(),
                objServer.Condition.StunCm.Value.ToString());

            DiffCollection(objResult, "Attributes", objLocal.Attributes, objServer.Attributes);
            DiffCollection(objResult, "Skill Groups", objLocal.SkillGroups, objServer.SkillGroups);
            DiffCollection(objResult, "Skills", objLocal.Skills, objServer.Skills);
            DiffCollection(objResult, "Knowledge Skills", objLocal.KnowledgeSkills, objServer.KnowledgeSkills);
            DiffCollection(objResult, "Complex Forms", objLocal.ComplexForms, objServer.ComplexForms);
            DiffCollection(objResult, "Foci", objLocal.Foci, objServer.Foci);
            DiffCollection(objResult, "Stacked Foci", objLocal.StackedFoci, objServer.StackedFoci);
            DiffCollection(objResult, "Improvements", objLocal.Improvements, objServer.Improvements);

            DiffCollection(objResult, "Qualities", objLocal.Qualities, objServer.Qualities);
            DiffCollection(objResult, "Contacts", objLocal.Contacts, objServer.Contacts);
            DiffCollection(objResult, "Spirits", objLocal.Spirits, objServer.Spirits);
            DiffCollection(objResult, "Spells", objLocal.Spells, objServer.Spells);
            DiffCollection(objResult, "Powers", objLocal.AdeptPowers, objServer.AdeptPowers);
            DiffCollection(objResult, "Martial Arts", objLocal.MartialArts, objServer.MartialArts);
            DiffCollection(objResult, "Armor", objLocal.Armor, objServer.Armor);
            DiffCollection(objResult, "Cyberware", objLocal.Cyberware, objServer.Cyberware);
            DiffCollection(objResult, "Weapons", objLocal.WeaponTrees, objServer.WeaponTrees);
            DiffCollection(objResult, "Lifestyles", objLocal.Lifestyles, objServer.Lifestyles);
            DiffCollection(objResult, "Gear", objLocal.Gear, objServer.Gear);
            DiffCollection(objResult, "Vehicles", objLocal.Vehicles, objServer.Vehicles);
            DiffCollection(objResult, "Karma Expenses", objLocal.KarmaExpenses, objServer.KarmaExpenses);
            DiffCollection(objResult, "Nuyen Expenses", objLocal.NuyenExpenses, objServer.NuyenExpenses);
            DiffCalendar(objResult, objLocal, objServer);

            return objResult;
        }

        private static void DiffCalendar(CharacterDiffResult objResult, CharacterDocument objLocal,
            CharacterDocument objServer)
        {
            var dicLocal = objLocal.Calendar.ToDictionary(objWeek => (objWeek.Year, objWeek.Week));
            var dicServer = objServer.Calendar.ToDictionary(objWeek => (objWeek.Year, objWeek.Week));
            foreach ((int intYear, int intWeek) objDate in dicServer.Keys.Union(dicLocal.Keys)
                         .OrderBy(objKey => objKey.Year).ThenBy(objKey => objKey.Week))
            {
                dicLocal.TryGetValue(objDate, out CalendarWeek? objLocalWeek);
                dicServer.TryGetValue(objDate, out CalendarWeek? objServerWeek);
                string strLocal = objLocalWeek?.Notes ?? string.Empty;
                string strServer = objServerWeek?.Notes ?? string.Empty;
                if (objLocalWeek != null && objServerWeek != null && strLocal == strServer)
                    continue;
                objResult.Entries.Add(new CharacterDiffEntry
                {
                    Collection = "Calendar",
                    Change = objLocalWeek == null ? "Added" : objServerWeek == null ? "Removed" : "Changed",
                    Name = $"{objDate.Item1}-W{objDate.Item2:00}",
                    Detail = strLocal + " -> " + strServer,
                    LocalValue = string.IsNullOrEmpty(strLocal) ? (objLocalWeek == null ? string.Empty : "(week)") : strLocal,
                    ServerValue = string.IsNullOrEmpty(strServer) ? (objServerWeek == null ? string.Empty : "(week)") : strServer
                });
            }
        }

        private static void CompareScalar(CharacterDiffResult objResult, string strName, string strLocal, string strServer)
        {
            if (strLocal == strServer)
                return;

            objResult.Entries.Add(new CharacterDiffEntry
            {
                Collection = string.Empty,
                Change = "Changed",
                Name = strName,
                Detail = strLocal + " -> " + strServer,
                LocalValue = strLocal,
                ServerValue = strServer
            });
        }

        private static void CompareInitiative(CharacterDiffResult objResult, string strName,
            CharacterInitiativeData objLocal, CharacterInitiativeData objServer)
        {
            CompareScalar(objResult, strName, objLocal.Display, objServer.Display);
        }

        private static void DiffCollection<T>(CharacterDiffResult objResult, string strCollectionName,
            IReadOnlyList<T> lstLocal, IReadOnlyList<T> lstServer)
        {
            Dictionary<string, int> dicLocal = CountBySignature(lstLocal);
            Dictionary<string, int> dicServer = CountBySignature(lstServer);

            foreach (string strSignature in dicServer.Keys.Union(dicLocal.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                int intLocal = dicLocal.TryGetValue(strSignature, out int intLocalValue) ? intLocalValue : 0;
                int intServer = dicServer.TryGetValue(strSignature, out int intServerValue) ? intServerValue : 0;
                if (intLocal == intServer)
                    continue;

                if (intLocal == 0)
                {
                    objResult.Entries.Add(new CharacterDiffEntry
                    {
                        Collection = strCollectionName,
                        Change = "Added",
                        Name = strSignature,
                        Detail = intServer > 1 ? "x" + intServer : string.Empty,
                        LocalValue = string.Empty,
                        ServerValue = intServer > 1 ? "x" + intServer : "1"
                    });
                }
                else if (intServer == 0)
                {
                    objResult.Entries.Add(new CharacterDiffEntry
                    {
                        Collection = strCollectionName,
                        Change = "Removed",
                        Name = strSignature,
                        Detail = intLocal > 1 ? "x" + intLocal : string.Empty,
                        LocalValue = intLocal > 1 ? "x" + intLocal : "1",
                        ServerValue = string.Empty
                    });
                }
                else
                {
                    objResult.Entries.Add(new CharacterDiffEntry
                    {
                        Collection = strCollectionName,
                        Change = "Changed",
                        Name = strSignature,
                        Detail = intLocal + " -> " + intServer,
                        LocalValue = intLocal.ToString(),
                        ServerValue = intServer.ToString()
                    });
                }
            }
        }

        private static Dictionary<string, int> CountBySignature<T>(IReadOnlyList<T> lstItems)
        {
            Dictionary<string, int> dicCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (T objItem in lstItems ?? Array.Empty<T>())
            {
                string strSignature = Signature(objItem!);
                if (dicCounts.ContainsKey(strSignature))
                    dicCounts[strSignature]++;
                else
                    dicCounts[strSignature] = 1;
            }

            return dicCounts;
        }

        private static string Signature(object objItem)
        {
            Type objType = objItem.GetType();
            StringBuilder sbdSignature = new StringBuilder(GetDisplayName(objItem));

            AppendPropertyValue(objType, objItem, sbdSignature, "Rating", " R");
            AppendPropertyValue(objType, objItem, sbdSignature, "Quantity", " x");
            AppendPropertyValue(objType, objItem, sbdSignature, "Force", " F");
            AppendPropertyValue(objType, objItem, sbdSignature, "Services", " S");
            AppendPropertyValue(objType, objItem, sbdSignature, "Connection", " C");
            AppendPropertyValue(objType, objItem, sbdSignature, "Loyalty", " L");
            AppendPropertyValue(objType, objItem, sbdSignature, "Type", " ");
            AppendPropertyValue(objType, objItem, sbdSignature, "Category", " ");
            AppendPropertyValue(objType, objItem, sbdSignature, "Damage", " ");
            AppendPropertyValue(objType, objItem, sbdSignature, "Ammo", " ");
            AppendPropertyValue(objType, objItem, sbdSignature, "Cost", " ¥");
            AppendPropertyValue(objType, objItem, sbdSignature, "Months", " M");
            AppendPropertyValue(objType, objItem, sbdSignature, "Amount", " ");
            AppendPropertyValue(objType, objItem, sbdSignature, "Reason", " ");
            AppendPropertyValue(objType, objItem, sbdSignature, "TotalValue", " =");
            AppendPropertyValue(objType, objItem, sbdSignature, "Minimum", " min:");
            AppendPropertyValue(objType, objItem, sbdSignature, "Maximum", " max:");
            AppendPropertyValue(objType, objItem, sbdSignature, "AugmentedMaximum", " augmax:");
            AppendPropertyValue(objType, objItem, sbdSignature, "BaseRating", " base");
            AppendPropertyValue(objType, objItem, sbdSignature, "Specialization", " spec:");
            AppendPropertyValue(objType, objItem, sbdSignature, "Broken", " broken:");
            AppendPropertyValue(objType, objItem, sbdSignature, "Bonded", " bonded:");
            AppendPropertyValue(objType, objItem, sbdSignature, "Extra", " ext:");
            AppendPropertyValue(objType, objItem, sbdSignature, "Value", " val:");

            int intChildCount = TotalChildCount(objItem);
            if (intChildCount > 0)
                sbdSignature.Append(" (").Append(intChildCount).Append(" child items)");

            return sbdSignature.ToString().Trim();
        }

        private static void AppendPropertyValue(Type objType, object objItem, StringBuilder sbdSignature,
            string strPropertyName, string strPrefix)
        {
            PropertyInfo objProperty = objType.GetProperty(strPropertyName);
            if (objProperty == null)
                return;

            object objValue = objProperty.GetValue(objItem, null);
            if (objValue == null)
                return;

            string strValue = objValue.ToString();
            if (string.IsNullOrWhiteSpace(strValue))
                return;

            sbdSignature.Append(strPrefix).Append(strValue.Trim());
        }

        private static string GetDisplayName(object objItem)
        {
            if (objItem is Improvement objImprovement)
                return objImprovement.SourceName + " " + objImprovement.ImprovedName;

            Type objType = objItem.GetType();
            string? strDisplayName = objType.GetProperty("DisplayName")?.GetValue(objItem, null) as string;
            if (strDisplayName != null && !string.IsNullOrWhiteSpace(strDisplayName))
                return strDisplayName;

            string? strName = objType.GetProperty("Name")?.GetValue(objItem, null) as string;
            if (strName != null && !string.IsNullOrWhiteSpace(strName))
                return strName;

            string? strCode = objType.GetProperty("Code")?.GetValue(objItem, null) as string;
            return strCode == null || string.IsNullOrWhiteSpace(strCode) ? "(unnamed)" : strCode;
        }

        private static int TotalChildCount(object objItem)
        {
            if (objItem is CharacterTreeItemData objTreeItem)
                return CountTreeChildren(objTreeItem);

            return 0;
        }

        private static int CountTreeChildren(CharacterTreeItemData objItem)
        {
            int intTotal = objItem.Children.Count;
            foreach (CharacterTreeItemData objChild in objItem.Children)
                intTotal += CountTreeChildren(objChild);
            return intTotal;
        }
    }
}
