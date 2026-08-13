using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SpiritRowViewModel
{
    public string Label { get; }
    public string Value { get; }

    /// <summary>Fields needed to identify this row for RemoveSpirit - see
    /// CharacterDocument.RemoveSpirit's first-occurrence-by-fields matching.</summary>
    public string Name { get; }
    public string Type { get; }
    public string Force { get; }

    public SpiritRowViewModel(CharacterSpiritData spirit)
    {
        Label = spirit.DisplayName + " (Kraft " + spirit.Force + (spirit.Bound ? ", gebunden" : "") + "):";
        Value = spirit.Services;
        Name = spirit.Name;
        Type = spirit.Type;
        Force = spirit.Force;
    }
}

public sealed class ComplexFormRowViewModel
{
    public string Guid { get; }
    public string Label { get; }
    public string Value { get; }
    public string Category { get; }

    public ComplexFormRowViewModel(CharacterComplexFormData form, int intKarmaCost)
    {
        Guid = form.Guid;
        Label = form.DisplayName;
        Value = form.Rating + " · " + intKarmaCost + " Karma";
        Category = form.Category;
    }
}

public sealed class CritterPowerRowViewModel
{
    public string Guid { get; }
    public string Label { get; }
    public string Value { get; }

    public CritterPowerRowViewModel(CharacterCritterPowerData power)
    {
        Guid = power.Guid;
        Label = power.DisplayName;
        Value = power.Points;
    }
}

public sealed class SpellsSectionViewModel : ViewModelBase
{
    private readonly TreeNodeViewModel _combat = new("Kampfzauber auswählen", blnExpanded: true);
    private readonly TreeNodeViewModel _detection = new("Wahrnehmungszauber auswählen");
    private readonly TreeNodeViewModel _health = new("Heilzauber auswählen");
    private readonly TreeNodeViewModel _illusion = new("Illusionszauber auswählen");
    private readonly TreeNodeViewModel _manipulation = new("Manipulationszauber auswählen");
    private readonly TreeNodeViewModel _ritual = new("Ausgewählte Geomantie Rituale");

    public ObservableCollection<TreeNodeViewModel> SpellCategories { get; }

    private TreeNodeViewModel? _selectedSpellNode;
    public TreeNodeViewModel? SelectedSpellNode
    {
        get => _selectedSpellNode;
        set => SetField(ref _selectedSpellNode, value);
    }

    public ObservableCollection<SpiritRowViewModel> Spirits { get; } = new();

    private SpiritRowViewModel? _selectedSpirit;
    public SpiritRowViewModel? SelectedSpirit
    {
        get => _selectedSpirit;
        set => SetField(ref _selectedSpirit, value);
    }

    public ObservableCollection<ComplexFormRowViewModel> ComplexForms { get; } = new();
    public ObservableCollection<CritterPowerRowViewModel> CritterPowers { get; } = new();

    private ComplexFormRowViewModel? _selectedComplexForm;
    public ComplexFormRowViewModel? SelectedComplexForm
    {
        get => _selectedComplexForm;
        set => SetField(ref _selectedComplexForm, value);
    }

    private CritterPowerRowViewModel? _selectedCritterPower;
    public CritterPowerRowViewModel? SelectedCritterPower
    {
        get => _selectedCritterPower;
        set => SetField(ref _selectedCritterPower, value);
    }

    private string _freeSpiritPowerPointsText = string.Empty;
    public string FreeSpiritPowerPointsText
    {
        get => _freeSpiritPowerPointsText;
        private set => SetField(ref _freeSpiritPowerPointsText, value);
    }

    public bool ShowFreeSpiritPowerPoints => FreeSpiritPowerPointsText.Length > 0;

    private CharacterDocument? _character;

    public ObservableCollection<string> Traditions { get; } = new();
    public ObservableCollection<string> Streams { get; } = new();

    public bool ShowTradition => _character?.Magician == true;
    public bool ShowStream => _character?.Technomancer == true;

    private string? _selectedTradition;
    public string? SelectedTradition
    {
        get => _selectedTradition;
        set
        {
            if (!SetField(ref _selectedTradition, value) || _character == null || value == null)
                return;
            _character.Tradition = value;
            RefreshResistancePools();
        }
    }

