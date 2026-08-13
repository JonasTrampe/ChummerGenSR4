using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmCreateSpell.cs: homebrews a new Spell by combining category/type/
/// range/duration/modifier building blocks and computing its Drain Value live, instead of
/// picking one from spells.xml.</summary>
public partial class CreateSpellDialog : Window
{
    private static readonly string[] s_categories = { "Combat", "Detection", "Health", "Illusion", "Manipulation" };
    private readonly List<CheckBox> _lstModifierBoxes = new();

    public CreateSpellDialog() : this(null) { }

    public CreateSpellDialog(CharacterDocument? character)
    {
        InitializeComponent();

        CategoryBox.ItemsSource = s_categories;
        TypeBox.ItemsSource = new[] { "P", "M" };
        RangeBox.ItemsSource = new[] { "T", "LOS" };
        DurationBox.ItemsSource = new[] { "I", "P", "S" };
        TypeBox.SelectedIndex = 0;
        RangeBox.SelectedIndex = 1;
        DurationBox.SelectedIndex = 0;
        CategoryBox.SelectedIndex = 0;
    }

    public string ResultName { get; private set; } = string.Empty;
    public string ResultCategory { get; private set; } = string.Empty;
    public string ResultType { get; private set; } = string.Empty;
    public string ResultRange { get; private set; } = string.Empty;
    public bool ResultArea { get; private set; }
    public bool ResultRestricted { get; private set; }
    public bool ResultVeryRestricted { get; private set; }
    public string ResultDuration { get; private set; } = string.Empty;
    public HashSet<string> ResultCheckedKeys { get; private set; } = new();
    public int ResultNumberOfEffects { get; private set; }

    private void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryBox.SelectedItem is not string strCategory)
            return;

        AreaBox.IsEnabled = strCategory != "Health";
        if (strCategory == "Health")
            AreaBox.IsChecked = false;

        bool blnShowEffectCount = strCategory is "Combat" or "Manipulation";
        NumberOfEffectsLabel.IsVisible = blnShowEffectCount;
        NumberOfEffectsBox.IsVisible = blnShowEffectCount;

        ModifiersPanel.Children.Clear();
        _lstModifierBoxes.Clear();
        foreach (CharacterDocument.SpellModifierOption objOption in CharacterDocument.GetSpellModifierOptions(strCategory))
        {
            var chkBox = new CheckBox { Content = objOption.Label + " (" + (objOption.Dv >= 0 ? "+" : "") + objOption.Dv + ")", Tag = objOption.Key };
            chkBox.Click += OnFieldChanged;
            _lstModifierBoxes.Add(chkBox);
            ModifiersPanel.Children.Add(chkBox);
        }

        UpdateDv();
    }

    private void OnFieldChanged(object? sender, RoutedEventArgs e) => UpdateDv();
    private void OnFieldChanged(object? sender, SelectionChangedEventArgs e) => UpdateDv();
    private void OnFieldChanged(object? sender, NumericUpDownValueChangedEventArgs e) => UpdateDv();

    private void UpdateDv()
    {
        if (CategoryBox.SelectedItem is not string strCategory)
            return;

        var setKeys = _lstModifierBoxes.Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToHashSet();
        DvText.Text = CharacterDocument.ComputeCustomSpellDv(strCategory, (string)TypeBox.SelectedItem!,
            (string)RangeBox.SelectedItem!, AreaBox.IsChecked == true, RestrictedBox.IsChecked == true,
            VeryRestrictedBox.IsChecked == true, (string)DurationBox.SelectedItem!, setKeys,
            (int)(NumberOfEffectsBox.Value ?? 0));
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_PleaseEnterNameMessage");
            return;
        }
        if (CategoryBox.SelectedItem is not string strCategory)
            return;

        ResultName = NameBox.Text.Trim();
        ResultCategory = strCategory;
        ResultType = (string)TypeBox.SelectedItem!;
        ResultRange = (string)RangeBox.SelectedItem!;
        ResultArea = AreaBox.IsChecked == true;
        ResultRestricted = RestrictedBox.IsChecked == true;
        ResultVeryRestricted = VeryRestrictedBox.IsChecked == true;
        ResultDuration = (string)DurationBox.SelectedItem!;
        ResultCheckedKeys = _lstModifierBoxes.Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToHashSet();
        ResultNumberOfEffects = (int)(NumberOfEffectsBox.Value ?? 0);
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
