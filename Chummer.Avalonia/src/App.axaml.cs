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

        LanguageCatalog.Load(GlobalOptions.Instance.Language);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
