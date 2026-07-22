using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Chummer.Core;
using Chummer.NewUI.Localization;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class OptionsDialog : Window
{
    public OptionsDialogViewModel ViewModel { get; } = new();

    public OptionsDialog()
    {
        DataContext = ViewModel;
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        AvaloniaLocalizationHelper.Apply(this);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        string strPreviousLanguage = GlobalOptions.Instance.Language;
        ViewModel.Save();
        if (!string.Equals(strPreviousLanguage, GlobalOptions.Instance.Language, StringComparison.OrdinalIgnoreCase))
            App.LanguageCatalog.Load(GlobalOptions.Instance.Language);
        Close(true);
    }

    public async void OnBrowsePdfApp(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? objStorage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (objStorage == null)
            return;

        var lstFiles = await objStorage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "PDF-Betrachter auswählen",
            AllowMultiple = false
        });

        if (lstFiles.Count > 0)
            ViewModel.PdfAppPath = lstFiles[0].TryGetLocalPath() ?? string.Empty;
    }

    public void OnResetBp(object? sender, RoutedEventArgs e)
    {
        ViewModel.RestoreBpDefaults();
    }

    public void OnResetKarma(object? sender, RoutedEventArgs e)
    {
        ViewModel.RestoreKarmaDefaults();
    }

    public void OnVerifyLanguage(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedLanguage?.Value == null)
            return;

        LanguageManager.Instance.VerifyStrings(ViewModel.SelectedLanguage.Value);
    }

    public async void OnVerifyData(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedLanguage?.Value == null)
            return;

        var lstBooks = new System.Collections.Generic.List<string>();
        foreach (OptionsBookItemViewModel objBook in ViewModel.Sourcebooks)
        {
            if (objBook.IsSelected)
                lstBooks.Add(objBook.Code);
        }

        XmlManager.Instance.Verify(ViewModel.SelectedLanguage.Value, lstBooks);
        string strFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "lang",
            "results_" + ViewModel.SelectedLanguage.Value + ".xml");

        await ShowInfoDialogAsync("Validation Results", "Results were written to " + strFilePath);
    }

    private async System.Threading.Tasks.Task ShowInfoDialogAsync(string strTitle, string strMessage)
    {
        Window objDialog = new Window
        {
            Width = 520,
            Height = 180,
            Title = strTitle,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(12),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = strMessage, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Width = 90
                    }
                }
            }
        };

        if (objDialog.Content is StackPanel objPanel && objPanel.Children[1] is Button objButton)
            objButton.Click += (_, _) => objDialog.Close();

        await objDialog.ShowDialog(this);
    }
}
