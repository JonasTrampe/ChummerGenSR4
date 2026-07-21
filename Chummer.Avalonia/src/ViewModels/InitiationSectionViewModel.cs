using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class InitiationSectionViewModel : ViewModelBase
{
    public ObservableCollection<string> Grades { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        Grades.Clear();
        foreach (CharacterInitiationGradeData grade in character.InitiationGrades)
            Grades.Add(grade.DisplayName);
    }
}
