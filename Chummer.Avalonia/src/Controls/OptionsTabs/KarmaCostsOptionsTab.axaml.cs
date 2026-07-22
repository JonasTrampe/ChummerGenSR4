using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.OptionsTabs;

public partial class KarmaCostsOptionsTab : UserControl
{
    public KarmaCostsOptionsTab()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private void OnResetKarma(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is OptionsDialog objDialog)
            objDialog.OnResetKarma(sender, e);
    }
}
