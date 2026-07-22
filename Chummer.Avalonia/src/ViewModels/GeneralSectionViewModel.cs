using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class GeneralSectionViewModel : ViewModelBase
{
    private CharacterDocument? _character;

    private string _strAlias = string.Empty;
    public string Alias
    {
        get => _strAlias;
        set
        {
            if (!SetField(ref _strAlias, value))
                return;
            if (_character != null)
                _character.Alias = value;
        }
    }

    private string _strMetatype = string.Empty;
    public string Metatype
    {
        get => _strMetatype;
        set => SetField(ref _strMetatype, value);
    }

    private string _strNuyen = string.Empty;
    public string Nuyen
    {
        get => _strNuyen;
        set
        {
            if (!SetField(ref _strNuyen, value))
                return;
            if (_character != null)
                _character.Nuyen = value;
            NuyenEquivalent = "= " + value + "¥";
        }
    }

    private string _strNuyenEquivalent = string.Empty;
    public string NuyenEquivalent
    {
        get => _strNuyenEquivalent;
        set => SetField(ref _strNuyenEquivalent, value);
    }

    private bool _blnShowMysticAdeptMagSplit;
    public bool ShowMysticAdeptMagSplit
    {
        get => _blnShowMysticAdeptMagSplit;
        set => SetField(ref _blnShowMysticAdeptMagSplit, value);
    }

    private string _strMysticAdeptMagicianMagSplit = string.Empty;
    public string MysticAdeptMagicianMagSplit
    {
        get => _strMysticAdeptMagicianMagSplit;
        set => SetField(ref _strMysticAdeptMagicianMagSplit, value);
    }

    private string _strMysticAdeptAdeptMagSplit = string.Empty;
    public string MysticAdeptAdeptMagSplit
    {
        get => _strMysticAdeptAdeptMagSplit;
        set => SetField(ref _strMysticAdeptAdeptMagSplit, value);
    }

    public ObservableCollection<AttributeRowViewModel> Attributes { get; } = new()
    {
        new AttributeRowViewModel("BOD", "Konstitution (KON)"),
        new AttributeRowViewModel("AGI", "Geschicklichkeit (GES)"),
        new AttributeRowViewModel("REA", "Reaktion (REA)"),
        new AttributeRowViewModel("STR", "Stärke (STR)"),
        new AttributeRowViewModel("CHA", "Charisma (CHA)"),
        new AttributeRowViewModel("INT", "Intuition (INT)"),
        new AttributeRowViewModel("LOG", "Logik (LOG)"),
        new AttributeRowViewModel("WIL", "Willenskraft (WIL)"),
        new AttributeRowViewModel("EDG", "Edge (EDG)", blnShowRemove: true),
        new AttributeRowViewModel("MAG", "Magie (MAG)"),
        new AttributeRowViewModel("RES", "Resonanz (RES)", blnIsAttributeEnabled: false),
    };

    public ObservableCollection<TreeNodeViewModel> Qualities { get; } = new();

    private TreeNodeViewModel? _selectedQualityNode;
    public TreeNodeViewModel? SelectedQualityNode
    {
        get => _selectedQualityNode;
        set => SetField(ref _selectedQualityNode, value);
    }

    public ObservableCollection<ContactRowViewModel> Contacts { get; } = new();

    public ObservableCollection<ContactRowViewModel> Enemies { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        Alias = character.Alias;
        Metatype = character.Metatype;
        Nuyen = character.Nuyen;
        NuyenEquivalent = "= " + character.Nuyen + "¥";
        ShowMysticAdeptMagSplit = character.MysticAdept;
        MysticAdeptMagicianMagSplit = character.MysticAdept ? character.MysticAdeptMagicianMagSplit.ToString() : string.Empty;
        MysticAdeptAdeptMagSplit = character.MysticAdept ? character.MysticAdeptAdeptMagSplit.ToString() : string.Empty;

        foreach (CharacterAttributeData attribute in character.Attributes)
        {
            AttributeRowViewModel? row = null;
            foreach (AttributeRowViewModel candidate in Attributes)
            {
                if (candidate.Code == attribute.Code)
                {
                    row = candidate;
                    break;
                }
            }

            if (row is null)
                continue;

            row.Base = attribute.Value;
            row.Augmented = attribute.TotalValue == attribute.Value ? string.Empty : "(" + attribute.TotalValue + ")";
            row.Range = attribute.Minimum + " / " + attribute.Maximum + " (" + attribute.AugmentedMaximum + ")";
        }

        Qualities.Clear();
        var positiveQualities = new TreeNodeViewModel("Positive qualities", blnExpanded: true);
        var negativeQualities = new TreeNodeViewModel("Negative qualities", blnExpanded: true);
        foreach (CharacterQualityData quality in character.Qualities)
        {
            var parent = quality.Type == "Negative" ? negativeQualities : positiveQualities;
            parent.AddChild(new TreeNodeViewModel(quality.DisplayName, strCategory: quality.Type,
                strRating: quality.Extra, strSourceName: quality.Name));
        }
        if (positiveQualities.Children.Count > 0) Qualities.Add(positiveQualities);
        if (negativeQualities.Children.Count > 0) Qualities.Add(negativeQualities);

        Contacts.Clear();
        foreach (CharacterContactData contact in character.Contacts)
            Contacts.Add(new ContactRowViewModel(contact));

        Enemies.Clear();
        foreach (CharacterContactData enemy in character.Enemies)
            Enemies.Add(new ContactRowViewModel(enemy));
    }
}
