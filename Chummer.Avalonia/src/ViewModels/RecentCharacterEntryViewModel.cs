namespace Chummer.NewUI.ViewModels;

public sealed class RecentCharacterEntryViewModel : ViewModelBase
{
    public string FilePath { get; }
    public string DisplayText { get; }
    public bool IsSticky { get; }

    public RecentCharacterEntryViewModel(string filePath, bool isSticky)
    {
        FilePath = filePath;
        IsSticky = isSticky;
        DisplayText = (isSticky ? "★ " : string.Empty) + filePath;
    }
}
