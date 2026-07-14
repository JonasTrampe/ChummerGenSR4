using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.AvaloniaSpike.Dialogs;

namespace Chummer.AvaloniaSpike;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Demo wiring only, for the look-and-feel spike: chains the three real character-creation
    // dialogs (settings profile -> GP count -> metatype) the same way the real app's "Neu"
    // toolbar button does.
    private async void OnNewCharacterClick(object? sender, RoutedEventArgs e)
    {
        var settingsDialog = new SettingsProfileDialog();
        await settingsDialog.ShowDialog(this);

        var gpDialog = new KarmaGpDialog();
        await gpDialog.ShowDialog(this);

        var metatypeDialog = new MetatypeDialog();
        await metatypeDialog.ShowDialog(this);
    }

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new QualityDialog();
        await dialog.ShowDialog(this);
    }

    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SpellDialog();
        await dialog.ShowDialog(this);
    }
}