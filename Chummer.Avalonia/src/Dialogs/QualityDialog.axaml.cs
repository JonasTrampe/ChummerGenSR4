using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class QualityDialog : Window
{
    public QualityDialogViewModel ViewModel { get; } = new();

    public QualityOptionViewModel? SelectedQuality => ViewModel.SelectedQuality;

    public QualityDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public QualityDialog(CharacterDocument character)
        : this()
    {
        ViewModel.LoadOptions(character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuality != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
