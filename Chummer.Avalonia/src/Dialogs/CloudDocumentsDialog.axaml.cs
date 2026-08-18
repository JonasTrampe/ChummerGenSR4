using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

// Split by concern into partial-class files: .Actions.cs (toolbar Click handlers),
// .Helpers.cs (prompt/confirm/error dialogs, cloud-conflict resolution, download saving),
// .DragDrop.cs (document-to-folder drag/drop), plus two nested-type files:
// .CloudMetadataDialog.cs and .CloudRevisionsDialog.cs. This file keeps construction/lifecycle.
public partial class CloudDocumentsDialog : Window
{
    private readonly CharacterFileService _characterFileService = new();

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
        catch (System.Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnOpened;
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (System.Exception ex)
        {
            await HandleCloudExceptionAsync(ex);
        }
    }

    private static string T(string strKey)
    {
        return App.LanguageCatalog.GetString(strKey);
    }
}
