using System.Collections.ObjectModel;
using System.Linq;
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

    private bool _blnIsCreateMode;
    public bool IsCreateMode
    {
        get => _blnIsCreateMode;
        set => SetField(ref _blnIsCreateMode, value);
    }

    private bool _blnIsLoadingNuyen;

    private int _intNuyenPointsMax;
    public int NuyenPointsMax
    {
        get => _intNuyenPointsMax;
        set => SetField(ref _intNuyenPointsMax, value);
    }

    private int _intNuyenPoints;
    public int NuyenPoints
    {
        get => _intNuyenPoints;
        set
        {
            if (!SetField(ref _intNuyenPoints, value))
                return;
            if (_blnIsLoadingNuyen || _character == null)
                return;

            int intCurrent = _character.NuyenPoints;
            while (intCurrent < value && _character.RaiseNuyenCreate())
                intCurrent++;
            while (intCurrent > value && _character.LowerNuyenCreate())
                intCurrent--;

            LoadCharacter(_character);
        }
    }

    private string _strNuyenPerPointLabel = string.Empty;
    public string NuyenPerPointLabel
    {
        get => _strNuyenPerPointLabel;
        set => SetField(ref _strNuyenPerPointLabel, value);
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

    public GeneralSectionViewModel()
    {
        foreach (AttributeRowViewModel row in Attributes)
            row.BaseValueEdited += OnAttributeBaseValueEdited;
    }

    private void OnAttributeBaseValueEdited(AttributeRowViewModel row, int intNewValue)
    {
        if (_character == null)
            return;

        int intCurrentValue = _character.Attributes.FirstOrDefault(a => a.Code == row.Code) is { } attribute
            && int.TryParse(attribute.Value, out int intParsed)
            ? intParsed
            : row.BaseValue;

        while (intCurrentValue < intNewValue && _character.RaiseAttributeCreate(row.Code))
            intCurrentValue++;
        while (intCurrentValue > intNewValue && _character.LowerAttributeCreate(row.Code))
            intCurrentValue--;

        LoadCharacter(_character);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        Alias = character.Alias;
        Metatype = character.Metatype;
        Nuyen = character.Nuyen;
        NuyenEquivalent = "= " + character.Nuyen + "¥";
        IsCreateMode = !character.Created;
        NuyenPointsMax = character.NuyenPointsMax;
        NuyenPerPointLabel = character.Nuyen + "¥ (1 Punkt = 1 "
            + (string.Equals(character.BuildMethod, "Karma", System.StringComparison.OrdinalIgnoreCase) ? "Karma" : "BP")
            + " = " + character.NuyenPerPoint + "¥)";
        _blnIsLoadingNuyen = true;
        NuyenPoints = character.NuyenPoints;
        _blnIsLoadingNuyen = false;
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

            row.IsLoading = true;
            row.Base = attribute.Value;
            row.Augmented = int.TryParse(attribute.Value, out int intBase) && intBase == attribute.Augmented.Value
                ? string.Empty
                : "(" + attribute.Augmented.Value + ")";
            row.Range = attribute.Minimum + " / " + attribute.Maximum + " (" + attribute.AugmentedMaximum + ")";
            row.IsCreateMode = !character.Created;
            row.BaseValue = intBase;
            row.MinValue = int.TryParse(attribute.Minimum, out int intMin) ? intMin : 0;
            row.MaxValue = int.TryParse(attribute.Maximum, out int intMax) ? intMax : 6;
            row.IsRowVisible = attribute.Code switch
            {
                "MAG" => character.Awakened,
                "RES" => character.Technomancer,
                _ => true
            };
            row.IsLoading = false;
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
            Contacts.Add(new ContactRowViewModel(character, contact));

        Enemies.Clear();
        foreach (CharacterContactData enemy in character.Enemies)
            Enemies.Add(new ContactRowViewModel(character, enemy));
    }
}
