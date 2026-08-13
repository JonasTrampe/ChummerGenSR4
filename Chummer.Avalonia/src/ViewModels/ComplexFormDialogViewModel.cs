using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class ComplexFormDialogViewModel : ViewModelBase
{
    public ObservableCollection<ComplexFormOptionViewModel> Options { get; } = new();

    private ComplexFormOptionViewModel? _selected;
    public ComplexFormOptionViewModel? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public void LoadOptions(CharacterDocument character)
    {
        Options.Clear();
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterComplexFormData form in character.ComplexForms)
            existingNames.Add(form.Name);

        XmlDocument document = XmlManager.Instance.Load("programs.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/programs/program");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;
            if (!character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                continue;

            Options.Add(new ComplexFormOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class ComplexFormOptionViewModel
{
    public ComplexFormOptionViewModel(string name, string category, string source, string page)
    {
        Name = name;
        Category = category;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
