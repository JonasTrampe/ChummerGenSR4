using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class PowerDialogViewModel : ViewModelBase
{
    public ObservableCollection<PowerOptionViewModel> Options { get; } = new();

    private PowerOptionViewModel? _selected;
    public PowerOptionViewModel? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value))
                return;
            Rating = 1;
            OnPropertyChanged(nameof(CanChooseRating));
        }
    }

    private int _rating = 1;
    public int Rating
    {
        get => _rating;
        set => SetField(ref _rating, value < 1 ? 1 : value);
    }

    public bool CanChooseRating => Selected?.HasLevels == true;

    public void LoadOptions(CharacterDocument character)
    {
        Options.Clear();
        var existingNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterPowerData power in character.AdeptPowers)
            existingNames.Add(power.Name);

        XmlDocument document = XmlManager.Instance.Load("powers.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/powers/power");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;

            string strPoints = node["points"]?.InnerText ?? "0";
            bool blnLevels = node["levels"]?.InnerText == "yes";
            Options.Add(new PowerOptionViewModel(name, strPoints, blnLevels,
                node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
        }
    }
}

public sealed class PowerOptionViewModel
{
    public PowerOptionViewModel(string name, string pointsPerLevel, bool hasLevels, string source, string page)
    {
        Name = name;
        PointsPerLevel = pointsPerLevel;
        HasLevels = hasLevels;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string PointsPerLevel { get; }
    public bool HasLevels { get; }
    public string Source { get; }
    public string Page { get; }
    public string SourcePage { get; }
}
