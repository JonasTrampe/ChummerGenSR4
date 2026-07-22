using Chummer.Core;
using Chummer.NewUI.Controls;

namespace Chummer.NewUI;

/// <summary>Presentation identity for one independently open character document.</summary>
public sealed class OpenCharacterTab
{
    public CharacterDocument Character { get; }
    public string Title { get; }
    public string? SourcePath { get; }

    /// <summary>The populated per-character view shown as this tab's content.</summary>
    public CharacterTab Content { get; }

    public OpenCharacterTab(CharacterDocument character, string? sourcePath = null)
    {
        Character = character;
        SourcePath = sourcePath;
        Title = string.IsNullOrEmpty(character.Name) ? character.DisplayName : character.Name;
        Content = new CharacterTab();
        Content.LoadCharacter(character);
    }
}
