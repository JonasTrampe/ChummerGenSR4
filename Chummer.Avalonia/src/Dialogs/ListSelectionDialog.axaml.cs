using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmSelectSkill.cs/frmSelectAttribute.cs (a plain list picker, no
/// category/rating extras this port doesn't otherwise model) - used by clsImprovement.cs's
/// selectskill/selectattribute bonus nodes.</summary>
public partial class ListSelectionDialog : Window
{
    public string? SelectedValue => OptionsList.SelectedItem as string;

    public ListSelectionDialog(string strDescription, IReadOnlyList<string> lstOptions)
    {
        InitializeComponent();
        DescriptionText.Text = strDescription;
        OptionsList.ItemsSource = lstOptions;
        if (lstOptions.Count > 0)
            OptionsList.SelectedIndex = 0;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (SelectedValue != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
