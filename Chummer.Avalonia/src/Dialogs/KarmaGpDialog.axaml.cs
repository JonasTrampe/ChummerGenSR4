using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class KarmaGpDialog : Window
{
    public KarmaGpDialogViewModel ViewModel { get; } = new();

    public KarmaGpDialog()
    {
        DataContext = ViewModel;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public KarmaGpDialog(SettingsProfileSelection objSelection) : this()
    {
        ViewModel.ApplyDefaults(objSelection);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close(new BuildSelection
        {
            BuildMethod = ViewModel.BuildMethod,
            BuildPoints = ViewModel.BuildPoints,
            MaxAvailability = ViewModel.MaxAvailability,
            IgnoreCreationRules = ViewModel.IgnoreCreationRules
        });
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
