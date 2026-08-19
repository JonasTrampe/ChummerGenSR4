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
    public void Contacts_And_Enemies_AreSplitByType()
    {
        CharacterDocument character = LoadFixture();

        Assert.Single(character.Contacts);
        Assert.Equal("Schieber: Stef", character.Contacts[0].Name);

        Assert.Single(character.Enemies);
        Assert.Equal("Lonestar Sergeant", character.Enemies[0].Name);
    }

    [Fact]
    public void ContactCreationBudget_ChargesEveryContactMutationAndRollsBackUnaffordableEdits()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>10</startingbuildpoints><bp>5</bp></character>");

        Assert.True(creation.AddContact("Fixer", "1", "1", blnEnemy: false));
        int intContactId = Assert.Single(creation.Contacts).ContactId;
        Assert.Equal("3", creation.Bp);

        Assert.True(creation.UpdateContact(intContactId, "Fixer", "4", "1"));
        Assert.Equal("0", creation.Bp);
        Assert.False(creation.UpdateContact(intContactId, "Fixer", "4", "2"));
        Assert.Equal("1", Assert.Single(creation.Contacts).Loyalty);
        Assert.True(creation.SetContactFree(intContactId, true));
        Assert.Equal("5", creation.Bp);
        Assert.True(creation.SetContactFree(intContactId, false));
        Assert.Equal("0", creation.Bp);

        Assert.True(creation.AddContact("Rival", "1", "1", blnEnemy: true));
        int intEnemyId = Assert.Single(creation.Enemies).ContactId;
        Assert.Equal("2", creation.Bp);
        Assert.True(creation.RemoveContact(intEnemyId));
        Assert.Equal("0", creation.Bp);
        Assert.True(creation.RemoveContact(intContactId));
        Assert.Equal("5", creation.Bp);
    }

    [Fact]
    public void ContactCreationBudget_RejectsUnaffordableGroupModifiersWithoutMutating()
    {
        CharacterDocument creation = LoadXml("<character><created>False</created><buildmethod>BP</buildmethod><startingbuildpoints>10</startingbuildpoints><bp>2</bp></character>");
        Assert.True(creation.AddContact("Fixer", "1", "1", blnEnemy: false));
        int intContactId = Assert.Single(creation.Contacts).ContactId;

        Assert.False(creation.UpdateContactGroup(intContactId, "Fixers", 1, 0, 0, 0));
        CharacterContactData contact = Assert.Single(creation.Contacts);
        Assert.Equal(0, contact.GroupRating);
        Assert.Equal("0", creation.Bp);
    }

    [Fact]
    public void PetCharacterLink_ClearedByUpdatingWithEmptyPaths()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddPet("Sparky");
        int intPetId = Assert.Single(character.Pets).ContactId;
        character.UpdateContactFile(intPetId, "/characters/sparky.chum", "../characters/sparky.chum");

        Assert.True(character.UpdateContactFile(intPetId, string.Empty, string.Empty));

        CharacterContactData pet = Assert.Single(character.Pets);
        Assert.Equal(string.Empty, pet.FileName);
        Assert.Equal(string.Empty, pet.RelativeFileName);
    }

}
