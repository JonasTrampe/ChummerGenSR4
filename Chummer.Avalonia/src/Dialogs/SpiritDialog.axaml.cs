using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class SpiritDialog : Window
{
    public string SpiritName { get; private set; } = string.Empty;
    public string CritterName { get; private set; } = string.Empty;
    public string Type { get; private set; } = "Spirit";
    public string Force { get; private set; } = "1";
    public string Services { get; private set; } = "0";

    public SpiritDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Ein Name ist erforderlich.";
            return;
        }
        if (!int.TryParse(ForceBox.Text, out int intForce) || intForce <= 0)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_ForceMustBeGreaterThanZero");
            return;
        }
        if (!int.TryParse(ServicesBox.Text, out int intServices) || intServices < 0)
        {
            ErrorText.Text = "Die Anzahl Dienste darf nicht negativ sein.";
            return;
        }

        SpiritName = NameBox.Text.Trim();
        CritterName = CritterNameBox.Text?.Trim() ?? string.Empty;
        Type = (TypeBox.SelectedItem as ComboBoxItem)?.Content as string ?? "Spirit";
        Force = intForce.ToString();
        Services = intServices.ToString();
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
