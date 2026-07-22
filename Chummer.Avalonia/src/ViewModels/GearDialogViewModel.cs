using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GearDialogViewModel : ViewModelBase
{
    public ObservableCollection<GearOptionViewModel> GearOptions { get; } = new();

    private GearOptionViewModel? _selectedGear;
    public GearOptionViewModel? SelectedGear
    {
        get => _selectedGear;
        set => SetField(ref _selectedGear, value);
    }

    public void LoadOptions()
    {
        GearOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load("gear.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/gears/gear");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            GearOptions.Add(new GearOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                node["rating"]?.InnerText ?? "0", node["avail"]?.InnerText ?? string.Empty,
                node["cost"]?.InnerText ?? string.Empty, node["source"]?.InnerText ?? string.Empty,
                node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class GearOptionViewModel
{
    public GearOptionViewModel(string name, string category, string rating, string availability, string cost,
        string source, string page)
    {
        Name = name;
        Category = category;
        Rating = rating;
        Availability = availability;
        Cost = cost;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Category { get; }
    public string Rating { get; }
    public string Availability { get; }
    public string Cost { get; }
    public string SourcePage { get; }
}
