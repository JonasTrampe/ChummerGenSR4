using Chummer;

namespace Chummer.AvaloniaSpike;

/// <summary>Presentation identity for one independently open character document.</summary>
public sealed class OpenCharacterTab
{
    public CharacterDocument Character { get; }
    public string Title { get; }

    public OpenCharacterTab(CharacterDocument character)
    {
        Character = character;
        Title = string.IsNullOrEmpty(character.Name) ? character.DisplayName : character.Name;
    }
}
