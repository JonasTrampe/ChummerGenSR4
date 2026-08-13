using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Xml;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class CyberwareDialogViewModel : ViewModelBase
{
    private List<CyberwareOptionViewModel> _lstAllOptions = new();

    public ObservableCollection<CyberwareOptionViewModel> CyberwareOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<GradeOptionViewModel> Grades { get; } = new();

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

    private CyberwareOptionViewModel? _selectedCyberware;
    public CyberwareOptionViewModel? SelectedCyberware
    {
        get => _selectedCyberware;
        set
        {
            if (_selectedCyberware != null)
                _selectedCyberware.PropertyChanged -= OnSelectedCyberwarePropertyChanged;

            if (!SetField(ref _selectedCyberware, value))
            {
                if (value != null)
                    value.PropertyChanged += OnSelectedCyberwarePropertyChanged;
                return;
            }

            if (value != null)
                value.PropertyChanged += OnSelectedCyberwarePropertyChanged;
            RaiseFinalValuesChanged();
        }
    }

    private GradeOptionViewModel? _selectedGrade;
    public GradeOptionViewModel? SelectedGrade
    {
        get => _selectedGrade;
        set
        {
            if (!SetField(ref _selectedGrade, value))
                return;
            RaiseFinalValuesChanged();
        }
    }

    /// <summary>Only true (and only then shown/usable in the dialog) when the character's settings
    /// profile has the AllowCyberwareEssDiscounts house rule on - see
    /// CharacterDocument.AllowCyberwareEssenceDiscounts.</summary>
    public bool AllowEssenceDiscount { get; private set; }

    /// <summary>Only exposed by the Bioware picker when the character enables the Augmentation
    /// house rule. Selecting it forces Standard grade and the Transgenics category on add.</summary>
    public bool AllowCustomTransgenics { get; private set; }

    private bool _blnIsTransgenic;
    public bool IsTransgenic
    {
        get => _blnIsTransgenic;
        set
        {
            if (!SetField(ref _blnIsTransgenic, value))
                return;

            if (value)
                SelectedGrade = Grades.FirstOrDefault(g => g.Name == "Standard") ?? SelectedGrade;
            OnPropertyChanged(nameof(CanSelectGrade));
            RaiseFinalValuesChanged();
        }
    }

    public bool CanSelectGrade => !IsTransgenic;

    private int _intEssenceDiscountPercent;
    public int EssenceDiscountPercent
    {
        get => _intEssenceDiscountPercent;
        set
        {
            if (!SetField(ref _intEssenceDiscountPercent, value))
                return;
            RaiseFinalValuesChanged();
        }
    }

    private void OnSelectedCyberwarePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CyberwareOptionViewModel.Essence) or nameof(CyberwareOptionViewModel.Cost)
            or nameof(CyberwareOptionViewModel.AvailabilityValue))
            RaiseFinalValuesChanged();
    }

    private void RaiseFinalValuesChanged()
    {
        OnPropertyChanged(nameof(FinalEssence));
        OnPropertyChanged(nameof(FinalCost));
        OnPropertyChanged(nameof(FinalAvailability));
    }

    /// <summary>Grade-adjusted Essence: <see cref="CyberwareOptionViewModel.Essence"/> (already
    /// resolved for the chosen rating) times the grade's Essence multiplier (e.g. Alphaware 0.8,
    /// Betaware 0.7), further reduced by the Essence discount when AllowCyberwareEssDiscounts
    /// permits one - this is what actually gets written into the saved character.</summary>
    public string FinalEssence
    {
        get
        {
            if (SelectedCyberware == null || SelectedGrade == null) return "–";
            double dblBase = double.TryParse(SelectedCyberware.Essence, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            double dblGraded = dblBase * SelectedGrade.EssMultiplier;
            if (AllowEssenceDiscount && EssenceDiscountPercent > 0)
                dblGraded *= 1.0 - EssenceDiscountPercent / 100.0;
            return dblGraded.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Grade-adjusted cost: base cost times the grade's cost multiplier (e.g. Betaware 4x, Deltaware 10x).</summary>
    public string FinalCost
    {
        get
        {
            if (SelectedCyberware == null || SelectedGrade == null) return "–";
            double dblBase = double.TryParse(SelectedCyberware.Cost, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            return ((int)(dblBase * SelectedGrade.CostMultiplier)).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Grade-adjusted availability: base availability plus the grade's flat modifier
    /// (e.g. Second-Hand -1), keeping the R/F restriction suffix.</summary>
    public string FinalAvailability
    {
        get
        {
            if (SelectedCyberware == null || SelectedGrade == null) return "–";
            return (SelectedCyberware.AvailabilityValue + SelectedGrade.AvailModifier) + SelectedCyberware.AvailabilitySuffix;
        }
    }

    public void LoadOptions(bool blnBioware, CharacterDocument character)
    {
        AllowEssenceDiscount = character.AllowCyberwareEssenceDiscounts;
        OnPropertyChanged(nameof(AllowEssenceDiscount));
        AllowCustomTransgenics = blnBioware && character.AllowCustomTransgenicsEnabled;
        OnPropertyChanged(nameof(AllowCustomTransgenics));
        IsTransgenic = false;
        EssenceDiscountPercent = 0;

        _lstAllOptions.Clear();
        XmlDocument document = XmlManager.Instance.Load(blnBioware ? "bioware.xml" : "cyberware.xml");
        XmlNodeList? nodes = document.SelectNodes(blnBioware ? "/chummer/biowares/bioware" : "/chummer/cyberwares/cyberware");
        if (nodes != null)
        {
            foreach (XmlNode node in nodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (!character.IsBookEnabled(node["source"]?.InnerText ?? string.Empty))
                    continue;

                _lstAllOptions.Add(new CyberwareOptionViewModel(name, node["category"]?.InnerText ?? string.Empty,
                    node["rating"]?.InnerText ?? "0", node["ess"]?.InnerText ?? "0",
                    node["capacity"]?.InnerText ?? string.Empty,
                    node["avail"]?.InnerText ?? string.Empty, node["cost"]?.InnerText ?? string.Empty,
                    node["source"]?.InnerText ?? string.Empty, node["page"]?.InnerText ?? string.Empty));
            }
        }

        Categories.Clear();
        Categories.Add(App.LanguageCatalog.GetString("UI_All"));
        foreach (string strCategory in _lstAllOptions.Select(o => o.Category).Where(c => !string.IsNullOrEmpty(c))
                     .Distinct().OrderBy(c => c))
            Categories.Add(strCategory);

        _strSelectedCategory = App.LanguageCatalog.GetString("UI_All");
        OnPropertyChanged(nameof(SelectedCategory));

        Grades.Clear();
        XmlNodeList? gradeNodes = document.SelectNodes("/chummer/grades/grade");
        if (gradeNodes != null)
        {
            foreach (XmlNode node in gradeNodes)
            {
                string name = node["name"]?.InnerText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                double.TryParse(node["ess"]?.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblEss);
                double.TryParse(node["cost"]?.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblCost);
                int.TryParse(node["avail"]?.InnerText, out int intAvail);
                Grades.Add(new GradeOptionViewModel(name, dblEss, dblCost, intAvail));
            }
        }

        SelectedGrade = Grades.FirstOrDefault(g => g.Name == "Standard") ?? Grades.FirstOrDefault();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        CyberwareOptions.Clear();
        IEnumerable<CyberwareOptionViewModel> query = _lstAllOptions;

        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != App.LanguageCatalog.GetString("UI_All"))
            query = query.Where(o => o.Category == SelectedCategory);

        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (CyberwareOptionViewModel option in query)
            CyberwareOptions.Add(option);
    }
}

/// <summary>One entry from cyberware.xml/bioware.xml's shared &lt;grades&gt; list - Standard,
/// Alphaware, Betaware, Deltaware, and their Second-Hand/Adapsin variants. Multiplies a piece of
/// Cyber-/Bioware's own Essence and cost, and adds a flat modifier to its availability.</summary>
public sealed class GradeOptionViewModel
{
    public GradeOptionViewModel(string name, double essMultiplier, double costMultiplier, int availModifier)
    {
        Name = name;
        EssMultiplier = essMultiplier;
        CostMultiplier = costMultiplier;
        AvailModifier = availModifier;
    }

    public string Name { get; }
    public double EssMultiplier { get; }
    public double CostMultiplier { get; }
    public int AvailModifier { get; }

    public override string ToString() => Name;
}

public sealed class CyberwareOptionViewModel : ViewModelBase
{
    private readonly string _strEssExpression;
    private readonly string _strCostExpression;
    private readonly string _strAvailExpression;

    public CyberwareOptionViewModel(string name, string category, string maxRating, string essExpression,
        string capacity, string availExpression, string costExpression, string source, string page)
    {
        Name = name;
        Category = category;
        int.TryParse(maxRating, out int intMaxRating);
        MaxRating = intMaxRating;
        _strEssExpression = essExpression;
        Capacity = capacity;
        _strAvailExpression = availExpression;
        _strCostExpression = costExpression;
        SourcePage = string.IsNullOrWhiteSpace(page) ? source : source + " " + page;

        RatingValue = MaxRating > 0 ? 1 : 0;
    }

    public string Name { get; }
    public string Category { get; }

    /// <summary>0 if this item has no variable rating (a fixed, one-off piece of gear).</summary>
    public int MaxRating { get; }

    public bool HasRating => MaxRating > 0;
    public string Capacity { get; }
    public string SourcePage { get; }

    private int _intRatingValue;
    public int RatingValue
    {
        get => _intRatingValue;
        set
        {
            if (!SetField(ref _intRatingValue, value))
                return;
            OnPropertyChanged(nameof(Essence));
            OnPropertyChanged(nameof(Cost));
            OnPropertyChanged(nameof(AvailabilityValue));
        }
    }

    /// <summary>Essence cost at the currently chosen rating, before the grade multiplier
    /// (Cyberware.CalculatedESS's own grade-multiplier/discount formula isn't ported beyond this).</summary>
    public string Essence => RatingExpression.Evaluate(_strEssExpression, RatingValue.ToString())
        .ToString("0.##", CultureInfo.InvariantCulture);

    public string Cost => ((int)RatingExpression.Evaluate(_strCostExpression, RatingValue.ToString()))
        .ToString(CultureInfo.InvariantCulture);

    public string AvailabilitySuffix
    {
        get
        {
            if (string.IsNullOrEmpty(_strAvailExpression)) return string.Empty;
            char chLast = _strAvailExpression[_strAvailExpression.Length - 1];
            return chLast is 'R' or 'F' ? chLast.ToString() : string.Empty;
        }
    }

    public int AvailabilityValue
    {
        get
        {
            if (string.IsNullOrEmpty(_strAvailExpression)) return 0;
            string strExpression = string.IsNullOrEmpty(AvailabilitySuffix)
                ? _strAvailExpression
                : _strAvailExpression.Substring(0, _strAvailExpression.Length - 1);
            return (int)RatingExpression.Evaluate(strExpression, RatingValue.ToString());
        }
    }

    /// <summary>Raw saved rating - "0" for fixed (no-rating) items, matching AddCyberware's schema.</summary>
    public string Rating => RatingValue.ToString(CultureInfo.InvariantCulture);
}
