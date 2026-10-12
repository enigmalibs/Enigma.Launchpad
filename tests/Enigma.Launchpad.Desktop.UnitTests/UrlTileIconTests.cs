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
    public async Task AUrlTile_ShowsItsWebsitesIcon()
    {
        StubImage icon = new();
        _harness.WebsiteIcons.Answer = url => url == Guide ? icon : null;

        ShortcutViewModel tile = await LoadedTileAsync();

        Assert.Same(icon, tile.TargetIcon);
        Assert.True(tile.HasTargetIcon);
        Assert.Equal([Guide], _harness.WebsiteIcons.Requests);
        Assert.Empty(_harness.ProgramIcons.Requests);
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
        _harness.WebsiteIcons.Answer = url => url == Blog ? blogIcon : new StubImage();
        ShortcutViewModel tile = await LoadedTileAsync();
        _harness.Dialogs.ShortcutAnswers.Enqueue(_guide with { Target = Blog });

        await tile.EditCommand.ExecuteAsync(null);

        Assert.Same(blogIcon, tile.TargetIcon);
        Assert.Equal([Guide, Blog], _harness.WebsiteIcons.Requests);
    }

    [Fact]
    public async Task ADirectoryTile_AsksForNoIcon()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Directory, ("Places", [PageHarness.Shortcut("Docs", @"C:\Docs")])), ShortcutKind.Directory);
        harness.WebsiteIcons.Answer = _ => new StubImage();

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.Empty(harness.WebsiteIcons.Requests);
        Assert.Empty(harness.ProgramIcons.Requests);
        Assert.False(page.Groups[0].Shortcuts[0].HasTargetIcon);
    }

    private async Task<ShortcutViewModel> LoadedTileAsync()
    {
        CategoryPageViewModel page = await _harness.LoadAsync();
        return page.Groups[0].Shortcuts[0];
    }
}
