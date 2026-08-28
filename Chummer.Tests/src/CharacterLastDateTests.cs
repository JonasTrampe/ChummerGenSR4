using System;
using System.IO;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public partial class CharacterFileServiceTests
{
    [Fact]
    public void EffectiveLastDate_PreservesFutureShadowrunDates()
    {
        DateTime datFuture = new(2080, 6, 14, 12, 0, 0, DateTimeKind.Local);
        CharacterDocument objCharacter = LoadXml($"<character><lastdate>{datFuture:O}</lastdate></character>");

        Assert.Equal(datFuture, objCharacter.EffectiveLastDate);
    }

    [Fact]
    public void EffectiveLastDate_StaleDatePolicyIsConfigurableAndSavePersistsMissingDate()
    {
        bool blnOriginal = GlobalOptions.Instance.UseCurrentDateWhenLastDateIsOlder;
        try
        {
            DateTime datOld = DateTime.Now.AddDays(-3);
            CharacterDocument objCharacter = LoadXml($"<character><lastdate>{datOld:O}</lastdate></character>");

            GlobalOptions.Instance.UseCurrentDateWhenLastDateIsOlder = false;
            Assert.Equal(datOld, objCharacter.EffectiveLastDate);

            GlobalOptions.Instance.UseCurrentDateWhenLastDateIsOlder = true;
            Assert.True(objCharacter.EffectiveLastDate >= DateTime.Now.AddMinutes(-1));

            CharacterDocument objMissing = LoadXml("<character><name>Timestamped</name></character>");
            using var objStream = new MemoryStream();
            new CharacterFileService().Save(objMissing, objStream, "timestamped.chum");
            objStream.Position = 0;
            CharacterDocument objReloaded = new CharacterFileService().Load(objStream, "timestamped.chum");
            Assert.NotEqual(DateTime.MinValue, objReloaded.LastDate);
            Assert.Contains("lastdate", objReloaded.Document.OuterXml, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            GlobalOptions.Instance.UseCurrentDateWhenLastDateIsOlder = blnOriginal;
        }
    }
}
