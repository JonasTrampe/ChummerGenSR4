using System;
using Chummer.Core;

namespace Chummer.NewUI.Controls;

/// <summary>Persists the application-wide arrangement used by every open character workspace.</summary>
public enum CharacterLayout
{
    Classic,
    FamiliarWorkbench,
    RunnersLedger
}

public static class CharacterLayoutSettings
{
    private const string RegistryPath = "Software\\Chummer";
    private const string ValueName = "characterlayout";

    public static CharacterLayout Current { get; private set; } = Load();

    public static void Save(CharacterLayout layout)
    {
        Current = layout;
        SettingsStore.CurrentUser.CreateSubKey(RegistryPath).SetValue(ValueName, ToStorageValue(layout));
    }

    private static CharacterLayout Load()
    {
        string? strValue = SettingsStore.CurrentUser.CreateSubKey(RegistryPath).GetValue(ValueName) as string;
        return Parse(strValue);
    }

    public static CharacterLayout Parse(string? strValue)
    {
        return strValue switch
        {
            "workbench" => CharacterLayout.FamiliarWorkbench,
            "ledger" => CharacterLayout.RunnersLedger,
            _ => CharacterLayout.Classic
        };
    }

    public static string ToStorageValue(CharacterLayout layout)
    {
        return layout switch
        {
            CharacterLayout.FamiliarWorkbench => "workbench",
            CharacterLayout.RunnersLedger => "ledger",
            _ => "classic"
        };
    }
}
