#nullable enable
using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Read-only presentation of the Core Improvement records for one character.</summary>
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

    /// <summary>The raw &lt;sourcename&gt; - identity for CharacterDocument.RemoveCustomImprovement.</summary>
    public string SourceName { get; }

    /// <summary>Only "Custom" (manually-created, see frmCreateImprovement.cs) Improvements can be
    /// deleted from this list - everything else is a side effect of some other owned item
    /// (Quality/Cyberware/Metamagic/...) and must be removed by removing that item instead, same
    /// as legacy's own cmdDeleteImprovement_Click.</summary>
    public bool IsCustom { get; }

    public ImprovementRowViewModel(Improvement improvement)
    {
        Type = improvement.Type.ToString();
        Target = improvement.ImprovedName;
        Value = improvement.Value.ToString("+#;-#;0");
        Source = string.IsNullOrEmpty(improvement.SourceName) ? improvement.Source.ToString() : improvement.SourceName;
        SourceName = improvement.SourceName;
        IsCustom = improvement.Source == ImprovementSource.Custom;
        IsEnabled = improvement.Enabled;
        Title = string.IsNullOrEmpty(Target) ? Source + ": " + Type : Source + ": " + Type + " (" + Target + ")";
    }
}
