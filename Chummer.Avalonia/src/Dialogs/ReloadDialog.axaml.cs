using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmReload.cs: picks which owned Ammunition Gear item (and how many
/// rounds) to load into a Weapon.</summary>
public partial class ReloadDialog : Window
{
    private readonly List<CharacterDocument.WeaponAmmoOption> _lstAmmo;

    public ReloadDialog() : this(null, Guid.Empty) { }

    public ReloadDialog(CharacterDocument? character, Guid guiWeaponId)
    {
        InitializeComponent();

        _lstAmmo = character != null ? character.GetWeaponAmmoOptions(guiWeaponId).ToList() : new List<CharacterDocument.WeaponAmmoOption>();
        AmmoBox.ItemsSource = _lstAmmo.Select(a => a.Name + " x" + a.Quantity).ToList();
        AmmoBox.SelectedIndex = _lstAmmo.Count > 0 ? 0 : -1;

        List<int> lstCounts = character != null ? character.GetWeaponAmmoCapacityChoices(guiWeaponId).ToList() : new List<int>();
        if (lstCounts.Count == 0)
            lstCounts.Add(1);
        CountBox.ItemsSource = lstCounts;
        CountBox.SelectedIndex = 0;
    }

    public int SelectedAmmoGearId { get; private set; } = -1;
    public int SelectedCount { get; private set; }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        int intIndex = AmmoBox.SelectedIndex;
        if (intIndex < 0 || intIndex >= _lstAmmo.Count)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_NoAmmoAvailableMessage");
            return;
        }
        if (CountBox.SelectedItem is not int intCount)
        {
            ErrorText.Text = "Eine Menge ist erforderlich.";
            return;
        }

        SelectedAmmoGearId = _lstAmmo[intIndex].GearId;
        SelectedCount = intCount;
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
