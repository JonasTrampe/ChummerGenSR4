using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class SkillsSectionTab : UserControl
{
    public SkillsSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var skillGroupsPanel = this.FindControl<StackPanel>("SkillGroupsPanel")!;
        skillGroupsPanel.Children.Clear();
        foreach (CharacterSkillGroupData group in character.SkillGroups)
            skillGroupsPanel.Children.Add(new GroupRow { GroupName = group.Name, Rating = group.Rating });

        var activeSkillsPanel = this.FindControl<WrapPanel>("ActiveSkillsPanel")!;
        activeSkillsPanel.Children.Clear();
        foreach (CharacterSkillData skill in character.Skills)
        {
            activeSkillsPanel.Children.Add(new SkillRow
            {
                SkillName = skill.Name,
                Attribute = skill.Attribute,
                Rating = skill.Rating,
                Pool = skill.TotalValue,
                PoolTooltip = skill.PoolTooltip,
                Specialization = skill.Specialization,
                IsGroupLocked = skill.IsGroupLocked,
            });
        }

        var knowledgeSkillsGrid = this.FindControl<Grid>("KnowledgeSkillsGrid")!;
        knowledgeSkillsGrid.Children.Clear();
        knowledgeSkillsGrid.RowDefinitions.Clear();
        for (int row = 0; row < character.KnowledgeSkills.Count; row++)
        {
            CharacterSkillData skill = character.KnowledgeSkills[row];
            knowledgeSkillsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddKnowledgeSkillText(knowledgeSkillsGrid, row, 0, skill.Name);
            AddKnowledgeSkillText(knowledgeSkillsGrid, row, 1, skill.Rating);
            AddKnowledgeSkillText(knowledgeSkillsGrid, row, 3, skill.TotalValue, skill.PoolTooltip);
            AddKnowledgeSkillText(knowledgeSkillsGrid, row, 4, skill.Specialization);
            AddKnowledgeSkillText(knowledgeSkillsGrid, row, 6, skill.Category);
        }
    }

    private static void AddKnowledgeSkillText(Grid grid, int row, int column, string text, string? tooltip = null)
    {
        var textBlock = new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(tooltip)) ToolTip.SetTip(textBlock, tooltip);
        Grid.SetRow(textBlock, row);
        Grid.SetColumn(textBlock, column);
        grid.Children.Add(textBlock);
    }
}
