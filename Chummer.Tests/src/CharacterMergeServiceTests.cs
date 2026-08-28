using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public sealed class CharacterMergeServiceTests
{
    [Fact]
    public void TryMerge_RealCharacters_CombinesIndependentCareerChanges()
    {
        string[] astrPaths = Directory.GetFiles(RealSavesDirectory(), "*.chum", SearchOption.AllDirectories)
            .OrderBy(strPath => strPath, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        Assert.True(astrPaths.Length >= 2, "At least two real character saves are required for merge coverage.");

        foreach (string strPath in astrPaths)
        {
            CharacterDocument objBase = Load(strPath);
            CharacterDocument objLocal = Clone(objBase);
            CharacterDocument objServer = Clone(objBase);

            int intBaseKarma = int.Parse(objBase.Karma, CultureInfo.InvariantCulture);
            int intBaseNuyen = int.Parse(objBase.Nuyen, CultureInfo.InvariantCulture);
            SetValue(objLocal, "karma", (intBaseKarma + 7).ToString(CultureInfo.InvariantCulture));
            SetValue(objServer, "nuyen", (intBaseNuyen + 275).ToString(CultureInfo.InvariantCulture));
            AddSkill(objLocal, "Merge-only local skill", "local-merge-test");
            AddSkill(objServer, "Merge-only server skill", "server-merge-test");

            CharacterMergeResult objResult = CharacterMergeService.TryMerge(objBase, objLocal, objServer);

            Assert.True(objResult.CanMerge, strPath + ": independent edits should merge.");
            Assert.Equal(intBaseKarma + 7, int.Parse(objResult.MergedCharacter!.Karma, CultureInfo.InvariantCulture));
            Assert.Equal(intBaseNuyen + 275, int.Parse(objResult.MergedCharacter.Nuyen, CultureInfo.InvariantCulture));
            Assert.Contains(objResult.MergedCharacter.Skills, objSkill => objSkill.Name == "Merge-only local skill");
            Assert.Contains(objResult.MergedCharacter.Skills, objSkill => objSkill.Name == "Merge-only server skill");
        }
    }

    [Fact]
    public void TryMerge_RealCharacter_SameFieldChangesAreReportedAsConflict()
    {
        string strPath = Directory.GetFiles(RealSavesDirectory(), "*.chum", SearchOption.AllDirectories)
            .OrderBy(strSavePath => strSavePath, StringComparer.Ordinal)
            .First();
        CharacterDocument objBase = Load(strPath);
        CharacterDocument objLocal = Clone(objBase);
        CharacterDocument objServer = Clone(objBase);

        SetValue(objLocal, "alias", "Local branch alias");
        SetValue(objServer, "alias", "Server branch alias");

        CharacterMergeResult objResult = CharacterMergeService.TryMerge(objBase, objLocal, objServer);

        Assert.False(objResult.CanMerge);
        Assert.Contains(objResult.Conflicts, strConflict => strConflict.Contains("alias", StringComparison.OrdinalIgnoreCase));
        Assert.Null(objResult.MergedCharacter);
    }

    [Fact]
    public void TryMerge_RealCharacter_ConflictsOnlyWhenTheSameCalendarWeekChanges()
    {
        string strPath = Directory.GetFiles(RealSavesDirectory(), "*.chum", SearchOption.AllDirectories).First();
        CharacterDocument objBase = Load(strPath);
        objBase.AddCalendarWeek(2080, 12, "Base entry");
        CharacterDocument objLocal = Clone(objBase);
        CharacterDocument objServer = Clone(objBase);
        SetCalendarNote(objLocal, 2080, 12, "Local entry");
        SetCalendarNote(objServer, 2080, 12, "Server entry");

        CharacterMergeResult objConflict = CharacterMergeService.TryMerge(objBase, objLocal, objServer);
        Assert.False(objConflict.CanMerge);
        Assert.Contains(objConflict.Conflicts, strConflict => strConflict.Contains("calendar week", StringComparison.OrdinalIgnoreCase));

        CharacterDocument objIndependentServer = Clone(objBase);
        objIndependentServer.AddCalendarWeek(2080, 13, "Independent entry");
        CharacterMergeResult objIndependent = CharacterMergeService.TryMerge(objBase, objLocal, objIndependentServer);
        Assert.True(objIndependent.CanMerge);
        Assert.Contains(objIndependent.MergedCharacter!.Calendar, objWeek => objWeek.Year == 2080 && objWeek.Week == 13);
    }

    [Fact]
    public void CharacterDiff_ReportsCalendarWeekChangesByDate()
    {
        string strPath = Directory.GetFiles(RealSavesDirectory(), "*.chum", SearchOption.AllDirectories).First();
        CharacterDocument objLocal = Load(strPath);
        CharacterDocument objServer = Clone(objLocal);
        objLocal.AddCalendarWeek(2081, 4, "Local note");
        objServer.AddCalendarWeek(2081, 4, "Server note");

        CharacterDiffEntry objEntry = Assert.Single(CharacterDiff.Compare(objLocal, objServer).Entries,
            objDiff => objDiff.Collection == "Calendar");
        Assert.Equal("2081-W04", objEntry.Name);
        Assert.Equal("Local note", objEntry.LocalValue);
        Assert.Equal("Server note", objEntry.ServerValue);
    }

    private static string RealSavesDirectory() => Path.Combine(AppContext.BaseDirectory, "data", "saves");

    private static CharacterDocument Load(string strPath)
    {
        using FileStream objStream = File.OpenRead(strPath);
        return new CharacterFileService().Load(objStream, Path.GetFileName(strPath));
    }

    private static CharacterDocument Clone(CharacterDocument objCharacter)
    {
        using MemoryStream objStream = new();
        new CharacterFileService().Save(objCharacter, objStream, objCharacter.DisplayName);
        objStream.Position = 0;
        return new CharacterFileService().Load(objStream, objCharacter.DisplayName);
    }

    private static void SetValue(CharacterDocument objCharacter, string strName, string strValue)
    {
        XmlElement objRoot = objCharacter.Document.DocumentElement!;
        XmlElement objElement = objRoot[strName] ?? (XmlElement)objRoot.AppendChild(objCharacter.Document.CreateElement(strName))!;
        objElement.InnerText = strValue;
    }

    private static void AddSkill(CharacterDocument objCharacter, string strName, string strGuid)
    {
        XmlElement objRoot = objCharacter.Document.DocumentElement!;
        XmlElement objSkills = objRoot["skills"] ?? (XmlElement)objRoot.AppendChild(objCharacter.Document.CreateElement("skills"))!;
        XmlElement objSkill = objCharacter.Document.CreateElement("skill");
        XmlElement objGuid = objCharacter.Document.CreateElement("guid");
        objGuid.InnerText = strGuid;
        objSkill.AppendChild(objGuid);
        XmlElement objName = objCharacter.Document.CreateElement("name");
        objName.InnerText = strName;
        objSkill.AppendChild(objName);
        XmlElement objRating = objCharacter.Document.CreateElement("rating");
        objRating.InnerText = "1";
        objSkill.AppendChild(objRating);
        objSkills.AppendChild(objSkill);
    }

    private static void SetCalendarNote(CharacterDocument objCharacter, int intYear, int intWeek, string strNotes)
    {
        XmlNode objWeek = objCharacter.Document.SelectSingleNode($"/character/calendar/week[year='{intYear}' and week='{intWeek}']")
            ?? throw new InvalidOperationException("Calendar week not found.");
        XmlElement objNotes = objWeek["notes"] ?? (XmlElement)objWeek.AppendChild(objCharacter.Document.CreateElement("notes"))!;
        objNotes.InnerText = strNotes;
    }
}
