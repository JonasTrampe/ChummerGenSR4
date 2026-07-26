using System;
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

    // Required by Avalonia's runtime XAML loader; normal callers use the typed constructor below.
    public WeaponCategoriesDialog() : this(Array.Empty<WeaponCategorySelectionItemViewModel>())
    {
    }

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
