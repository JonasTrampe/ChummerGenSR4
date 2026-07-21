using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab : UserControl
{
    // Tracks the item and button a drag session started with - DoDragDropAsync only needs an
    // IDataTransfer to satisfy the OS-level drag session, the actual "what to move"/"how"
    // bookkeeping is simpler kept here directly rather than round-tripped through a
    // DataTransferItem.
    private TreeViewItem? _draggedGearItem;
    private bool _draggedWithRightButton;

    // Press-then-threshold state: DoDragDropAsync requires the original PointerPressedEventArgs,
    // but the drag itself must only actually engage once the pointer has moved a few pixels -
    // otherwise every plain click (including on the expand/collapse box) immediately hijacks
    // itself into an OS-level drag session before the click can be processed normally.
    private PointerPressedEventArgs? _pendingPressArgs;
    private TreeViewItem? _pendingPressItem;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;

    public GearSectionTab()
    {
        InitializeComponent();
        SetUpGearDragDrop();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var gearTree = this.FindControl<TreeView>("AusruestungTree")!;
        gearTree.Items.Clear();
        foreach (CharacterTreeItemData item in character.Gear)
            gearTree.Items.Add(CharacterTreeViewBuilder.CreateTreeViewItem(item));

        var weaponsTree = this.FindControl<TreeView>("WeaponsTree")!;
        weaponsTree.Items.Clear();
        foreach (CharacterWeaponData weapon in character.Weapons)
            weaponsTree.Items.Add(new TreeViewItem { Header = weapon.DisplayName });

        var armorTree = this.FindControl<TreeView>("ArmorTree")!;
        armorTree.Items.Clear();
        foreach (CharacterTreeItemData item in character.Armor)
            armorTree.Items.Add(CharacterTreeViewBuilder.CreateTreeViewItem(item));

        var listBox = this.FindControl<ListBox>("LifestylesListBox")!;
        listBox.Items.Clear();
        decimal decTotalCost = 0;
        foreach (CharacterLifestyleData lifestyle in character.Lifestyles)
        {
            listBox.Items.Add(new ListBoxItem { Content = lifestyle.Name });
            if (decimal.TryParse(lifestyle.Cost, out var decCost))
                decTotalCost += decCost;
        }

        this.FindControl<TextBlock>("LifestyleCostText")!.Text =
            "Kosten/Monat: " + decTotalCost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "¥";
    }

    // Avalonia DragDrop prototype for the gear-reordering risk area flagged in the Linux port
    // plan's Avalonia audit (frmCareer/frmCreate gear/cyberware lists). Matches the real app's two
    // distinct gestures: left-button drag reorders siblings within the same level, right-button
    // drag reparents the dragged item to become a child of the drop target - which covers both
    // "embed into" (dropping on some other item) and "extract out of" (dropping on an ancestor,
    // moving the item back up a level) with the same single rule.
    private void SetUpGearDragDrop()
    {
        var tree = this.FindControl<TreeView>("AusruestungTree")!;

        tree.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var pointerProperties = e.GetCurrentPoint(tree).Properties;
            var isRightButton = pointerProperties.IsRightButtonPressed;
            if ((!pointerProperties.IsLeftButtonPressed && !isRightButton)
                || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(true) is not null
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } source)
                return;

            // Just remember the candidate - do NOT start the OS-level drag yet. Engaging it
            // immediately on every press (before any actual movement) is what broke plain clicks:
            // it hijacked the gesture before the expand/collapse ToggleButton, item selection, etc.
            // ever got a chance to see it.
            _pendingPressArgs = e;
            _pendingPressItem = source;
            _draggedWithRightButton = isRightButton;
            _pendingPressPoint = e.GetPosition(tree);
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (_pendingPressArgs is not { } pressArgs || _pendingPressItem is not { } source)
                return;

            var currentPoint = e.GetPosition(tree);
            var dx = currentPoint.X - _pendingPressPoint.X;
            var dy = currentPoint.Y - _pendingPressPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
                return;

            _pendingPressArgs = null;
            _pendingPressItem = null;
            _draggedGearItem = source;

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, source.Header?.ToString() ?? "gear");
            var transfer = new DataTransfer();
            transfer.Add(item);
            try
            {
                // A drag session can fail for all sorts of reasons at the OS/COM level (dropped
                // outside any valid target, input state changing mid-drag, etc.) - none of that is
                // fatal, it just means no move happens, so it must never be allowed to crash the app.
                await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move);
            }
            catch
            {
                // Swallow deliberately - see comment above.
            }
            finally
            {
                _draggedGearItem = null;
            }
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            _pendingPressArgs = null;
            _pendingPressItem = null;
        }, RoutingStrategies.Tunnel);

        DragDrop.SetAllowDrop(tree, true);
        tree.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Move;
            // Avalonia's Win32 drop target only honors DragEffects/actually allows a drop when the
            // event is marked Handled - without this the OS side falls back to treating the whole
            // gesture as rejected, so the Drop event below never fires at all (no crash, just a
            // silent no-op, which is exactly what "does nothing" looked like).
            e.Handled = true;
        });
        tree.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;

            // e.Source isn't reliable here - unlike pointer events, Avalonia's drag routing
            // doesn't consistently set it to the actual element under the cursor, so it's hit-
            // tested explicitly at the drop coordinates instead.
            if (_draggedGearItem is not { } source
                || FindTreeViewItemAt(tree, e.GetPosition(tree)) is not { } target
                || source == target
                || source.Parent is not ItemsControl sourceParent)
                return;

            // Deliberately typed as ItemCollection, not IList: ItemCollection's own Remove(object)
            // (which actually mutates the collection) has a different signature (returns bool) than
            // IList.Remove(object) (returns void), so it does NOT satisfy that interface member -
            // the interface slot instead falls back to the base ItemsSourceView's IList.Remove,
            // which unconditionally throws NotSupportedException. Calling through an IList-typed
            // reference silently ate that exception (Avalonia's Win32 drop-target COM shim swallows
            // exceptions from managed handlers), which is exactly why nothing ever moved.
            var sourceSiblings = sourceParent.Items;

            if (_draggedWithRightButton)
            {
                // Reparent: refuse to drop an item onto itself or into its own subtree, since that
                // would either no-op or orphan the whole branch.
                if (IsAncestorOf(source, target) || target is not ItemsControl targetParent)
                    return;

                sourceSiblings.Remove(source);
                targetParent.Items.Add(source);
            }
            else
            {
                if (source.Parent != target.Parent)
                    return;

                var targetIndex = sourceSiblings.IndexOf(target);
                sourceSiblings.Remove(source);
                sourceSiblings.Insert(targetIndex, source);
            }
        });
    }

    private static TreeViewItem? FindTreeViewItemAt(IInputElement tree, Point pointRelativeToTree)
        => (tree.InputHitTest(pointRelativeToTree) as Visual)?.FindAncestorOfType<TreeViewItem>(true);

    private static bool IsAncestorOf(TreeViewItem candidate, TreeViewItem item)
    {
        for (var node = item.Parent; node is not null; node = (node as Control)?.Parent)
        {
            if (ReferenceEquals(node, candidate))
                return true;
        }

        return false;
    }
}
