using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class MetamagicDialogViewModel : ViewModelBase
{
    public ObservableCollection<MetamagicOptionViewModel> Options { get; } = new();

    private MetamagicOptionViewModel? _selected;
    public MetamagicOptionViewModel? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        Options.Clear();
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterMetamagicData metamagic in character.Metamagics)
            existingNames.Add(metamagic.Name);

        XmlDocument document = XmlManager.Instance.Load("metamagic.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/metamagics/metamagic");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;

            Options.Add(new MetamagicOptionViewModel(name,
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class MetamagicOptionViewModel
{
    public MetamagicOptionViewModel(string name, string source, string page)
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
