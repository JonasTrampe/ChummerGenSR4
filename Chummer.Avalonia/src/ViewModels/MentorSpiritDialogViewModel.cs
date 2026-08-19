using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Rules-data backed Mentor Spirit/Paragon picker (frmSelectMentorSpirit.cs's
/// equivalent). <paramref name="strDataFile"/> is "mentors.xml" for the Mentor Spirit Quality
/// or "paragons.xml" for the Technomancer "The Beast's Way" Quality. Legacy splits a mentor's
/// &lt;choices&gt; into two independent picks via a choice's set="2" attribute (used by only 2
/// entries in mentors.xml); this port offers a single choose-one dropdown over every &lt;choice&gt;
/// instead, so those 2 mentors only get their first pick applied.</summary>
public sealed class MentorSpiritDialogViewModel : ViewModelBase
{
    private readonly List<MentorSpiritOptionViewModel> _allOptions = new();
    private string? _selectedCategory;
    private MentorSpiritOptionViewModel? _selectedMentor;
    private string? _choice;

    public ObservableCollection<MentorSpiritOptionViewModel> MentorOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<string> ChoiceOptions { get; } = new();

    public string? SelectedCategory { get => _selectedCategory; set { if (SetField(ref _selectedCategory, value)) ApplyFilter(); } }

    public MentorSpiritOptionViewModel? SelectedMentor
    {
        get => _selectedMentor;
        set
        {
            if (!SetField(ref _selectedMentor, value)) return;
            ChoiceOptions.Clear();
            if (value != null)
                foreach (string choice in value.ChoiceOptions) ChoiceOptions.Add(choice);
            Choice = ChoiceOptions.FirstOrDefault();
            OnPropertyChanged(nameof(HasChoice));
        }
    }

    public string? Choice { get => _choice; set => SetField(ref _choice, value); }
    public bool HasChoice => ChoiceOptions.Count > 0;

    public void LoadOptions(string strDataFile)
    {
        _allOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load(strDataFile);
        XmlNodeList? nodes = document.SelectNodes("/chummer/mentors/mentor");
        if (nodes != null)
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var lstChoices = node.SelectNodes("choices/choice")?.Cast<XmlNode>()
                    .Select(c => c["name"]?.InnerText ?? string.Empty).Where(n => n.Length > 0).ToList()
                    ?? new List<string>();
                _allOptions.Add(new MentorSpiritOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["advantage"]?.InnerText ?? string.Empty, node["disadvantage"]?.InnerText ?? string.Empty,
                    node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty, lstChoices));
            }

        Categories.Clear();
        Categories.Add(App.LanguageCatalog.GetString("UI_All"));
        foreach (string category in _allOptions.Select(o => o.Category).Where(c => c.Length > 0).Distinct().OrderBy(c => c))
            Categories.Add(category);
        _selectedCategory = App.LanguageCatalog.GetString("UI_All");
        OnPropertyChanged(nameof(SelectedCategory));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        MentorOptions.Clear();
        IEnumerable<MentorSpiritOptionViewModel> query = _allOptions;
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != App.LanguageCatalog.GetString("UI_All"))
            query = query.Where(o => o.Category == SelectedCategory);
        foreach (MentorSpiritOptionViewModel option in query.OrderBy(o => o.Name, StringComparer.Ordinal))
            MentorOptions.Add(option);
    }
}

public sealed class MentorSpiritOptionViewModel
{
    public MentorSpiritOptionViewModel(string name, string category, string advantage, string disadvantage,
        string source, string page, IReadOnlyList<string> choiceOptions)
    {
        Name = name;
        Category = category;
        Advantage = advantage;
        Disadvantage = disadvantage;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
        ChoiceOptions = choiceOptions;
    }

    public string Name { get; }
    public string Category { get; }
    public string Advantage { get; }
    public string Disadvantage { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
    public IReadOnlyList<string> ChoiceOptions { get; }
}
