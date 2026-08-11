using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmLifestyleNuyen.cs: lets the player enter their manual dice-roll
/// result for starting Nuyen (the app doesn't roll dice for them, same as legacy).</summary>
public partial class LifestyleNuyenDialog : Window
{
    public LifestyleNuyenDialogViewModel ViewModel { get; }
    public int DiceResult => (int)ViewModel.DiceResult;

    public LifestyleNuyenDialog() : this(new CharacterDocument.LifestyleNuyenRollInfo(1, 10, 0))
    {
    }

    public LifestyleNuyenDialog(CharacterDocument.LifestyleNuyenRollInfo info)
    {
        ViewModel = new LifestyleNuyenDialogViewModel(info);
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
