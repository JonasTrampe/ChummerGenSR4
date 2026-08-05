using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

public sealed class SettingsProfileSelection
{
    public string FileName { get; init; } = "default.xml";
    public string DisplayName { get; init; } = "Default Settings";
    public string BuildMethod { get; init; } = "BP";
    public int BuildPoints { get; init; } = 400;
    public int MaxAvailability { get; init; } = 12;
}

public sealed class BuildSelection
{
    public string BuildMethod { get; init; } = "BP";
    public int BuildPoints { get; init; } = 400;
    public int MaxAvailability { get; init; } = 12;
    public bool IgnoreCreationRules { get; init; }
}

public sealed class MetatypeSelection
{
    public NewCharacterMetatype Metatype { get; init; } = new();
    public string MetavariantName { get; init; } = string.Empty;

    /// <summary>"None", "Magician", "Adept", or "Technomancer" - which of the three mutually
    /// exclusive SR4 magic/resonance paths (if any) this character starts with.</summary>
    public string MagicType { get; init; } = "None";
}
