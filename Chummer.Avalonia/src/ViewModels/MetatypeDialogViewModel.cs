using System.Collections.ObjectModel;
using System.Linq;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class MetatypeDialogViewModel : ViewModelBase
{
    private const string NoMetavariantValue = "-";
    private readonly ObservableCollection<NewCharacterMetatype> _allMetatypes = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<NewCharacterMetatype> Metatypes { get; } = new();
    public ObservableCollection<string> Metavariants { get; } = new();

    private string? _selectedCategory;
    public string? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetField(ref _selectedCategory, value))
                return;
            RefreshFilteredMetatypes();
        }
    }

    private NewCharacterMetatype? _selectedMetatype;
    public NewCharacterMetatype? SelectedMetatype
    {
        get => _selectedMetatype;
        set
        {
            if (!SetField(ref _selectedMetatype, value))
                return;
            RefreshMetavariants();
        }
    }

    private string _selectedMetavariant = NoMetavariantValue;
    public string SelectedMetavariant
    {
        get => _selectedMetavariant;
        set => SetField(ref _selectedMetavariant, value);
    }

    public void LoadMetatypes()
    {
        _allMetatypes.Clear();
        Categories.Clear();
        Metatypes.Clear();
        foreach (NewCharacterMetatype objMetatype in NewCharacterFactory.LoadMetatypes())
            _allMetatypes.Add(objMetatype);

        foreach (string strCategory in _allMetatypes.Select(x => x.CategoryLabel).Distinct())
            Categories.Add(strCategory);

        SelectedCategory = Categories.FirstOrDefault(x => x == "Metamenschen") ?? Categories.FirstOrDefault();
    }

    private void RefreshFilteredMetatypes()
    {
        string? strSelectedMetatype = SelectedMetatype?.Name;
        Metatypes.Clear();
        foreach (NewCharacterMetatype objMetatype in _allMetatypes.Where(x => x.CategoryLabel == SelectedCategory))
            Metatypes.Add(objMetatype);

        SelectedMetatype = Metatypes.FirstOrDefault(x => x.Name == strSelectedMetatype) ?? Metatypes.FirstOrDefault();
    }

    private void RefreshMetavariants()
    {
        string strPrevious = SelectedMetavariant;
        Metavariants.Clear();
        Metavariants.Add(NoMetavariantValue);

        if (SelectedMetatype != null)
        {
            foreach (NewCharacterMetavariant objMetavariant in SelectedMetatype.Metavariants)
                Metavariants.Add(objMetavariant.Name);
        }

        SelectedMetavariant = Metavariants.Contains(strPrevious) ? strPrevious : NoMetavariantValue;
    }
}
