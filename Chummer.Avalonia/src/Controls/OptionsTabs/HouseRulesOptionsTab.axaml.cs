using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.Localization;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.OptionsTabs;

public partial class HouseRulesOptionsTab : UserControl
{
    public HouseRulesOptionsTab()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        AvaloniaLocalizationHelper.Apply(this);
    }

    private async void OnSelectStickNShockCategories(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OptionsDialogViewModel objViewModel || TopLevel.GetTopLevel(this) is not Window objWindow)
            return;

        List<WeaponCategorySelectionItemViewModel> lstCategories = BuildStickNShockCategories(objViewModel);
        WeaponCategoriesDialog objDialog = new WeaponCategoriesDialog(lstCategories)
        {
            Title = App.LanguageCatalog.GetString("Title_SelectWeaponCategories")
        };

        bool blnSaved = await objDialog.ShowDialog<bool>(objWindow);
        if (!blnSaved)
            return;

        HashSet<string> setSelectedCategories = new HashSet<string>(objDialog.SelectedCategories);
        objViewModel.CurrentOptions.StickNShockExcludedWeaponCategories.Clear();
        foreach (WeaponCategorySelectionItemViewModel objItem in lstCategories)
        {
            if (!setSelectedCategories.Contains(objItem.Value))
                objViewModel.CurrentOptions.StickNShockExcludedWeaponCategories.Add(objItem.Value);
        }
    }

    private static List<WeaponCategorySelectionItemViewModel> BuildStickNShockCategories(OptionsDialogViewModel objViewModel)
    {
        XmlDocument objWeaponsDocument = XmlManager.Instance.Load("weapons.xml");
        SortedSet<string> setCategories = new SortedSet<string>();
        string[] astrUnsupportedCategories =
        {
            "Bows", "Clubs", "Crossbows", "Flamethrowers", "Grenade Launchers", "Laser Weapons",
            "Missile Launchers", "Mortar Launchers", "Tasers", "Throwing Weapons"
        };

        XmlNodeList? lstNodes = objWeaponsDocument.SelectNodes("/chummer/weapons/weapon[ammo and ammo != '0' and mode and mode != '0']/category");
        if (lstNodes != null)
        {
            foreach (XmlNode objCategory in lstNodes)
            {
                string strCategory = objCategory.InnerText;
                if (!astrUnsupportedCategories.Any(strUnsupported => strCategory == strUnsupported || strCategory.EndsWith(" " + strUnsupported)))
                    setCategories.Add(strCategory);
            }
        }

        objViewModel.CurrentOptions.StickNShockExcludedWeaponCategories.RemoveWhere(strCategory => !setCategories.Contains(strCategory));

        List<WeaponCategorySelectionItemViewModel> lstCategories = new List<WeaponCategorySelectionItemViewModel>();
        foreach (string strCategory in setCategories)
        {
            lstCategories.Add(new WeaponCategorySelectionItemViewModel
            {
                Name = strCategory,
                Value = strCategory,
                IsSelected = !objViewModel.CurrentOptions.StickNShockExcludedWeaponCategories.Contains(strCategory)
            });
        }

        return lstCategories;
    }
}
