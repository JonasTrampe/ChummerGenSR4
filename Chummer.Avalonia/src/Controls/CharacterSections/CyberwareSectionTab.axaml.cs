using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CyberwareSectionTab : UserControl
{
    public CyberwareSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        // Cyberware and bioware share one <cyberwares> save list (split by Chummer.Core on
        // <improvementsource>) but are kept as two separate branches under the same tree here.
        var cyberwareHeader = this.FindControl<TreeViewItem>("CyberwareHeaderItem")!;
        cyberwareHeader.Items.Clear();
        foreach (CharacterTreeItemData item in character.Cyberware)
            cyberwareHeader.Items.Add(CharacterTreeViewBuilder.CreateTreeViewItem(item));

        var biowareHeader = this.FindControl<TreeViewItem>("BiowareHeaderItem")!;
        biowareHeader.Items.Clear();
        foreach (CharacterTreeItemData item in character.Bioware)
            biowareHeader.Items.Add(CharacterTreeViewBuilder.CreateTreeViewItem(item));
    }
}
