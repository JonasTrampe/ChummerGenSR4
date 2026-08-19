using System.Threading.Tasks;
using Avalonia.Controls;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Centralizes the legacy ConfirmDelete setting at the UI boundary. Core mutation APIs
/// remain usable by non-interactive callers; every UI deletion asks only when its profile enables
/// confirmation.</summary>
public static class DeleteConfirmation
{
    public static Task<bool> ConfirmAsync(Window owner, CharacterDocument character, string messageKey) =>
        !character.ConfirmDeleteEnabled
            ? Task.FromResult(true)
            : new ConfirmationDialog(App.LanguageCatalog.GetString("MessageTitle_Delete"),
                App.LanguageCatalog.GetString(messageKey)).ShowDialog<bool>(owner);
}
