using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using QualityDialog = Chummer.NewUI.Dialogs.QualityDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GeneralSectionTab : UserControl
{
    public GeneralSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        this.FindControl<TextBox>("AliasTextBox")!.Text = character.Alias;
        this.FindControl<TextBlock>("MetatypeText")!.Text = character.Metatype;
        this.FindControl<TextBox>("NuyenTextBox")!.Text = character.Nuyen;
        this.FindControl<TextBlock>("NuyenEquivalentText")!.Text = "= " + character.Nuyen + "¥";

        foreach (CharacterAttributeData attribute in character.Attributes)
        {
            var row = this.FindControl<AttributeRow>(attribute.Code + "Attribute");
            if (row is null)
                continue;

            row.Base = attribute.Value;
            row.Augmented = attribute.TotalValue == attribute.Value ? string.Empty : "(" + attribute.TotalValue + ")";
            row.Range = attribute.Minimum + " / " + attribute.Maximum + " (" + attribute.AugmentedMaximum + ")";
        }

        var qualitiesTree = this.FindControl<TreeView>("QualitiesTree")!;
        qualitiesTree.Items.Clear();
        var positiveQualities = new TreeViewItem { Header = "Positive qualities", IsExpanded = true };
        var negativeQualities = new TreeViewItem { Header = "Negative qualities", IsExpanded = true };
        foreach (CharacterQualityData quality in character.Qualities)
        {
            var parent = quality.Type == "Negative" ? negativeQualities : positiveQualities;
            parent.Items.Add(new TreeViewItem { Header = quality.DisplayName });
        }
        if (positiveQualities.ItemCount > 0) qualitiesTree.Items.Add(positiveQualities);
        if (negativeQualities.ItemCount > 0) qualitiesTree.Items.Add(negativeQualities);

        var contactsPanel = this.FindControl<StackPanel>("ContactsPanel")!;
        contactsPanel.Children.Clear();
        foreach (CharacterContactData contact in character.Contacts)
            contactsPanel.Children.Add(CreateContactRow(contact));

        var enemiesPanel = this.FindControl<StackPanel>("EnemiesPanel")!;
        enemiesPanel.Children.Clear();
        foreach (CharacterContactData enemy in character.Enemies)
            enemiesPanel.Children.Add(CreateContactRow(enemy));
    }

    private static Border CreateContactRow(CharacterContactData contact)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("140,Auto,50,Auto,50"),
            ColumnSpacing = 6,
        };
        grid.Children.Add(new TextBlock { Text = contact.Name, [Grid.ColumnProperty] = 0, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = "Connection:", [Grid.ColumnProperty] = 1, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = contact.Connection, [Grid.ColumnProperty] = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = "Loyalität:", [Grid.ColumnProperty] = 3, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = contact.Loyalty, [Grid.ColumnProperty] = 4, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        return new Border
        {
            BorderBrush = Avalonia.Media.Brushes.Gainsboro,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 4),
            Margin = new Thickness(0, 0, 0, 4),
            Child = grid,
        };
    }

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new QualityDialog();
        await dialog.ShowDialog(window);
    }
}
