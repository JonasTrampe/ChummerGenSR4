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

    private string _strPdfPath = string.Empty;
    public string PdfPath { get => _strPdfPath; set => SetField(ref _strPdfPath, value); }

    private int _intPdfOffset;
    public int PdfOffset { get => _intPdfOffset; set => SetField(ref _intPdfOffset, value); }
}
