using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class MetatypeDialog : Window
{
    public MetatypeDialogViewModel ViewModel { get; } = new();

    public MetatypeDialog(string strSettingsFileName = "default.xml", bool blnCritterMode = false)
    {
        DataContext = ViewModel;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        if (blnCritterMode)
            ViewModel.LoadCritterMetatypes(strSettingsFileName);
        else
            ViewModel.LoadMetatypes(strSettingsFileName);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedMetatype == null)
            return;

        var magicTypeBox = this.FindControl<ComboBox>("MagicTypeBox");
        string strMagicType = (magicTypeBox?.SelectedItem as ComboBoxItem)?.Tag as string ?? "None";

        Close(new MetatypeSelection
        {
            Metatype = ViewModel.SelectedMetatype,
            MetavariantName = ViewModel.SelectedMetavariant == "-" ? string.Empty : ViewModel.SelectedMetavariant,
            MagicType = strMagicType,
            Force = ViewModel.Force
        });
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
