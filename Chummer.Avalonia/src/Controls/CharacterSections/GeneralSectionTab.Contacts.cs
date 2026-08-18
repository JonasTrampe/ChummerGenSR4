using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;
using ContactGroupDialog = Chummer.NewUI.Dialogs.ContactGroupDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GeneralSectionTab
{
    private void OnAddContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        if (_character.AddContact("Neue Connection", "1", "1", blnEnemy: false))
            ViewModel.LoadCharacter(_character);
    }

    private void OnAddEnemyClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;

        if (_character.AddContact("Neuer Feind", "1", "1", blnEnemy: true))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteContactClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not Button { Tag: int intContactId }
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        string strKey = ViewModel.Enemies.Any(c => c.ContactId == intContactId)
            ? "Message_DeleteEnemy" : "Message_DeleteContact";
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, strKey))
            return;
        if (_character.RemoveContact(intContactId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditContactNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || sender is not Button { Tag: ContactRowViewModel contact })
            return;

        var dialog = new ContactNotesDialog { Notes = contact.Notes };
        bool? saved = await dialog.ShowDialog<bool?>(window);
        if (saved == true)
            contact.Notes = dialog.Notes;
    }

    private async void OnEditContactGroupClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || sender is not Button { Tag: ContactRowViewModel contact })
            return;

        var dialog = new ContactGroupDialog();
        dialog.ViewModel.LoadFrom(contact);
        bool? saved = await dialog.ShowDialog<bool?>(window);
        if (saved == true)
        {
            if (contact.UpdateGroup(dialog.ViewModel.GroupName, dialog.ViewModel.SelectedMembership?.Value ?? 0,
                dialog.ViewModel.SelectedAreaOfInfluence?.Value ?? 0, dialog.ViewModel.SelectedMagicalResources?.Value ?? 0,
                dialog.ViewModel.SelectedMatrixResources?.Value ?? 0))
                ViewModel.LoadCharacter(_character);
        }
    }
}
