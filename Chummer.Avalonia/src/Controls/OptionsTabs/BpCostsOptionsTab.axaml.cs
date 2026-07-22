using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.OptionsTabs;

public partial class BpCostsOptionsTab : UserControl
{
    public BpCostsOptionsTab()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnResetBp(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is OptionsDialog objDialog)
            objDialog.OnResetBp(sender, e);
    }
}
