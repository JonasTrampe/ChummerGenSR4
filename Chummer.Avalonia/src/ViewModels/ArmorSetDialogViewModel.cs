namespace Chummer.NewUI.ViewModels;

public sealed class ArmorSetDialogViewModel : ViewModelBase
{
    private string _strName = string.Empty;
    private string _strErrorMessage = string.Empty;

    public string Name
    {
        get => _strName;
        set => SetField(ref _strName, value);
    }

    public string ErrorMessage
    {
        get => _strErrorMessage;
        set => SetField(ref _strErrorMessage, value);
    }
}
