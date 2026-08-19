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
        set
        {
            if (SetField(ref _selected, value))
                SelectedRating = 1;
        }
    }

    private decimal _selectedRating = 1;
    public decimal SelectedRating
    {
        get => _selectedRating;
        set => SetField(ref _selectedRating, Math.Max(1, value));
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
            if (!character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                continue;

            Options.Add(new CritterPowerOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                node["points"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty,
                node["page"]?.InnerText ?? string.Empty, node["rating"]?.InnerText == "yes"));
        }
    }
}

public sealed class CritterPowerOptionViewModel
{
    public CritterPowerOptionViewModel(string name, string category, string points, string source, string page,
        bool hasRating = false)
    {
        Name = name;
        Category = category;
        Points = points;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
        HasRating = hasRating;
    }

    public string Name { get; }
    public string Category { get; }
    public string Points { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }

    /// <summary>Ported from frmSelectCritterPower.cs's &lt;rating&gt;yes&lt;/rating&gt; check
    /// (nudCritterPowerRating.Enabled) - whether this power's bonus scales with a player-chosen
    /// Rating (e.g. Armor (Ballistic)'s "Rating" points of Ballistic Armor).</summary>
    public bool HasRating { get; }
}
