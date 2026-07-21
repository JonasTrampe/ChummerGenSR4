using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class InitiationSectionTab : UserControl
{
    public InitiationSectionViewModel ViewModel { get; } = new();

    public InitiationSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);
}
