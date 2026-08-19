using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Which kind of row this is - only MartialArt and Maneuver rows are deletable (headers
/// and nested advantages aren't, since Core has no RemoveMartialArtAdvantage yet).</summary>
public enum MartialArtsItemKind
{
    Header,
    MartialArt,
    Advantage,
    Maneuver
}

/// <summary>One row of the flat Kampfkünste list - section headers ("Ausgewählte Kampfkunst"),
/// martial arts, their advantages (indented), and maneuvers, all in one ListBox like the legacy
/// layout.</summary>
public sealed class MartialArtsListItemViewModel
{
    public string Text { get; }
    public bool IsHeader { get; }
    public double IndentLeft { get; }
    public MartialArtsItemKind Kind { get; }

    /// <summary>The saved Martial Art/Maneuver name to pass to RemoveMartialArt(Maneuver) -
    /// empty for headers and advantages.</summary>
    public string Name { get; }
    public int ItemId { get; }
    public string Notes { get; }

    /// <summary>"&lt;book&gt; &lt;page&gt;" - only set (non-empty) for MartialArt rows.</summary>
    public string SourcePage { get; }

    public MartialArtsListItemViewModel(string strText, bool blnIsHeader = false, bool blnIsIndented = false,
        MartialArtsItemKind eKind = MartialArtsItemKind.Header, string strName = "", string strSourcePage = "",
        int intItemId = -1, string strNotes = "")
    {
        Text = strText;
        IsHeader = blnIsHeader;
        IndentLeft = blnIsIndented ? 16 : 0;
        Kind = eKind;
        Name = strName;
        SourcePage = strSourcePage;
        ItemId = intItemId;
        Notes = strNotes;
    }
}

public sealed class MartialArtsSectionViewModel : ViewModelBase
{
    public ObservableCollection<MartialArtsListItemViewModel> Items { get; } = new();

    private MartialArtsListItemViewModel? _selectedItem;
    public MartialArtsListItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        Items.Clear();
        Items.Add(new MartialArtsListItemViewModel("Ausgewählte Kampfkunst", blnIsHeader: true));
        foreach (CharacterMartialArtData martialArt in character.MartialArts)
        {
            Items.Add(new MartialArtsListItemViewModel(martialArt.Name + " (" + martialArt.Rating + ")",
                eKind: MartialArtsItemKind.MartialArt, strName: martialArt.Name,
                strSourcePage: martialArt.SourcePage, intItemId: martialArt.MartialArtId, strNotes: martialArt.Notes));
            foreach (string strAdvantage in martialArt.Advantages)
                Items.Add(new MartialArtsListItemViewModel(strAdvantage, blnIsIndented: true,
                    eKind: MartialArtsItemKind.Advantage));
        }

        Items.Add(new MartialArtsListItemViewModel("Ausgewählte Manöver", blnIsHeader: true));
        foreach (CharacterMartialArtManeuverData maneuver in character.MartialArtManeuvers)
            Items.Add(new MartialArtsListItemViewModel(maneuver.Name,
                eKind: MartialArtsItemKind.Maneuver, strName: maneuver.Name, intItemId: maneuver.ManeuverId,
                strNotes: maneuver.Notes));

        SelectedItem = null;
    }
}
