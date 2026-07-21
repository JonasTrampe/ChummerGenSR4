using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;
using KarmaGpDialog = Chummer.NewUI.Dialogs.KarmaGpDialog;
using MetatypeDialog = Chummer.NewUI.Dialogs.MetatypeDialog;
using SettingsProfileDialog = Chummer.NewUI.Dialogs.SettingsProfileDialog;
using SheetPreviewDialog = Chummer.NewUI.Dialogs.SheetPreviewDialog;

namespace Chummer.NewUI;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly CharacterFileService _characterFiles = new CharacterFileService();
    private OpenCharacterTab? _selectedOpenCharacter;

    public ObservableCollection<OpenCharacterTab> OpenCharacters { get; } = new();

    public OpenCharacterTab? SelectedOpenCharacter
    {
        get => _selectedOpenCharacter;
        set
        {
            if (ReferenceEquals(_selectedOpenCharacter, value))
                return;

            _selectedOpenCharacter = value;
            OnPropertyChanged();
            ActivateCharacter(value?.Character);
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        DataContext = this;
        try
        {
            Title = "Chummer - [" + App.LanguageCatalog.GetString("Title_CareerMode") + " (Default Settings)]";
        }
        catch
        {
            // The spike remains runnable when language resources are absent during design-time use.
        }
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
                var tab = new OpenCharacterTab(character);
                OpenCharacters.Add(tab);
                SelectedOpenCharacter = tab;
                SetErrorStatus(null);
            }
            catch (Exception ex)
            {
                // CharacterFileService already traced the failure; surface it in the UI too -
                // silently swallowing it here made a bad file look identical to "nothing happened".
                SetErrorStatus("Fehler beim Öffnen von " + files[0].Name + ": " + ex.Message);
            }
        }
    }

    private void SetErrorStatus(string? message)
    {
        var errorStatus = this.FindControl<TextBlock>("ErrorStatus")!;
        errorStatus.Text = message;
        errorStatus.IsVisible = !string.IsNullOrEmpty(message);
    }

    private void ActivateCharacter(CharacterDocument? character)
    {
        if (character is null)
        {
            Title = "Chummer";
            this.FindControl<TextBlock>("KarmaStatus")!.Text = "Karma: —";
            this.FindControl<TextBlock>("NuyenStatus")!.Text = "Nuyen: —";
            return;
        }

        this.FindControl<TextBlock>("KarmaStatus")!.Text = "Karma: " + character.Karma;
        this.FindControl<TextBlock>("NuyenStatus")!.Text = "Nuyen: " + character.Nuyen + "¥";
        Title = "Chummer - " + character.Name;
    }

    private void OnCloseCharacterTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: OpenCharacterTab tab })
            return;

        int index = OpenCharacters.IndexOf(tab);
        bool closingActiveCharacter = ReferenceEquals(SelectedOpenCharacter, tab);
        OpenCharacters.Remove(tab);
        if (!closingActiveCharacter)
            return;

        SelectedOpenCharacter = OpenCharacters.Count == 0
            ? null
            : OpenCharacters[Math.Min(index, OpenCharacters.Count - 1)];
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async void OnSaveCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedOpenCharacter is not { } tab)
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
