using System;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Chummer.Core;
using RunnersPoint.Api;

namespace Chummer.NewUI.Dialogs;

public partial class CloudDocumentsDialog
{
    private async Task SaveDownloadAsync(byte[] bytContent, string strSuggestedFileName,
        string? strDocumentId = null, string? strRevisionId = null)
    {
        var objStorage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (objStorage == null)
            return;

        IStorageFile? objFile = await objStorage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("Title_SaveCloudDocument"),
            SuggestedFileName = string.IsNullOrWhiteSpace(strSuggestedFileName) ? "character.chum" : strSuggestedFileName,
            DefaultExtension = "chum",
            FileTypeChoices = [new FilePickerFileType("Chummer characters") { Patterns = ["*.chum"] }],
        });

        if (objFile == null)
            return;

        await using Stream objWriteStream = await objFile.OpenWriteAsync();
        await objWriteStream.WriteAsync(bytContent);

        string? strLocalPath = objFile.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(strLocalPath))
            return;

        using MemoryStream objReadStream = new(bytContent);
        CharacterDocument objCharacter = _characterFileService.Load(objReadStream, Path.GetFileName(strLocalPath));
        bool blnUpdatedCloudLink = false;
        if (!string.IsNullOrWhiteSpace(strDocumentId) && objCharacter.CloudDocumentId != strDocumentId)
        {
            objCharacter.CloudDocumentId = strDocumentId;
            blnUpdatedCloudLink = true;
        }

        if (!string.IsNullOrWhiteSpace(strRevisionId) && objCharacter.CloudLastKnownRevisionId != strRevisionId)
        {
            objCharacter.CloudLastKnownRevisionId = strRevisionId;
            blnUpdatedCloudLink = true;
        }

        if (objCharacter.CloudIsShared != ViewModel.SharedMode)
        {
            objCharacter.CloudIsShared = ViewModel.SharedMode;
            blnUpdatedCloudLink = true;
        }

        if (blnUpdatedCloudLink)
        {
            await using Stream objUpdateStream = await objFile.OpenWriteAsync();
            _characterFileService.Save(objCharacter, objUpdateStream, Path.GetFileName(strLocalPath));
        }

        if (Owner is MainWindow objMainWindow)
            ((MainWindow)objMainWindow).LoadCharacterIntoTabs(objCharacter, strLocalPath);
    }

    private async Task<string?> PromptTextAsync(string strPrompt, string strInitialValue = "")
    {
        var objDialog = new Window
        {
            Width = 420,
            Height = 160,
            Title = T("Title_Input"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        TextBox objTextBox = new() { Text = strInitialValue };
        string? strResult = null;

        Button objOk = new() { Content = T("String_OK"), Width = 80 };
        objOk.Click += (_, _) =>
        {
            strResult = objTextBox.Text;
            objDialog.Close();
        };

        Button objCancel = new() { Content = T("String_Cancel"), Width = 80 };
        objCancel.Click += (_, _) => objDialog.Close();

        objDialog.Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Children =
            {
                new TextBlock { Text = strPrompt },
                new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 10,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children =
                    {
                        objTextBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { objCancel, objOk }
                        }
                    }
                }
            }
        };

        Grid objContentGrid = (Grid)objDialog.Content;
        Grid.SetRow((Control)objContentGrid.Children[1], 1);

        await objDialog.ShowDialog(this);
        return strResult;
    }

    private async Task<bool> ConfirmAsync(string strPrompt)
    {
        bool blnConfirmed = false;
        var objDialog = new Window
        {
            Width = 420,
            Height = 170,
            Title = T("Title_Confirm"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        Button objYes = new() { Content = T("String_Yes"), Width = 80 };
        objYes.Click += (_, _) =>
        {
            blnConfirmed = true;
            objDialog.Close();
        };

        Button objNo = new() { Content = T("String_No"), Width = 80 };
        objNo.Click += (_, _) => objDialog.Close();

        objDialog.Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = strPrompt, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { objNo, objYes }
                }
            }
        };

        Grid.SetRow((Control)((Grid)objDialog.Content).Children[1], 1);
        await objDialog.ShowDialog(this);
        return blnConfirmed;
    }

    private async Task HandlePushConflictAsync(bool blnShared = false)
    {
        CharacterDocument? objLocalCharacter = ViewModel.ActiveCharacter;
        if (objLocalCharacter == null || string.IsNullOrEmpty(objLocalCharacter.CloudDocumentId))
        {
            await ShowErrorAsync(T("String_Cloud_PushStale"));
            return;
        }

        CharacterDocument? objServerCharacter = null;
        try
        {
            Tuple<RunnersPointDocument, string> objCurrent = await ViewModel.GetDocumentForRevisionDialogAsync(
                objLocalCharacter.CloudDocumentId, blnShared);
            Tuple<byte[], string> objDownload = await ViewModel.DownloadRevisionAsync(
                objLocalCharacter.CloudDocumentId, objCurrent.Item1.CurrentRevision, blnShared);
            using MemoryStream objStream = new(objDownload.Item1);
            objServerCharacter = _characterFileService.Load(objStream, objDownload.Item2);
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
            return;
        }

        CharacterDiffResult objDiff = CharacterDiff.Compare(objLocalCharacter, objServerCharacter);
        CharacterMergeResult? objMerge = null;
        if (!string.IsNullOrWhiteSpace(objLocalCharacter.CloudLastKnownRevisionId))
        {
            try
            {
                Tuple<byte[], string> objBaseDownload = await ViewModel.DownloadRevisionAsync(
                    objLocalCharacter.CloudDocumentId, objLocalCharacter.CloudLastKnownRevisionId, blnShared);
                using MemoryStream objBaseStream = new(objBaseDownload.Item1);
                CharacterDocument objBaseCharacter = _characterFileService.Load(objBaseStream, objBaseDownload.Item2);
                objMerge = CharacterMergeService.TryMerge(objBaseCharacter, objLocalCharacter, objServerCharacter);
            }
            catch
            {
                // A missing/expired ancestor should not prevent the normal overwrite/local/cancel
                // choices from being offered.
            }
        }

        bool blnCanMerge = objMerge?.CanMerge == true;
        string[] astrChoices = blnCanMerge
            ? new[] { T("Button_CloudConflict_Merge"), T("Button_CloudConflict_OverwriteServer"),
                T("Button_CloudConflict_SaveLocallyOnly"), T("Button_CloudConflict_Cancel") }
            : new[] { T("Button_CloudConflict_OverwriteServer"), T("Button_CloudConflict_SaveLocallyOnly"),
                T("Button_CloudConflict_Cancel") };
        int intChoice = await ShowChoiceDialogAsync(T("Title_CloudConflict"),
            BuildConflictMessage(objDiff, objMerge), astrChoices);

        if (blnCanMerge && intChoice == 0)
        {
            try
            {
                if (blnShared)
                    await ViewModel.PushMergedSharedDocumentAsync(objMerge!.MergedCharacter!);
                else
                    await ViewModel.PushMergedCharacterAsync(objMerge!.MergedCharacter!);
            }
            catch (Exception ex)
            {
                await HandleCloudExceptionAsync(ex);
            }
            return;
        }

        int intSaveLocalChoice = blnCanMerge ? 2 : 1;
        int intCancelChoice = blnCanMerge ? 3 : 2;
        if (intChoice == intSaveLocalChoice)
        {
            ViewModel.SaveActiveCharacterSnapshot();
            return;
        }

        if (intChoice == intCancelChoice)
            return;

        try
        {
            if (blnShared)
                await ViewModel.ForcePushSharedDocumentAsync();
            else
                await ViewModel.ForcePushCurrentCharacterAsync();
        }
        catch (Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async Task<int> ShowChoiceDialogAsync(string strTitle, string strPrompt, params string[] astrButtons)
    {
        int intChoice = -1;
        var objDialog = new Window
        {
            Width = 640,
            Height = 320,
            Title = strTitle,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        StackPanel objButtonsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        for (int i = 0; i < astrButtons.Length; i++)
        {
            int intIndex = i;
            Button objButton = new() { Content = astrButtons[i], MinWidth = 130 };
            objButton.Click += (_, _) =>
            {
                intChoice = intIndex;
                objDialog.Close();
            };
            objButtonsPanel.Children.Add(objButton);
        }

        objDialog.Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = strPrompt,
                        TextWrapping = TextWrapping.Wrap
                    }
                },
                objButtonsPanel
            }
        };

        Grid.SetRow(objButtonsPanel, 1);
        await objDialog.ShowDialog(this);
        return intChoice;
    }

    private static string BuildConflictMessage(CharacterDiffResult objDiff, CharacterMergeResult? objMerge)
    {
        StringBuilder sb = new();
        sb.AppendLine(T("Label_CloudConflict_Explanation"));
        sb.AppendLine();

        if (!objDiff.HasChanges)
            return T("String_CloudConflict_NoDetectableDifference");

        foreach (CharacterDiffEntry objEntry in objDiff.Entries)
        {
            if (!string.IsNullOrWhiteSpace(objEntry.Collection))
                sb.Append('[').Append(objEntry.Collection).Append("] ");

            sb.Append(objEntry.Name).Append(" (" ).Append(objEntry.Change).AppendLine(")");
            sb.Append("  ").Append(T("Label_CloudConflict_Local")).Append(' ')
                .AppendLine(string.IsNullOrEmpty(objEntry.LocalValue) ? "—" : objEntry.LocalValue);
            sb.Append("  ").Append(T("Label_CloudConflict_Server")).Append(' ')
                .AppendLine(string.IsNullOrEmpty(objEntry.ServerValue) ? "—" : objEntry.ServerValue);
            sb.AppendLine();
        }

        if (objMerge?.CanMerge == true)
            sb.AppendLine().Append(T("String_CloudConflict_MergeAvailable").Replace("{0}",
                objMerge.AutoMergedChanges.ToString(CultureInfo.InvariantCulture))).AppendLine();
        else if (objMerge != null && objMerge.Conflicts.Count > 0)
            sb.AppendLine().AppendLine(T("String_CloudConflict_MergeUnavailable"));

        return sb.ToString().TrimEnd();
    }

    private string TranslateCloudException(Exception ex)
    {
        string strMessage = ex.Message;

        switch (ex)
        {
            case HttpRequestException:
                strMessage = T("String_Cloud_ServerUnreachable");
                break;
            case RunnersPointApiException objApiException when objApiException.StatusCode == HttpStatusCode.Unauthorized:
                if (ViewModel.IsApiTokenLogin())
                {
                    strMessage = T("String_Cloud_AuthTokenRejected");
                }
                else
                {
                    HandleAuthExpired();
                    strMessage = T("String_Cloud_AuthExpired");
                }

                break;
            case RunnersPointApiException objApiException when objApiException.StatusCode == HttpStatusCode.PreconditionFailed:
                strMessage = T("String_Cloud_PushStale");
                break;
            case RunnersPointApiException objApiException when objApiException.StatusCode == HttpStatusCode.Conflict:
                strMessage = T("String_Cloud_PushIdempotencyConflict");
                break;
            case RunnersPointApiException objApiException when objApiException.ProblemCode == "purge_not_eligible":
                strMessage = T("String_CloudRevisions_PurgeNotEligible");
                break;
            case RunnersPointApiException objApiException when objApiException.ProblemCode == "recent_authentication_required":
                strMessage = T("String_CloudRevisions_RecentAuthRequired");
                break;
        }

        return strMessage;
    }

    private async Task HandleCloudExceptionAsync(Exception ex)
    {
        string strMessage = TranslateCloudException(ex);
        ViewModel.ReportError(strMessage);
        await ShowErrorAsync(strMessage);
    }

    private void HandleAuthExpired()
    {
        if (!ViewModel.IsApiTokenLogin())
            ViewModel.Logout();
    }

    private async Task ShowErrorAsync(string strMessage)
    {
        var objDialog = new Window
        {
            Width = 520,
            Height = 180,
            Title = T("Title_CloudDocuments"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        Button objOk = new() { Content = T("String_OK"), Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
        objOk.Click += (_, _) => objDialog.Close();

        objDialog.Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = strMessage, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { objOk }
                }
            }
        };

        Grid.SetRow((Control)((Grid)objDialog.Content).Children[1], 1);
        await objDialog.ShowDialog(this);
    }
}
