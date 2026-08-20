using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Rules-data backed armor-mod picker (frmSelectArmorMod.cs's equivalent).</summary>
public sealed class ArmorModDialogViewModel : ViewModelBase
{
    private readonly List<ArmorModOptionViewModel> _allOptions = new();
    private string? _selectedCategory;
    private string _searchText = string.Empty;
    private ArmorModOptionViewModel? _selectedMod;
    private decimal _rating = 1;

    public ObservableCollection<ArmorModOptionViewModel> ModOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public string? SelectedCategory { get => _selectedCategory; set { if (SetField(ref _selectedCategory, value)) ApplyFilter(); } }
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value)) ApplyFilter(); } }

    public ArmorModOptionViewModel? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (!SetField(ref _selectedMod, value)) return;
            Rating = 1;
            OnPropertyChanged(nameof(HasRating));
        }
    }

    public decimal Rating
    {
        get => _rating;
        set => SetField(ref _rating, Math.Clamp(value, 1, Math.Max(1, SelectedMod?.MaxRating ?? 1)));
    }

    public bool HasRating => SelectedMod?.MaxRating > 1;

    public void LoadOptions(CharacterDocument? character = null)
    {
        _allOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("armor.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/mods/mod");
        if (nodes != null)
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (character != null && !character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                    continue;

                _allOptions.Add(new ArmorModOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["maxrating"]?.InnerText ?? "0", node["b"]?.InnerText ?? "0", node["i"]?.InnerText ?? "0",
                    node["avail"]?.InnerText ?? string.Empty, node["cost"]?.InnerText ?? "0",
                    node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
            }

        Categories.Clear();
        foreach (string category in _allOptions.Select(o => o.Category).Where(c => c.Length > 0).Distinct().OrderBy(c => c))
            Categories.Add(category);
        _selectedCategory = Categories.Count > 0 ? Categories[0] : string.Empty;
        OnPropertyChanged(nameof(SelectedCategory));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ModOptions.Clear();
        IEnumerable<ArmorModOptionViewModel> query = _allOptions;
        if (!string.IsNullOrEmpty(SelectedCategory))
            query = query.Where(o => o.Category == SelectedCategory);
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (ArmorModOptionViewModel option in query.OrderBy(o => o.Name, StringComparer.Ordinal))
            ModOptions.Add(option);
    }
}

public sealed class ArmorModOptionViewModel
{
    public ArmorModOptionViewModel(string name, string category, string maxRating, string b, string i,
        string availability, string cost, string source, string page)
    {
        Name = name;
        Category = category;
        MaxRating = int.TryParse(maxRating, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating) ? rating : 0;
        Ballistic = b;
        Impact = i;
        Availability = availability;
        Cost = cost;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public int MaxRating { get; }
    public string Ballistic { get; }
    public string Impact { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
