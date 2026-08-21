using System;
using System.IO;
using System.Linq;
using System.Text;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

/// <summary>
/// GetCharacterOptions() (CharacterFileService.cs) now caches its settings-file read per
/// CharacterDocument instance instead of re-reading the file from disk on every call (the
/// original fix for a slow skill/skill-group rating change, which used to do that read once per
/// active skill on every reload). This test proves the cache still picks up an Options dialog
/// save while the character stays open - i.e. it's invalidated by the settings file's own
/// last-write time, not just cached forever.
/// </summary>
public sealed class CharacterOptionsCacheTests
{
    private static CharacterDocument LoadXml(string strXml, string strSettingsFileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            strXml.Replace("</character>", $"<settings>{strSettingsFileName}</settings></character>")));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    private static void WriteSettingsFile(string strPath, string strKarmaAttribute, DateTime datWriteTimeUtc)
    {
        string strDefaultPath = Path.Combine(AppContext.BaseDirectory, "settings", "default.xml");
        // default.xml is UTF-16 (matches the legacy save format) - File.ReadAllText auto-detects
        // that via the BOM, but File.WriteAllText defaults to UTF-8, which corrupts the file's own
        // encoding="utf-16" XML declaration and breaks XmlDocument.Load. Write back as UTF-16.
        string strContent = File.ReadAllText(strDefaultPath, Encoding.Unicode);
        string strModified = System.Text.RegularExpressions.Regex.Replace(
            strContent, "<karmaattribute>\\d+</karmaattribute>", $"<karmaattribute>{strKarmaAttribute}</karmaattribute>");
        File.WriteAllText(strPath, strModified, Encoding.Unicode);
        // The cache keys off the file's own last-write time - set it explicitly rather than
        // relying on wall-clock writes, since some filesystems only have second-level mtime
        // resolution and two writes within the same test can otherwise land on the same tick.
        File.SetLastWriteTimeUtc(strPath, datWriteTimeUtc);
    }

    [Fact]
    public void GetCharacterOptions_PicksUpASettingsFileChangeWhileTheCharacterStaysOpen()
    {
        string strSettingsFileName = Guid.NewGuid() + ".xml";
        string strSettingsPath = Path.Combine(AppContext.BaseDirectory, "settings", strSettingsFileName);
        try
        {
            DateTime datNow = DateTime.UtcNow;
            WriteSettingsFile(strSettingsPath, "5", datNow);

            CharacterDocument character = LoadXml("<character><attributes>"
                + "<attribute><name>BOD</name><value>3</value><totalvalue>3</totalvalue><metatypemin>1</metatypemin></attribute>"
                + "</attributes></character>", strSettingsFileName);

            int intCostBefore = character.Attributes.Single(a => a.Code == "BOD").KarmaCostToIncrease;
            Assert.Equal((3 + 1) * 5, intCostBefore);

            // Simulate the Options dialog saving a house-rule change while the character stays
            // open: rewrite the same file with a different value and a later mtime.
            WriteSettingsFile(strSettingsPath, "10", datNow.AddSeconds(5));

            int intCostAfter = character.Attributes.Single(a => a.Code == "BOD").KarmaCostToIncrease;
            Assert.Equal((3 + 1) * 10, intCostAfter);
        }
        finally
        {
            if (File.Exists(strSettingsPath))
                File.Delete(strSettingsPath);
        }
    }

    [Fact]
    public void GetCharacterOptions_DoesNotReReadTheSettingsFileWhenItHasNotChanged()
    {
        string strSettingsFileName = Guid.NewGuid() + ".xml";
        string strSettingsPath = Path.Combine(AppContext.BaseDirectory, "settings", strSettingsFileName);
        try
        {
            WriteSettingsFile(strSettingsPath, "5", DateTime.UtcNow);

            CharacterDocument character = LoadXml("<character><attributes>"
                + "<attribute><name>BOD</name><value>3</value><totalvalue>3</totalvalue><metatypemin>1</metatypemin></attribute>"
                + "<attribute><name>AGI</name><value>2</value><totalvalue>2</totalvalue><metatypemin>1</metatypemin></attribute>"
                + "</attributes></character>", strSettingsFileName);

            // Every attribute's KarmaCostToIncrease calls GetCharacterOptions() once - reading
            // both without the file changing in between should only hit the disk once (the cache
            // miss count is internal-only and used just to prove this, not part of the public API).
            _ = character.Attributes.ToList();
            Assert.Equal(1, character.CharacterOptionsCacheMissCountForTesting);
        }
        finally
        {
            if (File.Exists(strSettingsPath))
                File.Delete(strSettingsPath);
        }
    }
}
