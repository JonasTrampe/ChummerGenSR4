using System.Collections.ObjectModel;
using System.Globalization;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CyberwareSectionViewModel : ViewModelBase
{
    public TreeNodeViewModel CyberwareRoot { get; } = new("Cyberware auswählen", blnExpanded: true);
    public TreeNodeViewModel BiowareRoot { get; } = new("Bioware auswählen", blnExpanded: true);

    public ObservableCollection<TreeNodeViewModel> Roots { get; }

    private TreeNodeViewModel? _selectedNode;
    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => SetField(ref _selectedNode, value);
    }

    private string _strCyberwareEssence = "0,00";
    public string CyberwareEssence
    {
        get => _strCyberwareEssence;
        set => SetField(ref _strCyberwareEssence, value);
    }

    private string _strBiowareEssence = "0,00";
    public string BiowareEssence
    {
        get => _strBiowareEssence;
        set => SetField(ref _strBiowareEssence, value);
    }

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

        CyberwareEssence = character.CyberwareEssence.ToString("0.00", CultureInfo.CurrentCulture);
        BiowareEssence = character.BiowareEssence.ToString("0.00", CultureInfo.CurrentCulture);
        SelectedNode = null;
    }
}
