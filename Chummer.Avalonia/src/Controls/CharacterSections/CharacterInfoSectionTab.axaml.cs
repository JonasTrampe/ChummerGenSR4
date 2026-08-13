using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CharacterInfoSectionTab : UserControl
{
    public CharacterInfoSectionViewModel ViewModel { get; } = new();

    public CharacterInfoSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character) => ViewModel.LoadCharacter(character);

    private async void OnChangeMugshotClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { StorageProvider: { } storage })
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = App.LanguageCatalog.GetString("UI_SelectPortraitTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"] }
            ]
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;

        ViewModel.SetMugshotFromFile(path);
    }

    private void OnDeleteMugshotClick(object? sender, RoutedEventArgs e) => ViewModel.ClearMugshot();
}
