using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chummer;
using System;
using System.IO;

namespace Chummer.AvaloniaSpike;

public partial class App : Application
{
    public static LanguageStringCatalog LanguageCatalog { get; } = new LanguageStringCatalog();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        string strLanguageDirectory = Path.Combine(AppContext.BaseDirectory, "lang");
        if (File.Exists(Path.Combine(strLanguageDirectory, "en-us.xml")))
            LanguageCatalog.LoadBase(strLanguageDirectory);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
