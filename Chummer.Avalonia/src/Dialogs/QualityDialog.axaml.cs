using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Chummer.Core;

namespace Chummer.NewUI.Dialogs;

public partial class QualityDialog : Window
{
    public ObservableCollection<QualityOption> QualityOptions { get; } = new();

    public QualityOption? SelectedQuality { get; private set; }

    public QualityDialog()
    {
        DataContext = this;
        InitializeComponent();
    }

    public QualityDialog(CharacterDocument character)
        : this()
    {
        LoadOptions(character);
    }

    private void LoadOptions(CharacterDocument character)
    {
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterQualityData quality in character.Qualities)
            existingNames.Add(quality.Name);

        XmlDocument document = XmlManager.Instance.Load("qualities.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/qualities/quality");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            string category = node["category"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || (category != "Positive" && category != "Negative") || existingNames.Contains(name))
                continue;

            QualityOptions.Add(new QualityOption(name, category, node["bp"]?.InnerText ?? "0",
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var option = QualityList.SelectedItem as QualityOption;
        CostText.Text = option?.Cost ?? "–";
        SourceText.Text = option is null ? "–" : option.SourcePage;
        CategoryText.Text = option?.Category ?? "–";
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        SelectedQuality = QualityList.SelectedItem as QualityOption;
        if (SelectedQuality != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}

public sealed class QualityOption
{
    public QualityOption(string name, string category, string cost, string source, string page)
    {
        Name = name;
        Category = category;
        Cost = cost;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public string Cost { get; }
    public string SourcePage { get; }
    public string DisplayName => Name + " (" + Cost + " GP)";
}
