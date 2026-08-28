using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

/// <summary>Editor for the manually-created Improvements Core can apply correctly.</summary>
public partial class CreateImprovementDialog : Window
{
    private static readonly (CharacterDocument.CustomImprovementType Type, string Label)[] s_types =
    {
        (CharacterDocument.CustomImprovementType.Attribute, "Attribut"),
        (CharacterDocument.CustomImprovementType.Skill, "Fertigkeit"),
        (CharacterDocument.CustomImprovementType.ConditionMonitorPhysical, "Zustandsmonitor, Körperlich"),
        (CharacterDocument.CustomImprovementType.ConditionMonitorStun, "Zustandsmonitor, Geistig"),
        (CharacterDocument.CustomImprovementType.ConditionMonitorThreshold, "Zustandsmonitor, Schwellenwert"),
        (CharacterDocument.CustomImprovementType.ConditionMonitorThresholdOffset, "Zustandsmonitor, Schwellenwert-Offset"),
        (CharacterDocument.CustomImprovementType.Initiative, "Initiative"),
        (CharacterDocument.CustomImprovementType.MovementPercent, "Bewegung (%)"),
        (CharacterDocument.CustomImprovementType.Concealability, "Verbergbarkeit"),
        (CharacterDocument.CustomImprovementType.UnarmedDv, "Waffenlos, Schadenswert"),
        (CharacterDocument.CustomImprovementType.UnarmedAp, "Waffenlos, Panzerungsdurchdringung"),
        (CharacterDocument.CustomImprovementType.Reach, "Reichweite"),
        (CharacterDocument.CustomImprovementType.LifestyleCost, "Lebensstilkosten"),
    };

    private static readonly string[] s_attributeCodes =
        { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "EDG", "MAG", "RES" };

    private readonly CharacterDocument? _character;

    public CreateImprovementDialog() : this(null) { }

    public CreateImprovementDialog(CharacterDocument? character, ImprovementRowViewModel? objExisting = null)
    {
        _character = character;
        InitializeComponent();
        TypeBox.ItemsSource = s_types.Select(t => t.Label).ToList();
        TypeBox.SelectedIndex = 0;
        if (objExisting != null)
            LoadExisting(objExisting);
    }

    public CharacterDocument.CustomImprovementType ResultType { get; private set; }
    public string ResultName { get; private set; } = string.Empty;
    public int ResultVal { get; private set; }
    public int ResultMin { get; private set; }
    public int ResultMax { get; private set; }
    public int ResultAug { get; private set; }
    public string ResultSelect { get; private set; } = string.Empty;
    public bool ResultApplyToRating { get; private set; }

    private CharacterDocument.CustomImprovementType SelectedType => s_types[TypeBox.SelectedIndex].Type;

    private void LoadExisting(ImprovementRowViewModel objExisting)
    {
        if (!TryMapType(objExisting, out CharacterDocument.CustomImprovementType eType))
            return;

        TypeBox.SelectedIndex = System.Array.FindIndex(s_types, objType => objType.Type == eType);
        NameBox.Text = objExisting.SourceName;
        ValBox.Value = objExisting.RawValue;
        MinBox.Value = objExisting.Minimum;
        MaxBox.Value = objExisting.Maximum;
        AugBox.Value = objExisting.AugmentedMaximum;
        ApplyToRatingBox.IsChecked = objExisting.AddToRating;
        if (SelectBox.IsVisible)
            SelectBox.SelectedItem = objExisting.Target;
    }