    private string? _selectedStream;
    public string? SelectedStream
    {
        get => _selectedStream;
        set
        {
            if (!SetField(ref _selectedStream, value) || _character == null || value == null)
                return;
            _character.Stream = value;
            RefreshResistancePools();
        }
    }

    private string _drainResistanceText = string.Empty;
    public string DrainResistanceText { get => _drainResistanceText; private set => SetField(ref _drainResistanceText, value); }
    public bool ShowDrainResistance => DrainResistanceText.Length > 0;

    private string _fadingResistanceText = string.Empty;
    public string FadingResistanceText { get => _fadingResistanceText; private set => SetField(ref _fadingResistanceText, value); }
    public bool ShowFadingResistance => FadingResistanceText.Length > 0;

    public SpellsSectionViewModel()
    {
        SpellCategories = new ObservableCollection<TreeNodeViewModel>
            { _combat, _detection, _health, _illusion, _manipulation, _ritual };
    }

    private void RefreshResistancePools()
    {
        if (_character == null)
            return;
        CharacterDerivedValueData? drain = _character.DrainResistance;
        DrainResistanceText = drain == null ? string.Empty : "Widerstand gegen Entzug: " + drain.Value;
        CharacterDerivedValueData? fading = _character.FadingResistance;
        FadingResistanceText = fading == null ? string.Empty : "Widerstand gegen Fading: " + fading.Value;
        OnPropertyChanged(nameof(ShowDrainResistance));
        OnPropertyChanged(nameof(ShowFadingResistance));
    }

    private static List<string> LoadTraditionNames(string strDataFile)
    {
        XmlDocument objDoc = XmlManager.Instance.Load(strDataFile);
        XmlNodeList? objNodes = objDoc.SelectNodes("/chummer/traditions/tradition/name");
        return objNodes == null
            ? new List<string>()
            : objNodes.Cast<XmlNode>().Select(n => n.InnerText).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;

        if (Traditions.Count == 0)
            foreach (string strName in LoadTraditionNames("traditions.xml"))
                Traditions.Add(strName);
        if (Streams.Count == 0)
            foreach (string strName in LoadTraditionNames("streams.xml"))
                Streams.Add(strName);

        _selectedTradition = string.IsNullOrEmpty(character.Tradition) ? null : character.Tradition;
        OnPropertyChanged(nameof(SelectedTradition));
        _selectedStream = string.IsNullOrEmpty(character.Stream) ? null : character.Stream;
        OnPropertyChanged(nameof(SelectedStream));
        OnPropertyChanged(nameof(ShowTradition));
        OnPropertyChanged(nameof(ShowStream));
        RefreshResistancePools();

        var byCategory = new (string Category, TreeNodeViewModel Node)[]
        {
            ("Combat", _combat),
            ("Detection", _detection),
            ("Health", _health),
            ("Illusion", _illusion),
            ("Manipulation", _manipulation),
            ("Geomancy Ritual", _ritual),
        };
        foreach (var entry in byCategory)
            entry.Node.Children.Clear();

        foreach (CharacterSpellData spell in character.Spells)
        {
            var target = _combat;
            foreach (var entry in byCategory)
            {
                if (entry.Category == spell.Category)
                {
                    target = entry.Node;
                    break;
                }
            }

            var spellNode = new TreeNodeViewModel(spell.Name, strCategory: spell.Category);
            spellNode.SetSpellDetails(spell);
            target.Children.Add(spellNode);
        }

        Spirits.Clear();
        foreach (CharacterSpiritData spirit in character.Spirits)
            Spirits.Add(new SpiritRowViewModel(spirit));

        ComplexForms.Clear();
        foreach (CharacterComplexFormData form in character.ComplexForms)
            ComplexForms.Add(new ComplexFormRowViewModel(form,
                character.ComputeComplexFormKarmaCost(form.Category, int.TryParse(form.Rating, out int rating) ? rating : 0)));

        CritterPowers.Clear();
        foreach (CharacterCritterPowerData power in character.CritterPowers)
            CritterPowers.Add(new CritterPowerRowViewModel(power));

        CharacterDerivedValueData? points = character.FreeSpiritPowerPoints;
        FreeSpiritPowerPointsText = points == null ? string.Empty : "Kraftpunkte: " + points.Value + " verbleibend";
        OnPropertyChanged(nameof(ShowFreeSpiritPowerPoints));
    }
}
