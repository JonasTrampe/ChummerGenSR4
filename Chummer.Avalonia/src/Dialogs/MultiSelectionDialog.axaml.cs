using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Chummer.NewUI.Dialogs;

/// <summary>Checkbox-list picker for actions that need 2+ items chosen at once (e.g. Create
/// Stacked Focus), unlike <see cref="ListSelectionDialog"/>'s single-selection list.</summary>
public partial class MultiSelectionDialog : Window
{
    public sealed class CheckableOption : INotifyPropertyChanged
    {
        private bool _blnIsChecked;

        public CheckableOption(string strLabel, string strValue)
        {
            Label = strLabel;
            Value = strValue;
        }

        public string Label { get; }
        public string Value { get; }

        public bool IsChecked
        {
            get => _blnIsChecked;
            set
            {
                _blnIsChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly List<CheckableOption> _lstOptions;

    public IReadOnlyList<string> SelectedValues => _lstOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();

    public MultiSelectionDialog(string strDescription, IReadOnlyList<(string Label, string Value)> lstOptions)
    {
        InitializeComponent();
        DescriptionText.Text = strDescription;
        _lstOptions = lstOptions.Select(o => new CheckableOption(o.Label, o.Value)).ToList();
        OptionsList.ItemsSource = _lstOptions;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (SelectedValues.Count >= 2)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
