using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmExport.cs: transforms a character through a data/export/*.xsl
/// template (e.g. Squad Manager's own XML schema) and saves the result to a file - a second,
/// separate export pipeline from data/sheets' HTML character sheets.</summary>
public partial class ExportDialog : Window
{
    private readonly CharacterDocument? _character;

    public ExportDialog() : this(null) { }

    public ExportDialog(CharacterDocument? character)
    {
        _character = character;
        InitializeComponent();

        var lstTemplates = CharacterSheetExporter.GetExportTemplateNames();
        TemplateBox.ItemsSource = lstTemplates;
        TemplateBox.SelectedIndex = lstTemplates.Count > 0 ? 0 : -1;
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TemplateBox.SelectedItem is not string strTemplate)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_NoExportFormatAvailableMessage");
            return;
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        string strContent;
        try
        {
            strContent = CharacterSheetExporter.RenderExport(_character, strTemplate);
        }
        catch (System.Exception ex)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_ErrorDuringExportPrefix") + ex.Message;
            return;
        }

        string strExtension = CharacterSheetExporter.GetExportTemplateExtension(strTemplate);
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = App.LanguageCatalog.GetString("UI_ExportCharacterTitle"),
            DefaultExtension = strExtension,
            SuggestedFileName = (_character.Alias.Length > 0 ? _character.Alias : _character.Name),
            FileTypeChoices = new[]
            {
                new FilePickerFileType(strExtension.ToUpperInvariant()) { Patterns = new[] { "*." + strExtension } },
            },
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(strContent);
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
