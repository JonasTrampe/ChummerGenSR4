namespace Chummer.NewUI.ViewModels;

/// <summary>Bound state for the vehicle Retrofit percentage prompt.</summary>
public sealed class RetrofitDialogViewModel : ViewModelBase
{
    private decimal _decPercentage;

    public decimal Percentage
    {
        get => _decPercentage;
        set => SetField(ref _decPercentage, value);
    }
}
