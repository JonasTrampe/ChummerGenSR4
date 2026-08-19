using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Character-sheet print preview - real Avalonia toolbar controls (proven 100% reliable
/// via direct C# event handlers) driving a <see cref="UltralightHtmlView"/> for the styled
/// preview itself. Printing reuses Avalonia.Controls.WebView's NativeWebDialog purely to trigger
/// the platform's native print dialog (the one thing Ultralight has no equivalent for) - that
/// mechanism was already proven reliable when driven by a direct button click, it just isn't used
/// as the visible preview surface anymore since embedding it as a child control failed to render
/// on this port's target NVIDIA/GBM hardware.</summary>
public partial class SheetPreviewDialog : Window
{
    private CharacterDocument? _character;

    public string SheetHtml { get; private set; } = string.Empty;

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
            ShowStatus(App.LanguageCatalog.GetString("UI_NoCharacterOpenMessage"));
            return;
        }
        if (strSheetName == null)
        {
            ShowStatus(App.LanguageCatalog.GetString("UI_NoCharacterSheetFoundMessage"));
            return;
        }

        try
        {
            SheetHtml = CharacterSheetExporter.RenderSheet(_character, strSheetName);
            HideStatus();
            this.FindControl<UltralightHtmlView>("SheetImage")!.LoadHtml(SheetHtml, (uint)Width - 40);
        }
        catch (Exception ex)
        {
            ShowStatus(App.LanguageCatalog.GetString("UI_ErrorGeneratingSheetPrefix") + ex.Message);
        }
    }

    private void ShowStatus(string strMessage)
    {
        var statusText = this.FindControl<TextBlock>("StatusText")!;
        statusText.Text = strMessage;
        statusText.IsVisible = true;
    }

    private void HideStatus() => this.FindControl<TextBlock>("StatusText")!.IsVisible = false;

    /// <summary>Prints via a transient Avalonia.Controls.WebView NativeWebDialog, the same
    /// mechanism a standalone print-preview dialog already proved capable of both rendering and
    /// printing to a real printer - it's shown only for the duration of the print action (the OS's
    /// own native print dialog is itself a separate system window on every platform anyway, so
    /// this doesn't meaningfully change the "one window" preview experience). When
    /// PrintToFileFirst is enabled, follows the legacy Wine workaround by navigating to a
    /// temporary HTML file first instead of an in-memory string.</summary>
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
            ShowStatus(App.LanguageCatalog.GetString("UI_ErrorGeneratingSheetPrefix") + ex.Message);
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
            ShowStatus(App.LanguageCatalog.GetString("UI_ErrorPdfExportPrefix") + ex.Message);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
