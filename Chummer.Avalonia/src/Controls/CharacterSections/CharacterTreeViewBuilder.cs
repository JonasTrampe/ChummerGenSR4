using Avalonia.Controls;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

/// <summary>Shared helper for building nested TreeViewItems from a CharacterTreeItemData subtree.</summary>
internal static class CharacterTreeViewBuilder
{
    public static TreeViewItem CreateTreeViewItem(CharacterTreeItemData item)
    {
        var treeItem = new TreeViewItem { Header = item.Name, IsExpanded = item.Children.Count > 0 };
        foreach (CharacterTreeItemData child in item.Children)
            treeItem.Items.Add(CreateTreeViewItem(child));
        return treeItem;
    }
}
