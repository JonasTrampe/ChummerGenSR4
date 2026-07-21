using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class MartialArtsSectionTab : UserControl
{
    public MartialArtsSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var listBox = this.FindControl<ListBox>("MartialArtsListBox")!;
        listBox.Items.Clear();
        listBox.Items.Add(new ListBoxItem { Content = "Ausgewählte Kampfkunst", FontStyle = Avalonia.Media.FontStyle.Italic, Foreground = Avalonia.Media.Brushes.Gray });
        foreach (CharacterMartialArtData martialArt in character.MartialArts)
        {
            listBox.Items.Add(new ListBoxItem { Content = martialArt.Name + " (" + martialArt.Rating + ")" });
            foreach (string strAdvantage in martialArt.Advantages)
                listBox.Items.Add(new ListBoxItem { Content = "    " + strAdvantage });
        }

        listBox.Items.Add(new ListBoxItem { Content = "Ausgewählte Manöver", FontStyle = Avalonia.Media.FontStyle.Italic, Foreground = Avalonia.Media.Brushes.Gray });
        foreach (CharacterMartialArtManeuverData maneuver in character.MartialArtManeuvers)
            listBox.Items.Add(new ListBoxItem { Content = maneuver.Name });
    }
}
