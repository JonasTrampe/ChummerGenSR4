using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace Chummer.Core;

/// <summary>Conservative, domain-oriented three-way merge for character data. The XML is only
/// used as the persistence representation; merge decisions are made per scalar value and
/// per logical item, never by textual XML hunks.</summary>
public static class CharacterMergeService
{
    private static readonly string[] s_collectionNames =
    {
        "attributes", "skillgroups", "skills", "knowledgeskills", "complexforms", "foci",
        "stackedfoci", "improvements", "qualities", "contacts", "spirits", "spells", "powers",
        "martialarts", "armor", "cyberware", "weapons", "lifestyles", "gears", "vehicles",
        "karmaexpenses", "nuyenexpenses"
    };

    public static CharacterMergeResult TryMerge(CharacterDocument objBase, CharacterDocument objLocal,
        CharacterDocument objServer)
    {
        if (objBase == null || objLocal == null || objServer == null)
            throw new ArgumentNullException();

        CharacterMergeResult objResult = new();
        CharacterDocument objMerged = Clone(objLocal);

        MergeScalar(objBase, objLocal, objServer, objMerged, "alias", objResult);
        MergeScalar(objBase, objLocal, objServer, objMerged, "notes", objResult);
        MergeScalar(objBase, objLocal, objServer, objMerged, "mugshot", objResult);
        MergeResource(objBase, objLocal, objServer, objMerged, "karma", objResult);
        MergeResource(objBase, objLocal, objServer, objMerged, "nuyen", objResult);
        MergeCalendar(objBase, objLocal, objServer, objMerged, objResult);

        foreach (string strCollectionName in s_collectionNames)
            MergeCollection(objBase, objLocal, objServer, objMerged, strCollectionName, objResult);

        if (int.TryParse(objMerged.Karma, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intKarma)
            && intKarma < 0)
            objResult.Conflicts.Add("The merged Karma balance would be negative.");
        if (int.TryParse(objMerged.Nuyen, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intNuyen)
            && intNuyen < 0)
            objResult.Conflicts.Add("The merged Nuyen balance would be negative.");

        objResult.MergedCharacter = objResult.Conflicts.Count == 0 ? objMerged : null;
        return objResult;
    }

    private static void MergeScalar(CharacterDocument objBase, CharacterDocument objLocal,
        CharacterDocument objServer, CharacterDocument objMerged, string strName, CharacterMergeResult objResult)
    {
        string strBase = Value(objBase, strName);
        string strLocal = Value(objLocal, strName);
        string strServer = Value(objServer, strName);
        if (strLocal == strBase)
            SetValue(objMerged, strName, strServer);
        else if (strServer != strBase && strLocal != strServer)
            objResult.Conflicts.Add("Both revisions changed " + strName + ".");
        else
            objResult.AutoMergedChanges++;
    }

