using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class RecentCharacterEntryViewModelTests
{
    [Fact]
    public void RegularEntry_IsNotAPlaceholderAndCarriesItsFilePath()
    {
        var entry = new RecentCharacterEntryViewModel("/home/runner/characters/Ghost.chum", isSticky: false);

        Assert.False(entry.IsPlaceholder);
        Assert.Equal("/home/runner/characters/Ghost.chum", entry.FilePath);
        Assert.Equal("/home/runner/characters/Ghost.chum", entry.DisplayText);
    }

    [Fact]
    public void StickyEntry_PrefixesTheDisplayTextWithAStar()
    {
        var entry = new RecentCharacterEntryViewModel("/home/runner/characters/Ghost.chum", isSticky: true);

        Assert.True(entry.IsSticky);
        Assert.Equal("★ /home/runner/characters/Ghost.chum", entry.DisplayText);
    }

    [Fact]
    public void Placeholder_IsMarkedAndHasNoFilePath()
    {
        // Ported from MainWindow.axaml's "Zuletzt geöffnet" submenu bug: Avalonia's MenuItem
        // can't mix ItemsSource with a declared static child MenuItem, which silently broke the
        // recent-files binding entirely - the "no recent files" placeholder now has to be a
        // member of the same bound collection, distinguished by this flag so it renders disabled
        // and OnOpenRecentCharacterClick refuses to act on it.
        var placeholder = RecentCharacterEntryViewModel.CreatePlaceholder("No recent files");

        Assert.True(placeholder.IsPlaceholder);
        Assert.Equal(string.Empty, placeholder.FilePath);
        Assert.Equal("No recent files", placeholder.DisplayText);
    }
}
