#nullable enable
using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Presentation of the Core Improvement records for one character.</summary>
public sealed class ImprovementsSectionViewModel : ViewModelBase
{
    private ImprovementRowViewModel? _selectedImprovement;

    public ObservableCollection<ImprovementRowViewModel> Improvements { get; } = new();

    public ImprovementRowViewModel? SelectedImprovement
    {
        get => _selectedImprovement;
        set => SetField(ref _selectedImprovement, value);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        Improvements.Clear();
        foreach (Improvement improvement in character.Improvements)
            Improvements.Add(new ImprovementRowViewModel(improvement));
        SelectedImprovement = Improvements.Count == 0 ? null : Improvements[0];
    }
}

public sealed class ImprovementRowViewModel
{
    public string Title { get; }
    public string Type { get; }
    public string Target { get; }
    public string Value { get; }
    public string Source { get; }
    public bool IsEnabled { get; }
    public string Group { get; }

    /// <summary>The raw &lt;sourcename&gt; - identity for CharacterDocument.RemoveCustomImprovement.</summary>
    public string SourceName { get; }

    /// <summary>Only "Custom" (manually-created, see frmCreateImprovement.cs) Improvements can be
    /// deleted from this list - everything else is a side effect of some other owned item
    /// (Quality/Cyberware/Metamagic/...) and must be removed by removing that item instead, same
    /// as legacy's own cmdDeleteImprovement_Click.</summary>
    public bool IsCustom { get; }
    public ImprovementType ImprovementType { get; }
    public int RawValue { get; }
    public int Minimum { get; }
    public int Maximum { get; }
    public int AugmentedMaximum { get; }
    public bool AddToRating { get; }
    public bool CanEdit { get; }

    public ImprovementRowViewModel(Improvement improvement)
    {
        Type = improvement.Type.ToString();
        Target = improvement.ImprovedName;
        Value = improvement.Value.ToString("+#;-#;0");
        Source = string.IsNullOrEmpty(improvement.SourceName) ? improvement.Source.ToString() : improvement.SourceName;
        SourceName = improvement.SourceName;
        IsCustom = improvement.Source == ImprovementSource.Custom;
        IsEnabled = improvement.Enabled;
        Group = improvement.CustomGroup;
        ImprovementType = improvement.Type;
        RawValue = improvement.Value;
        Minimum = improvement.Minimum;
        Maximum = improvement.Maximum;
        AugmentedMaximum = improvement.AugmentedMaximum;
        AddToRating = improvement.AddToRating;
        CanEdit = IsCustom && IsEditable(improvement);
        Title = string.IsNullOrEmpty(Target) ? Source + ": " + Type : Source + ": " + Type + " (" + Target + ")";
    }

    private static bool IsEditable(Improvement improvement) => improvement.Type switch
    {
        ImprovementType.Attribute or ImprovementType.Skill or ImprovementType.Initiative
            or ImprovementType.MovementPercent or ImprovementType.Concealability or ImprovementType.UnarmedDv
            or ImprovementType.UnarmedAp or ImprovementType.Reach or ImprovementType.LifestyleCost => true,
        ImprovementType.ConditionMonitor => improvement.ImprovedName.Equals("physical", System.StringComparison.OrdinalIgnoreCase)
            || improvement.ImprovedName.Equals("stun", System.StringComparison.OrdinalIgnoreCase)
            || improvement.ImprovedName.Equals("threshold", System.StringComparison.OrdinalIgnoreCase)
            || improvement.ImprovedName.Equals("thresholdoffset", System.StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
