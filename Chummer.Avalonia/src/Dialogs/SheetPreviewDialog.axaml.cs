using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class SheetPreviewDialog : Window
{
    // A trimmed-down stand-in for the real XSLT sheet transform ("Shadowrun 4.xsl"). A native
    // read-only text preview keeps this spike free of an Avalonia 11-only HTML renderer; a
    // production sheet viewer can later use a supported WebView implementation.
    public string SheetHtml { get; } = """
        <html>
        <head>
        <style>
            body { font-family: Arial, sans-serif; font-size: 11px; color: #222; margin: 12px; }
            h1 { font-size: 18px; border-bottom: 2px solid #333; padding-bottom: 4px; margin-bottom: 8px; }
            h2 { font-size: 13px; background: #333; color: white; padding: 3px 6px; margin: 12px 0 4px 0; }
            table { border-collapse: collapse; width: 100%; margin-bottom: 8px; }
            td, th { border: 1px solid #999; padding: 3px 6px; text-align: left; }
            th { background: #ddd; }
            .label { font-weight: bold; }
        </style>
        </head>
        <body>
            <h1>Huan Hu</h1>
            <table>
                <tr><td class="label">Metatyp</td><td>Ork</td><td class="label">Karriere Karma</td><td>17</td></tr>
                <tr><td class="label">Karma</td><td>1</td><td class="label">Nuyen</td><td>1.435&#165;</td></tr>
            </table>

            <h2>Attribute</h2>
            <table>
                <tr><th>KON</th><th>GES</th><th>REA</th><th>STR</th><th>CHA</th><th>INT</th><th>LOG</th><th>WIL</th><th>EDG</th></tr>
                <tr><td>4</td><td>5 (7)</td><td>5 (7)</td><td>3 (4)</td><td>2</td><td>5</td><td>5</td><td>2</td><td>3</td></tr>
            </table>

            <h2>Fertigkeiten</h2>
            <table>
                <tr><th>Fertigkeit</th><th>Attribut</th><th>Wert</th><th>Pool</th></tr>
                <tr><td>Akrobatik</td><td>GES</td><td>2</td><td>9</td></tr>
                <tr><td>Ausweichen</td><td>REA</td><td>4</td><td>11 (13)</td></tr>
                <tr><td>Wahrnehmung</td><td>INT</td><td>2</td><td>7</td></tr>
            </table>

            <h2>Ausr&#252;stung</h2>
            <table>
                <tr><th>Gegenstand</th><th>Menge</th><th>Verf&#252;gbarkeit</th><th>Kosten</th></tr>
                <tr><td>Custom Commlink ("Schicke Armbanduhr")</td><td>1</td><td>0</td><td>218.780&#165;</td></tr>
                <tr><td>Panzergeh&#228;use (Stufe 10)</td><td>1</td><td>10V</td><td>2.000&#165;</td></tr>
            </table>
        </body>
        </html>
        """;

    public SheetPreviewDialog()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
		this.FindControl<TextBox>("SheetHtmlPanel")!.Text = SheetHtml;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
