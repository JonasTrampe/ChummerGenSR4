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
    private void OnAddPhysicalDamageClick(object? sender, RoutedEventArgs e) => ViewModel.AddPhysicalDamage();
    private void OnHealPhysicalDamageClick(object? sender, RoutedEventArgs e) => ViewModel.HealPhysicalDamage();
    private void OnAddStunDamageClick(object? sender, RoutedEventArgs e) => ViewModel.AddStunDamage();
    private void OnHealStunDamageClick(object? sender, RoutedEventArgs e) => ViewModel.HealStunDamage();
    private void OnFinalizeCreationClick(object? sender, RoutedEventArgs e) => ViewModel.FinalizeCreation();
}
