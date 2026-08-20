using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Character-sheet print preview - two synced windows rather than one, because the only
/// engine on this port's target hardware that renders the sheet both correctly *and* interactively
/// (page-break toggles and any other in-sheet JS controls a sheet template uses) is
/// Avalonia.Controls.WebView's NativeWebDialog, which can only exist as its own top-level native
/// window - it can't be embedded as a child control (that failed to render at all on NVIDIA/GBM),
/// and Ultralight's off-screen bitmap rendering (this dialog's previous approach) has no
/// interactivity at all. This window is the toolbar (real Avalonia buttons - proven 100% reliable,
/// unlike the in-page HTML/JS bridges tried and abandoned earlier); a NativeWebDialog positioned
/// directly below it holds the actual interactive sheet content, moved to follow this window so
/// the pair reads as one attached unit.</summary>
public partial class SheetPreviewDialog : Window
{
    private CharacterDocument? _character;
    private NativeWebDialog? _contentDialog;

    public string SheetHtml { get; private set; } = string.Empty;

    public SheetPreviewDialog()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        PositionChanged += (_, _) => RepositionContentDialog();
        Opened += (_, _) => CreateContentDialogAndRender();
        Closing += (_, _) => _contentDialog?.Close();
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
    }

    private void CreateContentDialogAndRender()
    {
        _contentDialog = new NativeWebDialog { Title = App.LanguageCatalog.GetString("UI_SheetPreviewTitle") };
        _contentDialog.Show(this);
        RepositionContentDialog();
        RenderSelectedSheet();
    }

    /// <summary>Keeps the content window directly below and matching the width of this toolbar
    /// window, so moving the toolbar (the only window with a title bar/decorations) drags the pair
    /// together as one visual unit.</summary>
    private void RepositionContentDialog()
    {
        if (_contentDialog == null)
            return;
        _contentDialog.Move((int)Position.X, (int)Position.Y + (int)Height);
        _contentDialog.Resize((int)Width, 560);
    }

    private void OnSheetSelectionChanged(object? sender, SelectionChangedEventArgs e) => RenderSelectedSheet();

    private void RenderSelectedSheet()
    {
        if (_contentDialog == null)
            return;

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
            _contentDialog.NavigateToString(SheetHtml);
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

    /// <summary>Prints the sheet already loaded into the content window via its own ShowPrintUI,
    /// which opens the platform's native print dialog. When PrintToFileFirst is enabled, follows
    /// the legacy Wine workaround by re-navigating to a temporary HTML file first instead of the
    /// in-memory string; the file is cleaned up when this dialog closes.</summary>
    private void OnPrintNativeClick(object? sender, RoutedEventArgs e)
    {
        if (_contentDialog == null || string.IsNullOrEmpty(SheetHtml))
            return;

        try
        {
            if (GlobalOptions.Instance.PrintToFileFirst)
            {
                string strTemporaryHtmlPath = Path.Combine(Path.GetTempPath(), "chummer-sheet-" + Guid.NewGuid().ToString("N") + ".html");
                File.WriteAllText(strTemporaryHtmlPath, SheetHtml);
                var dialog = _contentDialog;
                void OnNavigated(object? s, EventArgs args)
                {
                    dialog.NavigationCompleted -= OnNavigated;
                    dialog.ShowPrintUI();
                    try { File.Delete(strTemporaryHtmlPath); }
                    catch { /* A failed cleanup must not prevent printing. */ }
                }
                dialog.NavigationCompleted += OnNavigated;
                dialog.Navigate(new Uri(strTemporaryHtmlPath));
            }
            else
            {
                _contentDialog.ShowPrintUI();
            }
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
