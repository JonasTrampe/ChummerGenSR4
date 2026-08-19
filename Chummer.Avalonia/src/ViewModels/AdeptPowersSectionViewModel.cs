using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class AdeptPowerRowViewModel
{
    /// <summary>Raw saved name (no Extra suffix) - what RemoveAdeptPower expects, as opposed to
    /// PowerName which is the DisplayName shown in the row.</summary>
    public string Name { get; }
    public int PowerId { get; }
    public string Notes { get; }

    public string PowerName { get; }
    public int PowerLevel { get; }
    public string PricePerLevel { get; }
    public string TotalCost { get; }
    public bool IsWayOfTheAdept { get; }
    public bool IsMagicFocus { get; }
    public bool IsNudEnabled { get; }

    public AdeptPowerRowViewModel(CharacterPowerData power)
    {
        Name = power.Name;
        PowerId = power.PowerId;
        Notes = power.Notes;
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

    private string _powerPointsText = string.Empty;
    public string PowerPointsText
    {
        get => _powerPointsText;
        private set => SetField(ref _powerPointsText, value);
    }

    private string _powerPointsTooltip = string.Empty;
    public string PowerPointsTooltip
    {
        get => _powerPointsTooltip;
        private set => SetField(ref _powerPointsTooltip, value);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        Powers.Clear();
        foreach (CharacterPowerData power in character.AdeptPowers)
            Powers.Add(new AdeptPowerRowViewModel(power));

        CharacterDerivedValueData points = character.AdeptPowerPoints;
        PowerPointsText = App.LanguageCatalog.GetString("Label_PowerPoints") + " " + points.Value
            + App.LanguageCatalog.GetString("UI_PowerPointsRemainingSuffix");
        PowerPointsTooltip = points.Tooltip;
    }
}
