using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.Localization;

namespace Chummer.NewUI.Controls.OptionsTabs;

public partial class GeneralOptionsTab : UserControl
{
    public GeneralOptionsTab()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        AvaloniaLocalizationHelper.Apply(this);
    }

    private OptionsDialog? GetOwnerDialog() => VisualRoot as OptionsDialog;

    private void OnVerifyLanguage(object? sender, RoutedEventArgs e)
    {
        GetOwnerDialog()?.OnVerifyLanguage(sender, e);
    }

    private void OnVerifyData(object? sender, RoutedEventArgs e)
    {
        GetOwnerDialog()?.OnVerifyData(sender, e);
    }

    private void OnBrowsePdfApp(object? sender, RoutedEventArgs e)
    {
        GetOwnerDialog()?.OnBrowsePdfApp(sender, e);
    }
}
