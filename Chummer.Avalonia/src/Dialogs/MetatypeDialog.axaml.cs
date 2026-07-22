using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class MetatypeDialog : Window
{
    public MetatypeDialogViewModel ViewModel { get; } = new();

    public MetatypeDialog()
    {
        DataContext = ViewModel;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        ViewModel.LoadMetatypes();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedMetatype == null)
            return;

        Close(new MetatypeSelection
        {
            Metatype = ViewModel.SelectedMetatype,
            MetavariantName = ViewModel.SelectedMetavariant == "-" ? string.Empty : ViewModel.SelectedMetavariant
        });
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
