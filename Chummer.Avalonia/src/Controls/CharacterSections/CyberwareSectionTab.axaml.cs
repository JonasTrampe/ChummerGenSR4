using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using CyberwareDialog = Chummer.NewUI.Dialogs.CyberwareDialog;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CyberwareSectionTab : UserControl
{
    // Same drag/drop plumbing as GearSectionTab's SetUpGearDragDrop, adapted for
    // CyberwareId/MoveCyberware - see that file's comments for the press-then-threshold and
    // hit-testing rationale.
    private TreeNodeViewModel? _draggedNode;
    private bool _draggedWithRightButton;
    private PointerPressedEventArgs? _pendingPressArgs;
    private TreeNodeViewModel? _pendingPressNode;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;

    private CharacterDocument? _character;

    public CyberwareSectionViewModel ViewModel { get; } = new();

    public CyberwareSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
        SetUpCyberwareDragDrop();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddCyberwareClick(object? sender, RoutedEventArgs e) => await AddAsync(blnBioware: false);

    private async void OnAddBiowareClick(object? sender, RoutedEventArgs e) => await AddAsync(blnBioware: true);

    private async System.Threading.Tasks.Task AddAsync(bool blnBioware)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new CyberwareDialog(_character, blnBioware);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedCyberware != null)
        {
            var item = dialog.SelectedCyberware;
            var viewModel = dialog.ViewModel;
            _character.AddCyberware(item.Name, item.Category, item.Rating, viewModel.FinalEssence,
                viewModel.FinalCost, viewModel.FinalAvailability, item.SourcePage, string.Empty,
                viewModel.SelectedGrade?.Name ?? "Standard", blnBioware: blnBioware);
            ViewModel.LoadCharacter(_character);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        // Roots is CyberwareRoot/BiowareRoot, two synthetic header nodes - actual saved items are
        // their immediate children, so only allow deleting a node one level under one of those
        // (deeper nesting would be an installed mod, not deletable this way yet).
        if (_character == null || ViewModel.SelectedNode is not { Parent: { Parent: null } parent } node)
            return;

        bool blnBioware = ReferenceEquals(parent, ViewModel.BiowareRoot);
        if (_character.RemoveCyberware(node.SourceName, node.Category, node.Rating, blnBioware))
            ViewModel.LoadCharacter(_character);
    }

    private void SetUpCyberwareDragDrop()
    {
        var tree = this.FindControl<TreeView>("CyberwareTree")!;

        tree.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var pointerProperties = e.GetCurrentPoint(tree).Properties;
            var isRightButton = pointerProperties.IsRightButtonPressed;
            if ((!pointerProperties.IsLeftButtonPressed && !isRightButton)
                || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(true) is not null
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } sourceItem
                || sourceItem.DataContext is not TreeNodeViewModel { CyberwareId: >= 0 } sourceNode)
                return;

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
            _draggedNode = sourceNode;

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, sourceNode.Name);
            var transfer = new DataTransfer();
            transfer.Add(item);
            try
            {
                await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move);
            }
            catch
            {
                // Swallow deliberately - a failed drag session (dropped outside any valid target,
                // etc.) is not fatal, it just means no move happens.
            }
            finally
            {
                _draggedNode = null;
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
            e.Handled = true;
        });
        tree.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;

            if (_character == null
                || _draggedNode is not { } source
                || FindTreeNodeAt(tree, e.GetPosition(tree)) is not { } target
                || source == target
                || source.CyberwareId < 0 || target.CyberwareId < 0)
                return;

            if (_draggedWithRightButton)
            {
                if (IsAncestorOf(source, target))
                    return;

                if (_character.MoveCyberware(source.CyberwareId, target.CyberwareId, blnReparent: true))
                    ViewModel.LoadCharacter(_character);
            }
            else
            {
                if (source.Parent != target.Parent)
                    return;

                if (_character.MoveCyberware(source.CyberwareId, target.CyberwareId, blnReparent: false))
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
}
