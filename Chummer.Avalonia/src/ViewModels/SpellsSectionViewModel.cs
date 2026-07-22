using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class SpiritRowViewModel
{
    public string Label { get; }
    public string Value { get; }

    public SpiritRowViewModel(CharacterSpiritData spirit)
    {
        Label = spirit.DisplayName + " (Kraft " + spirit.Force + (spirit.Bound ? ", gebunden" : "") + "):";
        Value = spirit.Services;
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
    }
}
