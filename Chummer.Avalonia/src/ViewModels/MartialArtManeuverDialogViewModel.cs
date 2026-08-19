using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class MartialArtManeuverDialogViewModel : ViewModelBase
{
    public ObservableCollection<MartialArtManeuverOptionViewModel> Options { get; } = new();

    private MartialArtManeuverOptionViewModel? _selected;
    public MartialArtManeuverOptionViewModel? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        Options.Clear();
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterMartialArtManeuverData maneuver in character.MartialArtManeuvers)
            existingNames.Add(maneuver.Name);

        XmlDocument document = XmlManager.Instance.Load("martialarts.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/maneuvers/maneuver");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;
            if (!character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                continue;

            Options.Add(new MartialArtManeuverOptionViewModel(name,
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class MartialArtManeuverOptionViewModel
{
    public MartialArtManeuverOptionViewModel(string name, string source, string page)
    {
        Name = name;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
