using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CharacterSidebar : UserControl
{
    public CharacterSidebar()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        CharacterConditionData condition = character.Condition;
        this.FindControl<InfoRow>("EssenceInfo")!.Value = condition.Essence;
        this.FindControl<InfoRow>("PhysicalDamageInfo")!.Value = condition.PhysicalDamage;
        this.FindControl<InfoRow>("StunDamageInfo")!.Value = condition.StunDamage;
        this.FindControl<InfoRow>("PhysicalMonitorInfo")!.Value = condition.PhysicalCm.ToString();
        this.FindControl<InfoRow>("StunMonitorInfo")!.Value = condition.StunCm.ToString();

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
        this.FindControl<InfoRow>("BallisticEncumbranceInfo")!.Value = encumbrance.BallisticPenalty.ToString();
        this.FindControl<InfoRow>("ImpactEncumbranceInfo")!.Value = encumbrance.ImpactPenalty.ToString();
    }
}
