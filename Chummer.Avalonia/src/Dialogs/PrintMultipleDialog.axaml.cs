using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmPrintMultiple.cs: batch-loads several .chum files and renders them
/// together into one combined "Game Master Summary" sheet - GM tooling for printing a whole
/// group's characters at once instead of one at a time.</summary>
public partial class PrintMultipleDialog : Window
{
    private readonly ObservableCollection<string> _lstPaths = new();
    private string _strHtml = string.Empty;

    public PrintMultipleDialog()
    {
        InitializeComponent();
        CharacterList.ItemsSource = _lstPaths;
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = App.LanguageCatalog.GetString("UI_SelectCharactersTitle"),
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Chummer characters") { Patterns = new[] { "*.chum", "*.xml" } },
                FilePickerFileTypes.All,
            },
        });

        foreach (var file in files)
        {
            string? strPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(strPath) && !_lstPaths.Contains(strPath))
                _lstPaths.Add(strPath);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (CharacterList.SelectedItem is string strPath)
            _lstPaths.Remove(strPath);
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (_lstPaths.Count == 0)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_NoCharactersSelectedMessage");
            return;
        }

        Progress.Maximum = _lstPaths.Count;
        Progress.Value = 0;

        var lstCharacters = new System.Collections.Generic.List<CharacterDocument>();
        var objService = new CharacterFileService();
        foreach (string strPath in _lstPaths)
        {
            try
            {
                await using FileStream stream = File.OpenRead(strPath);
                lstCharacters.Add(objService.Load(stream, Path.GetFileName(strPath)));
            }
            catch (Exception ex)
            {
                ErrorText.Text = App.LanguageCatalog.GetString("UI_ErrorLoadingFilePrefix") + Path.GetFileName(strPath) + ": " + ex.Message;
                return;
            }
            Progress.Value++;
        }

        try
        {
            _strHtml = CharacterSheetExporter.RenderSheet(lstCharacters, "Game Master Summary.xsl");
            OutputText.Text = HtmlToPlainText(_strHtml);
        }
        catch (Exception ex)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_ErrorGeneratingSheetPrefix") + ex.Message;
        }
        finally
        {
            Progress.Value = 0;
        }
    }

    private async void OnExportHtmlClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_strHtml) || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = App.LanguageCatalog.GetString("UI_ExportSheetsAsHtmlTitle"),
            DefaultExtension = "html",
            SuggestedFileName = App.LanguageCatalog.GetString("UI_CharactersSuggestedFileName"),
            FileTypeChoices = new[] { new FilePickerFileType("HTML-Datei") { Patterns = new[] { "*.html", "*.htm" } } },
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(_strHtml);
    }

    // Same approach as SheetPreviewDialog - no cross-platform HTML renderer in Avalonia, so the
    // preview strips tags to plain text; OnExportHtmlClick still saves the real rendered markup.
    private static string HtmlToPlainText(string strHtml)
    {
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

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
