using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmSelectAdvancedLifestyle.cs: builds a custom Advanced Lifestyle from
/// its five aspects, Roommates/Percentage, and Positive/Negative Qualities.</summary>
public partial class AdvancedLifestyleDialog : Window
{
    public AdvancedLifestyleDialogViewModel ViewModel { get; } = new();

    public AdvancedLifestyleDialog() : this(null) { }

    public AdvancedLifestyleDialog(CharacterDocument? character)
    {
        DataContext = ViewModel;
        InitializeComponent();
        if (character != null)
            ViewModel.LoadOptions(character);
    }

    public string LifestyleName => ViewModel.Name;
    public string Comforts => ViewModel.Comforts ?? string.Empty;
    public string Entertainment => ViewModel.Entertainment ?? string.Empty;
    public string Necessities => ViewModel.Necessities ?? string.Empty;
    public string Neighborhood => ViewModel.Neighborhood ?? string.Empty;
    public string Security => ViewModel.Security ?? string.Empty;
    public int Roommates => (int)ViewModel.Roommates;
    public int Percentage => (int)ViewModel.Percentage;
    public System.Collections.Generic.IReadOnlyList<string> PositiveQualities =>
        ViewModel.PositiveQualities.Where(q => q.IsChecked).Select(q => q.Name).ToList();
    public System.Collections.Generic.IReadOnlyList<string> NegativeQualities =>
        ViewModel.NegativeQualities.Where(q => q.IsChecked).Select(q => q.Name).ToList();

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ViewModel.Name))
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
