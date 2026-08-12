using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Backs the Advanced Lifestyle builder (frmSelectAdvancedLifestyle.cs's equivalent):
/// five aspect dropdowns, Roommates/Percentage, and Positive/Negative Quality checklists, with a
/// live LP/Nuyen preview via CharacterDocument.PreviewAdvancedLifestyle - the exact same
/// computation AddAdvancedLifestyle itself uses, so the two can never drift out of sync.</summary>
public sealed class AdvancedLifestyleDialogViewModel : ViewModelBase
{
    private CharacterDocument? _character;

    private string _strName = string.Empty;
    private string? _strComforts;
    private string? _strEntertainment;
    private string? _strNecessities;
    private string? _strNeighborhood;
    private string? _strSecurity;
    private decimal _decRoommates;
    private decimal _decPercentage = 100;

    public string Name { get => _strName; set => SetField(ref _strName, value); }

    public ObservableCollection<string> ComfortsOptions { get; } = new();
    public ObservableCollection<string> EntertainmentOptions { get; } = new();
    public ObservableCollection<string> NecessitiesOptions { get; } = new();
    public ObservableCollection<string> NeighborhoodOptions { get; } = new();
    public ObservableCollection<string> SecurityOptions { get; } = new();

    public string? Comforts { get => _strComforts; set { if (SetField(ref _strComforts, value)) RefreshPreview(); } }
    public string? Entertainment { get => _strEntertainment; set { if (SetField(ref _strEntertainment, value)) RefreshPreview(); } }
    public string? Necessities { get => _strNecessities; set { if (SetField(ref _strNecessities, value)) RefreshPreview(); } }
    public string? Neighborhood { get => _strNeighborhood; set { if (SetField(ref _strNeighborhood, value)) RefreshPreview(); } }
    public string? Security { get => _strSecurity; set { if (SetField(ref _strSecurity, value)) RefreshPreview(); } }

    public decimal Roommates { get => _decRoommates; set { if (SetField(ref _decRoommates, value)) RefreshPreview(); } }
    public decimal Percentage { get => _decPercentage; set { if (SetField(ref _decPercentage, value)) RefreshPreview(); } }

    public ObservableCollection<LifestyleQualityItemViewModel> PositiveQualities { get; } = new();
    public ObservableCollection<LifestyleQualityItemViewModel> NegativeQualities { get; } = new();

    private string _strLpPreview = "0";
    public string LpPreview { get => _strLpPreview; private set => SetField(ref _strLpPreview, value); }

    private string _strCostPreview = "0¥";
    public string CostPreview { get => _strCostPreview; private set => SetField(ref _strCostPreview, value); }

    public void LoadOptions(CharacterDocument character)
    {
        _character = character;

        void Fill(ObservableCollection<string> target, string tag)
        {
            target.Clear();
            foreach (string name in character.GetLifestyleAspectOptions(tag))
                target.Add(name);
        }

        Fill(ComfortsOptions, "comfort");
        Fill(EntertainmentOptions, "entertainment");
        Fill(NecessitiesOptions, "necessity");
        Fill(NeighborhoodOptions, "neighborhood");
        Fill(SecurityOptions, "security");

        PositiveQualities.Clear();
        NegativeQualities.Clear();
        foreach (var option in character.GetLifestyleQualityOptions())
        {
            var item = new LifestyleQualityItemViewModel(option.Name, option.Lp, option.Source, option.Page);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LifestyleQualityItemViewModel.IsChecked)) RefreshPreview(); };
            (option.Category == "Positive" ? PositiveQualities : NegativeQualities).Add(item);
        }

        // Set after the collections are populated so each setter's RefreshPreview call above has
        // real data to work with (also matches ComboBox.SelectedIndex defaulting to 0).
        _strComforts = ComfortsOptions.FirstOrDefault();
        _strEntertainment = EntertainmentOptions.FirstOrDefault();
        _strNecessities = NecessitiesOptions.FirstOrDefault();
        _strNeighborhood = NeighborhoodOptions.FirstOrDefault();
        _strSecurity = SecurityOptions.FirstOrDefault();
        OnPropertyChanged(nameof(Comforts));
        OnPropertyChanged(nameof(Entertainment));
        OnPropertyChanged(nameof(Necessities));
        OnPropertyChanged(nameof(Neighborhood));
        OnPropertyChanged(nameof(Security));

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_character == null || Comforts == null || Entertainment == null || Necessities == null
            || Neighborhood == null || Security == null)
            return;

        var lstPositive = PositiveQualities.Where(q => q.IsChecked).Select(q => q.Name).ToList();
        var lstNegative = NegativeQualities.Where(q => q.IsChecked).Select(q => q.Name).ToList();
        var preview = _character.PreviewAdvancedLifestyle(Comforts, Entertainment, Necessities, Neighborhood,
            Security, (int)Roommates, (int)Percentage, lstPositive, lstNegative);

        LpPreview = preview.Lp.ToString();
        CostPreview = $"{preview.Cost:###,###,##0}¥";
    }
}

public sealed class LifestyleQualityItemViewModel : ViewModelBase
{
    public LifestyleQualityItemViewModel(string name, int lp, string source, string page)
    {
        Name = name;
        Lp = lp;
        SourcePage = string.IsNullOrEmpty(page) ? source : source + " " + page;
    }

    public string Name { get; }
    public int Lp { get; }
    public string SourcePage { get; }
    public string Label => $"{Name} ({(Lp >= 0 ? "+" : string.Empty)}{Lp} LP)";

    private bool _blnIsChecked;
    public bool IsChecked { get => _blnIsChecked; set => SetField(ref _blnIsChecked, value); }
}
