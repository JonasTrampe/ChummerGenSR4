using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public partial class CharacterFileServiceTests
{
    [Fact]
    public void CharacterDiff_ExposesLocalAndServerValuesForConflictReview()
    {
        CharacterDocument local = LoadXml("<character><alias>Local</alias></character>");
        CharacterDocument server = LoadXml("<character><alias>Server</alias></character>");

        CharacterDiffEntry entry = Assert.Single(CharacterDiff.Compare(local, server).Entries,
            objEntry => objEntry.Name == "Alias");
        Assert.Equal("Local", entry.LocalValue);
        Assert.Equal("Server", entry.ServerValue);
    }

    [Fact]
    public void CharacterMerge_CombinesIndependentResourceAndItemChanges()
    {
        const string strBase = "<character><karma>100</karma><nuyen>1000</nuyen><skills>"
            + "<skill><guid>base</guid><name>Base</name><rating>1</rating></skill></skills></character>";
        CharacterDocument objBase = LoadXml(strBase);
        CharacterDocument objLocal = LoadXml(strBase.Replace("<karma>100", "<karma>80")
            .Replace("</skills>", "<skill><guid>local</guid><name>Pistols</name><rating>2</rating></skill></skills>"));
        CharacterDocument objServer = LoadXml(strBase.Replace("<nuyen>1000", "<nuyen>700")
            .Replace("</skills>", "<skill><guid>server</guid><name>Etiquette</name><rating>2</rating></skill></skills>"));

        CharacterMergeResult objResult = CharacterMergeService.TryMerge(objBase, objLocal, objServer);
        Assert.True(objResult.CanMerge);
        Assert.Equal("80", objResult.MergedCharacter!.Karma);
        Assert.Equal("700", objResult.MergedCharacter.Nuyen);
        Assert.Contains(objResult.MergedCharacter.Skills, objSkill => objSkill.Name == "Pistols");
        Assert.Contains(objResult.MergedCharacter.Skills, objSkill => objSkill.Name == "Etiquette");
    }

    [Fact]
    public void RemoveCustomImprovement_RemovesOnlyMatchingCustomSourcedEntries()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name><improvements>"
            + "<improvement><unique>gmbonus</unique><improvedname>BOD</improvedname><sourcename>abc-123</sourcename>"
            + "<min>0</min><max>0</max><aug>1</aug><augmax>0</augmax><val>0</val><rating>1</rating>"
            + "<improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource></improvement>"
            + "<improvement><unique>wiredreflexes</unique><improvedname>REA</improvedname><sourcename>Wired Reflexes</sourcename>"
            + "<min>0</min><max>0</max><aug>2</aug><augmax>0</augmax><val>0</val><rating>1</rating>"
            + "<improvementttype>Attribute</improvementttype><improvementsource>Cyberware</improvementsource></improvement>"
            + "</improvements></character>");
        Assert.Equal(2, character.Improvements.Count);

        // A non-Custom sourcename never matches, even if it happens to collide.
        Assert.False(character.RemoveCustomImprovement("Wired Reflexes"));
        Assert.Equal(2, character.Improvements.Count);

        Assert.True(character.RemoveCustomImprovement("abc-123"));
        Improvement remaining = Assert.Single(character.Improvements);
        Assert.Equal("Wired Reflexes", remaining.SourceName);

        Assert.False(character.RemoveCustomImprovement("abc-123"));
    }

    [Fact]
    public void ReplaceCustomImprovement_RetainsItsGroupAndDoesNotDeleteOnInvalidInput()
    {
        CharacterDocument character = LoadFixture();
        Assert.True(character.AddCustomImprovement(CharacterDocument.CustomImprovementType.Attribute,
            "GM Bonus", intVal: 2, strSelect: "BOD"));
        Assert.True(character.SetCustomImprovementGroup("GM Bonus", "House rules"));

        Assert.True(character.ReplaceCustomImprovement("GM Bonus", CharacterDocument.CustomImprovementType.Attribute,
            "Updated GM Bonus", intVal: 3, strSelect: "BOD"));
        Improvement replacement = Assert.Single(character.Improvements, obj => obj.SourceName == "Updated GM Bonus");
        Assert.Equal("House rules", replacement.CustomGroup);

        Assert.False(character.ReplaceCustomImprovement("Updated GM Bonus",
            CharacterDocument.CustomImprovementType.Attribute, "Broken", intVal: 1));
        Assert.Contains(character.Improvements, obj => obj.SourceName == "Updated GM Bonus");
    }

    [Fact]
    public void Condition_ComputesPhysicalAndStunTrackFromImprovements()
    {
        CharacterDocument character = LoadFixture();

        // BOD totalvalue 4 -> ceil(4/2) + 8 = 10, plus the fixture's +1 PhysicalCM improvement.
        Assert.Equal(11, character.Condition.PhysicalCm.Value);
        Assert.Contains("Wired Reflexes", character.Condition.PhysicalCm.Tooltip);
        // WIL totalvalue 3 -> ceil(3/2) + 8 = 10, no StunCM improvements in the fixture.
        Assert.Equal(10, character.Condition.StunCm.Value);
    }

    [Fact]
    public void Movement_AppliesLandSwimAndFlyImprovements()
    {
        CharacterDocument character = LoadXml("<character><movement>10/25,Swim 4/8,Fly 20/40</movement><improvements>"
            + ImprovementXml("MovementPercent", "10") + ImprovementXml("SwimPercent", "25")
            + ImprovementXml("FlyPercent", "50") + "</improvements></character>");

        Assert.Equal("11/27", character.WalkMovement);
        Assert.Equal("5/10", character.SwimMovement);
        Assert.Equal("30/60", character.FlyMovement);
    }

    [Fact]
    public void Movement_FlySpeedCanUseAMultipleOfWalkMovement()
    {
        CharacterDocument character = LoadXml("<character><movement>8/20</movement><improvements>"
            + ImprovementXml("FlySpeed", "-2") + "</improvements></character>");

        Assert.Equal("16/40", character.FlyMovement);
    }

    private static Improvement BuildImprovement(string strUnique, int intValue, string strImprovedName = "")
    {
        var doc = new XmlDocument();
        XmlElement objNode = doc.CreateElement("improvement");
        void Add(string strTag, string strText)
        {
            XmlElement objChild = doc.CreateElement(strTag);
            objChild.InnerText = strText;
            objNode.AppendChild(objChild);
        }
        Add("improvementttype", "Smartlink");
        Add("improvedname", strImprovedName);
        Add("sourcename", "Test");
        Add("min", "0");
        Add("max", "0");
        Add("aug", "0");
        Add("augmax", "0");
        Add("val", intValue.ToString());
        Add("rating", "1");
        Add("unique", strUnique);
        Add("improvementsource", "Quality");
        Add("addtorating", "False");
        Add("enabled", "True");
        Add("custom", "False");
        return Improvement.Load(objNode);
    }

    [Fact]
    public void ImprovementManagerValueOf_Precedence1_SumsOnlyPrecedence1EntriesIgnoringEverythingElse()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("", 5),
            BuildImprovement("somegroup", 3),
            BuildImprovement("precedence1", 2),
            BuildImprovement("precedence1", 4),
        };

        Assert.Equal(6, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }

    [Fact]
    public void ImprovementManagerValueOf_Precedence0_KeepsOnlyTheHighestPrecedence0Entry()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("", 5),
            BuildImprovement("precedence0", 2),
            BuildImprovement("precedence0", 7),
        };

        Assert.Equal(7, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }

    [Fact]
    public void ImprovementManagerValueOf_Precedence1BeatsPrecedence0_WhenBothArePresent()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("precedence0", 100),
            BuildImprovement("precedence1", 1),
            BuildImprovement("precedence1", 2),
        };

        Assert.Equal(3, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }

    [Fact]
    public void ImprovementManagerValueOf_NoPrecedenceEntries_UsesTheNormalUniqueNameMaxDedupSum()
    {
        var lstImprovements = new[]
        {
            BuildImprovement("", 5),
            BuildImprovement("somegroup", 3),
            BuildImprovement("somegroup", 7),
        };

        // 5 (no UniqueName, always summed) + 7 (max of the "somegroup" pair).
        Assert.Equal(12, ImprovementManager.ValueOf(lstImprovements, ImprovementType.Smartlink));
    }
}
