using System;
using System.Diagnostics;
using System.Globalization;

namespace Chummer.Core
{
    /// <summary>
    ///     Opens a sourcebook PDF at a specific page, ported from clsCommon.cs's OpenPDF. Requires
    ///     the user to have configured a PDF reader (GlobalOptions.PdfAppPath) and, per book, a file
    ///     path (GlobalOptions.SourcebookInfo's Path/Offset) via the Options dialog - there's no
    ///     bundled PDF content, same as legacy.
    /// </summary>
    public static class PdfLinkService
    {
        /// <summary>True if a PDF app and at least one sourcebook path are configured, so a host UI
        /// can decide whether to render a "Quelle:" label as clickable at all.</summary>
        public static bool CanOpen => !string.IsNullOrEmpty(GlobalOptions.Instance.PdfAppPath);

        /// <summary>Parses a "&lt;book code&gt; &lt;page&gt;" string (e.g. "SR4 118", the same
        /// shape every Source/Page pair in this port is already formatted as) and launches the
        /// configured PDF reader at that page. No-ops (returns false) if the app path, the book's
        /// path, or the page number aren't all valid - mirrors legacy's silent early-outs rather
        /// than surfacing an error for what's fundamentally an optional convenience feature.</summary>
        public static bool OpenPdf(string strSourcePage)
        {
            if (string.IsNullOrEmpty(GlobalOptions.Instance.PdfAppPath))
                return false;

            string[] astrParts = (strSourcePage ?? string.Empty).Split(' ');
            if (astrParts.Length < 2)
                return false;

            string strBook = astrParts[0];
            if (!int.TryParse(astrParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int intPage)
                || intPage < 1)
                return false;

            SourcebookInfo? objInfo = GlobalOptions.Instance.SourcebookInfo.Find(i => i.Code == strBook);
            if (objInfo == null || string.IsNullOrEmpty(objInfo.Path))
                return false;

            intPage += objInfo.Offset;

            string strParams = GlobalOptions.Instance.PdfArgumentStyle switch
            {
                "SumatraPDF" => " -page " + intPage + " \"" + objInfo.Path + "\"",
                _ => " /n /A \"page=" + intPage + "\" \"" + objInfo.Path + "\""
            };

            try
            {
                Process.Start(new ProcessStartInfo(GlobalOptions.Instance.PdfAppPath, strParams)
                {
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception)
            {
                // The configured PDF app path might be stale (moved/uninstalled reader) - don't
                // crash the host UI over what's an optional convenience action.
                return false;
            }
        }
    }
}
