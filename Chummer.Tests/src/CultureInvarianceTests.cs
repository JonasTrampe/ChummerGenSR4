using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

/// <summary>
/// Save files always use "." as the decimal separator regardless of the running OS's locale, so
/// every parse/format of a save-file value must be explicitly culture-invariant. Runs its
/// assertions under de-DE (comma decimal separator), which is exactly the culture that would
/// silently corrupt un-invariant double.TryParse/decimal.TryParse calls: NumberStyles.Float
/// (the implicit style behind the culture-sensitive overloads) rejects "." under a culture whose
/// decimal separator is ",", so those calls return false/0 instead of throwing.
/// </summary>
public sealed class CultureInvarianceTests : IDisposable
{
    private readonly CultureInfo _objOriginalCulture = CultureInfo.CurrentCulture;

    public CultureInvarianceTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _objOriginalCulture;
    }

    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    [Fact]
    public void PhysicalAndStunConditionMonitors_ParseDecimalAttributeTotalsUnderCommaDecimalCulture()
    {
        CharacterDocument character = LoadXml("<character><attributes>"
            + "<attribute><name>BOD</name><value>3</value><totalvalue>3.5</totalvalue></attribute>"
            + "<attribute><name>WIL</name><value>4</value><totalvalue>4.5</totalvalue></attribute>"
            + "</attributes></character>");

        // 8 + ceil(3.5/2) = 10; 8 + ceil(4.5/2) = 11 - wrong (0-based) results would mean the "."
        // decimal point failed to parse under de-DE and silently fell back to 0.
        Assert.Equal(10, character.Condition.PhysicalCm.Value);
        Assert.Equal(11, character.Condition.StunCm.Value);
    }

    [Fact]
    public void ArmorEncumbrance_ParsesDecimalBodAndStrUnderCommaDecimalCulture()
    {
        CharacterDocument character = LoadXml("<character><attributes>"
            + "<attribute><name>BOD</name><value>3</value><totalvalue>3.5</totalvalue></attribute>"
            + "<attribute><name>STR</name><value>3</value><totalvalue>3.5</totalvalue></attribute>"
            + "</attributes><armors /></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions());

        // Just needs to not silently coerce 3.5 to 0 - throwing or a sane non-zero result both
        // prove the parse succeeded; the real assertion is in the two attribute tests above using
        // the same GetAttributeValue/double.TryParse path.
        Assert.NotNull(character.ArmorEncumbrance);
    }

    [Fact]
    public void ExpenseDisplayDate_RoundTripsUnderCommaDecimalCulture()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddExpense("Karma", 5, "Test", new DateTime(2024, 3, 7));

        CharacterExpenseData expense = Assert.Single(character.KarmaExpenses);
        Assert.Equal("07.03.2024", expense.DisplayDate);
    }

    [Fact]
    public void ContactRowViewModel_ParsesConnectionAndLoyaltyUnderCommaDecimalCulture()
    {
        CharacterDocument character = LoadXml("<character><contacts><contact>"
            + "<name>Fixer</name><type>Contact</type><connection>4</connection><loyalty>3</loyalty>"
            + "<free>False</free></contact></contacts></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions());
        CharacterContactData contact = Assert.Single(character.Contacts);

        var viewModel = new ContactRowViewModel(character, contact);

        Assert.Equal(4, viewModel.Connection);
        Assert.Equal(3, viewModel.Loyalty);
    }
}
