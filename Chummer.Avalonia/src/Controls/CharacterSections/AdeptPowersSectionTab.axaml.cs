using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class AdeptPowersSectionTab : UserControl
{
    public AdeptPowersSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var panel = this.FindControl<StackPanel>("AdeptPowersPanel")!;
        panel.Children.Clear();
        foreach (CharacterPowerData power in character.AdeptPowers)
            panel.Children.Add(new InfoRow { Label = power.DisplayName + ":", Value = power.TotalPoints });
    }
}
