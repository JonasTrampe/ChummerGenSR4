using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>One row of the flat Kampfkünste list - section headers ("Ausgewählte Kampfkunst"),
/// martial arts, their advantages (indented), and maneuvers, all in one ListBox like the legacy
/// layout.</summary>
public sealed class MartialArtsListItemViewModel
{
    public string Text { get; }
    public bool IsHeader { get; }
    public double IndentLeft { get; }

    public MartialArtsListItemViewModel(string strText, bool blnIsHeader = false, bool blnIsIndented = false)
    {
        Text = strText;
        IsHeader = blnIsHeader;
        IndentLeft = blnIsIndented ? 16 : 0;
    }
}

public sealed class MartialArtsSectionViewModel : ViewModelBase
{
    public ObservableCollection<MartialArtsListItemViewModel> Items { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        Items.Clear();
        Items.Add(new MartialArtsListItemViewModel("Ausgewählte Kampfkunst", blnIsHeader: true));
        foreach (CharacterMartialArtData martialArt in character.MartialArts)
        {
            Items.Add(new MartialArtsListItemViewModel(martialArt.Name + " (" + martialArt.Rating + ")"));
            foreach (string strAdvantage in martialArt.Advantages)
                Items.Add(new MartialArtsListItemViewModel(strAdvantage, blnIsIndented: true));
        }

        Items.Add(new MartialArtsListItemViewModel("Ausgewählte Manöver", blnIsHeader: true));
        foreach (CharacterMartialArtManeuverData maneuver in character.MartialArtManeuvers)
            Items.Add(new MartialArtsListItemViewModel(maneuver.Name));
    }
}
