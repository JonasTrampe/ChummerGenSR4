using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Chummer.Core;

namespace Chummer.NewUI;

public partial class App : Application
{
    /// <summary>UI-string lookup shared with the rest of Chummer.Core (CharacterOptions, XmlManager, ...).</summary>
    public static LanguageManager LanguageCatalog => LanguageManager.Instance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplySkin(AppSkinSettings.Current);

        // GlobalOptions' own language default ("en-us") is shared with the legacy WinForms app,
        // which never auto-detected a language either - always defaulting to English regardless
        // of OS locale until the user manually picked one in Options. Match the OS/environment's
        // language instead on first run, so a German or French system gets a translated UI out of
        // the box like the platform's other apps do. Only applies when nothing was ever explicitly
        // persisted for "language", so an existing saved preference (including an explicit
        // "en-us") is always respected.
        if (string.IsNullOrEmpty(SettingsStore.CurrentUser.CreateSubKey("Software\\Chummer").GetValue("language") as string))
            GlobalOptions.Instance.Language = LanguageManager.DetectOsLanguageCode();

        LanguageCatalog.Load(GlobalOptions.Instance.Language);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Keep this at desktop-lifetime construction time so every supported desktop backend
            // gets a real fullscreen native window before it is first shown, matching the legacy
            // StartupFullscreen preference rather than merely saving an unused checkbox value.
            desktop.MainWindow = new MainWindow
            {
                WindowState = GlobalOptions.Instance.StartupFullscreen
                    ? WindowState.FullScreen
                    : WindowState.Normal
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Swaps the active brush set (App.axaml's Application.Resources merges exactly one
    /// skin dictionary at index 0) so every DynamicResource-bound Setter across the app repaints
    /// live - no restart needed when the user changes Options -&gt; Appearance -&gt; Skin. Also
    /// flips RequestedThemeVariant so Fluent's own implicit control defaults (Menu/MenuItem text,
    /// ScrollBar, and anything else App.axaml doesn't explicitly style) resolve against the right
    /// variant instead of staying Light-computed under Midnight - the cause of the native menu
    /// bar rendering unreadably dark-on-dark before this.</summary>
    public static void ApplySkin(AppSkin skin)
    {
        var dictionaries = Current!.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceInclude((System.Uri?)null)
        {
            Source = new System.Uri(AppSkinSettings.ResourceAvaresPath(skin))
        };
        Current!.RequestedThemeVariant = skin == AppSkin.Midnight
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
    }
}
