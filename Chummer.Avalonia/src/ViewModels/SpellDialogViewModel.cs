using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SpellDialogViewModel : ViewModelBase
{
    public ObservableCollection<SpellOptionViewModel> SpellOptions { get; } = new();

    private SpellOptionViewModel? _selectedSpell;
    public SpellOptionViewModel? SelectedSpell
    {
        get => _selectedSpell;
        set
        {
            if (!SetField(ref _selectedSpell, value))
                return;
            if (!CanSelectExtendedSpell)
                IsExtendedSpell = false;
            OnPropertyChanged(nameof(CanSelectExtendedSpell));
            OnPropertyChanged(nameof(SelectedSpellDescriptor));
            OnPropertyChanged(nameof(SelectedSpellDrainValue));
        }
    }

    private bool _blnAllowExtendedDetectionSpell;
    public bool AllowExtendedDetectionSpell
    {
        get => _blnAllowExtendedDetectionSpell;
        private set => SetField(ref _blnAllowExtendedDetectionSpell, value);
    }

    public bool CanSelectExtendedSpell => AllowExtendedDetectionSpell
        && string.Equals(SelectedSpell?.Category, "Detection", StringComparison.Ordinal);

    private bool _blnIsExtendedSpell;
    public bool IsExtendedSpell
    {
        get => _blnIsExtendedSpell;
        set
        {
            if (SetField(ref _blnIsExtendedSpell, value))
            {
                OnPropertyChanged(nameof(SelectedSpellDescriptor));
                OnPropertyChanged(nameof(SelectedSpellDrainValue));
            }
        }
    }

    public string SelectedSpellDescriptor => SelectedSpell == null ? string.Empty
        : GetDisplayDescriptor(SelectedSpell.Descriptor, IsExtendedSpell && CanSelectExtendedSpell);
    public string SelectedSpellDrainValue => SelectedSpell == null ? string.Empty
        : CharacterDocument.GetSpellDrainValue(SelectedSpell.DrainValue, IsExtendedSpell && CanSelectExtendedSpell);

    public void LoadOptions(CharacterDocument character)
    {
        SpellOptions.Clear();
        AllowExtendedDetectionSpell = character.ExtendAnyDetectionSpellEnabled;
        IsExtendedSpell = false;
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterSpellData spell in character.Spells)
        {
            existingNames.Add(spell.Name);
            if (AllowExtendedDetectionSpell && spell.Name.EndsWith(", Extended", StringComparison.OrdinalIgnoreCase))
                existingNames.Add(spell.Name.Substring(0, spell.Name.Length - ", Extended".Length));
        }

        XmlDocument document = XmlManager.Instance.Load("spells.xml");
        XmlNodeList? nodes = document.SelectNodes("/chummer/spells/spell");
        if (nodes == null)
            return;

        foreach (XmlNode node in nodes)
        {
            string name = node["name"]?.InnerText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || existingNames.Contains(name))
                continue;
            if (AllowExtendedDetectionSpell && name.EndsWith(", Extended", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                continue;

            SpellOptions.Add(new SpellOptionViewModel(
                name, node["category"]?.InnerText ?? string.Empty, node["descriptor"]?.InnerText ?? string.Empty,
                node["type"]?.InnerText ?? string.Empty, node["range"]?.InnerText ?? string.Empty,
                node["damage"]?.InnerText ?? string.Empty, node["duration"]?.InnerText ?? string.Empty,
                node["dv"]?.InnerText ?? string.Empty, node["source"]?.InnerText ?? string.Empty,
                node["page"]?.InnerText ?? string.Empty));
        }
    }

    private static string GetDisplayDescriptor(string strDescriptor, bool blnExtended)
    {
        if (!blnExtended)
            return strDescriptor;

        string[] astrDescriptors = strDescriptor.Split(',', StringSplitOptions.RemoveEmptyEntries);
        bool blnHasExtendedArea = false;
        for (int i = 0; i < astrDescriptors.Length; i++)
        {
            string strDescriptorPart = astrDescriptors[i].Trim();
            if (string.Equals(strDescriptorPart, "Area", StringComparison.OrdinalIgnoreCase))
            {
                astrDescriptors[i] = "Extended Area";
                blnHasExtendedArea = true;
            }
            else if (string.Equals(strDescriptorPart, "Extended Area", StringComparison.OrdinalIgnoreCase))
                blnHasExtendedArea = true;
        }
        return string.Join(", ", astrDescriptors) + (blnHasExtendedArea ? string.Empty : ", Extended Area");
    }
}

public sealed class SpellOptionViewModel
{
    public SpellOptionViewModel(string name, string category, string descriptor, string type, string range,
        string damage, string duration, string drainValue, string source, string page)
    {
        Name = name;
        Category = category;
        Descriptor = descriptor;
        Type = type;
        Range = range;
        Damage = damage;
        Duration = duration;
        DrainValue = drainValue;
        Source = source;
        Page = page;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public string Source { get; }
    public string Page { get; }
    public string Category { get; }
    public string Descriptor { get; }
    public string Type { get; }
    public string Range { get; }
    public string Damage { get; }
    public string Duration { get; }
    public string DrainValue { get; }
    public string SourcePage { get; }
}
