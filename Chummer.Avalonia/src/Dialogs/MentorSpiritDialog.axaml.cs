using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

public partial class MentorSpiritDialog : Window
{
    public MentorSpiritDialogViewModel ViewModel { get; } = new();
    public MentorSpiritOptionViewModel? SelectedMentor => ViewModel.SelectedMentor;
    public string? Choice => ViewModel.Choice;

    public MentorSpiritDialog() : this("mentors.xml") { }

    public MentorSpiritDialog(string strDataFile)
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.LoadOptions(strDataFile);
    }

    private void OnOk(object? sender, RoutedEventArgs e) { if (SelectedMentor != null) Close(true); }
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
