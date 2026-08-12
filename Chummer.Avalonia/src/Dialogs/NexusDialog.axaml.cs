using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.NewUI.ViewModels;

namespace Chummer.NewUI.Dialogs;

/// <summary>Ported from frmSelectNexus.cs: builds a custom Nexus (a build-your-own Matrix
/// node) by prompting for Processor/System/Response/Firewall/Signal/Persona-Limit.</summary>
public partial class NexusDialog : Window
{
    public NexusDialogViewModel ViewModel { get; } = new();

    public int Processor => (int)ViewModel.Processor;
    public int System => (int)ViewModel.System;
    public int Response => (int)ViewModel.Response;
    public int Firewall => (int)ViewModel.Firewall;
    public int Signal => (int)ViewModel.Signal;
    public int Persona => (int)ViewModel.Persona;
    public bool Free => ViewModel.Free;

    public NexusDialog()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
