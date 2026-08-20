using System.Globalization;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public sealed class LanguageManagerOsDetectionTests
{
    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("fr-FR", "fr")]
    [InlineData("ja-JP", "jp")]
    [InlineData("en-US", "en-us")]
    [InlineData("es-ES", "en-us")]
    public void DetectOsLanguageCode_MapsOsUiCultureToAnAvailableLanguageFile(string strOsCulture, string strExpectedCode)
    {
        // CultureInfo.CurrentUICulture is execution-context (async-local), not a true process-wide
        // static, so concurrently running tests don't observe each other's changes here the way
        // they would through GlobalOptions.Instance - no shared-collection isolation needed.
        CultureInfo strOriginalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(strOsCulture);
            Assert.Equal(strExpectedCode, LanguageManager.DetectOsLanguageCode());
        }
        finally
        {
            CultureInfo.CurrentUICulture = strOriginalUiCulture;
        }
    }
}
