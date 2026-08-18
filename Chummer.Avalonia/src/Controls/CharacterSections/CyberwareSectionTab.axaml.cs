using Avalonia.Controls;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

// Split by concern into partial-class files: .Actions.cs (Add/Delete/Notes/Sell/Copy/Paste) and
// .DragDrop.cs (reorder/reparent gesture plumbing). This file keeps the shared construction/load
// surface.
public partial class CyberwareSectionTab : UserControl
{
    private CharacterDocument? _character;

    public CyberwareSectionViewModel ViewModel { get; } = new();

    public CyberwareSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
        SetUpCyberwareDragDrop();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }
}
