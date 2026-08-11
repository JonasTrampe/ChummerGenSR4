using System;
using System.IO;
using System.Text;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class ContactRowViewModelTests
{
    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    [Fact]
    public void LinkedMetatype_PeeksTheLinkedCompanionFilesMetatypeAndMetavariant()
    {
        string strTempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".chum");
        try
        {
            File.WriteAllText(strTempFile, "<character><metatype>Elf</metatype><metavariant>Wakyambi</metavariant></character>");

            CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
            character.AddPet("Fido");
            int intContactId = character.Pets[0].ContactId;
            Assert.True(character.UpdateContactFile(intContactId, strTempFile, string.Empty));

            var row = new ContactRowViewModel(character, character.Pets[0]);

            Assert.Equal("Elf (Wakyambi)", row.LinkedMetatype);
            Assert.True(row.HasLinkedCharacter);
        }
        finally
        {
            File.Delete(strTempFile);
        }
    }

    [Fact]
    public void LinkedMetatype_EmptyWhenNoFileIsLinked()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddPet("Fido");

        var row = new ContactRowViewModel(character, character.Pets[0]);

        Assert.Equal(string.Empty, row.LinkedMetatype);
        Assert.False(row.HasLinkedCharacter);
    }

    [Fact]
    public void LinkedMetatype_EmptyWhenTheLinkedFileDoesNotExist()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        character.AddPet("Fido");
        int intContactId = character.Pets[0].ContactId;
        Assert.True(character.UpdateContactFile(intContactId, "/does/not/exist.chum", string.Empty));

        var row = new ContactRowViewModel(character, character.Pets[0]);

        Assert.Equal(string.Empty, row.LinkedMetatype);
        Assert.True(row.HasLinkedCharacter);
    }
}
