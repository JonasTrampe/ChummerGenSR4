using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

// The Maneuver-picker counterpart (MartialArtManeuverDialogViewModel/
// MartialArtManeuverOptionViewModel) lives in its own file.
public sealed class MartialArtDialogViewModel : ViewModelBase
{
    public ObservableCollection<MartialArtOptionViewModel> Options { get; } = new();

    private MartialArtOptionViewModel? _selected;
    public MartialArtOptionViewModel? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        Options.Clear();
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterMartialArtData martialArt in character.MartialArts)
            existingNames.Add(martialArt.Name);

        XmlDocument document = XmlManager.Instance.Load("martialarts.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/martialarts/martialart");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;
            if (!character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                continue;

            var lstAdvantages = new List<string>();
            XmlNodeList? advantageNodes = node.SelectNodes("advantages/advantage");
            if (advantageNodes != null)
                foreach (XmlNode advantageNode in advantageNodes)
                    lstAdvantages.Add(advantageNode["name"]?.InnerText ?? string.Empty);

            Options.Add(new MartialArtOptionViewModel(name, lstAdvantages,
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class MartialArtOptionViewModel
{
    public MartialArtOptionViewModel(string name, IReadOnlyList<string> advantages, string source, string page)
    {
        Name = name;
        Advantages = advantages;
        AdvantagesDisplay = string.Join("\n", advantages);
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public IReadOnlyList<string> Advantages { get; }
    public string AdvantagesDisplay { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
