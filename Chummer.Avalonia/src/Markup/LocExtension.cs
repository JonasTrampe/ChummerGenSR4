using System;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Markup;

/// <summary>
/// XAML markup extension that resolves a Chummer language-catalog key (see
/// Chummer.Core/data/lang/*.xml) to its translated string via <see cref="LanguageManager"/> -
/// the first piece of UI actually driven by the language catalog instead of a hardcoded literal.
///
/// Resolved once, at XAML load time (when the control tree is built), not re-evaluated on a
/// runtime language change - OptionsDialog's language switch already requires reopening open
/// character windows to see data-translation changes take effect (see its own VerifyStrings
/// call), so this follows the same "restart to see string changes" convention rather than adding
/// live-rebind machinery that nothing else in this port has yet.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key) || !LanguageManager.Instance.Loaded)
            return Key;

        try
        {
            return LanguageManager.Instance.GetString(Key);
        }
        catch (Exception)
        {
            // Key not present in the loaded catalog - fall back to the key itself rather than
            // crashing XAML load, matching GetString's own dictionary-indexer failure mode.
            return Key;
        }
    }
}
