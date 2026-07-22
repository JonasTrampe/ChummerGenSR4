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

            return objResult;
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
                Detail = strLocal + " -> " + strServer
            });
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
                        Detail = intServer > 1 ? "x" + intServer : string.Empty
                    });
                }
                else if (intServer == 0)
                {
                    objResult.Entries.Add(new CharacterDiffEntry
                    {
                        Collection = strCollectionName,
                        Change = "Removed",
                        Name = strSignature,
                        Detail = intLocal > 1 ? "x" + intLocal : string.Empty
                    });
                }
                else
                {
                    objResult.Entries.Add(new CharacterDiffEntry
                    {
                        Collection = strCollectionName,
                        Change = "Changed",
                        Name = strSignature,
                        Detail = intLocal + " -> " + intServer
                    });
                }
            }
        }

        private static Dictionary<string, int> CountBySignature<T>(IReadOnlyList<T> lstItems)
        {
            Dictionary<string, int> dicCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (T objItem in lstItems ?? Array.Empty<T>())
            {
                string strSignature = Signature(objItem);
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
            Type objType = objItem.GetType();
            string strDisplayName = objType.GetProperty("DisplayName")?.GetValue(objItem, null) as string;
            if (!string.IsNullOrWhiteSpace(strDisplayName))
                return strDisplayName;

            string strName = objType.GetProperty("Name")?.GetValue(objItem, null) as string;
            return string.IsNullOrWhiteSpace(strName) ? "(unnamed)" : strName;
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
