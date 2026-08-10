using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
            SheetText = "Kein Charakter geöffnet.";
        }
        else if (strSheetName == null)
        {
            SheetText = "Kein Charakterbogen gefunden.";
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
                SheetText = "Fehler beim Erzeugen des Charakterbogens: " + ex.Message;
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

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
