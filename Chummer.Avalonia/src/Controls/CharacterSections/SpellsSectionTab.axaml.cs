using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using SpellDialog = Chummer.NewUI.Dialogs.SpellDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SpellsSectionTab : UserControl
{
    private static readonly (string Category, string ItemName)[] s_categoryItems =
    {
        ("Combat", "CombatSpellsItem"),
        ("Detection", "DetectionSpellsItem"),
        ("Health", "HealthSpellsItem"),
        ("Illusion", "IllusionSpellsItem"),
        ("Manipulation", "ManipulationSpellsItem"),
        ("Geomancy Ritual", "RitualSpellsItem"),
    };

    public SpellsSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        foreach (var entry in s_categoryItems)
            this.FindControl<TreeViewItem>(entry.ItemName)!.Items.Clear();

        foreach (CharacterSpellData spell in character.Spells)
        {
            var entry = Array.Find(s_categoryItems, e => e.Category == spell.Category);
            var target = entry.ItemName is null
                ? this.FindControl<TreeViewItem>("CombatSpellsItem")!
                : this.FindControl<TreeViewItem>(entry.ItemName)!;
            target.Items.Add(new TreeViewItem { Header = spell.Name });
        }

        var spiritsPanel = this.FindControl<StackPanel>("SpiritsPanel")!;
        spiritsPanel.Children.Clear();
        foreach (CharacterSpiritData spirit in character.Spirits)
        {
            spiritsPanel.Children.Add(new InfoRow
            {
                Label = spirit.DisplayName + " (Kraft " + spirit.Force + (spirit.Bound ? ", gebunden" : "") + "):",
                Value = spirit.Services,
            });
        }
    }

    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SpellDialog();
        await dialog.ShowDialog(window);
    }
}
