using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed partial class OptionsDialogViewModel
{
    public void Save()
    {
        CurrentOptions.Books.Clear();
        foreach (OptionsBookItemViewModel objBook in Sourcebooks.Where(x => x.IsSelected))
            CurrentOptions.Books.Add(objBook.Code);
        if (!CurrentOptions.Books.Contains("SR4"))
            CurrentOptions.Books.Add("SR4");

        if (SelectedLanguage?.Value != null)
            GlobalOptions.Instance.Language = SelectedLanguage.Value;
        if (SelectedSheet?.Value != null)
            GlobalOptions.Instance.DefaultCharacterSheet = SelectedSheet.Value;

        CurrentOptions.Save();

        SettingsRegistryKey objRegistry = SettingsStore.CurrentUser.CreateSubKey("Software\\Chummer");
        objRegistry.SetValue("autoupdate", AutomaticUpdate.ToString());
        objRegistry.SetValue("localisedupdatesonly", LocalisedUpdatesOnly.ToString());
        objRegistry.SetValue("language", GlobalOptions.Instance.Language);
        objRegistry.SetValue("startupfullscreen", StartupFullscreen.ToString());
        objRegistry.SetValue("singlediceroller", SingleDiceRoller.ToString());
        objRegistry.SetValue("defaultsheet", GlobalOptions.Instance.DefaultCharacterSheet);
        objRegistry.SetValue("pdfargumentstyle", GlobalOptions.Instance.PdfArgumentStyle);
        objRegistry.SetValue("datesincludetime", DatesIncludeTime.ToString());
        objRegistry.SetValue("printtofilefirst", PrintToFileFirst.ToString());
        objRegistry.SetValue("pdfapppath", PdfAppPath);
        objRegistry.SetValue("cloudapibaseurl", CloudApiBaseUrl);
        objRegistry.SetValue("suppresscloudunreachablewarning", SuppressCloudUnreachableWarning.ToString());

        // Ported from clsCommon.cs's SourcebookInfo load: one registry value per book code,
        // "path|offset" - only written for books that actually have a path configured.
        GlobalOptions.Instance.SourcebookInfo.Clear();
        foreach (OptionsBookItemViewModel objBook in Sourcebooks)
        {
            if (string.IsNullOrWhiteSpace(objBook.PdfPath))
                continue;
            objRegistry.SetValue(objBook.Code, objBook.PdfPath + "|" + objBook.PdfOffset);
            GlobalOptions.Instance.SourcebookInfo.Add(new SourcebookInfo
            {
                Code = objBook.Code,
                Path = objBook.PdfPath,
                Offset = objBook.PdfOffset
            });
        }
    }
}
