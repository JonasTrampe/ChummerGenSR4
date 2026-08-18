using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RunnersPoint.Api;

namespace Chummer.NewUI.ViewModels;

public sealed partial class CloudDocumentsDialogViewModel
{
    public async Task CreateFolderAsync(string strName)
    {
        await _apiClient.CreateFolderAsync(strName.Trim(), GetSelectedFolderId(false));
        await RefreshAsync();
    }

    public async Task RenameSelectedFolderAsync(string strName)
    {
        if (!CanManageSelectedFolder || SelectedFolder == null)
            return;

        await _apiClient.UpdateFolderAsync(SelectedFolder.Id, strName.Trim());
        await RefreshAsync();
    }

    public async Task DeleteSelectedFolderAsync()
    {
        if (!CanManageSelectedFolder || SelectedFolder == null)
            return;

        await _apiClient.DeleteFolderAsync(SelectedFolder.Id);
        await RefreshAsync();
    }

    private void RebuildFolderTree()
    {
        int intSelectedId = SelectedFolder?.Id ?? AllDocumentsFolderId;
        Folders.Clear();

        FolderNodeViewModel objAll = new(AllDocumentsFolderId, T("String_Cloud_AllDocuments")) { IsExpanded = true };
        FolderNodeViewModel objUnfiled = new(UnfiledFolderId, T("String_Cloud_Unfiled"));
        Folders.Add(objAll);
        Folders.Add(objUnfiled);

        Dictionary<int, FolderNodeViewModel> dicNodes = new();
        foreach (RunnersPointFolder objFolder in _folders.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            dicNodes[objFolder.Id] = new FolderNodeViewModel(objFolder.Id, objFolder.Name);

        foreach (RunnersPointFolder objFolder in _folders.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            FolderNodeViewModel objNode = dicNodes[objFolder.Id];
            objNode.IsExpanded = true;
            if (objFolder.ParentFolderId.HasValue && dicNodes.ContainsKey(objFolder.ParentFolderId.Value))
                dicNodes[objFolder.ParentFolderId.Value].Children.Add(objNode);
            else
                Folders.Add(objNode);
        }

        SelectedFolder = FindFolderNode(intSelectedId) ?? objAll;
    }

    private FolderNodeViewModel? FindFolderNode(int intId)
    {
        foreach (FolderNodeViewModel objNode in Folders)
        {
            FolderNodeViewModel? objMatch = FindFolderNodeRecursive(objNode, intId);
            if (objMatch != null)
                return objMatch;
        }

        return null;
    }

    private static FolderNodeViewModel? FindFolderNodeRecursive(FolderNodeViewModel objNode, int intId)
    {
        if (objNode.Id == intId)
            return objNode;

        foreach (FolderNodeViewModel objChild in objNode.Children)
        {
            FolderNodeViewModel? objMatch = FindFolderNodeRecursive(objChild, intId);
            if (objMatch != null)
                return objMatch;
        }

        return null;
    }

    private int? GetSelectedFolderId(bool blnRequireActualFolder)
    {
        if (SelectedFolder == null)
            return null;
        if (blnRequireActualFolder && SelectedFolder.Id < 0)
            return null;
        return SelectedFolder.Id >= 0 ? SelectedFolder.Id : null;
    }
}
