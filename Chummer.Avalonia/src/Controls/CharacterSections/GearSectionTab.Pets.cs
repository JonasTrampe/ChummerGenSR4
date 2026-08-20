using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    private void OnAddPetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;
        _character.AddPet("Neues Haustier");
        ViewModel.LoadCharacter(_character);
    }

    private void OnPetsListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            OnDeletePetClick(sender, e);
    }

    private async void OnDeletePetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedPet == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteContact"))
            return;
        if (_character.RemoveContact(ViewModel.SelectedPet.ContactId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnLinkPetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedPet == null || TopLevel.GetTopLevel(this) is not { StorageProvider: { } storage })
            return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = App.LanguageCatalog.GetString("UI_SelectCompanionCharacterTitle"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Chummer character") { Patterns = ["*.chum"] }]
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;
        _character.UpdateContactFile(ViewModel.SelectedPet.ContactId, path,
            Path.GetRelativePath(AppContext.BaseDirectory, path));
        ViewModel.LoadCharacter(_character);
    }

    /// <summary>Clears a Pet's linked companion file without deleting the Pet itself - matches
    /// PetControl.cs's tsRemoveCharacter context-menu entry.</summary>
    private void OnUnlinkPetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedPet is not { HasLinkedCharacter: true } selected)
            return;
        if (_character.UpdateContactFile(selected.ContactId, string.Empty, string.Empty))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Opens a Pet's linked companion file in a new tab - matches PetControl.cs's
    /// tsContactOpen context-menu entry.</summary>
    private void OnOpenLinkedPetCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPet is not { HasLinkedCharacter: true } selected
            || !File.Exists(selected.FileName)
            || TopLevel.GetTopLevel(this) is not MainWindow window)
            return;

        using var stream = File.OpenRead(selected.FileName);
        CharacterDocument linked = new CharacterFileService().Load(stream, Path.GetFileName(selected.FileName));
        window.LoadCharacterIntoTabs(linked, selected.FileName);
    }
}
