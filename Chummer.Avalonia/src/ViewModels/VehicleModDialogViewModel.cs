using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Rules-data backed vehicle-mod picker. It exposes the persisted rule fields; Core
/// resolves the selected rating and owning vehicle Body when applying the cost.</summary>
public sealed class VehicleModDialogViewModel : ViewModelBase
{
    private readonly List<VehicleModOptionViewModel> _allOptions = new();
    private string? _selectedCategory;
    private string _searchText = string.Empty;
    private VehicleModOptionViewModel? _selectedMod;
    private decimal _rating;

    public ObservableCollection<VehicleModOptionViewModel> ModOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public string? SelectedCategory { get => _selectedCategory; set { if (SetField(ref _selectedCategory, value)) ApplyFilter(); } }
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value)) ApplyFilter(); } }
    public VehicleModOptionViewModel? SelectedMod
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
        XmlDocument document = XmlManager.Instance.Load("vehicles.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/mods/mod");
        if (nodes != null)
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (character != null && !character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty)) continue;
                _allOptions.Add(new VehicleModOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["rating"]?.InnerText ?? "0", node["slots"]?.InnerText ?? "0", node["avail"]?.InnerText ?? string.Empty,
                    node["cost"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty,
                    node["page"]?.InnerText ?? string.Empty, node["limit"]?.InnerText ?? string.Empty));
            }
        Categories.Clear();
        Categories.Add(App.LanguageCatalog.GetString("UI_All"));
        foreach (string category in _allOptions.Select(option => option.Category).Where(category => category.Length > 0).Distinct().OrderBy(category => category))
            Categories.Add(category);
        _selectedCategory = App.LanguageCatalog.GetString("UI_All");
        OnPropertyChanged(nameof(SelectedCategory));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ModOptions.Clear();
        IEnumerable<VehicleModOptionViewModel> query = _allOptions;
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != App.LanguageCatalog.GetString("UI_All")) query = query.Where(option => option.Category == SelectedCategory);
        if (!string.IsNullOrWhiteSpace(SearchText)) query = query.Where(option => option.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (VehicleModOptionViewModel option in query) ModOptions.Add(option);
    }
}

public sealed class VehicleModOptionViewModel
{
    public VehicleModOptionViewModel(string name, string category, string defaultRating, string slots, string availability,
        string cost, string source, string page, string limit)
    {
        Name = name; Category = category;
        DefaultRating = decimal.TryParse(defaultRating, NumberStyles.Number, CultureInfo.InvariantCulture, out var rating) ? rating : 0;
        Slots = slots; Availability = availability; Cost = cost; Limit = limit; Source = source; Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }
    public string Name { get; }
    public string Category { get; }
    public decimal DefaultRating { get; }
    public string Slots { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string Limit { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
