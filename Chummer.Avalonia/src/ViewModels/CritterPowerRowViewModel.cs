using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CritterPowerRowViewModel
{
    public string Guid { get; }
    public string Label { get; }
    public string Value { get; }
    public string Notes { get; }

    public CritterPowerRowViewModel(CharacterCritterPowerData power)
    {
        Guid = power.Guid;
        Label = power.DisplayName;
        Value = power.Points;
        Notes = power.Notes;
    }
}
