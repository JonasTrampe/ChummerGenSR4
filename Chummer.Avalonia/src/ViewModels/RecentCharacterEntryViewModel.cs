namespace Chummer.NewUI.ViewModels;

public sealed class RecentCharacterEntryViewModel : ViewModelBase
{
    public string FilePath { get; }
    public string DisplayText { get; }
    public bool IsSticky { get; }

    // Avalonia's MenuItem can't mix ItemsSource with a declared static child MenuItem in XAML
    // (that silently breaks the binding), so the "no recent files" placeholder has to be a member
    // of this same bound collection instead - not clickable, distinguished by this flag.
    public bool IsPlaceholder { get; }

    public RecentCharacterEntryViewModel(string filePath, bool isSticky)
    {
        FilePath = filePath;
        IsSticky = isSticky;
        DisplayText = (isSticky ? "★ " : string.Empty) + filePath;
    }

    private RecentCharacterEntryViewModel(string strPlaceholderText)
    {
        FilePath = string.Empty;
        DisplayText = strPlaceholderText;
        IsPlaceholder = true;
    }

    public static RecentCharacterEntryViewModel CreatePlaceholder(string strPlaceholderText) => new(strPlaceholderText);
}
