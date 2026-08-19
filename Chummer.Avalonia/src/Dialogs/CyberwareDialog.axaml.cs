using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class CyberwareDialog : Window
{
    public CyberwareDialogViewModel ViewModel { get; } = new();
    public CyberwareOptionViewModel? SelectedCyberware => ViewModel.SelectedCyberware;
    public bool IsBioware { get; }

    // Required by Avalonia's runtime XAML loader (and useful for designer tooling).
    public CyberwareDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public CyberwareDialog(CharacterDocument character, bool blnBioware = false)
        : this()
    {
        IsBioware = blnBioware;
        Title = blnBioware ? "Bioware auswählen" : "Cyberware auswählen";
        ViewModel.LoadOptions(blnBioware, character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCyberware != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
