using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class ArmorDialogViewModel : ViewModelBase
{
    private List<ArmorOptionViewModel> _lstAllOptions = new();

    public ObservableCollection<ArmorOptionViewModel> ArmorOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    private string? _strSelectedCategory;
    public string? SelectedCategory
    {
        get => _strSelectedCategory;
        set
        {
            if (!SetField(ref _strSelectedCategory, value))
                return;
            ApplyFilter();
        }
    }

    private string _strSearchText = string.Empty;
    public string SearchText
    {
        get => _strSearchText;
        set
        {
            if (!SetField(ref _strSearchText, value))
                return;
            ApplyFilter();
        }
    }

    private ArmorOptionViewModel? _selectedArmor;
    public ArmorOptionViewModel? SelectedArmor
    {
        get => _selectedArmor;
        set => SetField(ref _selectedArmor, value);
    }

    public void LoadOptions(CharacterDocument? character = null)
    {
        _lstAllOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("armor.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/armors/armor");
        if (nodes != null)
        {
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (character != null && !character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                    continue;

                _lstAllOptions.Add(new ArmorOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["b"]?.InnerText ?? "0", node["i"]?.InnerText ?? "0",
                    node["armorcapacity"]?.InnerText ?? string.Empty,
                    node["avail"]?.InnerText ?? string.Empty, node["cost"]?.InnerText ?? "0",
                    node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
            }
        }

        Categories.Clear();
        Categories.Add("Alle");
        foreach (string strCategory in _lstAllOptions.Select(o => o.Category).Where(c => !string.IsNullOrEmpty(c))
                     .Distinct().OrderBy(c => c))
            Categories.Add(strCategory);

        _strSelectedCategory = "Alle";
        OnPropertyChanged(nameof(SelectedCategory));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ArmorOptions.Clear();
        IEnumerable<ArmorOptionViewModel> query = _lstAllOptions;

        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "Alle")
            query = query.Where(o => o.Category == SelectedCategory);

        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (ArmorOptionViewModel option in query)
            ArmorOptions.Add(option);
    }
}

public sealed class ArmorOptionViewModel : ViewModelBase
{
    private static readonly Regex s_regVariableCost = new(@"^Variable\((\d+)-(\d+)\)$");

    public ArmorOptionViewModel(string name, string category, string ballistic, string impact, string capacity,
        string availability, string costExpression, string source, string page)
    {
        Name = name;
        Category = category;
        Ballistic = ballistic;
        Impact = impact;
        Capacity = capacity;
        Availability = availability;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;

        Match match = s_regVariableCost.Match(costExpression);
        if (match.Success)
        {
            IsCostVariable = true;
            MinCost = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            MaxCost = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            _intCostValue = MinCost;
        }
        else
        {
            IsCostVariable = false;
            MinCost = 0;
            MaxCost = 0;
            int.TryParse(costExpression, NumberStyles.Integer, CultureInfo.InvariantCulture, out _intCostValue);
        }
    }

    public string Name { get; }
    public string Category { get; }
    public string Ballistic { get; }
    public string Impact { get; }
    public string Capacity { get; }
    public string Availability { get; }
    public string SourcePage { get; }

    /// <summary>True when this item's cost is a player-chosen range (e.g. "Variable(20-100000)",
    /// used by generic Clothing) rather than a fixed price.</summary>
    public bool IsCostVariable { get; }

    public int MinCost { get; }
    public int MaxCost { get; }

    private int _intCostValue;
    public int CostValue
    {
        get => _intCostValue;
        set => SetField(ref _intCostValue, value);
    }

    /// <summary>Raw saved cost - the player-chosen value within range for Variable-cost items,
    /// or the fixed cost otherwise.</summary>
    public string Cost => CostValue.ToString(CultureInfo.InvariantCulture);
}
