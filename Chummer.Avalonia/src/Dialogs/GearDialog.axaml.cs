using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class GearDialog : Window
{
    public GearDialogViewModel ViewModel { get; } = new();
    public GearOptionViewModel? SelectedGear => ViewModel.SelectedGear;

    /// <summary>True if closed via "Weitere" - the caller should add the selection and reopen a
    /// fresh dialog instead of treating this as the final pick.</summary>
    public bool ContinueAdding { get; private set; }

    public GearDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadOptions();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGear != null)
        {
            ContinueAdding = false;
            Close(true);
        }
    }

    private void OnAddAnother(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGear != null)
        {
            ContinueAdding = true;
            Close(true);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
