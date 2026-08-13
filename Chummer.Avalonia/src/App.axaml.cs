using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI;

public partial class App : Application
{
    /// <summary>UI-string lookup shared with the rest of Chummer.Core (CharacterOptions, XmlManager, ...).</summary>
    public static LanguageManager LanguageCatalog => LanguageManager.Instance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // GlobalOptions' own language default ("en-us") is shared with the legacy WinForms app -
        // this port's Avalonia views default to German placeholder text (matching this fork's
        // target audience), so without an override, files already converted to the {loc:Loc}
        // catalog lookup would show English while their still-hardcoded neighbors show German.
        // Only applies when nothing was ever explicitly persisted for "language", so an existing
        // saved preference (including an explicit "en-us") is always respected.
        if (string.IsNullOrEmpty(SettingsStore.CurrentUser.CreateSubKey("Software\\Chummer").GetValue("language") as string))
            GlobalOptions.Instance.Language = "de";

        LanguageCatalog.Load(GlobalOptions.Instance.Language);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
