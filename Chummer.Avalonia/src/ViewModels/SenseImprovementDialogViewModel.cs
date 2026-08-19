using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SenseImprovementOptionViewModel
{
    public SenseImprovementOptionViewModel(CharacterDocument.SenseImprovementOption option)
    {
        Name = option.Name;
        DisplayName = option.DisplayName;
    }

    public string Name { get; }
    public string DisplayName { get; }
}

/// <summary>Backs SenseImprovementDialog - lists the Cyberware/Bioware/Gear options a
/// selectsenseware-granting Adept Power (e.g. "Improved Sense") offers.</summary>
public sealed class SenseImprovementDialogViewModel : ViewModelBase
{
    public ObservableCollection<SenseImprovementOptionViewModel> Options { get; } = new();

    private SenseImprovementOptionViewModel? _selectedOption;
    public SenseImprovementOptionViewModel? SelectedOption
    {
        get => _selectedOption;
        set => SetField(ref _selectedOption, value);
    }

    public void LoadOptions(CharacterDocument character, string strPowerName)
    {
        Options.Clear();
        foreach (var option in character.GetSenseImprovementOptions(strPowerName))
            Options.Add(new SenseImprovementOptionViewModel(option));
        SelectedOption = Options.Count > 0 ? Options[0] : null;
    }
}
