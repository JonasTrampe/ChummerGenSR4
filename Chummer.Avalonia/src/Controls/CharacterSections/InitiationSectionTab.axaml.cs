using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class InitiationSectionTab : UserControl
{
    public InitiationSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var listBox = this.FindControl<ListBox>("InitiationGradesListBox")!;
        listBox.Items.Clear();
        foreach (CharacterInitiationGradeData grade in character.InitiationGrades)
            listBox.Items.Add(new ListBoxItem { Content = grade.DisplayName });
    }
}
