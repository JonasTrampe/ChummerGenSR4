using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using RunnersPoint.Api;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class CloudDocumentsDialog
{
    private sealed class CloudRevisionRow
    {
        public CloudRevisionRow(RunnersPointRevision objRevision, bool blnCurrent)
        {
            Revision = objRevision;
            CreatedAt = objRevision.CreatedAt.ToLocalTime().ToString("g");
            State = objRevision.ValidationState;
            Size = objRevision.SizeBytes.ToString();
            Current = blnCurrent ? T("String_CloudRevisions_Current") : string.Empty;
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
            Title = T("Title_CloudRevisions").Replace("{0}", string.IsNullOrWhiteSpace(objDocument.DisplayName) ? objDocument.Id : objDocument.DisplayName);
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

            _downloadButton = new Button { Content = T("Button_CloudRevisions_Download"), IsEnabled = false };
            _downloadButton.Click += OnDownloadRevisionClick;

            _purgeRevisionButton = new Button { Content = T("Button_CloudRevisions_PurgeRevision"), IsEnabled = false };
            _purgeRevisionButton.Click += OnPurgeRevisionClick;

            _purgeDocumentButton = new Button
            {
                Content = T("Button_CloudRevisions_PurgeDocument"),
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
                            new TextBlock { Text = T("String_Cloud_RevisionCreated"), FontWeight = FontWeight.Bold },
                            new TextBlock { Text = T("String_Cloud_RevisionState"), FontWeight = FontWeight.Bold },
                            new TextBlock { Text = T("String_Cloud_RevisionSize"), FontWeight = FontWeight.Bold },
                            new TextBlock { Text = T("String_CloudRevisions_Current"), FontWeight = FontWeight.Bold }
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
                                Content = T("Button_CloudRevisions_Close")
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

        private async Task RefreshAsync()
        {
            try
            {
                _status.Text = T("String_Cloud_Refreshing");
                _revisions.Clear();
                _currentDocument = (await _viewModel.GetDocumentForRevisionDialogAsync(_document.Id, _shared)).Item1;
                var lstRevisions = await _viewModel.ListRevisionsAsync(_document.Id, _shared);
                foreach (RunnersPointRevision objRevision in lstRevisions)
                    _revisions.Add(new CloudRevisionRow(objRevision, objRevision.Id == _currentDocument.CurrentRevision));
                _status.Text = T("String_Cloud_Ready");
                _purgeDocumentButton.IsEnabled = _canPurge && _currentDocument.ValidationState == "archived";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                _status.Text = Owner is CloudDocumentsDialog objDialog
                    ? objDialog.TranslateCloudException(ex)
                    : ex.Message;
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
                _status.Text = T("String_Cloud_Downloading");
                Tuple<byte[], string> objDownload = await _viewModel.DownloadRevisionAsync(_document.Id, SelectedRevision.Revision.Id, _shared);

                if (Owner is CloudDocumentsDialog objOwner)
                    await objOwner.SaveDownloadAsync(objDownload.Item1, objDownload.Item2, _document.Id, SelectedRevision.Revision.Id);

                _status.Text = T("String_Cloud_Ready");
            }
            catch (Exception ex)
            {
                _status.Text = Owner is CloudDocumentsDialog objDialog
                    ? objDialog.TranslateCloudException(ex)
                    : ex.Message;
            }
        }

        private async void OnPurgeRevisionClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedRevision == null || !_canPurge)
                return;

            if (Owner is not CloudDocumentsDialog objOwner || !await objOwner.ConfirmAsync(T("Message_CloudRevisions_ConfirmPurgeRevision").Replace("{0}", SelectedRevision.CreatedAt)))
                return;

            try
            {
                _status.Text = T("String_CloudRevisions_Purging");
                await _viewModel.PurgeRevisionAsync(_document.Id, SelectedRevision.Revision.Id, _shared);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _status.Text = Owner is CloudDocumentsDialog objDialog
                    ? objDialog.TranslateCloudException(ex)
                    : ex.Message;
            }
        }

        private async void OnPurgeDocumentClick(object? sender, RoutedEventArgs e)
        {
            if (!_canPurge)
                return;

            if (Owner is not CloudDocumentsDialog objOwner || !await objOwner.ConfirmAsync(T("Message_CloudRevisions_ConfirmPurgeDocument").Replace("{0}", _document.DisplayName ?? _document.Id)))
                return;

            try
            {
                _status.Text = T("String_CloudRevisions_Purging");
                await _viewModel.PurgeDocumentAsync(_document.Id, _shared);
                Close();
            }
            catch (Exception ex)
            {
                _status.Text = Owner is CloudDocumentsDialog objDialog
                    ? objDialog.TranslateCloudException(ex)
                    : ex.Message;
            }
        }
    }
}
