using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class AdeptPowerRowViewModel
{
    public string Label { get; }
    public string Value { get; }

    public AdeptPowerRowViewModel(CharacterPowerData power)
    {
        Label = power.DisplayName + ":";
        Value = power.TotalPoints;
    }
}

public sealed class AdeptPowersSectionViewModel : ViewModelBase
{
    public ObservableCollection<AdeptPowerRowViewModel> Powers { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        Powers.Clear();
        foreach (CharacterPowerData power in character.AdeptPowers)
            Powers.Add(new AdeptPowerRowViewModel(power));
    }
}
