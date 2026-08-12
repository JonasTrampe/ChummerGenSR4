using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v0.1.501", 0, 1, 501)]
    [InlineData("0.1.501", 0, 1, 501)]
    [InlineData("v0.1.501-beta+abc123", 0, 1, 501)]
    [InlineData("V2.3.4", 2, 3, 4)]
    public void ParseReleaseVersion_StripsLeadingVAndPreReleaseSuffix(string strInput, int intMajor, int intMinor,
        int intBuild)
    {
        var objVersion = UpdateChecker.ParseReleaseVersion(strInput);

        Assert.NotNull(objVersion);
        Assert.Equal(intMajor, objVersion.Major);
        Assert.Equal(intMinor, objVersion.Minor);
        Assert.Equal(intBuild, objVersion.Build);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    public void ParseReleaseVersion_ReturnsNullForUnparsableInput(string strInput)
    {
        Assert.Null(UpdateChecker.ParseReleaseVersion(strInput));
    }

    [Fact]
    public void ParseReleaseJson_NewerReleaseAvailable_ReturnsResult()
    {
        string strJson = """
        {
            "tag_name": "v0.1.501",
            "html_url": "https://github.com/JonasTrampe/ChummerGenSR4/releases/tag/v0.1.501",
            "body": "Release notes here."
        }
        """;

        var result = UpdateChecker.ParseReleaseJson(strJson, "0.1.496");

        Assert.NotNull(result);
        Assert.Equal("v0.1.501", result.LatestVersion);
        Assert.Equal("Release notes here.", result.ReleaseNotes);
        Assert.Equal("https://github.com/JonasTrampe/ChummerGenSR4/releases/tag/v0.1.501", result.ReleaseUrl);
    }

    [Fact]
    public void ParseReleaseJson_AlreadyUpToDate_ReturnsNull()
    {
        string strJson = """{"tag_name": "v0.1.496", "html_url": "https://x", "body": ""}""";

        Assert.Null(UpdateChecker.ParseReleaseJson(strJson, "0.1.496"));
    }

    [Fact]
    public void ParseReleaseJson_OlderTag_ReturnsNull()
    {
        string strJson = """{"tag_name": "v0.1.400", "html_url": "https://x", "body": ""}""";

        Assert.Null(UpdateChecker.ParseReleaseJson(strJson, "0.1.496"));
    }

    [Fact]
    public void ParseReleaseJson_MissingTagName_ReturnsNull()
    {
        Assert.Null(UpdateChecker.ParseReleaseJson("""{"html_url": "https://x"}""", "0.1.496"));
    }

    [Fact]
    public void ParseReleaseJson_MalformedJson_ReturnsNull()
    {
        Assert.Null(UpdateChecker.ParseReleaseJson("not json at all", "0.1.496"));
    }

    [Fact]
    public void ParseReleaseJson_MissingHtmlUrl_FallsBackToReleasesLatestPage()
    {
        string strJson = """{"tag_name": "v0.1.501", "body": ""}""";

        var result = UpdateChecker.ParseReleaseJson(strJson, "0.1.496");

        Assert.NotNull(result);
        Assert.Equal("https://github.com/JonasTrampe/ChummerGenSR4/releases/latest", result.ReleaseUrl);
    }
}