    private static void MergeResource(CharacterDocument objBase, CharacterDocument objLocal,
        CharacterDocument objServer, CharacterDocument objMerged, string strName, CharacterMergeResult objResult)
    {
        if (!int.TryParse(Value(objBase, strName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intBase)
            || !int.TryParse(Value(objLocal, strName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intLocal)
            || !int.TryParse(Value(objServer, strName), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intServer))
        {
            objResult.Conflicts.Add("The " + strName + " balance could not be interpreted.");
            return;
        }

        int intMerged = intLocal + intServer - intBase;
        SetValue(objMerged, strName, intMerged.ToString(CultureInfo.InvariantCulture));
        if (intLocal != intBase && intServer != intBase)
            objResult.AutoMergedChanges++;
    }

    private static void MergeCollection(CharacterDocument objBase, CharacterDocument objLocal,
        CharacterDocument objServer, CharacterDocument objMerged, string strName, CharacterMergeResult objResult)
    {
        Dictionary<string, string> dicBase = ReadItems(objBase, strName);
        Dictionary<string, string> dicLocal = ReadItems(objLocal, strName);
        Dictionary<string, string> dicServer = ReadItems(objServer, strName);
        XmlElement? objMergedParent = objMerged.Document.SelectSingleNode("/character/" + strName) as XmlElement;
        if (objMergedParent == null)
        {
            XmlElement? objRoot = objMerged.Document.DocumentElement;
            if (objRoot == null || (dicLocal.Count == 0 && dicServer.Count == 0))
                return;
            objMergedParent = objMerged.Document.CreateElement(strName);
            objRoot.AppendChild(objMergedParent);
        }

        Dictionary<string, string> dicMerged = new(StringComparer.Ordinal);
        foreach (string strKey in dicBase.Keys.Union(dicLocal.Keys).Union(dicServer.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            dicBase.TryGetValue(strKey, out string? strBase);
            dicLocal.TryGetValue(strKey, out string? strLocal);
            dicServer.TryGetValue(strKey, out string? strServer);
            string? strChosen;
            if (strLocal == strBase)
                strChosen = strServer;
            else if (strServer == strBase || strLocal == strServer)
                strChosen = strLocal;
            else
            {
                objResult.Conflicts.Add("Both revisions changed " + strName + " item " + strKey + ".");
                continue;
            }
            if (strChosen != null)
                dicMerged[strKey] = strChosen;
            if (strLocal != strBase || strServer != strBase)
                objResult.AutoMergedChanges++;
        }

        while (objMergedParent.HasChildNodes)
            objMergedParent.RemoveChild(objMergedParent.FirstChild!);
        foreach (string strXml in dicMerged.Values)
        {
            var objDocument = new XmlDocument();
            objDocument.LoadXml(strXml);
            objMergedParent.AppendChild(objMerged.Document.ImportNode(objDocument.DocumentElement!, true));
        }
    }

    private static void MergeCalendar(CharacterDocument objBase, CharacterDocument objLocal,
        CharacterDocument objServer, CharacterDocument objMerged, CharacterMergeResult objResult)
    {
        Dictionary<string, string> dicBase = ReadCalendarItems(objBase);
        Dictionary<string, string> dicLocal = ReadCalendarItems(objLocal);
        Dictionary<string, string> dicServer = ReadCalendarItems(objServer);
        XmlElement? objMergedParent = objMerged.Document.SelectSingleNode("/character/calendar") as XmlElement;
        if (objMergedParent == null)
        {
            XmlElement? objRoot = objMerged.Document.DocumentElement;
            if (objRoot == null || (dicLocal.Count == 0 && dicServer.Count == 0))
                return;
            objMergedParent = objMerged.Document.CreateElement("calendar");
            objRoot.AppendChild(objMergedParent);
        }

        var dicMerged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string strKey in dicBase.Keys.Union(dicLocal.Keys).Union(dicServer.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            dicBase.TryGetValue(strKey, out string? strBase);
            dicLocal.TryGetValue(strKey, out string? strLocal);
            dicServer.TryGetValue(strKey, out string? strServer);
            string? strChosen;
            if (strLocal == strBase)
                strChosen = strServer;
            else if (strServer == strBase || strLocal == strServer)
                strChosen = strLocal;
            else
            {
                objResult.Conflicts.Add("Both revisions changed calendar week " + strKey + ".");
                continue;
            }
            if (strChosen != null)
                dicMerged[strKey] = strChosen;
            if (strLocal != strBase || strServer != strBase)
                objResult.AutoMergedChanges++;
        }

        while (objMergedParent.HasChildNodes)
            objMergedParent.RemoveChild(objMergedParent.FirstChild!);
        foreach (string strXml in dicMerged.Values)
        {
            var objDocument = new XmlDocument();
            objDocument.LoadXml(strXml);
            objMergedParent.AppendChild(objMerged.Document.ImportNode(objDocument.DocumentElement!, true));
        }
    }

    private static Dictionary<string, string> ReadCalendarItems(CharacterDocument objCharacter)
    {
        var dicItems = new Dictionary<string, string>(StringComparer.Ordinal);
        XmlNode? objParent = objCharacter.Document.SelectSingleNode("/character/calendar");
        if (objParent == null)
            return dicItems;
        foreach (XmlNode objNode in objParent.ChildNodes.OfType<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element))
        {
            string strYear = objNode["year"]?.InnerText ?? string.Empty;
            string strWeek = objNode["week"]?.InnerText ?? string.Empty;
            string strKey = strYear + "-W" + strWeek;
            if (dicItems.ContainsKey(strKey))
                strKey += "#" + dicItems.Count;
            dicItems[strKey] = objNode.OuterXml;
        }
        return dicItems;
    }

    private static Dictionary<string, string> ReadItems(CharacterDocument objCharacter, string strName)
    {
        var dicItems = new Dictionary<string, string>(StringComparer.Ordinal);
        XmlNode? objParent = objCharacter.Document.SelectSingleNode("/character/" + strName);
        if (objParent == null)
            return dicItems;
        foreach (XmlNode objNode in objParent.ChildNodes.OfType<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element))
        {
            string strKey = LogicalKey(objNode);
            int intDuplicate = 1;
            string strUniqueKey = strKey;
            while (dicItems.ContainsKey(strUniqueKey))
                strUniqueKey = strKey + "#" + ++intDuplicate;
            dicItems[strUniqueKey] = objNode.OuterXml;
        }
        return dicItems;
    }

    private static string LogicalKey(XmlNode objNode)
    {
        foreach (string strName in new[] { "guid", "id", "unique", "name", "sourcename", "improvedname" })
        {
            string? strValue = objNode[strName]?.InnerText;
            if (!string.IsNullOrWhiteSpace(strValue))
                return objNode.Name + ":" + strName + ":" + strValue.Trim();
        }
        return objNode.Name + ":" + objNode.OuterXml;
    }

    private static CharacterDocument Clone(CharacterDocument objCharacter)
    {
        using var objStream = new MemoryStream();
        new CharacterFileService().Save(objCharacter, objStream, objCharacter.DisplayName);
        objStream.Position = 0;
        return new CharacterFileService().Load(objStream, objCharacter.DisplayName);
    }

    private static string Value(CharacterDocument objCharacter, string strName) =>
        objCharacter.Document.SelectSingleNode("/character/" + strName)?.InnerText ?? string.Empty;

    private static void SetValue(CharacterDocument objCharacter, string strName, string strValue)
    {
        XmlElement? objRoot = objCharacter.Document.DocumentElement;
        if (objRoot == null)
            return;
        XmlElement? objElement = objRoot[strName] ?? objRoot.AppendChild(objCharacter.Document.CreateElement(strName)) as XmlElement;
        if (objElement != null)
            objElement.InnerText = strValue;
    }
}

public sealed class CharacterMergeResult
{
    public CharacterDocument? MergedCharacter { get; internal set; }
    public List<string> Conflicts { get; } = new();
    public int AutoMergedChanges { get; internal set; }
    public bool CanMerge => MergedCharacter != null && Conflicts.Count == 0;
}
