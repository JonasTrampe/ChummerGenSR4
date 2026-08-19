using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
namespace Chummer.NewUI.Dialogs;
public partial class LifestyleDialog : Window
{
 public LifestyleDialogViewModel ViewModel { get; } = new(); public LifestyleOptionViewModel? SelectedLifestyle => ViewModel.Selected;
 public LifestyleDialog() : this(null) { }
 public LifestyleDialog(CharacterDocument? character) { DataContext=ViewModel; InitializeComponent(); ViewModel.LoadOptions(character); }
 private void OnOk(object? sender, RoutedEventArgs e) { if (SelectedLifestyle != null) Close(true); }
 private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
