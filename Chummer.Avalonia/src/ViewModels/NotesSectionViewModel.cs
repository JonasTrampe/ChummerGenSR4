#nullable enable
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class NotesSectionViewModel : ViewModelBase
{
    private CharacterDocument? _character;
    private string _notes = string.Empty;

    public string Notes
    {
        get => _notes;
        set
        {
            if (!SetField(ref _notes, value))
                return;
            if (_character != null)
                _character.Notes = value;
        }
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        Notes = character.Notes;
    }
}
