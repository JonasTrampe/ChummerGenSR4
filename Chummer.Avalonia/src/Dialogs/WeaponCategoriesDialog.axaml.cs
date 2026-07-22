using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Localization;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class WeaponCategoriesDialog : Window
{
    public ObservableCollection<WeaponCategorySelectionItemViewModel> Categories { get; }

    public WeaponCategoriesDialog(IEnumerable<WeaponCategorySelectionItemViewModel> lstCategories)
    {
        Categories = new ObservableCollection<WeaponCategorySelectionItemViewModel>(lstCategories);
        DataContext = this;
        InitializeComponent();
        AvaloniaLocalizationHelper.Apply(this);
    }

    public IEnumerable<string> SelectedCategories
    {
        get
        {
            foreach (WeaponCategorySelectionItemViewModel objItem in Categories)
            {
                if (objItem.IsSelected)
                    yield return objItem.Value;
            }
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
