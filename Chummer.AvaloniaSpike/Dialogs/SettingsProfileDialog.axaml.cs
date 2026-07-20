using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer;

namespace Chummer.AvaloniaSpike.Dialogs;

public partial class SettingsProfileDialog : Window
{
	public IReadOnlyList<ListItem> SettingsProfiles { get; } = new List<ListItem>
	{
		new ListItem { Name = "Default Settings", Value = "default.xml" },
	};

    public SettingsProfileDialog()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
		DataContext = this;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
