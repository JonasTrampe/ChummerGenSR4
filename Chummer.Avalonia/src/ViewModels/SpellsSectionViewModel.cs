using System.Collections.ObjectModel;
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

    public ComplexFormRowViewModel(CharacterComplexFormData form)
    {
        Guid = form.Guid;
        Label = form.DisplayName;
        Value = form.Rating;
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

    public SpellsSectionViewModel()
    {
        SpellCategories = new ObservableCollection<TreeNodeViewModel>
            { _combat, _detection, _health, _illusion, _manipulation, _ritual };
    }

    public void LoadCharacter(CharacterDocument character)
    {
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

            target.Children.Add(new TreeNodeViewModel(spell.Name));
        }

        Spirits.Clear();
        foreach (CharacterSpiritData spirit in character.Spirits)
            Spirits.Add(new SpiritRowViewModel(spirit));

        ComplexForms.Clear();
        foreach (CharacterComplexFormData form in character.ComplexForms)
            ComplexForms.Add(new ComplexFormRowViewModel(form));

        CritterPowers.Clear();
        foreach (CharacterCritterPowerData power in character.CritterPowers)
            CritterPowers.Add(new CritterPowerRowViewModel(power));
    }
}
