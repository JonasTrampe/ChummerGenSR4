using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Rules-data backed weapon-mod picker (frmSelectWeaponMod.cs's equivalent). Cost
/// formulas may reference "Weapon Cost"/"Rating" - Core resolves both when applying the cost.</summary>
public sealed class WeaponModDialogViewModel : ViewModelBase
{
    private readonly List<WeaponModOptionViewModel> _allOptions = new();
    private string? _selectedCategory;
    private string _searchText = string.Empty;
    private WeaponModOptionViewModel? _selectedMod;
    private decimal _rating;

    public ObservableCollection<WeaponModOptionViewModel> ModOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public string? SelectedCategory { get => _selectedCategory; set { if (SetField(ref _selectedCategory, value)) ApplyFilter(); } }
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value)) ApplyFilter(); } }

    public WeaponModOptionViewModel? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (!SetField(ref _selectedMod, value)) return;
            Rating = value?.DefaultRating ?? 0;
            OnPropertyChanged(nameof(HasRating));
        }
    }

    public decimal Rating { get => _rating; set => SetField(ref _rating, Math.Max(0, value)); }
    public bool HasRating => SelectedMod?.DefaultRating > 0;

    public void LoadOptions(CharacterDocument? character = null)
    {
        _allOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("weapons.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/mods/mod");
        if (nodes != null)
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (character != null && !character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                    continue;

                _allOptions.Add(new WeaponModOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["rating"]?.InnerText ?? "0", node["slots"]?.InnerText ?? "0",
                    node["avail"]?.InnerText ?? string.Empty,
                    node["cost"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty,
                    node["page"]?.InnerText ?? string.Empty, node["rc"]?.InnerText ?? string.Empty,
                    node["rcgroup"]?.InnerText ?? "0"));
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
        IEnumerable<WeaponModOptionViewModel> query = _allOptions;
        if (!string.IsNullOrEmpty(SelectedCategory))
            query = query.Where(o => o.Category == SelectedCategory);
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (WeaponModOptionViewModel option in query.OrderBy(o => o.Name, StringComparer.Ordinal))
            ModOptions.Add(option);
    }
}

public sealed class WeaponModOptionViewModel
{
    public WeaponModOptionViewModel(string name, string category, string defaultRating, string slots,
        string availability, string cost, string source, string page, string rc = "", string rcGroup = "0")
    {
        Name = name;
        Category = category;
        DefaultRating = decimal.TryParse(defaultRating, NumberStyles.Number, CultureInfo.InvariantCulture, out var rating) ? rating : 0;
        Slots = slots;
        Availability = availability;
        Cost = cost;
        Source = source;
        Page = page;
        Rc = rc;
        RcGroup = rcGroup;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public decimal DefaultRating { get; }
    public string Slots { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string Source { get; }
    public string Page { get; }
    public string Rc { get; }
    public string RcGroup { get; }
    public string SourcePage { get; }
}
