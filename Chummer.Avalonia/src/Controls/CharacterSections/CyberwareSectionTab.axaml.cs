using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CyberwareSectionTab : UserControl
{
    public CyberwareSectionViewModel ViewModel { get; } = new();

    public CyberwareSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);
}
