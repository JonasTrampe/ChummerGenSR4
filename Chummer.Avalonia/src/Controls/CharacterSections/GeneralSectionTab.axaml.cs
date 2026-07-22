using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using QualityDialog = Chummer.NewUI.Dialogs.QualityDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

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

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (_character == null)
            return;

        var dialog = new QualityDialog(_character);
        bool? added = await dialog.ShowDialog<bool?>(window);
        if (added == true && dialog.SelectedQuality != null)
        {
            _character.AddQuality(dialog.SelectedQuality.Name, dialog.SelectedQuality.Category);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteQualityClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedQualityNode?.Parent == null)
            return;

        var quality = ViewModel.SelectedQualityNode;
        if (_character.RemoveQuality(quality.SourceName, quality.Category, quality.Rating))
            ViewModel.LoadCharacter(_character);
    }

    private void OnAddContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        _character.AddContact("Neue Connection", "1", "1", blnEnemy: false);
        ViewModel.LoadCharacter(_character);
    }

    private void OnAddEnemyClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        _character.AddContact("Neuer Feind", "1", "1", blnEnemy: true);
        ViewModel.LoadCharacter(_character);
    }

    private void OnDeleteContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not Button { Tag: int intContactId })
            return;

        if (_character.RemoveContact(intContactId))
            ViewModel.LoadCharacter(_character);
    }
}
