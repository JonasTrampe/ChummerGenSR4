using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class SettingsProfileDialog : Window
{
    public SettingsProfileDialogViewModel ViewModel { get; } = new();

    public SettingsProfileDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile == null)
            return;

        CharacterOptions objOptions = ViewModel.LoadSelectedOptions();

        Close(new SettingsProfileSelection
        {
            FileName = ViewModel.SelectedProfile.Value?.ToString() ?? "default.xml",
            DisplayName = ViewModel.SelectedProfile.Name ?? "Default Settings",
            BuildMethod = objOptions.BuildMethod,
            BuildPoints = objOptions.BuildPoints,
            MaxAvailability = objOptions.Availability
        });
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
