using System;
using System.IO;
using System.Net;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Chummer.Core;
using Chummer.NewUI.Api;
using Chummer.NewUI.Dialogs;
using Chummer.NewUI.ViewModels;
using KarmaGpDialog = Chummer.NewUI.Dialogs.KarmaGpDialog;
using MetatypeDialog = Chummer.NewUI.Dialogs.MetatypeDialog;
using RunnersPointAuth = Chummer.NewUI.Api.RunnersPointAuth;
using SettingsProfileDialog = Chummer.NewUI.Dialogs.SettingsProfileDialog;
using SheetPreviewDialog = Chummer.NewUI.Dialogs.SheetPreviewDialog;

namespace Chummer.NewUI;

public partial class MainWindow : Window
{
    private readonly CharacterFileService _characterFiles = new CharacterFileService();
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        DataContext = new MainWindowViewModel();
    }

    // Demo wiring only, for the look-and-feel spike: chains the three real character-creation
    // dialogs (settings profile -> GP count -> metatype) the same way the real app's "Neu"
    // toolbar button does.
    private async void OnNewCharacterClick(object? sender, RoutedEventArgs e)
    {
        var settingsDialog = new SettingsProfileDialog();
        SettingsProfileSelection? objSettingsSelection = await settingsDialog.ShowDialog<SettingsProfileSelection?>(this);
        if (objSettingsSelection == null)
            return;

        var gpDialog = new KarmaGpDialog(objSettingsSelection);
        BuildSelection? objBuildSelection = await gpDialog.ShowDialog<BuildSelection?>(this);
        if (objBuildSelection == null)
            return;

        var metatypeDialog = new MetatypeDialog();
        MetatypeSelection? objMetatypeSelection = await metatypeDialog.ShowDialog<MetatypeSelection?>(this);
        if (objMetatypeSelection == null)
            return;

        string strCharacterName = "New " + objMetatypeSelection.Metatype.Name;
        CharacterDocument objCharacter = NewCharacterFactory.CreateNewCharacter(
            strCharacterName,
            objSettingsSelection.FileName,
            objBuildSelection.BuildMethod,
            objBuildSelection.BuildPoints,
            objBuildSelection.MaxAvailability,
            objMetatypeSelection.Metatype,
            objMetatypeSelection.MetavariantName,
            objBuildSelection.IgnoreCreationRules);
        ViewModel.AddOpenCharacter(objCharacter, null);
    }

    private async void OnOpenCharacterClick(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Chummer character",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Chummer characters") { Patterns = new[] { "*.chum", "*.xml" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count > 0)
        {
            try
            {
                string? strLocalPath = files[0].TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(strLocalPath))
                {
                    await LoadCharacterFromPathAsync(strLocalPath);
                }
                else
                {
                    await using var stream = await files[0].OpenReadAsync();
                    CharacterDocument character = _characterFiles.Load(stream, files[0].Name);
                    ViewModel.AddOpenCharacter(character, null);
                }
            }
            catch (Exception ex)
            {
                // CharacterFileService already traced the failure; surface it in the UI too -
                // silently swallowing it here made a bad file look identical to "nothing happened".
                ViewModel.ReportError("Fehler beim Öffnen von " + files[0].Name + ": " + ex.Message);
            }
        }
    }

    private void OnCloseCharacterTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: OpenCharacterTab tab })
            return;

        ViewModel.CloseCharacter(tab);
    }

    private async void OnSaveCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedOpenCharacter is not { } tab)
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Chummer character",
            DefaultExtension = "chum",
            SuggestedFileName = tab.Character.Name,
            FileTypeChoices = [new FilePickerFileType("Chummer characters") { Patterns = ["*.chum"] }],
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            _characterFiles.Save(tab.Character, stream, file.Name);
            string? strLocalPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(strLocalPath))
                ViewModel.RememberSavedPath(strLocalPath);
        }
    }

    private async void OnOpenRecentCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: RecentCharacterEntryViewModel entry })
            return;

        try
        {
            await LoadCharacterFromPathAsync(entry.FilePath);
        }
        catch (Exception ex)
        {
            ViewModel.RemoveRecentCharacter(entry.FilePath, entry.IsSticky);
            ViewModel.ReportError("Fehler beim Öffnen von " + entry.FilePath + ": " + ex.Message);
        }
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SheetPreviewDialog();
        await dialog.ShowDialog(this);
    }

    private async void OnCloudDocumentsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new CloudDocumentsDialog(ViewModel.SelectedOpenCharacter?.Character, ViewModel.SelectedOpenCharacter?.SourcePath);
        await dialog.ShowDialog(this);
    }

    private async void OnOptionsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new OptionsDialog();
        await dialog.ShowDialog(this);
    }

    public void LoadCharacterIntoTabs(CharacterDocument character, string? sourcePath = null)
    {
        ViewModel.AddOpenCharacter(character, sourcePath);
    }

    private async System.Threading.Tasks.Task LoadCharacterFromPathAsync(string strFilePath)
    {
        if (!await CheckCloudFreshnessAsync(strFilePath))
            return;

        await using var objStream = File.OpenRead(strFilePath);
        CharacterDocument objCharacter = _characterFiles.Load(objStream, Path.GetFileName(strFilePath));
        ViewModel.AddOpenCharacter(objCharacter, strFilePath);
    }

    private static Tuple<string, string>? PeekCloudIds(string strFilePath)
    {
        try
        {
            var objXmlDocument = new XmlDocument();
            using FileStream objStream = File.OpenRead(strFilePath);
            objXmlDocument.Load(objStream);

            XmlNode? objIdNode = objXmlDocument.SelectSingleNode("/character/clouddocumentid");
            if (objIdNode == null || string.IsNullOrWhiteSpace(objIdNode.InnerText))
                return null;

            XmlNode? objRevisionNode = objXmlDocument.SelectSingleNode("/character/cloudlastknownrevisionid");
            return Tuple.Create(objIdNode.InnerText, objRevisionNode?.InnerText ?? string.Empty);
        }
        catch (Exception ex) when (ex is XmlException || ex is IOException || ex is UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async System.Threading.Tasks.Task<bool> CheckCloudFreshnessAsync(string strFilePath)
    {
        Tuple<string, string>? objCloudIds = PeekCloudIds(strFilePath);
        if (objCloudIds == null)
            return true;

        if (!await EnsureCloudLoginForOpenAsync())
            return false;

        string strDocumentId = objCloudIds.Item1;
        string strLastKnownRevisionId = objCloudIds.Item2;
        if (string.IsNullOrEmpty(strLastKnownRevisionId))
            return true;

        bool blnShared = false;
        try
        {
            await using FileStream objReadStream = File.OpenRead(strFilePath);
            CharacterDocument objLinkedCharacter = _characterFiles.Load(objReadStream, Path.GetFileName(strFilePath));
            blnShared = objLinkedCharacter.CloudIsShared;
        }
        catch
        {
            // If the lightweight read fails here, continue with the XML-peek result only. The full
            // load later will still surface any real file-corruption problem to the user.
        }

        RunnersPointDocument objDocument;
        try
        {
            objDocument = await GetCloudDocumentForFreshnessCheckAsync(strDocumentId, blnShared, strFilePath);
        }
        catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            await ShowMessageDialogAsync(
                T("MessageTitle_CloudLoad_DocumentMissing"),
                T("Message_CloudLoad_DocumentMissing"));
            return true;
        }
        catch (Exception ex)
        {
            if (GlobalOptions.Instance.SuppressCloudUnreachableWarning)
                return true;

            int intChoice = await ShowChoiceDialogAsync(
                T("MessageTitle_CloudLoad_CheckFailed"),
                T("Message_CloudLoad_CheckFailed").Replace("{0}", ex.Message),
                T("Button_CloudLoad_Abort"),
                T("Button_CloudLoad_OpenLocalFile"));
            return intChoice == 1;
        }

        if (objDocument.CurrentRevision == strLastKnownRevisionId)
            return true;

        int intNewerChoice = await ShowChoiceDialogAsync(
            T("MessageTitle_CloudLoad_NewerRevision"),
            T("Message_CloudLoad_NewerRevision"),
            T("Button_CloudLoad_DownloadNewer"),
            T("Button_CloudLoad_OpenLocalCopy"),
            T("Button_CloudLoad_Cancel"));
        if (intNewerChoice == 1)
            return true;
        if (intNewerChoice != 0)
            return false;

        try
        {
            Tuple<byte[], string> objDownload = await CreateCloudApiClient().DownloadRevisionAsync(strDocumentId, objDocument.CurrentRevision);
            string strBackupFilePath = strFilePath + ".chum-bak-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(strFilePath, strBackupFilePath, true);
            CharacterDocument objCharacter;
            using (MemoryStream objMemoryStream = new(objDownload.Item1))
            {
                objCharacter = _characterFiles.Load(objMemoryStream, Path.GetFileName(strFilePath));
            }

            objCharacter.CloudDocumentId = strDocumentId;
            objCharacter.CloudLastKnownRevisionId = objDocument.CurrentRevision;
            using FileStream objWriteStream = File.Create(strFilePath);
            _characterFiles.Save(objCharacter, objWriteStream, Path.GetFileName(strFilePath));
            return true;
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync(
                T("MessageTitle_CloudLoad_DownloadFailed"),
                T("Message_CloudLoad_DownloadFailed").Replace("{0}", ex.Message));
            return false;
        }
    }

    private async System.Threading.Tasks.Task<bool> EnsureCloudLoginForOpenAsync()
    {
        var objAuth = new RunnersPointAuth();
        if (objAuth.HasStoredLogin())
            return true;

        int intChoice = await ShowChoiceDialogAsync(
            T("MessageTitle_CloudLoad_LoginRequired"),
            T("Message_CloudLoad_LoginRequired"),
            T("Button_CloudLoad_ConfigureCloud"),
            T("Button_CloudLoad_OpenLocalFile"),
            T("Button_CloudLoad_Abort"));
        if (intChoice == 1)
            return true;
        if (intChoice != 0)
            return false;

        var objDialog = new CloudDocumentsDialog();
        await objDialog.ShowDialog(this);
        return objAuth.HasStoredLogin();
    }

    private async System.Threading.Tasks.Task<RunnersPointDocument> GetCloudDocumentForFreshnessCheckAsync(
        string strDocumentId, bool blnShared, string strFilePath)
    {
        if (blnShared)
            return (await CreateCloudApiClient().GetSharedDocumentAsync(strDocumentId)).Item1;

        try
        {
            return (await CreateCloudApiClient().GetDocumentAsync(strDocumentId)).Item1;
        }
        catch (RunnersPointApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Tuple<RunnersPointSharedDocument, string> objShared = await CreateCloudApiClient().GetSharedDocumentAsync(strDocumentId);
            await MarkLocalCloudLinkAsSharedAsync(strFilePath);
            return objShared.Item1;
        }
    }

    private static RunnersPointApiClient CreateCloudApiClient()
    {
        return new RunnersPointApiClient(new RunnersPointAuth());
    }

    private async System.Threading.Tasks.Task MarkLocalCloudLinkAsSharedAsync(string strFilePath)
    {
        try
        {
            await using FileStream objReadStream = File.OpenRead(strFilePath);
            CharacterDocument objCharacter = _characterFiles.Load(objReadStream, Path.GetFileName(strFilePath));
            if (objCharacter.CloudIsShared)
                return;

            objCharacter.CloudIsShared = true;
            await using FileStream objWriteStream = File.Create(strFilePath);
            _characterFiles.Save(objCharacter, objWriteStream, Path.GetFileName(strFilePath));
        }
        catch
        {
            // Best-effort migration of legacy local files; the cloud read already succeeded, so
            // don't block opening if persisting the marker fails.
        }
    }

    private async System.Threading.Tasks.Task<int> ShowChoiceDialogAsync(string strTitle, string strMessage, params string[] astrButtons)
    {
        var objDialog = new Window
        {
            Width = 540,
            Height = 200,
            Title = strTitle,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        int intChoice = -1;
        StackPanel objButtonsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        for (int i = 0; i < astrButtons.Length; i++)
        {
            int intIndex = i;
            Button objButton = new()
            {
                Content = astrButtons[i],
                MinWidth = 120
            };
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
                new TextBlock { Text = strMessage, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                objButtonsPanel
            }
        };

        Grid.SetRow(objButtonsPanel, 1);
        await objDialog.ShowDialog(this);
        return intChoice;
    }

    private System.Threading.Tasks.Task ShowMessageDialogAsync(string strTitle, string strMessage)
    {
        return ShowChoiceDialogAsync(strTitle, strMessage, T("String_OK"));
    }

    private static string T(string strKey)
    {
        return App.LanguageCatalog.GetString(strKey);
    }

    private void OnCloseWindowClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
