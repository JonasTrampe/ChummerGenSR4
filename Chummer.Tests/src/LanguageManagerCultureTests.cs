using System.Globalization;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

[Collection("GlobalOptionsState")]
public sealed class LanguageManagerCultureTests
{
    [Theory]
    [InlineData("de", "de-DE")]
    [InlineData("fr", "fr-FR")]
    [InlineData("jp", "ja-JP")]
    [InlineData("en-us", "en-US")]
    public void CurrentNumberFormatCulture_MapsCorePageLanguageCodeToRealCulture(string strLanguage, string strExpectedCultureName)
    {
        string strOriginalLanguage = GlobalOptions.Instance.Language;
        try
        {
            GlobalOptions.Instance.Language = strLanguage;
            Assert.Equal(strExpectedCultureName, LanguageManager.CurrentNumberFormatCulture.Name);
        }
        finally
        {
            GlobalOptions.Instance.Language = strOriginalLanguage;
        }
    }

    [Fact]
    public void CurrentNumberFormatCulture_FallsBackToInvariantForUnrecognizedLanguageCode()
    {
        string strOriginalLanguage = GlobalOptions.Instance.Language;
        try
        {
            GlobalOptions.Instance.Language = "not-a-real-code";
            Assert.Equal(CultureInfo.InvariantCulture, LanguageManager.CurrentNumberFormatCulture);
        }
        finally
        {
            GlobalOptions.Instance.Language = strOriginalLanguage;
        }
    }
}
