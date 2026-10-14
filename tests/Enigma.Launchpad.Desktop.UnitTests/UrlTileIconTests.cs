using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class UrlTileIconTests
{
    private const string Guide = "https://docs.example.com/guide?page=2";
    private const string Blog = "https://blog.example.org/";

    private readonly Shortcut _guide = PageHarness.Shortcut("Guide", Guide);
    private readonly PageHarness _harness;

    public UrlTileIconTests()
        => _harness = new PageHarness(PageHarness.WithShortcuts(ShortcutKind.Url, ("Reading", [_guide])), ShortcutKind.Url);

    [Fact]
    public async Task AUrlTile_ShowsItsWebsitesIcon_AndKeepsIt()
    {
        StubImage icon = new();
        _harness.Icons.Answer = url => url == Guide ? icon : null;

        ShortcutViewModel tile = await LoadedTileAsync();

        Assert.Same(icon, tile.TargetIcon);
        Assert.True(tile.HasTargetIcon);
        Assert.Equal([Guide], _harness.Icons.Requests);
        Assert.Equal(FakeShortcutIcons.FoundIcon(Guide), Assert.Single(Assert.Single(await _harness.SavedGroupsAsync()).Shortcuts).Icon);
    }

    [Fact]
    public async Task AUrlTileWithoutAnIcon_KeepsTheGlobe()
    {
        ShortcutViewModel tile = await LoadedTileAsync();

        Assert.False(tile.HasTargetIcon);
        Assert.Equal(PhosphorIcon.Globe, tile.Icon);
    }

    [Fact]
    public async Task AnEditToAnotherSite_ShowsThatSitesIcon()
    {
        StubImage blogIcon = new();
        _harness.Icons.Answer = url => url == Blog ? blogIcon : new StubImage();
        ShortcutViewModel tile = await LoadedTileAsync();
        _harness.Dialogs.ShortcutAnswers.Enqueue(tile.Model with { Target = Blog, Icon = null });

        await tile.EditCommand.ExecuteAsync(null);

        Assert.Same(blogIcon, tile.TargetIcon);
        Assert.Equal([Guide, Blog], _harness.Icons.Requests);
    }

    private async Task<ShortcutViewModel> LoadedTileAsync()
    {
        WorkspacePageViewModel page = await _harness.LoadAsync();
        return page.Groups[0].Shortcuts[0];
    }
}
