#nullable enable
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Read-only character notes until the shared Core write path exists.</summary>
public sealed class NotesSectionViewModel : ViewModelBase
{
    private string _notes = string.Empty;

    public string Notes
    {
        get => _notes;
        private set => SetField(ref _notes, value);
    }

    public void LoadCharacter(CharacterDocument character) => Notes = character.Notes;
}
