using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ApplicationTileIconTests
{
    private const string CodePath = @"C:\Tools\code.exe";
    private const string MovedCodePath = @"D:\Apps\code.exe";

    private readonly Shortcut _code = PageHarness.Shortcut("Code", CodePath);
    private readonly PageHarness _harness;

    public ApplicationTileIconTests()
        => _harness = new PageHarness(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code])));

    private FakeTargetIcons Icons => _harness.ProgramIcons;

    [Fact]
    public async Task AnApplicationTile_ShowsTheIconOfItsProgram()
    {
        StubImage icon = new();
        Icons.Answer = path => path == CodePath ? icon : null;

        ShortcutViewModel tile = await LoadedTileAsync();

        Assert.Same(icon, tile.TargetIcon);
        Assert.True(tile.HasTargetIcon);
        Assert.Equal([CodePath], Icons.Requests);
    }

    [Fact]
    public async Task AProgramWithoutAnIcon_KeepsTheGlyph()
    {
        ShortcutViewModel tile = await LoadedTileAsync();

        Assert.Null(tile.TargetIcon);
        Assert.False(tile.HasTargetIcon);
        Assert.Equal(PhosphorIcon.AppWindow, tile.Icon);
    }

    [Fact]
    public async Task ThePage_DoesNotWaitForIcons_WhichFillInAsTheyArrive()
    {
        Icons.Hold = true;
        ShortcutViewModel tile = await LoadedTileAsync();
        Assert.False(tile.HasTargetIcon);

        List<string?> changed = [];
        tile.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        StubImage icon = new();
        Icons.Release(CodePath, icon);

        Assert.Same(icon, tile.TargetIcon);
        Assert.Equal([nameof(ShortcutViewModel.TargetIcon), nameof(ShortcutViewModel.HasTargetIcon)], changed);
    }

    [Theory]
    [InlineData(ShortcutKind.Directory, @"C:\Docs")]
    [InlineData(ShortcutKind.Url, "https://example.com")]
    public async Task DirectoryAndLinkTiles_NeverAskForAProgramIcon(ShortcutKind kind, string target)
    {
        PageHarness harness = new(PageHarness.WithShortcuts(kind, ("Places", [PageHarness.Shortcut("Place", target)])), kind);
        harness.ProgramIcons.Answer = _ => new StubImage();

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.Empty(harness.ProgramIcons.Requests);
        Assert.False(page.Groups[0].Shortcuts[0].HasTargetIcon);
    }

    [Fact]
    public async Task AnEditThatChangesTheTarget_ShowsTheNewProgramsIcon()
    {
        StubImage oldIcon = new();
        StubImage newIcon = new();
        Icons.Answer = path => path == CodePath ? oldIcon : newIcon;
        ShortcutViewModel tile = await LoadedTileAsync();

        await EditAsync(tile, _code with { Target = MovedCodePath });

        Assert.Same(newIcon, tile.TargetIcon);
        Assert.Equal([CodePath, MovedCodePath], Icons.Requests);
    }

    [Fact]
    public async Task AnEditThatChangesTheTarget_DropsTheOldIconAtOnce()
    {
        Icons.Answer = path => path == CodePath ? new StubImage() : null;
        ShortcutViewModel tile = await LoadedTileAsync();
        Assert.True(tile.HasTargetIcon);

        Icons.Hold = true;
        await EditAsync(tile, _code with { Target = MovedCodePath });

        Assert.Null(tile.TargetIcon);
        Assert.False(tile.HasTargetIcon);
    }

    [Fact]
    public async Task AnIconForATargetTheTileNoLongerHas_IsIgnored()
    {
        Icons.Hold = true;
        ShortcutViewModel tile = await LoadedTileAsync();
        await EditAsync(tile, _code with { Target = MovedCodePath });
        StubImage newIcon = new();

        Icons.Release(MovedCodePath, newIcon);
        Icons.Release(CodePath, new StubImage());

        Assert.Same(newIcon, tile.TargetIcon);
    }

    [Fact]
    public async Task AnEditThatKeepsTheTarget_DoesNotAskAgain()
    {
        StubImage icon = new();
        Icons.Answer = _ => icon;
        ShortcutViewModel tile = await LoadedTileAsync();

        await EditAsync(tile, _code with { Name = "VS Code", Arguments = "--new-window" });

        Assert.Equal([CodePath], Icons.Requests);
        Assert.Same(icon, tile.TargetIcon);
    }

    private async Task<ShortcutViewModel> LoadedTileAsync()
    {
        CategoryPageViewModel page = await _harness.LoadAsync();
        return page.Groups[0].Shortcuts[0];
    }

    private async Task EditAsync(ShortcutViewModel tile, Shortcut edited)
    {
        _harness.Dialogs.ShortcutAnswers.Enqueue(edited);
        await tile.EditCommand.ExecuteAsync(null);
    }
}
