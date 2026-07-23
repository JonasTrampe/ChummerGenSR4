using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class WeaponDialogViewModel : ViewModelBase
{
    private List<WeaponOptionViewModel> _lstAllOptions = new();

    public ObservableCollection<WeaponOptionViewModel> WeaponOptions { get; } = new();
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

    private WeaponOptionViewModel? _selectedWeapon;
    public WeaponOptionViewModel? SelectedWeapon
    {
        get => _selectedWeapon;
        set => SetField(ref _selectedWeapon, value);
    }

    public void LoadOptions()
    {
        _lstAllOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("weapons.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/weapons/weapon");
        if (nodes != null)
        {
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _lstAllOptions.Add(new WeaponOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["damage"]?.InnerText ?? string.Empty, node["ap"]?.InnerText ?? string.Empty,
                    node["mode"]?.InnerText ?? string.Empty, node["rc"]?.InnerText ?? string.Empty,
                    node["ammo"]?.InnerText ?? string.Empty, node["avail"]?.InnerText ?? string.Empty,
                    node["cost"]?.InnerText ?? string.Empty, node["source"]?.InnerText ?? string.Empty,
                    node["page"]?.InnerText ?? string.Empty));
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
        WeaponOptions.Clear();
        IEnumerable<WeaponOptionViewModel> query = _lstAllOptions;

        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "Alle")
            query = query.Where(o => o.Category == SelectedCategory);

        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (WeaponOptionViewModel option in query)
            WeaponOptions.Add(option);
    }
}

public sealed class WeaponOptionViewModel
{
    public WeaponOptionViewModel(string name, string category, string damage, string ap, string mode, string rc,
        string ammo, string availability, string cost, string source, string page)
    {
        Name = name;
        Category = category;
        Damage = damage;
        Ap = ap;
        Mode = mode;
        Rc = rc;
        Ammo = ammo;
        Availability = availability;
        Cost = cost;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public string Damage { get; }
    public string Ap { get; }
    public string Mode { get; }
    public string Rc { get; }
    public string Ammo { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string SourcePage { get; }
}
