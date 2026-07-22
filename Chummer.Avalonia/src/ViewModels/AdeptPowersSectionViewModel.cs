using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class AdeptPowerRowViewModel
{
    public string PowerName { get; }
    public int PowerLevel { get; }
    public string PricePerLevel { get; }
    public string TotalCost { get; }
    public bool IsWayOfTheAdept { get; }
    public bool IsMagicFocus { get; }
    public bool IsNudEnabled { get; }

    public AdeptPowerRowViewModel(CharacterPowerData power)
    {
        PowerName = power.DisplayName;
        PowerLevel = int.TryParse(power.Rating, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out int intRating)
            ? intRating
            : 0;
        PricePerLevel = string.IsNullOrEmpty(power.CalculatedPointsPerLevel)
            ? string.Empty
            : power.CalculatedPointsPerLevel + "/Stufe";
        TotalCost = string.IsNullOrEmpty(power.TotalPoints)
            ? string.Empty
            : "= " + power.TotalPoints;
        IsWayOfTheAdept = power.DiscountedAdeptWay;
        IsMagicFocus = power.DiscountedGeas;
        IsNudEnabled = true;
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
