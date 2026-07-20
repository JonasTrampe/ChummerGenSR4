using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chummer
{
	/// <summary>
	/// Compares two Characters loaded from independent .chum content - "yours" (the in-memory character
	/// being saved) vs. "the server's" (a revision just downloaded because of a 412 conflict) - and
	/// reports what differs. Matches items within each of Character's 13 collections by InternalId (a
	/// GUID persisted in the file's own XML - see clsEquipment.cs's Load(), stable across saves as long
	/// as the item itself isn't deleted/recreated), so renamed/re-rated items are recognized as the same
	/// item rather than showing up as a spurious add+remove pair. Reflection-driven rather than
	/// hand-written per item type, since the 13 collections span entirely unrelated classes that only
	/// share InternalId/Name/DisplayNameShort/Rating/Quantity by convention, not by a common interface.
	/// </summary>
	public static class CharacterDiff
	{
		public static CharacterDiffResult Compare(Character objLocal, Character objServer)
		{
			CharacterDiffResult objResult = new CharacterDiffResult();

			CompareScalar(objResult, "Alias", objLocal.Alias, objServer.Alias);
			CompareScalar(objResult, "Karma", objLocal.Karma.ToString(), objServer.Karma.ToString());
			CompareScalar(objResult, "Career Karma", objLocal.CareerKarma.ToString(), objServer.CareerKarma.ToString());
			CompareScalar(objResult, "Nuyen", objLocal.Nuyen.ToString(), objServer.Nuyen.ToString());

			DiffCollection(objResult, "Qualities", objLocal.Qualities, objServer.Qualities);
			DiffCollection(objResult, "Contacts", objLocal.Contacts, objServer.Contacts);
			DiffCollection(objResult, "Spirits", objLocal.Spirits, objServer.Spirits);
			DiffCollection(objResult, "Spells", objLocal.Spells, objServer.Spells);
			DiffCollection(objResult, "Powers", objLocal.Powers, objServer.Powers);
			DiffCollection(objResult, "Martial Arts", objLocal.MartialArts, objServer.MartialArts);
			DiffCollection(objResult, "Armor", objLocal.Armor, objServer.Armor);
			DiffCollection(objResult, "Cyberware", objLocal.Cyberware, objServer.Cyberware);
			DiffCollection(objResult, "Weapons", objLocal.Weapons, objServer.Weapons);
			DiffCollection(objResult, "Lifestyles", objLocal.Lifestyles, objServer.Lifestyles);
			DiffCollection(objResult, "Gear", objLocal.Gear, objServer.Gear);
			DiffCollection(objResult, "Vehicles", objLocal.Vehicles, objServer.Vehicles);
			DiffCollection(objResult, "Critter Powers", objLocal.CritterPowers, objServer.CritterPowers);

			return objResult;
		}

		private static void CompareScalar(CharacterDiffResult objResult, string strName, string strLocal, string strServer)
		{
			if (strLocal == strServer)
				return;
			objResult.Entries.Add(new CharacterDiffEntry
			{
				Collection = "",
				Change = "Changed",
				Name = strName,
				Detail = strLocal + " -> " + strServer,
			});
		}

		private static void DiffCollection<T>(CharacterDiffResult objResult, string strCollectionName, List<T> lstLocal, List<T> lstServer)
		{
			Dictionary<string, T> objLocalById = ToDictionaryByInternalId(lstLocal);
			Dictionary<string, T> objServerById = ToDictionaryByInternalId(lstServer);

			foreach (KeyValuePair<string, T> objPair in objServerById)
			{
				if (!objLocalById.ContainsKey(objPair.Key))
				{
					objResult.Entries.Add(new CharacterDiffEntry
					{
						Collection = strCollectionName,
						Change = "Added",
						Name = GetDisplayName(objPair.Value),
						Detail = ChildCountDetail(objPair.Value),
					});
				}
			}

			foreach (KeyValuePair<string, T> objPair in objLocalById)
			{
				if (!objServerById.TryGetValue(objPair.Key, out T objServerItem))
				{
					objResult.Entries.Add(new CharacterDiffEntry
					{
						Collection = strCollectionName,
						Change = "Removed",
						Name = GetDisplayName(objPair.Value),
						Detail = "",
					});
					continue;
				}

				string strLocalSignature = Signature(objPair.Value);
				string strServerSignature = Signature(objServerItem);
				if (strLocalSignature == strServerSignature)
					continue;

				string strChildCounts = ChildCountDetail(objPair.Value, objServerItem);
				objResult.Entries.Add(new CharacterDiffEntry
				{
					Collection = strCollectionName,
					Change = "Changed",
					Name = GetDisplayName(objServerItem),
					Detail = (strLocalSignature + " -> " + strServerSignature + strChildCounts).Trim(),
				});
			}
		}

		private static Dictionary<string, T> ToDictionaryByInternalId<T>(List<T> lstItems)
		{
			Dictionary<string, T> objDictionary = new Dictionary<string, T>();
			if (lstItems == null)
				return objDictionary;
			PropertyInfo objIdProperty = typeof(T).GetProperty("InternalId");
			foreach (T objItem in lstItems)
			{
				string strId = objIdProperty?.GetValue(objItem, null) as string;
				if (!string.IsNullOrEmpty(strId) && !objDictionary.ContainsKey(strId))
					objDictionary[strId] = objItem;
			}
			return objDictionary;
		}

		private static string GetDisplayName(object objItem)
		{
			Type objType = objItem.GetType();
			string strDisplayNameShort = objType.GetProperty("DisplayNameShort")?.GetValue(objItem, null) as string;
			if (!string.IsNullOrEmpty(strDisplayNameShort))
				return strDisplayNameShort;
			string strName = objType.GetProperty("Name")?.GetValue(objItem, null) as string;
			return string.IsNullOrEmpty(strName) ? "(unnamed)" : strName;
		}

		/// <summary>
		/// A cheap "did this item's own values change" fingerprint - name plus whatever of Rating/
		/// Quantity the type happens to expose (most equipment types have Rating, Gear also has
		/// Quantity; collections like Contacts/Spells that have neither just compare by name).
		/// </summary>
		private static string Signature(object objItem)
		{
			Type objType = objItem.GetType();
			StringBuilder objSignature = new StringBuilder(GetDisplayName(objItem));

			PropertyInfo objRatingProperty = objType.GetProperty("Rating");
			if (objRatingProperty != null && objRatingProperty.PropertyType == typeof(int))
				objSignature.Append(" R").Append(objRatingProperty.GetValue(objItem, null));

			PropertyInfo objQuantityProperty = objType.GetProperty("Quantity");
			if (objQuantityProperty != null)
				objSignature.Append(" x").Append(objQuantityProperty.GetValue(objItem, null));

			return objSignature.ToString();
		}

		/// <summary>
		/// Reports how many nested child items (mods, plugins, mounted gear/weapons - whatever List&lt;&gt;
		/// properties the type happens to carry) sit under a changed container, without recursing into
		/// diffing them individually - per the agreed scope, nested containers only get a count.
		/// </summary>
		private static string ChildCountDetail(object objItem)
		{
			int intCount = TotalChildCount(objItem);
			return intCount > 0 ? " (" + intCount + " child items)" : "";
		}

		private static string ChildCountDetail(object objLocalItem, object objServerItem)
		{
			int intLocalCount = TotalChildCount(objLocalItem);
			int intServerCount = TotalChildCount(objServerItem);
			if (intLocalCount == 0 && intServerCount == 0)
				return "";
			return intLocalCount == intServerCount
				? " (" + intLocalCount + " child items)"
				: " (" + intLocalCount + " -> " + intServerCount + " child items)";
		}

		private static int TotalChildCount(object objItem)
		{
			int intTotal = 0;
			foreach (PropertyInfo objProperty in objItem.GetType().GetProperties())
			{
				if (!typeof(IEnumerable).IsAssignableFrom(objProperty.PropertyType) || objProperty.PropertyType == typeof(string))
					continue;
				if (!objProperty.PropertyType.IsGenericType || objProperty.PropertyType.GetGenericTypeDefinition() != typeof(List<>))
					continue;
				if (!(objProperty.GetValue(objItem, null) is IEnumerable objList))
					continue;
				foreach (object objChild in objList)
					intTotal++;
			}
			return intTotal;
		}
	}
}