    public static bool TryMapType(ImprovementRowViewModel objExisting,
        out CharacterDocument.CustomImprovementType eType)
    {
        switch (objExisting.ImprovementType)
        {
            case ImprovementType.Attribute: eType = CharacterDocument.CustomImprovementType.Attribute; return true;
            case ImprovementType.Skill: eType = CharacterDocument.CustomImprovementType.Skill; return true;
            case ImprovementType.Initiative: eType = CharacterDocument.CustomImprovementType.Initiative; return true;
            case ImprovementType.MovementPercent: eType = CharacterDocument.CustomImprovementType.MovementPercent; return true;
            case ImprovementType.Concealability: eType = CharacterDocument.CustomImprovementType.Concealability; return true;
            case ImprovementType.UnarmedDv: eType = CharacterDocument.CustomImprovementType.UnarmedDv; return true;
            case ImprovementType.UnarmedAp: eType = CharacterDocument.CustomImprovementType.UnarmedAp; return true;
            case ImprovementType.Reach: eType = CharacterDocument.CustomImprovementType.Reach; return true;
            case ImprovementType.LifestyleCost: eType = CharacterDocument.CustomImprovementType.LifestyleCost; return true;
            case ImprovementType.ConditionMonitor:
                eType = objExisting.Target.ToLowerInvariant() switch
                {
                    "physical" => CharacterDocument.CustomImprovementType.ConditionMonitorPhysical,
                    "stun" => CharacterDocument.CustomImprovementType.ConditionMonitorStun,
                    "threshold" => CharacterDocument.CustomImprovementType.ConditionMonitorThreshold,
                    "thresholdoffset" => CharacterDocument.CustomImprovementType.ConditionMonitorThresholdOffset,
                    _ => default
                };
                return objExisting.Target.Equals("physical", System.StringComparison.OrdinalIgnoreCase)
                    || objExisting.Target.Equals("stun", System.StringComparison.OrdinalIgnoreCase)
                    || objExisting.Target.Equals("threshold", System.StringComparison.OrdinalIgnoreCase)
                    || objExisting.Target.Equals("thresholdoffset", System.StringComparison.OrdinalIgnoreCase);
            default:
                eType = default;
                return false;
        }
    }

    private void OnTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TypeBox.SelectedIndex < 0)
            return;

        CharacterDocument.CustomImprovementType eType = SelectedType;
        bool blnIsAttribute = eType == CharacterDocument.CustomImprovementType.Attribute;
        bool blnIsSkill = eType == CharacterDocument.CustomImprovementType.Skill;
        bool blnNeedsSelect = blnIsAttribute || blnIsSkill;

        SelectLabel.IsVisible = blnNeedsSelect;
        SelectBox.IsVisible = blnNeedsSelect;
        SelectBox.ItemsSource = blnIsAttribute ? s_attributeCodes
            : blnIsSkill ? (_character?.GetActiveSkillNames() ?? new List<string>())
            : new List<string>();
        if (blnNeedsSelect)
            SelectBox.SelectedIndex = 0;

        MinLabel.IsVisible = blnIsAttribute;
        MinBox.IsVisible = blnIsAttribute;
        MaxLabel.IsVisible = blnIsAttribute;
        MaxBox.IsVisible = blnIsAttribute;
        AugLabel.IsVisible = blnIsAttribute;
        AugBox.IsVisible = blnIsAttribute;
        ValLabel.Text = blnIsAttribute ? "Wert (Erw. Wert):" : "Wert:";

        ApplyToRatingBox.IsVisible = blnIsSkill;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_PleaseEnterNameMessage");
            return;
        }

        CharacterDocument.CustomImprovementType eType = SelectedType;
        bool blnNeedsSelect = eType is CharacterDocument.CustomImprovementType.Attribute
            or CharacterDocument.CustomImprovementType.Skill;
        if (blnNeedsSelect && SelectBox.SelectedItem is not string)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_SelectionRequired");
            return;
        }

        ResultType = eType;
        ResultName = NameBox.Text.Trim();
        ResultVal = (int)(ValBox.Value ?? 0);
        ResultMin = (int)(MinBox.Value ?? 0);
        ResultMax = (int)(MaxBox.Value ?? 0);
        ResultAug = (int)(AugBox.Value ?? 0);
        ResultSelect = blnNeedsSelect ? (string)SelectBox.SelectedItem! : string.Empty;
        ResultApplyToRating = ApplyToRatingBox.IsChecked == true;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
