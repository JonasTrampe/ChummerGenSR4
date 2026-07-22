using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class CloudDocumentsDialog : Window
{
    private readonly CharacterFileService _characterFileService = new();
    private CloudDocumentEntryViewModel? _draggedDocument;
    private PointerPressedEventArgs? _pendingPressArgs;
    private CloudDocumentEntryViewModel? _pendingPressedDocument;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;

    public CloudDocumentsDialogViewModel ViewModel { get; }

    public CloudDocumentsDialog()
        : this(null, null)
    {
    }

    public CloudDocumentsDialog(CharacterDocument? objActiveCharacter = null, string? strActiveCharacterPath = null)
    {
        ViewModel = new CloudDocumentsDialogViewModel(objActiveCharacter, strActiveCharacterPath);
        AvaloniaXamlLoader.Load(this);
        DataContext = ViewModel;
        Opened += OnOpened;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SetUpDocumentFolderDragDrop();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CloudDocumentsDialogViewModel.SharedMode) || !ViewModel.IsLoggedIn)
            return;

        try
        {
            await ViewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            ViewModel.ReportError(ex.Message);
            await ShowErrorAsync(ex);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.LoginWithOAuthAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnUseApiTokenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.UseApiTokenAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.Logout();
    }

    private async void OnNewFolderClick(object? sender, RoutedEventArgs e)
    {
        string? strName = await PromptTextAsync("Enter the new folder name.");
        if (string.IsNullOrWhiteSpace(strName))
            return;

        try
        {
            await ViewModel.CreateFolderAsync(strName);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnRenameFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder == null)
            return;

        string? strName = await PromptTextAsync("Enter the folder name.", ViewModel.SelectedFolder.Name);
        if (string.IsNullOrWhiteSpace(strName))
            return;

        try
        {
            await ViewModel.RenameSelectedFolderAsync(strName);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnDeleteFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder == null)
            return;

        if (!await ConfirmAsync("Delete the selected folder?"))
            return;

        try
        {
            await ViewModel.DeleteSelectedFolderAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnPushCurrentClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.PushCurrentCharacterAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnPushSharedClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null)
            return;

        if (!await ConfirmAsync("Push the active character to the selected shared document?"))
            return;

        try
        {
            await ViewModel.PushSelectedSharedDocumentAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Tuple<byte[], string> objDownload = await ViewModel.DownloadSelectedDocumentAsync();
            await SaveDownloadAsync(objDownload.Item1, objDownload.Item2);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async void OnArchiveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedDocument == null)
            return;

        string strPrompt = ViewModel.SelectedDocument.IsArchived
            ? "Unarchive the selected document?"
            : "Archive the selected document?";
        if (!await ConfirmAsync(strPrompt))
            return;

        try
        {
            await ViewModel.ArchiveOrUnarchiveSelectedDocumentAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
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
            await ShowErrorAsync(ex);
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
            await ShowErrorAsync(ex);
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
            await ShowErrorAsync(ex);
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
            await ShowErrorAsync(ex);
        }
    }

    private async System.Threading.Tasks.Task SaveDownloadAsync(byte[] bytContent, string strSuggestedFileName)
    {
        var objStorage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (objStorage == null)
            return;

        IStorageFile? objFile = await objStorage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save cloud document",
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
        if (string.IsNullOrEmpty(objCharacter.CloudDocumentId) && ViewModel.SelectedDocument != null)
        {
            objCharacter.CloudDocumentId = ViewModel.SelectedDocument.Id;
            await using Stream objUpdateStream = await objFile.OpenWriteAsync();
            _characterFileService.Save(objCharacter, objUpdateStream, Path.GetFileName(strLocalPath));
        }

        if (Owner is MainWindow objMainWindow)
            ((MainWindow)objMainWindow).LoadCharacterIntoTabs(objCharacter, strLocalPath);
    }

    private async System.Threading.Tasks.Task<string?> PromptTextAsync(string strPrompt, string strInitialValue = "")
    {
        var objDialog = new Window
        {
            Width = 420,
            Height = 160,
            Title = "Input",
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        TextBox objTextBox = new() { Text = strInitialValue };
        string? strResult = null;

        Button objOk = new() { Content = "OK", Width = 80 };
        objOk.Click += (_, _) =>
        {
            strResult = objTextBox.Text;
            objDialog.Close();
        };

        Button objCancel = new() { Content = "Cancel", Width = 80 };
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

    private async System.Threading.Tasks.Task<bool> ConfirmAsync(string strPrompt)
    {
        bool blnConfirmed = false;
        var objDialog = new Window
        {
            Width = 420,
            Height = 170,
            Title = "Confirm",
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        Button objYes = new() { Content = "Yes", Width = 80 };
        objYes.Click += (_, _) =>
        {
            blnConfirmed = true;
            objDialog.Close();
        };

        Button objNo = new() { Content = "No", Width = 80 };
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

    private async System.Threading.Tasks.Task ShowErrorAsync(Exception ex)
    {
        var objDialog = new Window
        {
            Width = 520,
            Height = 180,
            Title = "Cloud Error",
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        Button objOk = new() { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
        objOk.Click += (_, _) => objDialog.Close();

        objDialog.Content = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap },
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
                await ShowErrorAsync(ex);
            }
        });
    }

    private static FolderNodeViewModel? FindFolderNodeAt(IInputElement objTree, Point objPointRelativeToTree)
        => ((objTree.InputHitTest(objPointRelativeToTree) as Visual)?.FindAncestorOfType<TreeViewItem>(true))
            ?.DataContext as FolderNodeViewModel;

    private sealed class CloudMetadataDialog : Window
    {
        public CloudMetadataDialog(string strDisplayName, string strDescription, string strImageUrl)
        {
            Width = 520;
            Height = 260;
            Title = "Cloud Metadata";
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            TextBox objDisplayName = new() { Text = strDisplayName };
            TextBox objDescription = new() { Text = strDescription, AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
            TextBox objImageUrl = new() { Text = strImageUrl };

            Button objOk = new() { Content = "OK", Width = 80 };
            objOk.Click += (_, _) =>
            {
                Close(new CloudMetadataDialogResult(objDisplayName.Text ?? string.Empty, objDescription.Text ?? string.Empty, objImageUrl.Text ?? string.Empty));
            };

            Button objCancel = new() { Content = "Cancel", Width = 80 };
            objCancel.Click += (_, _) => Close(null);

            Content = new Grid
            {
                Margin = new Thickness(12),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children =
                {
                    new TextBlock { Text = "Display Name:", VerticalAlignment = VerticalAlignment.Center },
                    objDisplayName,
                    new TextBlock { Text = "Description:", VerticalAlignment = VerticalAlignment.Top },
                    objDescription,
                    new TextBlock { Text = "Image URL:", VerticalAlignment = VerticalAlignment.Center },
                    objImageUrl,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { objCancel, objOk }
                    }
                }
            };

            Grid.SetColumn(objDisplayName, 1);
            Grid.SetColumn(objDescription, 1);
            Grid.SetColumn(objImageUrl, 1);

            Grid.SetRow((Control)((Grid)Content).Children[1], 0);
            Grid.SetRow((Control)((Grid)Content).Children[2], 1);
            Grid.SetColumn((Control)((Grid)Content).Children[2], 0);
            Grid.SetRow((Control)((Grid)Content).Children[3], 1);
            Grid.SetColumn((Control)((Grid)Content).Children[3], 1);
            Grid.SetRow((Control)((Grid)Content).Children[4], 2);
            Grid.SetRow((Control)((Grid)Content).Children[5], 2);
            Grid.SetColumn((Control)((Grid)Content).Children[5], 1);
            Grid.SetRow((Control)((Grid)Content).Children[6], 4);
            Grid.SetColumnSpan((Control)((Grid)Content).Children[6], 2);
        }
    }

    private sealed record CloudMetadataDialogResult(string DisplayName, string Description, string ImageUrl);

    private sealed class CloudRevisionRow
    {
        public CloudRevisionRow(RunnersPointRevision objRevision, bool blnCurrent)
        {
            Revision = objRevision;
            CreatedAt = objRevision.CreatedAt.ToLocalTime().ToString("g");
            State = objRevision.ValidationState;
            Size = objRevision.SizeBytes.ToString();
            Current = blnCurrent ? "Current" : string.Empty;
        }

        public RunnersPointRevision Revision { get; }
        public string CreatedAt { get; }
        public string State { get; }
        public string Size { get; }
        public string Current { get; }
    }

    private sealed class CloudRevisionsDialog : Window
    {
        private readonly RunnersPointDocument _document;
        private readonly bool _shared;
        private readonly bool _canPurge;
        private readonly CloudDocumentsDialogViewModel _viewModel;
        private readonly ObservableCollection<CloudRevisionRow> _revisions = new();
        private readonly ListBox _listBox;
        private readonly TextBlock _status;
        private readonly Button _downloadButton;
        private readonly Button _purgeRevisionButton;
        private readonly Button _purgeDocumentButton;
        private RunnersPointDocument _currentDocument;

        public CloudRevisionsDialog(RunnersPointDocument objDocument, bool blnShared, bool blnCanPurge,
            CloudDocumentsDialogViewModel objViewModel)
        {
            _document = objDocument;
            _currentDocument = objDocument;
            _shared = blnShared;
            _canPurge = blnCanPurge;
            _viewModel = objViewModel;

            Width = 760;
            Height = 520;
            Title = "Revisions - " + (string.IsNullOrWhiteSpace(objDocument.DisplayName) ? objDocument.Id : objDocument.DisplayName);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _listBox = new ListBox
            {
                ItemsSource = _revisions,
                ItemTemplate = new FuncDataTemplate<CloudRevisionRow>((objRow, _) =>
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("180,140,100,*"),
                        Margin = new Thickness(4, 1),
                        Children =
                        {
                            new TextBlock { Text = objRow.CreatedAt },
                            new TextBlock { Text = objRow.State },
                            new TextBlock { Text = objRow.Size },
                            new TextBlock { Text = objRow.Current }
                        }
                    }, true)
            };
            _listBox.SelectionChanged += (_, _) => UpdateButtons();

            _downloadButton = new Button { Content = "Download", IsEnabled = false };
            _downloadButton.Click += OnDownloadRevisionClick;

            _purgeRevisionButton = new Button { Content = "Purge Revision", IsEnabled = false };
            _purgeRevisionButton.Click += OnPurgeRevisionClick;

            _purgeDocumentButton = new Button
            {
                Content = "Purge Document",
                IsEnabled = _canPurge && _document.ValidationState == "archived"
            };
            _purgeDocumentButton.Click += OnPurgeDocumentClick;

            _status = new TextBlock();

            Content = new Grid
            {
                Margin = new Thickness(12),
                RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("180,140,100,*"),
                        Margin = new Thickness(4,0,4,6),
                        Children =
                        {
                            new TextBlock { Text = "Created", FontWeight = FontWeight.Bold },
                            new TextBlock { Text = "State", FontWeight = FontWeight.Bold },
                            new TextBlock { Text = "Size", FontWeight = FontWeight.Bold },
                            new TextBlock { Text = "Current", FontWeight = FontWeight.Bold }
                        }
                    },
                    _listBox,
                    new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        ItemWidth = 120,
                        Children =
                        {
                            _downloadButton,
                            _purgeRevisionButton,
                            _purgeDocumentButton,
                            new Button
                            {
                                Content = "Close"
                            }
                        }
                    },
                    _status
                }
            };

            Grid.SetColumn((Control)((Grid)((Grid)Content).Children[0]).Children[1], 1);
            Grid.SetColumn((Control)((Grid)((Grid)Content).Children[0]).Children[2], 2);
            Grid.SetColumn((Control)((Grid)((Grid)Content).Children[0]).Children[3], 3);
            Grid.SetRow(_listBox, 1);
            Grid.SetRow((Control)((Grid)Content).Children[2], 2);
            Grid.SetRow(_status, 3);

            ((Button)((WrapPanel)((Grid)Content).Children[2]).Children[3]).Click += (_, _) => Close();
            Opened += async (_, _) => await RefreshAsync();
        }

        private CloudRevisionRow? SelectedRevision => _listBox.SelectedItem as CloudRevisionRow;

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            try
            {
                _status.Text = "Refreshing...";
                _revisions.Clear();
                _currentDocument = (await _viewModel.GetDocumentForRevisionDialogAsync(_document.Id, _shared)).Item1;
                var lstRevisions = await _viewModel.ListRevisionsAsync(_document.Id, _shared);
                foreach (RunnersPointRevision objRevision in lstRevisions)
                    _revisions.Add(new CloudRevisionRow(objRevision, objRevision.Id == _currentDocument.CurrentRevision));
                _status.Text = "Ready";
                _purgeDocumentButton.IsEnabled = _canPurge && _currentDocument.ValidationState == "archived";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
            }
        }

        private void UpdateButtons()
        {
            bool blnSelected = SelectedRevision != null;
            _downloadButton.IsEnabled = blnSelected;
            _purgeRevisionButton.IsEnabled = blnSelected && _canPurge;
        }

        private async void OnDownloadRevisionClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedRevision == null)
                return;

            try
            {
                _status.Text = "Downloading...";
                Tuple<byte[], string> objDownload = await _viewModel.DownloadRevisionAsync(_document.Id, SelectedRevision.Revision.Id, _shared);

                if (Owner is CloudDocumentsDialog objOwner)
                    await objOwner.SaveDownloadAsync(objDownload.Item1, objDownload.Item2);

                _status.Text = "Ready";
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
            }
        }

        private async void OnPurgeRevisionClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedRevision == null || !_canPurge)
                return;

            if (Owner is not CloudDocumentsDialog objOwner || !await objOwner.ConfirmAsync("Purge the selected revision permanently?"))
                return;

            try
            {
                _status.Text = "Purging...";
                await _viewModel.PurgeRevisionAsync(_document.Id, SelectedRevision.Revision.Id, _shared);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
            }
        }

        private async void OnPurgeDocumentClick(object? sender, RoutedEventArgs e)
        {
            if (!_canPurge)
                return;

            if (Owner is not CloudDocumentsDialog objOwner || !await objOwner.ConfirmAsync("Purge the document permanently?"))
                return;

            try
            {
                _status.Text = "Purging...";
                await _viewModel.PurgeDocumentAsync(_document.Id, _shared);
                Close();
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
            }
        }
    }
}
