using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

public partial class SheetPreviewDialog : Window
{
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
        if (character == null)
        {
            SheetText = "Kein Charakter geöffnet.";
        }
        else
        {
            try
            {
                SheetHtml = CharacterSheetExporter.RenderSheet(character, "Text-Only.xsl");
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
        string strText = Regex.Replace(strHtml, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        strText = Regex.Replace(strText, "<[^>]+>", string.Empty);
        strText = System.Net.WebUtility.HtmlDecode(strText);
        return strText.Trim();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
