using System.IO;
using System.Text;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public class ContactCostTests
{
    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    private static CharacterDocument NewKarmaBuildCharacter(int intCha = 0)
    {
        CharacterDocument character = LoadXml(
            "<character><buildmethod>Karma</buildmethod><attributes>"
            + "<attribute><name>CHA</name><value>" + intCha + "</value><totalvalue>" + intCha + "</totalvalue></attribute>"
            + "</attributes></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions());
        return character;
    }

    [Fact]
    public void ContactPointsUsed_CostsConnectionPlusLoyaltyTimesKarmaContact()
    {
        CharacterDocument character = NewKarmaBuildCharacter();
        character.AddContact("Fixer", "3", "2", blnEnemy: false);

        // Default KarmaContact is 2: (3 + 2) * 2 = 10.
        Assert.Equal(10, character.ContactPointsUsed);
    }

    [Fact]
    public void ContactPointsUsed_FreeContact_CostsNothing()
    {
        CharacterDocument character = NewKarmaBuildCharacter();
        character.AddContact("Fixer", "3", "2", blnEnemy: false);
        int intContactId = character.Contacts[0].ContactId;

        Assert.True(character.SetContactFree(intContactId, true));
        Assert.Equal(0, character.ContactPointsUsed);
    }

    [Fact]
    public void ContactPointsUsed_Enemy_RefundsPointsInsteadOfCosting()
    {
        CharacterDocument character = NewKarmaBuildCharacter();
        character.AddContact("Fixer", "3", "2", blnEnemy: false);
        character.AddContact("Old Rival", "2", "1", blnEnemy: true);

        // Fixer: (3+2)*2 = 10. Enemy: (2+1)*2 = 6 refund. Net = 4.
        Assert.Equal(4, character.ContactPointsUsed);
    }

    [Fact]
    public void ContactPointsUsed_GroupRating_AddsToConnectionAndLoyalty()
    {
        CharacterDocument character = NewKarmaBuildCharacter();
        character.AddContact("Fixer Crew", "3", "2", blnEnemy: false);
        int intContactId = character.Contacts[0].ContactId;

        // Membership +2, Area of Influence +4 -> Group Rating 6.
        Assert.True(character.UpdateContactGroup(intContactId, "Fixers", 2, 4, 0, 0));
        Assert.Equal(6, character.Contacts[0].GroupRating);

        // (3 + 6 + 2) * 2 = 22.
        Assert.Equal(22, character.ContactPointsUsed);
    }

    [Fact]
    public void ContactPointsUsed_FreeContactsHouseRule_DeductsChaTimesMultiplier()
    {
        CharacterDocument character = NewKarmaBuildCharacter(intCha: 3);
        character.SetCharacterOptionsForTesting(new CharacterOptions { FreeContacts = true, FreeContactsMultiplier = 1 });
        character.AddContact("Fixer", "3", "2", blnEnemy: false);

        // Cost 10, free points = CHA(3) * multiplier(1) * KarmaContact(2) = 6 -> net 4.
        Assert.Equal(4, character.ContactPointsUsed);
    }

    [Fact]
    public void ContactPointsUsed_FreeContactsFlatHouseRule_DeductsFlatKarmaAmount()
    {
        CharacterDocument character = NewKarmaBuildCharacter();
        character.SetCharacterOptionsForTesting(new CharacterOptions { FreeContactsFlat = true, FreeContactsFlatNumber = 2 });
        character.AddContact("Fixer", "3", "2", blnEnemy: false);

        // Cost 10, free points = 2 * KarmaContact(2) = 4 -> net 6.
        Assert.Equal(6, character.ContactPointsUsed);
    }

    [Fact]
    public void UpdateContactNotes_PersistsAcrossSaveReload()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddContact("Fixer", "1", "1", blnEnemy: false);
        int intContactId = character.Contacts[0].ContactId;

        Assert.True(character.UpdateContactNotes(intContactId, "Met at the Cellar."));

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Met at the Cellar.", reloaded.Contacts[0].Notes);
    }
}
