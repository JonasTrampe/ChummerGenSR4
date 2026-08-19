using System.Globalization;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

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
