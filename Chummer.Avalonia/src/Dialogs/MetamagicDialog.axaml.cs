using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class MetamagicDialog : Window
{
    public MetamagicDialogViewModel ViewModel { get; } = new();
    public MetamagicOptionViewModel? SelectedMetamagic => ViewModel.Selected;

    public MetamagicDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public MetamagicDialog(CharacterDocument character)
        : this()
    {
        ViewModel.LoadOptions(character);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
