using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

public partial class AddExoticSkillDialog : Window
{
    public string SkillName { get; private set; } = string.Empty;
    public string SubType { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Attribute { get; private set; } = string.Empty;

    public AddExoticSkillDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_SkillNameRequired");
            return;
        }
        if (string.IsNullOrWhiteSpace(SubTypeBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_SubTypeRequired");
            return;
        }

        SkillName = NameBox.Text.Trim();
        SubType = SubTypeBox.Text.Trim();
        Category = (CategoryBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Combat Active";
        Attribute = (AttributeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "AGI";
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
