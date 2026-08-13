using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

public partial class SheetPreviewDialog : Window
{
    private CharacterDocument? _character;

    // Raw XHTML from CharacterSheetExporter.RenderSheet - kept around for a future "export" action
    // (e.g. saving straight to a .html file) even though the TextBox below only shows the
    // tag-stripped SheetText, since Avalonia has no first-party cross-platform HTML renderer.
    public string SheetHtml { get; private set; } = string.Empty;
    public string SheetText { get; private set; } = string.Empty;

    public SheetPreviewDialog()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public SheetPreviewDialog(CharacterDocument? character)
        : this()
    {
        _character = character;

        var selector = this.FindControl<ComboBox>("SheetSelector")!;
        string strSheetDir = Path.Combine(AppContext.BaseDirectory, "data", "sheets");
        var lstSheets = Directory.Exists(strSheetDir)
            ? Directory.GetFiles(strSheetDir, "*.xsl")
                .Concat(Directory.GetFiles(strSheetDir, "*.xslt"))
                .Where(f => !Path.GetFileName(f).Contains("Base", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new System.Collections.Generic.List<string?>();

        selector.ItemsSource = lstSheets;
        selector.SelectedItem = lstSheets.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f), GlobalOptions.Instance.DefaultCharacterSheet,
                StringComparison.OrdinalIgnoreCase)) ?? lstSheets.FirstOrDefault(f => f == "Text-Only.xsl")
            ?? lstSheets.FirstOrDefault();

        RenderSelectedSheet();
    }

    private void OnSheetSelectionChanged(object? sender, SelectionChangedEventArgs e) => RenderSelectedSheet();

    private void RenderSelectedSheet()
    {
        var selector = this.FindControl<ComboBox>("SheetSelector")!;
        string? strSheetName = selector.SelectedItem as string;

        if (_character == null)
        {
            SheetText = App.LanguageCatalog.GetString("UI_NoCharacterOpenMessage");
        }
        else if (strSheetName == null)
        {
            SheetText = App.LanguageCatalog.GetString("UI_NoCharacterSheetFoundMessage");
        }
        else
        {
            try
            {
                SheetHtml = CharacterSheetExporter.RenderSheet(_character, strSheetName);
                SheetText = HtmlToPlainText(SheetHtml);
            }
            catch (Exception ex)
            {
                SheetText = App.LanguageCatalog.GetString("UI_ErrorGeneratingSheetPrefix") + ex.Message;
            }
        }

        this.FindControl<TextBox>("SheetHtmlPanel")!.Text = SheetText;
    }

    // Text-Only.xsl is meant to read like plain text once a browser renders its <br/> tags as line
    // breaks - since the TextBox here can't render HTML, do that translation by hand instead of
    // showing raw markup.
    private static string HtmlToPlainText(string strHtml)
    {
        // Strip <style>/<script> blocks including their contents first - the generic tag-strip
        // below only removes the tags themselves, which would otherwise leave raw CSS/JS visible
        // as text (every shipped sheet embeds both, not just the fancier ones).
        string strText = Regex.Replace(strHtml, "<style[^>]*>.*?</style>", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        strText = Regex.Replace(strText, "<script[^>]*>.*?</script>", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        strText = Regex.Replace(strText, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        strText = Regex.Replace(strText, "</(p|div|tr|table|h[1-6])>", "\n", RegexOptions.IgnoreCase);
        strText = Regex.Replace(strText, "<td[^>]*>", "  ", RegexOptions.IgnoreCase);
        strText = Regex.Replace(strText, "<[^>]+>", string.Empty);
        strText = System.Net.WebUtility.HtmlDecode(strText);
        strText = Regex.Replace(strText, "\n{3,}", "\n\n");
        return strText.Trim();
    }

    /// <summary>Uses Avalonia's native WebView dialog rather than a bundled browser. Its
    /// ShowPrintUI method opens the platform's own print dialog on supported desktop platforms.
    /// When PrintToFileFirst is enabled, follow the legacy Wine workaround by writing the rendered
    /// sheet to a temporary HTML file and navigating the native browser to its file URI; the file
    /// stays alive until that browser closes so asynchronous navigation cannot observe a deleted
    /// source.</summary>
    private void OnPrintNativeClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SheetHtml))
            return;

        try
        {
            var dialog = new NativeWebDialog { Title = Title };
            dialog.NavigationCompleted += (_, _) => dialog.ShowPrintUI();
            string? strTemporaryHtmlPath = null;
            if (GlobalOptions.Instance.PrintToFileFirst)
            {
                strTemporaryHtmlPath = Path.Combine(Path.GetTempPath(), "chummer-sheet-" + Guid.NewGuid().ToString("N") + ".html");
                File.WriteAllText(strTemporaryHtmlPath, SheetHtml);
                string strPathToDelete = strTemporaryHtmlPath;
                dialog.Closing += (_, _) =>
                {
                    try { File.Delete(strPathToDelete); }
                    catch { /* A failed cleanup must not prevent the native dialog from closing. */ }
                };
            }
            dialog.Show(this);
            if (strTemporaryHtmlPath == null)
                dialog.NavigateToString(SheetHtml);
            else
                dialog.Navigate(new Uri(strTemporaryHtmlPath));
        }
        catch (Exception ex)
        {
            SheetText = App.LanguageCatalog.GetString("UI_ErrorGeneratingSheetPrefix") + ex.Message;
            this.FindControl<TextBox>("SheetHtmlPanel")!.Text = SheetText;
        }
    }

    /// <summary>Saves the raw rendered XHTML to disk - the closest this port gets to "export" or
    /// "print" for now: there's no PDF library referenced and no cross-platform print backend
    /// wired up, but the actual rendered sheet (not just the tag-stripped preview text) is already
    /// sitting in SheetHtml, so at minimum the user can open/print/convert it externally.</summary>
    private async void OnExportHtmlClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SheetHtml) || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = App.LanguageCatalog.GetString("UI_ExportSheetAsHtmlTitle"),
            DefaultExtension = "html",
            SuggestedFileName = _character?.Name ?? "Charakterbogen",
            FileTypeChoices = [new FilePickerFileType("HTML-Datei") { Patterns = ["*.html", "*.htm"] }],
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(SheetHtml);
    }

    /// <summary>Renders the currently-selected sheet to PDF via
    /// <see cref="CharacterSheetExporter.RenderSheetToPdf(CharacterDocument,string,string)"/>
    /// (a system headless Chromium/Chrome browser's own print-to-pdf, since no PDF library is
    /// bundled) and saves it to a user-chosen path.</summary>
    private async void OnExportPdfClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var selector = this.FindControl<ComboBox>("SheetSelector")!;
        if (selector.SelectedItem is not string strSheetName)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = App.LanguageCatalog.GetString("UI_ExportSheetAsPdfTitle"),
            DefaultExtension = "pdf",
            SuggestedFileName = _character.Name ?? "Charakterbogen",
            FileTypeChoices = [new FilePickerFileType("PDF-Datei") { Patterns = ["*.pdf"] }],
        });
        if (file is null)
            return;

        string? strLocalPath = file.TryGetLocalPath();
        if (strLocalPath == null)
            return;

        try
        {
            CharacterSheetExporter.RenderSheetToPdf(_character, strSheetName, strLocalPath);
        }
        catch (Exception ex)
        {
            SheetText = App.LanguageCatalog.GetString("UI_ErrorPdfExportPrefix") + ex.Message;
            this.FindControl<TextBox>("SheetHtmlPanel")!.Text = SheetText;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
