using System.Threading.Tasks;
using Avalonia.Controls;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Centralizes the legacy ConfirmKarmaExpense setting at the UI boundary. Core career
/// mutations remain usable by non-interactive callers; interactive callers ask before spending.</summary>
public static class KarmaExpenseConfirmation
{
    public static Task<bool> ConfirmAsync(Window owner, CharacterDocument character, string message) =>
        !character.ConfirmKarmaExpenseEnabled
            ? Task.FromResult(true)
            : new ConfirmationDialog(App.LanguageCatalog.GetString("MessageTitle_ConfirmKarmaExpense"), message)
                .ShowDialog<bool>(owner);
}
