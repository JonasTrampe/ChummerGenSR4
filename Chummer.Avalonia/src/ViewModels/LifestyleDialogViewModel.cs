using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class LifestyleDialogViewModel : ViewModelBase
{
    private readonly List<LifestyleOptionViewModel> _all = new();
    public ObservableCollection<LifestyleOptionViewModel> Options { get; } = new();
    private LifestyleOptionViewModel? _selected;
    public LifestyleOptionViewModel? Selected { get => _selected; set => SetField(ref _selected, value); }
    private string _search = string.Empty;
    public string Search { get => _search; set { if (SetField(ref _search, value)) ApplyFilter(); } }
    public void LoadOptions()
    {
        _all.Clear();
        XmlDocument document = XmlManager.Instance.Load("lifestyles.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/lifestyles/lifestyle");
        if (nodes != null) foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name)) _all.Add(new LifestyleOptionViewModel(name, node["translate"]?.InnerText ?? name, node["cost"]?.InnerText ?? "0", node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
        ApplyFilter();
    }
    private void ApplyFilter()
    {
        Options.Clear();
        foreach (var option in _all.Where(o => string.IsNullOrWhiteSpace(Search) || o.Name.Contains(Search, System.StringComparison.OrdinalIgnoreCase) || o.DisplayName.Contains(Search, System.StringComparison.OrdinalIgnoreCase))) Options.Add(option);
        Selected = Options.FirstOrDefault();
    }
}
public sealed class LifestyleOptionViewModel
{
    public LifestyleOptionViewModel(string name, string displayName, string cost, string source, string page) { Name=name; DisplayName=displayName; Cost=cost; Source=string.IsNullOrEmpty(page)?source:source+" "+page; }
    public string Name { get; } public string DisplayName { get; } public string Cost { get; } public string Source { get; }
    public string Summary => DisplayName + " — " + Cost + "¥/Monat (" + Source + ")";
}
