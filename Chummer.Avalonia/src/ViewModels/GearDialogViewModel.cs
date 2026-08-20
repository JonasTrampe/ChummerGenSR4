using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GearDialogViewModel : ViewModelBase
{
    private List<GearOptionViewModel> _lstAllOptions = new();

    public ObservableCollection<GearOptionViewModel> GearOptions { get; } = new();
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

    private GearOptionViewModel? _selectedGear;
    public GearOptionViewModel? SelectedGear
    {
        get => _selectedGear;
        set => SetField(ref _selectedGear, value);
    }

    public void LoadOptions(CharacterDocument? character = null)
    {
        _lstAllOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("gear.xml");

        // Category labels get a "translate" attribute the same way item names get a <translate>
        // child element - ported from frmSelectGear.cs's cboCategory/lstGear population.
        var dicCategoryTranslations = new Dictionary<string, string>();
        XmlNodeList? categoryNodes = document.SelectNodes("/chummer/categories/category");
        if (categoryNodes != null)
        {
            foreach (XmlNode categoryNode in categoryNodes)
            {
                string strCategoryName = categoryNode.InnerText;
                string? strTranslate = categoryNode.Attributes?["translate"]?.Value;
                if (!string.IsNullOrEmpty(strCategoryName) && !string.IsNullOrEmpty(strTranslate))
                    dicCategoryTranslations[strCategoryName] = strTranslate!;
            }
        }

        XmlNodeList? nodes = document.SelectNodes("/chummer/gears/gear");
        if (nodes != null)
        {
            foreach (XmlNode node in nodes)
            {
                string strName = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(strName))
                    continue;
                if (character != null && !character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                    continue;

                string strCategory = node["category"]?.InnerText ?? string.Empty;
                string strTranslatedName = node["translate"]?.InnerText ?? strName;
                string strTranslatedCategory = dicCategoryTranslations.TryGetValue(strCategory, out var strCategoryTranslate)
                    ? strCategoryTranslate
                    : strCategory;

                _lstAllOptions.Add(new GearOptionViewModel(strName, strTranslatedName, strCategory, strTranslatedCategory,
                    node["rating"]?.InnerText ?? "0", node["avail"]?.InnerText ?? string.Empty,
                    node["cost"]?.InnerText ?? string.Empty, node["source"]?.InnerText ?? string.Empty,
                    node["page"]?.InnerText ?? string.Empty, node["capacity"]?.InnerText ?? string.Empty,
                    node["response"]?.InnerText ?? string.Empty, node["signal"]?.InnerText ?? string.Empty,
                    node["system"]?.InnerText ?? string.Empty, node["firewall"]?.InnerText ?? string.Empty));
            }
        }

        Categories.Clear();
        foreach (string strCategory in _lstAllOptions.Select(o => o.CategoryDisplay).Where(c => !string.IsNullOrEmpty(c))
                     .Distinct().OrderBy(c => c))
            Categories.Add(strCategory);

        _strSelectedCategory = Categories.Count > 0 ? Categories[0] : string.Empty;
        OnPropertyChanged(nameof(SelectedCategory));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        GearOptions.Clear();
        IEnumerable<GearOptionViewModel> query = _lstAllOptions;

        // Searching always looks across every category (matching frmSelectGear.cs's XPath search,
        // which ignores the category filter entirely) rather than being narrowed by it.
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || o.SourceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrEmpty(SelectedCategory))
        {
            query = query.Where(o => o.CategoryDisplay == SelectedCategory);
        }

        foreach (GearOptionViewModel option in query)
            GearOptions.Add(option);
    }
}

public sealed class GearOptionViewModel : ViewModelBase
{
    public GearOptionViewModel(string strSourceName, string strName, string strCategory, string strCategoryDisplay,
        string rating, string availability, string cost, string source, string page, string capacity = "",
        string response = "", string signal = "", string systemRating = "", string firewall = "")
    {
        SourceName = strSourceName;
        Name = strName;
        Category = strCategory;
        CategoryDisplay = strCategoryDisplay;
        Rating = rating;
        Availability = availability;
        Cost = cost;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
        Capacity = capacity;
        Response = response;
        Signal = signal;
        SystemRating = systemRating;
        Firewall = firewall;
        Source = source;
        Page = page;
    }

    public string Capacity { get; }
    public string Response { get; }
    public string Signal { get; }
    public string SystemRating { get; }
    public string Firewall { get; }
    public string Source { get; }
    public string Page { get; }
    public bool IsCommlink => !string.IsNullOrEmpty(Response);

    /// <summary>Canonical (untranslated) name as saved in the character file - matches the rules
    /// data and stays stable across language settings.</summary>
    public string SourceName { get; }

    /// <summary>Display name - the &lt;translate&gt; value if the current language pack provides
    /// one for this item, otherwise the same as SourceName.</summary>
    public string Name { get; }

    /// <summary>Canonical (untranslated) category, as saved in the character file.</summary>
    public string Category { get; }

    /// <summary>Display category - translated if the language pack provides a category label.</summary>
    public string CategoryDisplay { get; }

    public string Rating { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string SourcePage { get; }

    private int _intQuantity = 1;
    public int Quantity
    {
        get => _intQuantity;
        set => SetField(ref _intQuantity, value);
    }
}
