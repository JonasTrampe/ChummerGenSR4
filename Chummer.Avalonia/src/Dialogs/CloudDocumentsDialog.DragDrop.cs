using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class CloudDocumentsDialog
{
    private CloudDocumentEntryViewModel? _draggedDocument;
    private PointerPressedEventArgs? _pendingPressArgs;
    private CloudDocumentEntryViewModel? _pendingPressedDocument;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;

    private void SetUpDocumentFolderDragDrop()
    {
        TreeView objFoldersTree = this.FindControl<TreeView>("FoldersTree")!;
        ListBox objDocumentsList = this.FindControl<ListBox>("DocumentsList")!;

        objDocumentsList.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(objDocumentsList).Properties.IsLeftButtonPressed
                || (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>() is not { } objItem
                || objItem.DataContext is not CloudDocumentEntryViewModel objDocument)
                return;

            _pendingPressArgs = e;
            _pendingPressedDocument = objDocument;
            _pendingPressPoint = e.GetPosition(objDocumentsList);
        }, RoutingStrategies.Tunnel);

        objDocumentsList.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (_pendingPressArgs is not { } objPressArgs || _pendingPressedDocument is not { } objDocument)
                return;

            Point objCurrentPoint = e.GetPosition(objDocumentsList);
            double dblDeltaX = objCurrentPoint.X - _pendingPressPoint.X;
            double dblDeltaY = objCurrentPoint.Y - _pendingPressPoint.Y;
            if (Math.Sqrt(dblDeltaX * dblDeltaX + dblDeltaY * dblDeltaY) < DragThreshold)
                return;

            _pendingPressArgs = null;
            _pendingPressedDocument = null;
            _draggedDocument = objDocument;

            DataTransferItem objTransferItem = new();
            objTransferItem.Set(DataFormat.Text, objDocument.DisplayName);
            DataTransfer objTransfer = new();
            objTransfer.Add(objTransferItem);

            try
            {
                await DragDrop.DoDragDropAsync(objPressArgs, objTransfer, DragDropEffects.Move);
            }
            catch
            {
                // Ignore drag-drop transport failures.
            }
            finally
            {
                _draggedDocument = null;
            }
        }, RoutingStrategies.Tunnel);

        objDocumentsList.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            _pendingPressArgs = null;
            _pendingPressedDocument = null;
        }, RoutingStrategies.Tunnel);

        DragDrop.SetAllowDrop(objFoldersTree, true);
        objFoldersTree.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            FolderNodeViewModel? objFolder = FindFolderNodeAt(objFoldersTree, e.GetPosition(objFoldersTree));
            if (_draggedDocument == null || objFolder == null || objFolder.Id == CloudDocumentsDialogViewModel.AllDocumentsFolderId)
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            ViewModel.SelectedFolder = objFolder;
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        });

        objFoldersTree.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            e.Handled = true;

            FolderNodeViewModel? objFolder = FindFolderNodeAt(objFoldersTree, e.GetPosition(objFoldersTree));
            if (_draggedDocument == null || objFolder == null || objFolder.Id == CloudDocumentsDialogViewModel.AllDocumentsFolderId)
                return;

            int? intFolderId = objFolder.Id == CloudDocumentsDialogViewModel.UnfiledFolderId ? null : objFolder.Id;
            try
            {
                await ViewModel.MoveDocumentToFolderAsync(_draggedDocument, intFolderId);
            }
            catch (Exception ex)
            {
                await HandleCloudExceptionAsync(ex);
            }
        });
    }

    private static FolderNodeViewModel? FindFolderNodeAt(IInputElement objTree, Point objPointRelativeToTree)
        => ((objTree.InputHitTest(objPointRelativeToTree) as Visual)?.FindAncestorOfType<TreeViewItem>(true))
            ?.DataContext as FolderNodeViewModel;
}
