using System;
using System.Net;
using System.Net.Http;
using Avalonia.Interactivity;
using RunnersPoint.Api;

namespace Chummer.NewUI.Dialogs;

public partial class CloudDocumentsDialog
{
    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.LoginWithOAuthAsync();
        }
        catch (HttpRequestException)
        {
            ViewModel.ReportError(T("String_Cloud_ServerUnreachable"));
            await ShowErrorAsync(T("String_Cloud_ServerUnreachable"));
        }
        catch (Exception ex)
        {
            ViewModel.ReportError(string.Format(T("String_Cloud_LoginFailed"), ex.Message));
            await ShowErrorAsync(string.Format(T("String_Cloud_LoginFailed"), ex.Message));
        }
    }

    private async void OnUseApiTokenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.UseApiTokenAsync();
        }
        catch (HttpRequestException)
        {
            ViewModel.ReportError(T("String_Cloud_ServerUnreachable"));
            await ShowErrorAsync(T("String_Cloud_ServerUnreachable"));
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.Logout();
    }

    private async void OnNewFolderClick(object? sender, RoutedEventArgs e)
    {
        string? strName = await PromptTextAsync(T("Message_Cloud_PromptNewFolder"));
        if (string.IsNullOrWhiteSpace(strName))
            return;

        try
        {
            await ViewModel.CreateFolderAsync(strName);
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnRenameFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder == null)
            return;

        string? strName = await PromptTextAsync(T("Message_Cloud_PromptRenameFolder"), ViewModel.SelectedFolder.Name);
        if (string.IsNullOrWhiteSpace(strName))
            return;

        try
        {
            await ViewModel.RenameSelectedFolderAsync(strName);
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnDeleteFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder == null)
            return;

        if (!await ConfirmAsync(string.Format(T("Message_Cloud_ConfirmDeleteFolder"), ViewModel.SelectedFolder.Name)))
            return;

        try
        {
            await ViewModel.DeleteSelectedFolderAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnPushCurrentClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.PushCurrentCharacterAsync();
        }
        catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            await HandlePushConflictAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnPushSharedClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null)
            return;

        if (!await ConfirmAsync(string.Format(T("Message_Cloud_ConfirmPushShared"), ViewModel.SelectedDocument.DisplayName)))
            return;

        try
        {
            await ViewModel.PushSelectedSharedDocumentAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Tuple<byte[], string> objDownload = await ViewModel.DownloadSelectedDocumentAsync();
            string? strDocumentId = ViewModel.SelectedDocument?.Id;
            string? strRevisionId = ViewModel.SelectedDocument?.Document.CurrentRevision;
            await SaveDownloadAsync(objDownload.Item1, objDownload.Item2, strDocumentId, strRevisionId);
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnArchiveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null)
            return;

        string strPrompt = ViewModel.SelectedDocument.IsArchived
            ? string.Format(T("Message_Cloud_ConfirmUnarchive"), ViewModel.SelectedDocument.DisplayName)
            : string.Format(T("Message_Cloud_ConfirmArchive"), ViewModel.SelectedDocument.DisplayName);
        if (!await ConfirmAsync(strPrompt))
            return;

        try
        {
            await ViewModel.ArchiveOrUnarchiveSelectedDocumentAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnEditMetadataClick(object? sender, RoutedEventArgs e)
    {
        var objDialog = new CloudMetadataDialog(
            ViewModel.GetActiveCharacterDisplayName(),
            ViewModel.GetActiveCharacterDescription(),
            ViewModel.GetActiveCharacterImageUrl());

        CloudMetadataDialogResult? objResult = await objDialog.ShowDialog<CloudMetadataDialogResult?>(this);
        if (objResult == null)
            return;

        try
        {
            await ViewModel.UpdateActiveCharacterMetadataAsync(objResult.DisplayName, objResult.Description, objResult.ImageUrl);
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnRevisionsClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null)
            return;

        var objDialog = new CloudRevisionsDialog(ViewModel.SelectedDocument.Document, ViewModel.SelectedDocument.IsShared,
            ViewModel.SelectedDocument.CanPurge, ViewModel);
        await objDialog.ShowDialog(this);

        try
        {
            await ViewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnFileInFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.FileSelectedDocumentAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnUnfileClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.UnfileSelectedDocumentAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }
}
