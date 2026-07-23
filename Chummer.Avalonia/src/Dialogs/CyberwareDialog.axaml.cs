using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class CyberwareDialog : Window
{
    public CyberwareDialogViewModel ViewModel { get; } = new();
    public CyberwareOptionViewModel? SelectedCyberware => ViewModel.SelectedCyberware;
    public bool IsBioware { get; }

    public CyberwareDialog(bool blnBioware = false)
    {
        IsBioware = blnBioware;
        DataContext = ViewModel;
        InitializeComponent();
        Title = blnBioware ? "Bioware auswählen" : "Cyberware auswählen";
        ViewModel.LoadOptions(blnBioware);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCyberware != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
