using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class MetamagicRowViewModel
{
    public string Guid { get; }
    public string Name { get; }
    public string SourcePage { get; }

    public MetamagicRowViewModel(CharacterMetamagicData metamagic)
    {
        Guid = metamagic.Guid;
        Name = metamagic.Name;
        SourcePage = metamagic.SourcePage;
    }
}

public sealed class InitiationSectionViewModel : ViewModelBase
{
    public ObservableCollection<string> Grades { get; } = new();
    public ObservableCollection<MetamagicRowViewModel> Metamagics { get; } = new();

    private MetamagicRowViewModel? _selectedMetamagic;
    public MetamagicRowViewModel? SelectedMetamagic
    {
        get => _selectedMetamagic;
        set => SetField(ref _selectedMetamagic, value);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        Grades.Clear();
        foreach (CharacterInitiationGradeData grade in character.InitiationGrades)
            Grades.Add(grade.DisplayName);

        Metamagics.Clear();
        foreach (CharacterMetamagicData metamagic in character.Metamagics)
            Metamagics.Add(new MetamagicRowViewModel(metamagic));
    }
}
