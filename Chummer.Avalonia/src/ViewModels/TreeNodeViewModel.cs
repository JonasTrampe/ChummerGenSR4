using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Bindable node for a TreeView (via TreeDataTemplate/HierarchicalDataTemplate), replacing
/// the old pattern of building TreeViewItem objects by hand in code-behind. Tracks its own Parent
/// so drag-and-drop reordering/reparenting (see GearSectionTab) can find and mutate the right
/// Children collection without walking the visual tree.</summary>
public sealed class TreeNodeViewModel
{
    public string Name { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
    public bool IsExpanded { get; set; }

    /// <summary>Null for a root-level node (its collection lives directly on the owning
    /// section ViewModel instead of another node's Children).</summary>
    public TreeNodeViewModel? Parent { get; set; }

    public TreeNodeViewModel(string strName, bool blnExpanded = false)
    {
        Name = strName;
        IsExpanded = blnExpanded;
    }

    public void AddChild(TreeNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public static TreeNodeViewModel FromTreeItem(CharacterTreeItemData item)
    {
        var node = new TreeNodeViewModel(item.Name, item.Children.Count > 0);
        foreach (CharacterTreeItemData child in item.Children)
            node.AddChild(FromTreeItem(child));
        return node;
    }
}
