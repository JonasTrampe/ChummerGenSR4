using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SpiritRowViewModel
{
    public string Label { get; }
    public string Value { get; }

    /// <summary>Fields needed to identify this row for RemoveSpirit - see
    /// CharacterDocument.RemoveSpirit's first-occurrence-by-fields matching.</summary>
    public string Name { get; }
    public string Type { get; }
    public string Force { get; }
    public int SpiritId { get; }
    public string Notes { get; }

    public SpiritRowViewModel(CharacterSpiritData spirit)
    {
        Label = spirit.DisplayName + " (Kraft " + spirit.Force + (spirit.Bound ? ", gebunden" : "") + "):";
        Value = spirit.Services;
        Name = spirit.Name;
        Type = spirit.Type;
        Force = spirit.Force;
        SpiritId = spirit.SpiritId;
        Notes = spirit.Notes;
    }
}
