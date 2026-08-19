using System.Collections.Generic;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests;

// GlobalOptions.Instance is a mutable singleton, so every test here restores it in a finally
// block - these can't run in parallel with anything else that reads PdfAppPath/SourcebookInfo.
[Collection("GlobalOptionsState")]
public class PdfLinkServiceTests
{
    [Fact]
    public void OpenPdf_NoAppConfigured_ReturnsFalse()
    {
        string strOriginalPath = GlobalOptions.Instance.PdfAppPath;
        try
        {
            GlobalOptions.Instance.PdfAppPath = string.Empty;
            Assert.False(PdfLinkService.CanOpen);
            Assert.False(PdfLinkService.OpenPdf("SR4 118"));
        }
        finally
        {
            GlobalOptions.Instance.PdfAppPath = strOriginalPath;
        }
    }

    [Fact]
    public void OpenPdf_AppConfiguredButNoMatchingSourcebookPath_ReturnsFalse()
    {
        string strOriginalPath = GlobalOptions.Instance.PdfAppPath;
        List<SourcebookInfo> lstOriginalInfo = GlobalOptions.Instance.SourcebookInfo;
        try
        {
            GlobalOptions.Instance.PdfAppPath = "/usr/bin/true";
            GlobalOptions.Instance.SourcebookInfo = new List<SourcebookInfo>();

            Assert.True(PdfLinkService.CanOpen);
            Assert.False(PdfLinkService.OpenPdf("SR4 118"));
        }
        finally
        {
            GlobalOptions.Instance.PdfAppPath = strOriginalPath;
            GlobalOptions.Instance.SourcebookInfo = lstOriginalInfo;
        }
    }

    [Theory]
    [InlineData("SR4")]
    [InlineData("SR4 0")]
    [InlineData("SR4 -3")]
    [InlineData("SR4 notanumber")]
    public void OpenPdf_MalformedSourceString_ReturnsFalse(string strSource)
    {
        string strOriginalPath = GlobalOptions.Instance.PdfAppPath;
        List<SourcebookInfo> lstOriginalInfo = GlobalOptions.Instance.SourcebookInfo;
        try
        {
            GlobalOptions.Instance.PdfAppPath = "/usr/bin/true";
            GlobalOptions.Instance.SourcebookInfo = new List<SourcebookInfo>
            {
                new SourcebookInfo { Code = "SR4", Path = "/usr/bin/true", Offset = 0 }
            };

            Assert.False(PdfLinkService.OpenPdf(strSource));
        }
        finally
        {
            GlobalOptions.Instance.PdfAppPath = strOriginalPath;
            GlobalOptions.Instance.SourcebookInfo = lstOriginalInfo;
        }
    }

    [Fact]
    public void OpenPdf_ValidConfiguration_LaunchesTheConfiguredAppAndReturnsTrue()
    {
        string strOriginalPath = GlobalOptions.Instance.PdfAppPath;
        List<SourcebookInfo> lstOriginalInfo = GlobalOptions.Instance.SourcebookInfo;
        try
        {
            // /usr/bin/true always exists and exits immediately - a harmless stand-in for a real
            // PDF reader so this test can assert Process.Start actually got called without
            // depending on one being installed.
            GlobalOptions.Instance.PdfAppPath = "/usr/bin/true";
            GlobalOptions.Instance.SourcebookInfo = new List<SourcebookInfo>
            {
                new SourcebookInfo { Code = "SR4", Path = "/tmp/fake.pdf", Offset = 5 }
            };

            Assert.True(PdfLinkService.OpenPdf("SR4 118"));
        }
        finally
        {
            GlobalOptions.Instance.PdfAppPath = strOriginalPath;
            GlobalOptions.Instance.SourcebookInfo = lstOriginalInfo;
        }
    }
}
