using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class TryToGetIconTests
{
    private const string CodePath = @"C:\Tools\code.exe";
    private const string Docs = "https://docs.example.com/guide";

    [Fact]
    public async Task AnAutomaticIcon_IsLookedUpAgain_WithoutAQuestion_AndSaved()
    {
        Shortcut code = PageHarness.Shortcut("Code", CodePath) with { Icon = new ShortcutIcon("old") };
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [code])));
        StubImage fresh = new();
        harness.Icons.Answer = _ => fresh;
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];

        await tile.FindIconCommand.ExecuteAsync(null);

        Assert.Empty(harness.Dialogs.Questions);
        Assert.Equal([CodePath], harness.Icons.Refreshes);
        Assert.Same(fresh, tile.TargetIcon);
        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), tile.Model.Icon);
        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), Assert.Single(Assert.Single(await harness.SavedApplicationsAsync()).Shortcuts).Icon);
    }

    [Fact]
    public async Task AWebsitesIcon_IsLookedUpAgain()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Url, ("Reading", [PageHarness.Shortcut("Guide", Docs)])), ShortcutKind.Url);
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        harness.Icons.Answer = _ => new StubImage();

        await tile.FindIconCommand.ExecuteAsync(null);

        Assert.Equal([Docs], harness.Icons.Refreshes);
        Assert.Equal(FakeShortcutIcons.FoundIcon(Docs), tile.Model.Icon);
    }

    [Fact]
    public async Task AChosenImage_IsOnlyReplaced_OnceTheUserSaysSo()
    {
        ShortcutIcon chosen = new("chosen", IsCustom: true);
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [PageHarness.Shortcut("Code", CodePath) with { Icon = chosen }])));
        harness.Icons.Answer = _ => new StubImage();
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        harness.Dialogs.ConfirmAnswers.Enqueue(true);

        await tile.FindIconCommand.ExecuteAsync(null);

        (string title, string message, string confirmText) = Assert.Single(harness.Dialogs.Questions);
        Assert.Equal("Replace your icon?", title);
        Assert.Equal("\u201CCode\u201D uses an image you chose. Look for the program's own icon and use it instead?", message);
        Assert.Equal("Replace", confirmText);
        Assert.Equal(FakeShortcutIcons.FoundIcon(CodePath), tile.Model.Icon);
        Assert.False(tile.Model.Icon!.IsCustom);
    }

    [Fact]
    public async Task AChosenImage_IsKept_WhenTheUserDeclines()
    {
        ShortcutIcon chosen = new("chosen", IsCustom: true);
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [PageHarness.Shortcut("Code", CodePath) with { Icon = chosen }])));
        harness.Icons.Answer = _ => new StubImage();
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        harness.Dialogs.ConfirmAnswers.Enqueue(false);

        await tile.FindIconCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Single(harness.Dialogs.Questions);
        Assert.Empty(harness.Icons.Requests);
        Assert.Equal(chosen, tile.Model.Icon);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task NothingFound_TellsTheUser_AndKeepsTheIcon()
    {
        ShortcutIcon kept = new("kept");
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [PageHarness.Shortcut("Code", CodePath) with { Icon = kept }])));
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        Assert.True(tile.HasTargetIcon);

        await tile.FindIconCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        (string severity, string title, string message) = Assert.Single(harness.Notifications.Shown);
        Assert.Equal(("Warning", "No icon found"), (severity, title));
        Assert.Equal("No icon could be found for \u201CCode\u201D. Its icon is unchanged.", message);
        Assert.Equal(kept, tile.Model.Icon);
        Assert.True(tile.HasTargetIcon);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task AnAnswerForATileEditedMeanwhile_IsIgnored()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [PageHarness.Shortcut("Code", CodePath)])));
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        harness.Icons.Hold = true;
        Task lookup = tile.FindIconCommand.ExecuteAsync(null);
        harness.Dialogs.ShortcutAnswers.Enqueue(tile.Model with { Target = @"D:\Apps\code.exe" });
        await tile.EditCommand.ExecuteAsync(null);

        harness.Icons.Release(CodePath, new StubImage());
        await lookup;

        Assert.Empty(harness.Notifications.Shown);
        Assert.NotEqual(FakeShortcutIcons.FoundIcon(CodePath), tile.Model.Icon);
    }

    [Fact]
    public async Task AFolder_HasNoIconToLookFor()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Directory, ("Places", [PageHarness.Shortcut("Docs", @"C:\Docs")])), ShortcutKind.Directory);
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];

        Assert.False(tile.CanFindIcon);
        Assert.False(tile.FindIconCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(ShortcutKind.Application, true)]
    [InlineData(ShortcutKind.Url, true)]
    [InlineData(ShortcutKind.Directory, false)]
    public async Task OnlyApplicationAndLinkTiles_OfferIt(ShortcutKind kind, bool offered)
    {
        string target = kind == ShortcutKind.Url ? Docs : CodePath;
        PageHarness harness = new(PageHarness.WithShortcuts(kind, ("Group", [PageHarness.Shortcut("Item", target)])), kind);

        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.All(page.Groups.SelectMany(group => group.Shortcuts), tile => Assert.Equal(offered, tile.CanFindIcon));
    }
}
