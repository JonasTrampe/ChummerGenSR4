using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Chummer.NewUI.Localization;

internal static class AvaloniaLocalizationHelper
{
    public static void Apply(ILogical objRoot)
    {
        ApplySingle(objRoot);

        foreach (ILogical objChild in objRoot.LogicalChildren)
            Apply(objChild);
    }

    private static void ApplySingle(ILogical objLogical)
    {
        if (objLogical is Window objWindow && objWindow.Tag is string strWindowKey)
        {
            objWindow.Title = GetString(strWindowKey);
            return;
        }

        if (objLogical is TextBlock objTextBlock && objTextBlock.Tag is string strTextKey)
        {
            objTextBlock.Text = GetString(strTextKey);
            return;
        }

        if (objLogical is Button objButton && objButton.Tag is string strButtonKey)
        {
            objButton.Content = GetString(strButtonKey);
            return;
        }

        if (objLogical is CheckBox objCheckBox && objCheckBox.Tag is string strCheckBoxKey)
        {
            objCheckBox.Content = GetString(strCheckBoxKey);
            return;
        }

        if (objLogical is TabItem objTabItem && objTabItem.Tag is string strTabKey)
            objTabItem.Header = GetString(strTabKey);
    }

    private static string GetString(string strKey)
    {
        try
        {
            return App.LanguageCatalog.GetString(strKey);
        }
        catch
        {
            return strKey;
        }
    }
}
