using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class MetamagicRowViewModel
{
    public string Guid { get; }
    public string Name { get; }
    public string SourcePage { get; }
    public string Notes { get; }

    public MetamagicRowViewModel(CharacterMetamagicData metamagic)
    {
        Guid = metamagic.Guid;
        Name = metamagic.Name;
        SourcePage = metamagic.SourcePage;
        Notes = metamagic.Notes;
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

    private int _intInitiateGrade;
    public int InitiateGrade { get => _intInitiateGrade; set => SetField(ref _intInitiateGrade, value); }

    /// <summary>Whether raising the Initiate/Submersion Grade is even possible for this
    /// character - matches CharacterDocument.RaiseInitiateGrade's own guard.</summary>
    private bool _blnCanInitiate;
    public bool CanInitiate { get => _blnCanInitiate; set => SetField(ref _blnCanInitiate, value); }

    private bool _blnIsGroup;
    public bool IsGroup { get => _blnIsGroup; set => SetField(ref _blnIsGroup, value); }

    private bool _blnIsOrdeal;
    public bool IsOrdeal { get => _blnIsOrdeal; set => SetField(ref _blnIsOrdeal, value); }

    public void LoadCharacter(CharacterDocument character)
    {
        Grades.Clear();
        foreach (CharacterInitiationGradeData grade in character.InitiationGrades)
            Grades.Add(grade.DisplayName);

        Metamagics.Clear();
        foreach (CharacterMetamagicData metamagic in character.Metamagics)
            Metamagics.Add(new MetamagicRowViewModel(metamagic));

        InitiateGrade = character.InitiateGrade;
        CanInitiate = character.Magician || character.Technomancer;
    }
}
