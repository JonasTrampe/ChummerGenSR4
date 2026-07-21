using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class CharacterInfoSectionTab : UserControl
{
    public CharacterInfoSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        this.FindControl<TextBox>("GenderTextBox")!.Text = character.Gender;
        this.FindControl<TextBox>("EyeColorTextBox")!.Text = character.EyeColor;
        this.FindControl<TextBox>("HairColorTextBox")!.Text = character.HairColor;
        this.FindControl<TextBox>("PlayerNameTextBox")!.Text = character.PlayerName;
        this.FindControl<TextBox>("HeightTextBox")!.Text = character.Height;
        this.FindControl<TextBox>("WeightTextBox")!.Text = character.Weight;
        this.FindControl<TextBox>("SkinColorTextBox")!.Text = character.SkinColor;
        this.FindControl<TextBox>("StreetCredTextBox")!.Text = character.StreetCred;
        this.FindControl<TextBox>("NotorietyTextBox")!.Text = character.Notoriety;
        this.FindControl<TextBox>("PublicAwarenessTextBox")!.Text = character.PublicAwareness;
        this.FindControl<TextBox>("DescriptionTextBox")!.Text = character.Description;
        this.FindControl<TextBox>("BackgroundTextBox")!.Text = character.Background;
        this.FindControl<TextBox>("ConceptTextBox")!.Text = character.Concept;
        this.FindControl<TextBox>("NotesTextBox")!.Text = character.Notes;
    }
}
