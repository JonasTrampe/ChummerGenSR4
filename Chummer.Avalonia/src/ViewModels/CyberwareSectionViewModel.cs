using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CyberwareSectionViewModel : ViewModelBase
{
    public TreeNodeViewModel CyberwareRoot { get; } = new("Cyberware auswählen", blnExpanded: true);
    public TreeNodeViewModel BiowareRoot { get; } = new("Bioware auswählen", blnExpanded: true);

    public ObservableCollection<TreeNodeViewModel> Roots { get; }

    public CyberwareSectionViewModel()
    {
        Roots = new ObservableCollection<TreeNodeViewModel> { CyberwareRoot, BiowareRoot };
    }

    public void LoadCharacter(CharacterDocument character)
    {
        // Cyberware and bioware share one <cyberwares> save list (split by Chummer.Core on
        // <improvementsource>) but are kept as two separate branches under the same tree here.
        CyberwareRoot.Children.Clear();
        foreach (CharacterTreeItemData item in character.Cyberware)
            CyberwareRoot.Children.Add(TreeNodeViewModel.FromTreeItem(item));

        BiowareRoot.Children.Clear();
        foreach (CharacterTreeItemData item in character.Bioware)
            BiowareRoot.Children.Add(TreeNodeViewModel.FromTreeItem(item));
    }
}
