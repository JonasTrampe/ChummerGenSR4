using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

/// <summary>Collects the percentage selected when replacing an obsolete vehicle mod.</summary>
public partial class RetrofitDialog : Window
{
    public RetrofitDialogViewModel ViewModel { get; } = new();
    public int Percentage => (int)ViewModel.Percentage;

    public RetrofitDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
