using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.Controls.CharacterSections;

namespace Chummer.NewUI.Controls;

/// <summary>
/// Shell that hosts one <see cref="CharacterDocument"/>'s worth of UI - a tab strip of
/// section UserControls (Allgemein, Fertigkeiten, ...) plus the always-visible sidebar. Each
/// section owns its own XAML and load logic; this class only wires a loaded character to them.
/// </summary>
public partial class CharacterTab : UserControl
{
    public CharacterDocument? Character { get; private set; }

    public CharacterTab()
    {
        InitializeComponent();
    }

    /// <summary>Populates every section tab from the given character's data.</summary>
    public void LoadCharacter(CharacterDocument character)
    {
        Character = character;
        this.FindControl<GeneralSectionTab>("GeneralTab")!.LoadCharacter(character);
        this.FindControl<SkillsSectionTab>("SkillsTab")!.LoadCharacter(character);
        this.FindControl<MartialArtsSectionTab>("MartialArtsTab")!.LoadCharacter(character);
        this.FindControl<AdeptPowersSectionTab>("AdeptPowersTab")!.LoadCharacter(character);
        this.FindControl<SpellsSectionTab>("SpellsTab")!.LoadCharacter(character);
        this.FindControl<InitiationSectionTab>("InitiationTab")!.LoadCharacter(character);
        this.FindControl<CyberwareSectionTab>("CyberwareTab")!.LoadCharacter(character);
        this.FindControl<GearSectionTab>("GearTab")!.LoadCharacter(character);
        this.FindControl<VehiclesSectionTab>("VehiclesTab")!.LoadCharacter(character);
        this.FindControl<CharacterInfoSectionTab>("CharacterInfoTab")!.LoadCharacter(character);
        this.FindControl<KarmaNuyenSectionTab>("KarmaNuyenTab")!.LoadCharacter(character);
        this.FindControl<NotesTab>("NotesTab")!.LoadCharacter(character);
        this.FindControl<ImprovementsTab>("ImprovementsTab")!.LoadCharacter(character);
        this.FindControl<CharacterSidebar>("Sidebar")!.LoadCharacter(character);
    }
}
