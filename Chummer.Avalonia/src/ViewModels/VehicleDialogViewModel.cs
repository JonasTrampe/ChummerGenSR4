using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class VehicleDialogViewModel : ViewModelBase
{
    private readonly List<VehicleOptionViewModel> _allOptions = new();
    public ObservableCollection<VehicleOptionViewModel> VehicleOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    private string? _selectedCategory;
    public string? SelectedCategory { get => _selectedCategory; set { if (SetField(ref _selectedCategory, value)) ApplyFilter(); } }
    private string _searchText = string.Empty;
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value)) ApplyFilter(); } }
    private VehicleOptionViewModel? _selectedVehicle;
    public VehicleOptionViewModel? SelectedVehicle { get => _selectedVehicle; set => SetField(ref _selectedVehicle, value); }

    public void LoadOptions()
    {
        _allOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("vehicles.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/vehicles/vehicle");
        if (nodes != null)
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                _allOptions.Add(new VehicleOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["handling"]?.InnerText ?? "0", node["accel"]?.InnerText ?? "0", node["speed"]?.InnerText ?? "0",
                    node["pilot"]?.InnerText ?? "0", node["body"]?.InnerText ?? "0", node["armor"]?.InnerText ?? "0",
                    node["sensor"]?.InnerText ?? "0", node["devicerating"]?.InnerText ?? "0", node["avail"]?.InnerText ?? string.Empty,
                    node["cost"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
            }
        Categories.Clear(); Categories.Add("Alle");
        foreach (string category in _allOptions.Select(o => o.Category).Where(c => c.Length > 0).Distinct().OrderBy(c => c)) Categories.Add(category);
        _selectedCategory = "Alle"; OnPropertyChanged(nameof(SelectedCategory)); ApplyFilter();
    }

    private void ApplyFilter()
    {
        VehicleOptions.Clear(); IEnumerable<VehicleOptionViewModel> query = _allOptions;
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "Alle") query = query.Where(o => o.Category == SelectedCategory);
        if (!string.IsNullOrWhiteSpace(SearchText)) query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (VehicleOptionViewModel item in query) VehicleOptions.Add(item);
    }
}

public sealed class VehicleOptionViewModel
{
    public VehicleOptionViewModel(string name, string category, string handling, string acceleration, string speed, string pilot,
        string body, string armor, string sensor, string deviceRating, string availability, string cost, string source, string page)
    { Name=name; Category=category; Handling=handling; Acceleration=acceleration; Speed=speed; Pilot=pilot; Body=body; Armor=armor; Sensor=sensor; DeviceRating=deviceRating; Availability=availability; Cost=cost; Source=source; Page=page; SourcePage=string.IsNullOrWhiteSpace(page)?source:source+" "+page; }
    public string Name { get; } public string Category { get; } public string Handling { get; } public string Acceleration { get; }
    public string Speed { get; } public string Pilot { get; } public string Body { get; } public string Armor { get; }
    public string Sensor { get; } public string DeviceRating { get; } public string Availability { get; } public string Cost { get; } public string Source { get; } public string Page { get; } public string SourcePage { get; }
}
