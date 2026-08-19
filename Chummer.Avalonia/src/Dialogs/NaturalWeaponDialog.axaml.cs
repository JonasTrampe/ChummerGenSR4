using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmNaturalWeapon.cs: manually defines a melee Weapon (name, linked Combat
/// Active Skill, Damage Value base/modifier/type, AP, Reach) for adept/critter natural weapons not
/// in weapons.xml.</summary>
public partial class NaturalWeaponDialog : Window
{
    public NaturalWeaponDialog() : this(null) { }

    public NaturalWeaponDialog(CharacterDocument? character)
    {
        InitializeComponent();

        var lstSkills = new List<string>();
        if (character != null)
            lstSkills.AddRange(character.GetCombatActiveSkillNames());
        SkillBox.ItemsSource = lstSkills;
        SkillBox.SelectedIndex = lstSkills.Count > 0 ? 0 : -1;

        var lstDvBase = new List<string> { "(STR/2)" };
        lstDvBase.AddRange(Enumerable.Range(1, 20).Select(i => i.ToString()));
        DvBaseBox.ItemsSource = lstDvBase;
        DvBaseBox.SelectedIndex = 0;

        DvTypeBox.ItemsSource = new[] { "P", "S" };
        DvTypeBox.SelectedIndex = 0;
    }

    public string ResultName { get; private set; } = string.Empty;
    public string ResultSkill { get; private set; } = string.Empty;
    public string ResultDvBase { get; private set; } = string.Empty;
    public int ResultDvMod { get; private set; }
    public string ResultDvType { get; private set; } = string.Empty;
    public int ResultAp { get; private set; }
    public int ResultReach { get; private set; }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_PleaseEnterNameMessage");
            return;
        }
        if (SkillBox.SelectedItem is not string strSkill)
        {
            ErrorText.Text = App.LanguageCatalog.GetString("UI_SkillRequired");
            return;
        }

        ResultName = NameBox.Text.Trim();
        ResultSkill = strSkill;
        ResultDvBase = (string)DvBaseBox.SelectedItem!;
        ResultDvMod = (int)(DvModBox.Value ?? 0);
        ResultDvType = (string)DvTypeBox.SelectedItem!;
        ResultAp = (int)(ApBox.Value ?? 0);
        ResultReach = (int)(ReachBox.Value ?? 0);
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
