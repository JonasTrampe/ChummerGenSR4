using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab
{
    // Tracks the item and button a drag session started with - DoDragDropAsync only needs an
    // IDataTransfer to satisfy the OS-level drag session, the actual "what to move"/"how"
    // bookkeeping is simpler kept here directly rather than round-tripped through a
    // DataTransferItem. These track the bound ViewModel node, not the visual TreeViewItem - the
    // tree is data-bound now, so drag/drop mutates the ViewModel's TreeNodeViewModel graph and
    // lets the bindings redraw it, rather than reaching into the visual tree's Items collection.
    private TreeNodeViewModel? _draggedGearNode;
    private bool _draggedWithRightButton;

    // Press-then-threshold state: DoDragDropAsync requires the original PointerPressedEventArgs,
    // but the drag itself must only actually engage once the pointer has moved a few pixels -
    // otherwise every plain click (including on the expand/collapse box) immediately hijacks
    // itself into an OS-level drag session before the click can be processed normally.
    private PointerPressedEventArgs? _pendingPressArgs;
    private TreeNodeViewModel? _pendingPressNode;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;

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
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } sourceItem
                || sourceItem.DataContext is not TreeNodeViewModel sourceNode)
                return;

            // Just remember the candidate - do NOT start the OS-level drag yet. Engaging it
            // immediately on every press (before any actual movement) is what broke plain clicks:
            // it hijacked the gesture before the expand/collapse ToggleButton, item selection, etc.
            // ever got a chance to see it.
            _pendingPressArgs = e;
            _pendingPressNode = sourceNode;
            _draggedWithRightButton = isRightButton;
            _pendingPressPoint = e.GetPosition(tree);
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (_pendingPressArgs is not { } pressArgs || _pendingPressNode is not { } sourceNode)
                return;

            var currentPoint = e.GetPosition(tree);
            var dx = currentPoint.X - _pendingPressPoint.X;
            var dy = currentPoint.Y - _pendingPressPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
                return;

            _pendingPressArgs = null;
            _pendingPressNode = null;
            _draggedGearNode = sourceNode;

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, sourceNode.Name);
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
                _draggedGearNode = null;
            }
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            _pendingPressArgs = null;
            _pendingPressNode = null;
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
            if (_character == null
                || _draggedGearNode is not { } source
                || FindTreeNodeAt(tree, e.GetPosition(tree)) is not { } target
                || source == target
                || source.GearId < 0 || target.GearId < 0)
                return;

            if (_draggedWithRightButton)
            {
                // Reparent: refuse to drop an item onto itself or into its own subtree, since that
                // would either no-op or orphan the whole branch (MoveGear also guards against this).
                if (IsAncestorOf(source, target))
                    return;

                if (_character.MoveGear(source.GearId, target.GearId, blnReparent: true))
                    ViewModel.LoadCharacter(_character);
            }
            else
            {
                if (source.Parent != target.Parent)
                    return;

                if (_character.MoveGear(source.GearId, target.GearId, blnReparent: false))
                    ViewModel.LoadCharacter(_character);
            }
        });
    }

    private static TreeNodeViewModel? FindTreeNodeAt(IInputElement tree, Point pointRelativeToTree)
        => ((tree.InputHitTest(pointRelativeToTree) as Visual)?.FindAncestorOfType<TreeViewItem>(true))
            ?.DataContext as TreeNodeViewModel;

    private static bool IsAncestorOf(TreeNodeViewModel candidate, TreeNodeViewModel node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidate))
                return true;
        }

        return false;
    }

    /// <summary>Sets up left-button reordering for trees whose groups (weapon locations and armor
    /// sets) are presentation-only. Unlike Gear/Cyberware, those XML items cannot be reparented;
    /// drops are accepted only between siblings and persist their underlying root XML order.</summary>
    private void SetUpRootTreeReorderDragDrop(TreeView tree, Func<TreeNodeViewModel, TreeNodeViewModel, bool> move)
    {
        TreeNodeViewModel? pending = null;
        PointerPressedEventArgs? pendingArgs = null;
        Point pendingPoint = default;
        TreeNodeViewModel? dragged = null;

        tree.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var props = e.GetCurrentPoint(tree).Properties;
            if (!props.IsLeftButtonPressed
                || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(true) is not null
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } item
                || item.DataContext is not TreeNodeViewModel node)
                return;
            pending = node;
            pendingArgs = e;
            pendingPoint = e.GetPosition(tree);
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (pending is not { } source || pendingArgs is not { } pressArgs)
                return;
            Point current = e.GetPosition(tree);
            double dx = current.X - pendingPoint.X;
            double dy = current.Y - pendingPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
                return;

            pending = null;
            pendingArgs = null;
            dragged = source;
            var transfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, source.Name);
            transfer.Add(item);
            try { await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move); }
            catch { /* An abandoned OS drag is a harmless no-op. */ }
            finally { dragged = null; }
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerReleasedEvent, (_, _) => { pending = null; pendingArgs = null; }, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(tree, true);
        tree.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Move; e.Handled = true; });
        tree.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            if (dragged is not { } source || FindTreeNodeAt(tree, e.GetPosition(tree)) is not { } target
                || source == target || source.Parent != target.Parent)
                return;
            if (move(source, target))
                ViewModel.LoadCharacter(_character!);
        });
    }

    private bool MoveWeapon(TreeNodeViewModel source, TreeNodeViewModel target) => _character != null
        && Guid.TryParse(source.ItemGuid, out Guid sourceId) && Guid.TryParse(target.ItemGuid, out Guid targetId)
        && _character.MoveWeapon(sourceId, targetId);

    private bool MoveArmor(TreeNodeViewModel source, TreeNodeViewModel target) => _character != null
        && source.ArmorId >= 0 && target.ArmorId >= 0 && _character.MoveArmor(source.ArmorId, target.ArmorId);
}
