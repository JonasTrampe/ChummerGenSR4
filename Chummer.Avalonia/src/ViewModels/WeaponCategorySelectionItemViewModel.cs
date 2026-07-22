namespace Chummer.NewUI.ViewModels;

public sealed class WeaponCategorySelectionItemViewModel : ViewModelBase
{
    private bool _blnIsSelected;

    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _blnIsSelected;
        set => SetField(ref _blnIsSelected, value);
    }
}
