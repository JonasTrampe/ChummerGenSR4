namespace Chummer.NewUI.ViewModels;

public sealed class OptionsBookItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool CanToggle => !IsRequired;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (IsRequired)
            {
                _isSelected = true;
                OnPropertyChanged();
                return;
            }

            SetField(ref _isSelected, value);
        }
    }
}
