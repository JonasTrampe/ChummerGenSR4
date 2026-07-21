using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SkillsSectionTab : UserControl
{
    public SkillsSectionViewModel ViewModel { get; } = new();

    public SkillsSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);
}
