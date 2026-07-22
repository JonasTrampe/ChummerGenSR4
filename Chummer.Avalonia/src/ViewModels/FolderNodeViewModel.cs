using System.Collections.ObjectModel;

namespace Chummer.NewUI.ViewModels;

public sealed class FolderNodeViewModel : ViewModelBase
{
    private bool _isExpanded;

    public int Id { get; }
    public string Name { get; }
    public ObservableCollection<FolderNodeViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public FolderNodeViewModel(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
