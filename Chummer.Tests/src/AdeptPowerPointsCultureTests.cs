using System.IO;
using System.Text;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

// Shares the "GlobalOptionsState" collection with PdfLinkServiceTests/LanguageManagerCultureTests
// so tests that mutate the GlobalOptions.Instance singleton (here: Language, which now drives
// AdeptPowerPoints' tooltip decimal formatting) never run concurrently with each other.
[Collection("GlobalOptionsState")]
public sealed class AdeptPowerPointsCultureTests
{
    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    [Fact]
    public void AdeptPowerPoints_TooltipUsesGermanCommaDecimalWhenLanguageIsGerman()
    {
        var character = LoadXml("<character><adept>True</adept><magician>False</magician><attributes>"
            + "<attribute><name>MAG</name><value>4</value><totalvalue>4</totalvalue></attribute>"
            + "</attributes><powers>"
            + "<power><name>Killing Hands</name><rating>1</rating><pointsperlevel>0.5</pointsperlevel></power>"
            + "</powers></character>");

        string strOriginalLanguage = GlobalOptions.Instance.Language;
        try
        {
            GlobalOptions.Instance.Language = "de";
            Assert.Contains("Verbraucht: 0,5", character.AdeptPowerPoints.Tooltip);

            GlobalOptions.Instance.Language = "en-us";
            Assert.Contains("Verbraucht: 0.5", character.AdeptPowerPoints.Tooltip);
        }
        finally
        {
            GlobalOptions.Instance.Language = strOriginalLanguage;
        }
    }
}
