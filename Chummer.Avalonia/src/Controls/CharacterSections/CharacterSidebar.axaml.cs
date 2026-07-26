using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CharacterSidebar : UserControl
{
    public CharacterSidebarViewModel ViewModel { get; } = new();

    public CharacterSidebar()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);

    private void OnRegainEdgeClick(object? sender, RoutedEventArgs e) => ViewModel.RegainEdge();
    private void OnSpendEdgeClick(object? sender, RoutedEventArgs e) => ViewModel.SpendEdge();
}
