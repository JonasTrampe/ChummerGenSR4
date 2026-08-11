using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Rules-data backed weapon-accessory picker (frmSelectWeaponAccessory.cs's equivalent).
/// Mount-slot eligibility isn't validated - same scoped-down treatment as the vehicle-mod picker.</summary>
public sealed class WeaponAccessoryDialogViewModel : ViewModelBase
{
    private readonly List<WeaponAccessoryOptionViewModel> _allOptions = new();
    private string _searchText = string.Empty;

    public ObservableCollection<WeaponAccessoryOptionViewModel> Options { get; } = new();
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value)) ApplyFilter(); } }

    private WeaponAccessoryOptionViewModel? _selected;
    public WeaponAccessoryOptionViewModel? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public void LoadOptions()
    {
        _allOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("weapons.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/accessories/accessory");
        if (nodes != null)
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _allOptions.Add(new WeaponAccessoryOptionViewModel(name, node["mount"]?.InnerText ?? string.Empty,
                    node["rc"]?.InnerText ?? string.Empty, node["avail"]?.InnerText ?? string.Empty,
                    node["cost"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty,
                    node["page"]?.InnerText ?? string.Empty));
            }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Options.Clear();
        IEnumerable<WeaponAccessoryOptionViewModel> query = _allOptions;
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (WeaponAccessoryOptionViewModel option in query.OrderBy(o => o.Name, StringComparer.Ordinal))
            Options.Add(option);
    }
}

public sealed class WeaponAccessoryOptionViewModel
{
    public WeaponAccessoryOptionViewModel(string name, string mount, string rc, string availability, string cost,
        string source, string page)
    {
        Name = name;
        Mount = mount;
        Rc = rc;
        Availability = availability;
        Cost = cost;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Mount { get; }
    public string Rc { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
