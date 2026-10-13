using System;
using System.Collections.Generic;
using System.Linq;
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

    private FakeShortcutIcons Icons => _harness.Icons;

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
    public async Task AProgramWithoutAnIcon_KeepsTheGlyph_AndNothingIsSaved()
    {
        ShortcutViewModel tile = await LoadedTileAsync();
        await _harness.Session.FlushAsync();

        Assert.Null(tile.TargetIcon);
        Assert.False(tile.HasTargetIcon);
        Assert.Equal(PhosphorIcon.AppWindow, tile.Icon);
        Assert.Null(tile.Model.Icon);
        Assert.Empty(_harness.Store.Saved);
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
        Assert.Equal([nameof(ShortcutViewModel.TargetIcon), nameof(ShortcutViewModel.HasTargetIcon)], changed.Take(2));
    }

    [Fact]
    public async Task AFoundIcon_IsKeptWithTheShortcut_AndSaved()
    {
        Icons.Hold = true;
        ShortcutViewModel tile = await LoadedTileAsync();

        Icons.Release(CodePath, new StubImage());

        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), tile.Model.Icon);
        Shortcut saved = Assert.Single(Assert.Single(await _harness.SavedApplicationsAsync()).Shortcuts);
        Assert.Equal(_code with { Icon = FakeShortcutIcons.FoundIcon(CodePath) }, saved);
    }

    [Fact]
    public async Task IconsFoundWhileThePageIsBuilt_AreSavedOnce_WithEveryGroup()
    {
        Shortcut git = PageHarness.Shortcut("Git", @"C:\Tools\git.exe");
        PageHarness harness = new(PageHarness.WithShortcuts(
            ShortcutKind.Application,
            ("Dev tools", [_code]),
            ("Source control", [git]),
            ("Empty", [])));
        harness.Icons.Answer = _ => new StubImage();

        await harness.LoadAsync();
        ShortcutGroup[] saved = await harness.SavedApplicationsAsync();

        Assert.Single(harness.Store.Saved);
        Assert.Equal(["Dev tools", "Source control", "Empty"], saved.Select(group => group.Name));
        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), saved[0].Shortcuts[0].Icon);
        Assert.Equal(FakeShortcutIcons.FoundIcon(git.Target), saved[1].Shortcuts[0].Icon);
    }

    [Fact]
    public async Task AKeptIcon_IsShown_WithoutLookingForOne()
    {
        ShortcutIcon kept = new("kept");
        StubImage image = new();
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code with { Icon = kept }])));
        harness.Icons.Images["kept"] = image;
        harness.Icons.Answer = _ => new StubImage();

        CategoryPageViewModel page = await harness.LoadAsync();
        await harness.Session.FlushAsync();

        Assert.Same(image, page.Groups[0].Shortcuts[0].TargetIcon);
        Assert.Equal([kept], harness.Icons.Shown);
        Assert.Empty(harness.Icons.Requests);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task AFoundIcon_IsShownAfterARestart_WithoutLookingForItAgain()
    {
        Icons.Answer = _ => new StubImage();
        await LoadedTileAsync();
        LaunchpadData saved = await SavedDocumentAsync(_harness);

        PageHarness restarted = new(saved);
        CategoryPageViewModel page = await restarted.LoadAsync();

        Assert.True(page.Groups[0].Shortcuts[0].HasTargetIcon);
        Assert.Equal([FakeShortcutIcons.FoundIcon(CodePath)], restarted.Icons.Shown);
        Assert.Empty(restarted.Icons.Requests);
    }

    [Fact]
    public async Task AKeptAutomaticIconThatCannotBeShown_IsLookedForAgain_AndReplaced()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code with { Icon = new ShortcutIcon("broken") }])));
        harness.Icons.Images["broken"] = null;
        harness.Icons.Answer = _ => new StubImage();

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), page.Groups[0].Shortcuts[0].Model.Icon);
        Assert.Equal([CodePath], harness.Icons.Requests);
    }

    [Fact]
    public async Task AChosenIconThatCannotBeShown_KeepsTheGlyph_AndIsNeverReplaced()
    {
        ShortcutIcon chosen = new("broken", IsCustom: true);
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code with { Icon = chosen }])));
        harness.Icons.Images["broken"] = null;
        harness.Icons.Answer = _ => new StubImage();

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.False(page.Groups[0].Shortcuts[0].HasTargetIcon);
        Assert.Equal(chosen, page.Groups[0].Shortcuts[0].Model.Icon);
        Assert.Empty(harness.Icons.Requests);
    }

    [Fact]
    public async Task ADirectoryTile_NeverLooksForAnIcon()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Directory, ("Places", [PageHarness.Shortcut("Place", @"C:\Docs")])), ShortcutKind.Directory);
        harness.Icons.Answer = _ => new StubImage();

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.Empty(harness.Icons.Requests);
        Assert.False(page.Groups[0].Shortcuts[0].HasTargetIcon);
    }

    [Fact]
    public async Task ADirectoryTile_ShowsTheIconItKeeps()
    {
        ShortcutIcon chosen = new("chosen", IsCustom: true);
        PageHarness harness = new(
            PageHarness.WithShortcuts(ShortcutKind.Directory, ("Places", [PageHarness.Shortcut("Place", @"C:\Docs") with { Icon = chosen }])),
            ShortcutKind.Directory);

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.True(page.Groups[0].Shortcuts[0].HasTargetIcon);
        Assert.Equal([chosen], harness.Icons.Shown);
    }

    [Fact]
    public async Task AnEditThatChangesTheTarget_ShowsTheNewProgramsIcon_AndKeepsIt()
    {
        StubImage oldIcon = new();
        StubImage newIcon = new();
        Icons.Answer = path => path == CodePath ? oldIcon : newIcon;
        ShortcutViewModel tile = await LoadedTileAsync();

        // The form drops an icon found from the old target (ShortcutEditorViewModel.ToShortcut).
        await EditAsync(tile, tile.Model with { Target = MovedCodePath, Icon = null });

        Assert.Same(newIcon, tile.TargetIcon);
        Assert.Equal([CodePath, MovedCodePath], Icons.Requests);
        Assert.Equal(FakeShortcutIcons.FoundIcon(MovedCodePath), Assert.Single(Assert.Single(await _harness.SavedApplicationsAsync()).Shortcuts).Icon);
    }

    [Fact]
    public async Task AnEditThatChangesTheTarget_DropsTheOldIconAtOnce()
    {
        Icons.Answer = path => path == CodePath ? new StubImage() : null;
        ShortcutViewModel tile = await LoadedTileAsync();
        Assert.True(tile.HasTargetIcon);

        Icons.Hold = true;
        await EditAsync(tile, tile.Model with { Target = MovedCodePath, Icon = null });

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
        Assert.Equal(FakeShortcutIcons.FoundIcon(MovedCodePath), tile.Model.Icon);
    }

    [Fact]
    public async Task AnIconFoundAfterAnEditGaveTheShortcutOne_IsIgnored()
    {
        Icons.Hold = true;
        ShortcutViewModel tile = await LoadedTileAsync();
        ShortcutIcon chosen = new("chosen", IsCustom: true);
        await EditAsync(tile, _code with { Icon = chosen });

        Icons.Release(CodePath, new StubImage());

        Assert.Equal(chosen, tile.Model.Icon);
    }

    [Fact]
    public async Task AnIconFoundForATileNoLongerOnThePage_IsNotSaved()
    {
        Icons.Hold = true;
        ShortcutViewModel tile = await LoadedTileAsync();
        _harness.Dialogs.ConfirmAnswers.Enqueue(true);
        await tile.DeleteCommand.ExecuteAsync(null);
        await _harness.Session.FlushAsync();
        int saves = _harness.Store.Saved.Count;

        Icons.Release(CodePath, new StubImage());
        await _harness.Session.FlushAsync();

        Assert.Equal(saves, _harness.Store.Saved.Count);
        Assert.Empty(Assert.Single(_harness.Store.LastSaved!.GroupsOf(ShortcutKind.Application)).Shortcuts);
    }

    [Fact]
    public async Task AnEditThatChoosesAnImage_ShowsIt_AndSavesIt()
    {
        Icons.Answer = _ => new StubImage();
        ShortcutViewModel tile = await LoadedTileAsync();
        ShortcutIcon chosen = FakeShortcutIcons.ChosenIcon(@"C:\Pictures\code.png");
        StubImage image = new();
        Icons.Images[chosen.Png] = image;

        await EditAsync(tile, tile.Model with { Icon = chosen });

        Assert.Same(image, tile.TargetIcon);
        Assert.Equal([CodePath], Icons.Requests);
        Assert.Equal(chosen, Assert.Single(Assert.Single(await _harness.SavedApplicationsAsync()).Shortcuts).Icon);
    }

    [Fact]
    public async Task AnEditThatGivesUpAChosenImage_LooksForTheProgramsIcon()
    {
        ShortcutIcon chosen = FakeShortcutIcons.ChosenIcon(@"C:\Pictures\code.png");
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code with { Icon = chosen }])));
        StubImage found = new();
        harness.Icons.Answer = _ => found;
        CategoryPageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];
        Assert.Empty(harness.Icons.Requests);

        harness.Dialogs.ShortcutAnswers.Enqueue(tile.Model with { Icon = null });
        await tile.EditCommand.ExecuteAsync(null);

        Assert.Same(found, tile.TargetIcon);
        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), tile.Model.Icon);
    }

    [Fact]
    public async Task AnEditThatKeepsTheTarget_DoesNotLookAgain()
    {
        StubImage icon = new();
        Icons.Answer = _ => icon;
        ShortcutViewModel tile = await LoadedTileAsync();

        await EditAsync(tile, tile.Model with { Name = "VS Code", Arguments = "--new-window" });

        Assert.Equal([CodePath], Icons.Requests);
        Assert.Same(icon, tile.TargetIcon);
    }

    private static async Task<LaunchpadData> SavedDocumentAsync(PageHarness harness)
    {
        await harness.Session.FlushAsync();
        return harness.Store.LastSaved ?? throw new InvalidOperationException("Nothing was saved.");
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
