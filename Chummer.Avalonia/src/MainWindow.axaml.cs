using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using KarmaGpDialog = Chummer.NewUI.Dialogs.KarmaGpDialog;
using MetatypeDialog = Chummer.NewUI.Dialogs.MetatypeDialog;
using SettingsProfileDialog = Chummer.NewUI.Dialogs.SettingsProfileDialog;
using SheetPreviewDialog = Chummer.NewUI.Dialogs.SheetPreviewDialog;

namespace Chummer.NewUI;

public partial class MainWindow : Window
{
    private readonly CharacterFileService _characterFiles = new CharacterFileService();
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        DataContext = new MainWindowViewModel();
    }

    // Demo wiring only, for the look-and-feel spike: chains the three real character-creation
    // dialogs (settings profile -> GP count -> metatype) the same way the real app's "Neu"
    // toolbar button does.
    private async void OnNewCharacterClick(object? sender, RoutedEventArgs e)
    {
        var settingsDialog = new SettingsProfileDialog();
        await settingsDialog.ShowDialog(this);

        var gpDialog = new KarmaGpDialog();
        await gpDialog.ShowDialog(this);

        var metatypeDialog = new MetatypeDialog();
        await metatypeDialog.ShowDialog(this);
    }

    private async void OnOpenCharacterClick(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Chummer character",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Chummer characters") { Patterns = new[] { "*.chum", "*.xml" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count > 0)
        {
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                CharacterDocument character = _characterFiles.Load(stream, files[0].Name);
                ViewModel.AddOpenCharacter(character, files[0].TryGetLocalPath());
            }
            catch (Exception ex)
            {
                // CharacterFileService already traced the failure; surface it in the UI too -
                // silently swallowing it here made a bad file look identical to "nothing happened".
                ViewModel.ReportError("Fehler beim Öffnen von " + files[0].Name + ": " + ex.Message);
            }
        }
    }

    private void OnCloseCharacterTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: OpenCharacterTab tab })
            return;

        ViewModel.CloseCharacter(tab);
    }

    private async void OnSaveCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedOpenCharacter is not { } tab)
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Chummer character",
            DefaultExtension = "chum",
            SuggestedFileName = tab.Character.Name,
            FileTypeChoices = [new FilePickerFileType("Chummer characters") { Patterns = ["*.chum"] }],
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            _characterFiles.Save(tab.Character, stream, file.Name);
            string? strLocalPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(strLocalPath))
                ViewModel.RememberSavedPath(strLocalPath);
        }
    }

    private async void OnOpenRecentCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: RecentCharacterEntryViewModel entry })
            return;

        try
        {
            await using var stream = File.OpenRead(entry.FilePath);
            CharacterDocument character = _characterFiles.Load(stream, Path.GetFileName(entry.FilePath));
            ViewModel.AddOpenCharacter(character, entry.FilePath);
        }
        catch (Exception ex)
        {
            ViewModel.RemoveRecentCharacter(entry.FilePath, entry.IsSticky);
            ViewModel.ReportError("Fehler beim Öffnen von " + entry.FilePath + ": " + ex.Message);
        }
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SheetPreviewDialog();
        await dialog.ShowDialog(this);
    }

    private void OnCloseWindowClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
