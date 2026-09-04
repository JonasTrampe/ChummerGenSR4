using System;
using Avalonia;
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
    public event Func<CharacterDocument, System.Threading.Tasks.Task<bool>>? FinalizingCreation;
    private CharacterLayout _layout = CharacterLayout.Classic;
    private bool _blnInspectorVisible = true;

    public CharacterTab()
    {
        InitializeComponent();
        this.FindControl<CharacterSidebar>("Sidebar")!.FinalizingCreation += character =>
            FinalizingCreation?.Invoke(character) ?? System.Threading.Tasks.Task.FromResult(true);
        ApplyLayout(CharacterLayoutSettings.Current);
    }

    /// <summary>Rearranges the existing workspace controls without recreating a loaded character.</summary>
    public void ApplyLayout(CharacterLayout layout)
    {
        _layout = layout;
        if (layout != CharacterLayout.FamiliarWorkbench)
            _blnInspectorVisible = true;

        Grid objLayoutRoot = this.FindControl<Grid>("LayoutRoot")!;
        Grid objContentLayout = this.FindControl<Grid>("ContentLayout")!;
        TabControl objSectionTabs = this.FindControl<TabControl>("SectionTabs")!;
        GridSplitter objSidebarSplitter = this.FindControl<GridSplitter>("SidebarSplitter")!;
        CharacterSidebar objSidebar = this.FindControl<CharacterSidebar>("Sidebar")!;
        Border objSummary = this.FindControl<Border>("WorkbenchSummary")!;

        objSummary.IsVisible = layout == CharacterLayout.FamiliarWorkbench;
        objLayoutRoot.RowDefinitions = layout == CharacterLayout.FamiliarWorkbench
            ? new RowDefinitions("Auto,*")
            : new RowDefinitions("*");
        Grid.SetRow(objContentLayout, layout == CharacterLayout.FamiliarWorkbench ? 1 : 0);

        if (layout == CharacterLayout.RunnersLedger)
        {
            objContentLayout.ColumnDefinitions = new ColumnDefinitions("300,4,*");
            objSidebar.IsVisible = true;
            objSidebarSplitter.IsVisible = true;
            Grid.SetColumn(objSidebar, 0);
            Grid.SetColumn(objSidebarSplitter, 1);
            Grid.SetColumn(objSectionTabs, 2);
        }
        else
        {
            UpdateWorkbenchInspector();
            Grid.SetColumn(objSectionTabs, 0);
            Grid.SetColumn(objSidebarSplitter, 1);
            Grid.SetColumn(objSidebar, 2);
        }

        UpdateWorkbenchSummary();
    }

    private void OnToggleInspectorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _blnInspectorVisible = !_blnInspectorVisible;
        UpdateWorkbenchInspector();
    }

    private void UpdateWorkbenchInspector()
    {
        if (_layout == CharacterLayout.RunnersLedger)
            return;

        Grid objContentLayout = this.FindControl<Grid>("ContentLayout")!;
        CharacterSidebar objSidebar = this.FindControl<CharacterSidebar>("Sidebar")!;
        GridSplitter objSidebarSplitter = this.FindControl<GridSplitter>("SidebarSplitter")!;

        bool blnCollapsible = _layout == CharacterLayout.FamiliarWorkbench;
        bool blnShowInspector = !blnCollapsible || _blnInspectorVisible;
        objContentLayout.ColumnDefinitions = blnShowInspector
            ? new ColumnDefinitions(_layout == CharacterLayout.FamiliarWorkbench ? "*,4,320" : "*,4,270")
            : new ColumnDefinitions("*,0,0");
        objSidebar.IsVisible = blnShowInspector;
        objSidebarSplitter.IsVisible = blnShowInspector;
    }

    private void UpdateWorkbenchSummary()
    {
        TextBlock objName = this.FindControl<TextBlock>("WorkbenchCharacterName")!;
        TextBlock objStatus = this.FindControl<TextBlock>("WorkbenchCharacterStatus")!;
        Button objToggle = this.FindControl<Button>("InspectorToggleButton")!;

        if (_layout != CharacterLayout.FamiliarWorkbench)
            return;

        objName.Text = Character?.DisplayName ?? App.LanguageCatalog.GetString("UI_Layout_Workbench");
        if (Character == null)
        {
            objStatus.Text = string.Empty;
        }
        else
        {
            objStatus.Text = App.LanguageCatalog.GetString("UI_Layout_WorkbenchStatus")
                .Replace("{0}", Character.CareerKarma.ToString())
                .Replace("{1}", Character.Nuyen);
        }

        objToggle.Content = App.LanguageCatalog.GetString(_blnInspectorVisible
            ? "UI_Layout_HideInspector"
            : "UI_Layout_ShowInspector");
    }

    /// <summary>Populates every section tab from the given character's data.</summary>
    public void LoadCharacter(CharacterDocument character)
    {
        Character = character;
        UpdateWorkbenchSummary();
        this.FindControl<TabItem>("AdeptPowersTabItem")!.IsVisible = character.Adept;
        this.FindControl<TabItem>("SpellsTabItem")!.IsVisible = character.Magician;
        this.FindControl<TabItem>("InitiationTabItem")!.IsVisible = character.Awakened;
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
        this.FindControl<CalendarTab>("CalendarTab")!.LoadCharacter(character);
        this.FindControl<NotesTab>("NotesTab")!.LoadCharacter(character);
        this.FindControl<ImprovementsTab>("ImprovementsTab")!.LoadCharacter(character);
        this.FindControl<CharacterSidebar>("Sidebar")!.LoadCharacter(character);
    }
}
