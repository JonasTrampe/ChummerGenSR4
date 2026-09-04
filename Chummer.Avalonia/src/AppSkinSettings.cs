using Chummer.Core;

namespace Chummer.NewUI;

/// <summary>The application-wide color skin, independent of <see cref="Controls.CharacterLayout"/>
/// (which arranges regions; this only recolors them). See Themes/LightTheme.axaml and
/// Themes/MidnightTheme.axaml for the brush sets each value selects.</summary>
public enum AppSkin
{
    Light,
    Midnight
}

public static class AppSkinSettings
{
    private const string RegistryPath = "Software\\Chummer";
    private const string ValueName = "appskin";

    public static AppSkin Current { get; private set; } = Load();

    public static void Save(AppSkin skin)
    {
        Current = skin;
        SettingsStore.CurrentUser.CreateSubKey(RegistryPath).SetValue(ValueName, ToStorageValue(skin));
    }

    private static AppSkin Load()
    {
        string? strValue = SettingsStore.CurrentUser.CreateSubKey(RegistryPath).GetValue(ValueName) as string;
        return Parse(strValue);
    }

    public static AppSkin Parse(string? strValue)
    {
        return strValue switch
        {
            "midnight" => AppSkin.Midnight,
            _ => AppSkin.Light
        };
    }

    public static string ToStorageValue(AppSkin skin)
    {
        return skin switch
        {
            AppSkin.Midnight => "midnight",
            _ => "light"
        };
    }

    public static string ResourceAvaresPath(AppSkin skin)
    {
        return skin switch
        {
            AppSkin.Midnight => "avares://Chummer.Avalonia/src/Themes/MidnightTheme.axaml",
            _ => "avares://Chummer.Avalonia/src/Themes/LightTheme.axaml"
        };
    }
}
