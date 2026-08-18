using Avalonia.Controls;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

// Split by concern into partial-class files: .Qualities.cs (add/swap/delete Quality + PACKS
// Kits, and their selecttext/selectskill/selectattribute/mentor-spirit picker helpers),
// .Attributes.cs (raise/Burn Edge), .Contacts.cs (contact/enemy CRUD). This file keeps the
// shared construction/load surface.
public partial class GeneralSectionTab : UserControl
{
    private CharacterDocument? _character;

    public GeneralSectionViewModel ViewModel { get; } = new();

    public GeneralSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }
}
