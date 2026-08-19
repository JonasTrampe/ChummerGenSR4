using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed partial class OptionsDialogViewModel
{
    private void LoadSettingsProfiles()
    {
        SettingsProfiles.Clear();
        string strDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        if (!Directory.Exists(strDirectory))
            return;

        foreach (string strFile in Directory.GetFiles(strDirectory, "*.xml"))
        {
            try
            {
                XmlDocument objDoc = new XmlDocument();
                objDoc.Load(strFile);
                SettingsProfiles.Add(new ListItem
                {
                    Value = Path.GetFileName(strFile),
                    Name = objDoc.SelectSingleNode("/settings/name")?.InnerText ?? Path.GetFileNameWithoutExtension(strFile)
                });
            }
            catch
            {
            }
        }

        SelectedSettingsProfile = SettingsProfiles.FirstOrDefault(x => x.Value == "default.xml") ?? SettingsProfiles.FirstOrDefault();
    }

    private void LoadLanguages()
    {
        Languages.Clear();
        string strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "lang");
        foreach (string strFile in Directory.GetFiles(strPath, "*.xml"))
        {
            string strCode = Path.GetFileNameWithoutExtension(strFile);
            if (strCode.EndsWith("_data", StringComparison.OrdinalIgnoreCase) ||
                strCode.StartsWith("results_", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                XmlDocument objXmlDocument = new XmlDocument();
                objXmlDocument.Load(strFile);
                string? strName = objXmlDocument.SelectSingleNode("/chummer/name")?.InnerText;
                if (string.IsNullOrWhiteSpace(strName))
                    continue;

                Languages.Add(new ListItem
                {
                    Value = strCode,
                    Name = strName
                });
            }
            catch
            {
            }
        }

        Sort(Languages);
        SelectedLanguage = Languages.FirstOrDefault(x => x.Value == GlobalOptions.Instance.Language) ??
                           Languages.FirstOrDefault(x => x.Value == "en-us") ??
                           Languages.FirstOrDefault();
    }

    private void LoadSheets()
    {
        Sheets.Clear();
        string strPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "sheets");
        if (!Directory.Exists(strPath))
            return;

        foreach (string strFile in Directory.GetFiles(strPath, "*.xsl"))
        {
            if (strFile.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase))
                continue;
            string strName = Path.GetFileNameWithoutExtension(strFile);
            Sheets.Add(new ListItem { Value = strName, Name = strName });
        }

        SelectedSheet = Sheets.FirstOrDefault(x => x.Value == GlobalOptions.Instance.DefaultCharacterSheet) ?? Sheets.FirstOrDefault();
    }

    private void LoadSourcebooks()
    {
        Sourcebooks.Clear();
        XmlDocument objDocument = XmlManager.Instance.Load("books.xml");
        XmlNodeList? objBookNodes = objDocument.SelectNodes("/chummer/books/book");
        if (objBookNodes == null)
            return;

        foreach (XmlNode objBookNode in objBookNodes)
        {
            string strCode = objBookNode["code"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(strCode))
                continue;
            string strName = objBookNode["translate"]?.InnerText
                             ?? objBookNode["name"]?.InnerText
                             ?? strCode;
            SourcebookInfo? objInfo = GlobalOptions.Instance.SourcebookInfo.Find(i => i.Code == strCode);
            Sourcebooks.Add(new OptionsBookItemViewModel
            {
                Code = strCode,
                DisplayName = strName,
                IsRequired = strCode == "SR4",
                IsSelected = CurrentOptions.BookEnabled(strCode) || strCode == "SR4",
                PdfPath = objInfo?.Path ?? string.Empty,
                PdfOffset = objInfo?.Offset ?? 0
            });
        }

        foreach (OptionsBookItemViewModel objBook in Sourcebooks.OrderBy(x => x.DisplayName).ToList())
        {
            Sourcebooks.Remove(objBook);
            Sourcebooks.Add(objBook);
        }
    }

    private void SyncSelectionsFromOptions()
    {
        SelectedBuildMethod = BuildMethods.FirstOrDefault(x => x.Value == CurrentOptions.BuildMethod) ?? BuildMethods.FirstOrDefault();
        SelectedEssenceDecimals = EssenceDecimals.FirstOrDefault(x => x.Value == CurrentOptions.EssenceDecimals.ToString()) ?? EssenceDecimals.FirstOrDefault();
        string strLimbKey = CurrentOptions.LimbCount == 6 ? "all" : CurrentOptions.ExcludeLimbSlot == "skull" ? "torso" : "skull";
        SelectedLimbCount = LimbCounts.FirstOrDefault(x => x.Value == strLimbKey) ?? LimbCounts.FirstOrDefault();
        OnPropertyChanged(nameof(CurrentOptions));
    }

    private void SyncSourcebookSelection()
    {
        HashSet<string> setBooks = CurrentOptions.Books.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (OptionsBookItemViewModel objBook in Sourcebooks)
            objBook.IsSelected = objBook.IsRequired || setBooks.Contains(objBook.Code);
    }

    private static void Sort(ObservableCollection<ListItem> lstItems)
    {
        List<ListItem> lstSorted = lstItems.OrderBy(x => x.Name).ToList();
        lstItems.Clear();
        foreach (ListItem objItem in lstSorted)
            lstItems.Add(objItem);
    }
}
