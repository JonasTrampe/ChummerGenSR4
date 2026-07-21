using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class QualityDialogViewModel : ViewModelBase
{
    public ObservableCollection<QualityOptionViewModel> QualityOptions { get; } = new();

    private QualityOptionViewModel? _selectedQuality;
    public QualityOptionViewModel? SelectedQuality
    {
        get => _selectedQuality;
        set => SetField(ref _selectedQuality, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        QualityOptions.Clear();
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
            QualityOptions.Add(new QualityOptionViewModel(name, category, node["bp"]?.InnerText ?? "0",
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class QualityOptionViewModel
{
    public QualityOptionViewModel(string name, string category, string cost, string source, string page)
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
