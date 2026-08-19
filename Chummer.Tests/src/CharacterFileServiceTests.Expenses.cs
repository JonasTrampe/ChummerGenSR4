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
    private const string ExpenseFixtureXml = "<character><expenses><expense><guid>11111111-1111-1111-1111-111111111111</guid>"
        + "<date>2020-01-01</date><amount>5</amount><reason>Test expense</reason><type>Karma</type><refund>False</refund>"
        + "</expense></expenses></character>";

    [Fact]
    public void CharacterSheetExporter_PrintExpensesOff_OmitsExpenseEntries()
    {
        CharacterDocument character = LoadXml(ExpenseFixtureXml);
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintExpenses = false });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        Assert.Empty(xml.SelectNodes("//expenses/expense")!);
    }

    [Fact]
    public void CharacterSheetExporter_PrintExpensesOn_IncludesExpenseEntries()
    {
        CharacterDocument character = LoadXml(ExpenseFixtureXml);
        character.SetCharacterOptionsForTesting(new CharacterOptions { PrintExpenses = true });

        XmlDocument xml = CharacterSheetExporter.BuildExportXml(character);
        Assert.NotEmpty(xml.SelectNodes("//expenses/expense")!);
    }

    [Fact]
    public void AddExpense_MutatesCharacterAndPersistsSignedHistory()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 4, "Session reward", new DateTime(2026, 7, 22));
        character.AddExpense("Nuyen", -250, "New fake SIN", new DateTime(2026, 7, 23));

        Assert.Equal(4, character.CareerKarma);
        Assert.Equal(0, character.CareerNuyen);
        Assert.Equal("-250", character.NuyenExpenses[0].Amount);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");

        Assert.Equal("Session reward", reloaded.KarmaExpenses[0].Reason);
        Assert.Equal("New fake SIN", reloaded.NuyenExpenses[0].Reason);
    }

    [Fact]
    public void UpdateExpense_ChangesReasonAmountAndDateForTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 4, "Session reward", new DateTime(2026, 7, 22));
        character.AddExpense("Karma", 2, "Another entry", new DateTime(2026, 7, 23));
        string strGuid = character.KarmaExpenses[0].Guid;
        Assert.NotEmpty(strGuid);

        Assert.True(character.UpdateExpense(strGuid, "Corrected reward", 6, new DateTime(2026, 7, 24)));
        Assert.False(character.UpdateExpense("missing-guid", "x", 1, DateTime.Now));

        CharacterExpenseData updated = character.KarmaExpenses.Single(e => e.Guid == strGuid);
        Assert.Equal("Corrected reward", updated.Reason);
        Assert.Equal("6", updated.Amount);
        Assert.Equal("24.07.2026", updated.DisplayDate);
        // The other entry is untouched.
        Assert.Equal("Another entry", character.KarmaExpenses.Single(e => e.Guid != strGuid).Reason);
        // Updated entry (6) + the other, untouched entry (2) = 8.
        Assert.Equal(8, character.CareerKarma);
    }

    [Fact]
    public void RemoveExpense_RemovesOnlyTheMatchingEntry()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 4, "Session reward", new DateTime(2026, 7, 22));
        character.AddExpense("Karma", 2, "Another entry", new DateTime(2026, 7, 23));
        string strGuid = character.KarmaExpenses[0].Guid;

        Assert.True(character.RemoveExpense(strGuid));
        Assert.False(character.RemoveExpense(strGuid));
        CharacterExpenseData remaining = Assert.Single(character.KarmaExpenses);
        Assert.Equal("Another entry", remaining.Reason);
    }

    [Fact]
    public void KarmaAndNuyenExpenses_AreSplitByType()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.KarmaExpenses);
        Assert.Equal("5", character.KarmaExpenses[0].Amount);

        Assert.Single(character.NuyenExpenses);
        Assert.Equal("-500", character.NuyenExpenses[0].Amount);
    }

    private static CharacterDocument LoadCreationFirearmsGroupAtRating2()
        => LoadXml("<character><created>False</created><buildmethod>Karma</buildmethod>"
            + "<startingbuildpoints>100</startingbuildpoints><karma>100</karma>"
            + "<skillgroups><skillgroup><name>Firearms</name><rating>2</rating><broken>False</broken></skillgroup></skillgroups>"
            + "<skills><skill><name>Pistolen</name><attribute>AGI</attribute><skillcategory>Combat Active</skillcategory>"
            + "<skillgroup>Firearms</skillgroup><grouped>True</grouped><rating>2</rating><knowledge>False</knowledge>"
            + "<exotic>False</exotic><spec /><allowdelete>True</allowdelete></skill>"
            + "<skill><name>Automatik</name><attribute>AGI</attribute><skillcategory>Combat Active</skillcategory>"
            + "<skillgroup>Firearms</skillgroup><grouped>True</grouped><rating>2</rating><knowledge>False</knowledge>"
            + "<exotic>False</exotic><spec /><allowdelete>True</allowdelete></skill></skills></character>");

    [Fact]
    public void CareerKarmaAndNuyen_SumOnlyPositiveNonRefundEntries()
    {
        CharacterDocument character = LoadFixture();

        // The fixture's one Karma entry is +5 (earned) -> CareerKarma 5.
        Assert.Equal(5, character.CareerKarma);
        // The fixture's one Nuyen entry is -500 (spent, not earned) -> CareerNuyen 0.
        Assert.Equal(0, character.CareerNuyen);
    }

    [Fact]
    public void ConfirmKarmaExpenseEnabled_UsesCharacterSettingsProfile()
    {
        CharacterDocument character = LoadXml("<character />");
        character.SetCharacterOptionsForTesting(new CharacterOptions { ConfirmKarmaExpense = false });
        Assert.False(character.ConfirmKarmaExpenseEnabled);

        character.SetCharacterOptionsForTesting(new CharacterOptions { ConfirmKarmaExpense = true });
        Assert.True(character.ConfirmKarmaExpenseEnabled);
    }

}
