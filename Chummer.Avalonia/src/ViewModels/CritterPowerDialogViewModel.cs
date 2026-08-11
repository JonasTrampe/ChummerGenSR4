using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CritterPowerDialogViewModel : ViewModelBase
{
    public ObservableCollection<CritterPowerOptionViewModel> Options { get; } = new();

    private CritterPowerOptionViewModel? _selected;
    public CritterPowerOptionViewModel? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        Options.Clear();
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterCritterPowerData power in character.CritterPowers)
            existingNames.Add(power.Name);

        XmlDocument document = XmlManager.Instance.Load("critterpowers.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/powers/power");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;

            Options.Add(new CritterPowerOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                node["points"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty,
                node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class CritterPowerOptionViewModel
{
    public CritterPowerOptionViewModel(string name, string category, string points, string source, string page)
    {
        Name = name;
        Category = category;
        Points = points;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public string Points { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
