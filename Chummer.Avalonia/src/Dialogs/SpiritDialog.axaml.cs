using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class SpiritDialog : Window
{
    // 0 = no cap enforced (character not passed in, or MaxSpiritForce couldn't be determined).
    private readonly int _intMaxForce;

    public string SpiritName { get; private set; } = string.Empty;
    public string CritterName { get; private set; } = string.Empty;
    public string Type { get; private set; } = "Spirit";
    public string Force { get; private set; } = "1";
    public string Services { get; private set; } = "0";

    public SpiritDialog()
    {
        InitializeComponent();
    }

    /// <summary>Ported from frmCareer.cs's/frmCreate.cs's cmdAddSpirit_Click: caps the Force a
    /// player can enter at <see cref="Core.CharacterFileService.MaxSpiritForce"/> instead of
    /// letting it be typed freely.</summary>
    public SpiritDialog(int intMaxForce) : this()
    {
        _intMaxForce = intMaxForce;
        if (intMaxForce > 0)
            ForceLabel.Text += $" (max. {intMaxForce})";
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_PleaseEnterNameMessage");
            return;
        }
        if (!int.TryParse(ForceBox.Text, out int intForce) || intForce <= 0)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_ForceMustBeGreaterThanZero");
            return;
        }
        if (_intMaxForce > 0 && intForce > _intMaxForce)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_SpiritForceExceedsMaximum").Replace("{0}", _intMaxForce.ToString());
            return;
        }
        if (!int.TryParse(ServicesBox.Text, out int intServices) || intServices < 0)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_ServicesMustNotBeNegative");
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
