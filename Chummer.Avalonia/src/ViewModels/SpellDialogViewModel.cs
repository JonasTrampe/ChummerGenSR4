using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SpellDialogViewModel : ViewModelBase
{
    public ObservableCollection<SpellOptionViewModel> SpellOptions { get; } = new();

    private SpellOptionViewModel? _selectedSpell;
    public SpellOptionViewModel? SelectedSpell
    {
        get => _selectedSpell;
        set => SetField(ref _selectedSpell, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        SpellOptions.Clear();
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterSpellData spell in character.Spells)
            existingNames.Add(spell.Name);

        XmlDocument document = XmlManager.Instance.Load("spells.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/spells/spell");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;

            SpellOptions.Add(new SpellOptionViewModel(
                name, node["category"]?.InnerText ?? string.Empty, node["descriptor"]?.InnerText ?? string.Empty,
                node["type"]?.InnerText ?? string.Empty, node["range"]?.InnerText ?? string.Empty,
                node["damage"]?.InnerText ?? string.Empty, node["duration"]?.InnerText ?? string.Empty,
                node["dv"]?.InnerText ?? string.Empty, node["source"]?.InnerText ?? string.Empty,
                node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class SpellOptionViewModel
{
    public SpellOptionViewModel(string name, string category, string descriptor, string type, string range,
        string damage, string duration, string drainValue, string source, string page)
    {
        Name = name;
        Category = category;
        Descriptor = descriptor;
        Type = type;
        Range = range;
        Damage = damage;
        Duration = duration;
        DrainValue = drainValue;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Source { get; }
    public string Page { get; }
    public string Category { get; }
    public string Descriptor { get; }
    public string Type { get; }
    public string Range { get; }
    public string Damage { get; }
    public string Duration { get; }
    public string DrainValue { get; }
    public string SourcePage { get; }
}
