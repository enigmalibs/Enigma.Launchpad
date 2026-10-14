using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WorkspacePageShortcutsTests
{
    [Fact]
    public async Task AddShortcut_AppendsIt_UnfoldsTheGroup_AndSaves()
    {
        LaunchpadData data = LaunchpadData.Empty.With(
            ShortcutKind.Application,
            [new ShortcutGroup(Guid.NewGuid(), "Dev tools", ShortcutKind.Application, [PageHarness.Shortcut("Code")], IsCollapsed: true)]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        Shortcut terminal = PageHarness.Shortcut("Terminal", @"C:\Tools\wt.exe");
        harness.Dialogs.ShortcutAnswers.Enqueue(terminal);

        await page.Groups[0].AddShortcutCommand.ExecuteAsync(null);

        Assert.Equal((ShortcutKind.Application, (Shortcut?)null), Assert.Single(harness.Dialogs.ShortcutForms));
        Assert.Equal(["Code", "Terminal"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.True(page.Groups[0].IsExpanded);
        ShortcutGroup saved = Assert.Single(await harness.SavedApplicationsAsync());
        Assert.Equal(terminal, saved.Shortcuts[1]);
        Assert.False(saved.IsCollapsed);
    }

    [Fact]
    public async Task AddShortcut_Cancelled_ChangesNothing()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[0].AddShortcutCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Empty(page.Groups[0].Shortcuts);
        Assert.Empty(harness.Store.Saved);
    }

    [Theory]
    [InlineData(ShortcutKind.Directory)]
    [InlineData(ShortcutKind.Url)]
    public async Task AddShortcut_AsksForTheKindOfItsPage(ShortcutKind kind)
    {
        PageHarness harness = new(LaunchpadData.Empty.With(kind, [new ShortcutGroup(Guid.NewGuid(), "G", ShortcutKind.Application, [])]), kind);
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[0].AddShortcutCommand.ExecuteAsync(null);

        Assert.Equal(kind, Assert.Single(harness.Dialogs.ShortcutForms).Kind);
    }

    [Fact]
    public async Task Edit_ReplacesTheShortcutInPlace_AndSaves()
    {
        Shortcut code = PageHarness.Shortcut("Code");
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [code, PageHarness.Shortcut("Git")])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];
        Shortcut edited = code with { Name = "VS Code", Arguments = "--new-window" };
        harness.Dialogs.ShortcutAnswers.Enqueue(edited);

        await tile.EditCommand.ExecuteAsync(null);

        Assert.Same(code, Assert.Single(harness.Dialogs.ShortcutForms).Existing);
        Assert.Equal("VS Code", tile.Name);
        Assert.Equal(edited, (await harness.SavedApplicationsAsync())[0].Shortcuts[0]);
    }

    [Fact]
    public async Task Edit_WithoutAChange_DoesNotSave()
    {
        Shortcut code = PageHarness.Shortcut("Code");
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [code])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.ShortcutAnswers.Enqueue(code with { });

        await page.Groups[0].Shortcuts[0].EditCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesTheShortcut_AndSaves()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [PageHarness.Shortcut("Code"), PageHarness.Shortcut("Git")])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.ConfirmAnswers.Enqueue(true);

        await page.Groups[0].Shortcuts[0].DeleteCommand.ExecuteAsync(null);

        Assert.Contains("Code", Assert.Single(harness.Dialogs.Confirmations).Title, StringComparison.Ordinal);
        Assert.Equal(["Git"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(["Git"], (await harness.SavedApplicationsAsync())[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(1, page.Groups[0].Count);
    }

    [Fact]
    public async Task Delete_Declined_KeepsTheShortcut()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [PageHarness.Shortcut("Code")])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.ConfirmAnswers.Enqueue(false);

        await page.Groups[0].Shortcuts[0].DeleteCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Single(page.Groups[0].Shortcuts);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task MoveEarlierAndLater_StayInsideTheGroup_AndSave()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(
            ShortcutKind.Application,
            ("Dev tools", [PageHarness.Shortcut("A"), PageHarness.Shortcut("B"), PageHarness.Shortcut("C")])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel a = page.Groups[0].Shortcuts[0];
        ShortcutViewModel c = page.Groups[0].Shortcuts[2];

        Assert.False(a.MoveUpCommand.CanExecute(null));
        Assert.False(c.MoveDownCommand.CanExecute(null));

        a.MoveDownCommand.Execute(null);
        c.MoveUpCommand.Execute(null);

        Assert.Equal(["B", "C", "A"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.True(a.MoveUpCommand.CanExecute(null));
        Assert.False(a.MoveDownCommand.CanExecute(null));
        Assert.Equal(["B", "C", "A"], (await harness.SavedApplicationsAsync())[0].Shortcuts.Select(shortcut => shortcut.Name));
    }

    [Fact]
    public async Task Open_Reveal_AndCopy_GoThroughTheActions_WithThePagesKind()
    {
        Shortcut folder = PageHarness.Shortcut("Projects", @"C:\Projects");
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Directory, ("Work", [folder])), ShortcutKind.Directory);
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];

        tile.OpenCommand.Execute(null);
        tile.RevealCommand.Execute(null);
        await tile.CopyTargetCommand.ExecuteAsync(null);

        Assert.Equal(
            [("Open", ShortcutKind.Directory, folder), ("Reveal", ShortcutKind.Directory, folder), ("Copy", default(ShortcutKind), folder)],
            harness.Actions.Performed);
    }

    [Fact]
    public async Task ALink_CannotBeRevealed_AndShowsItsHost()
    {
        PageHarness harness = new(
            PageHarness.WithShortcuts(ShortcutKind.Url, ("Docs", [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net/docs/welcome")])),
            ShortcutKind.Url);
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];

        Assert.False(tile.CanReveal);
        Assert.False(tile.RevealCommand.CanExecute(null));
        Assert.Equal("docs.avaloniaui.net", tile.Subtitle);
        Assert.Equal(PhosphorIcon.Globe, tile.Icon);
    }

    [Fact]
    public async Task AnApplicationTile_ShowsItsPath_AndItsArgumentsInTheToolTip()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(
            ShortcutKind.Application,
            ("Dev tools", [PageHarness.Shortcut("Code", @"C:\Tools\code.exe", "--new-window")])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];

        Assert.True(tile.CanReveal);
        Assert.Equal(@"C:\Tools\code.exe", tile.Subtitle);
        Assert.Equal(@"C:\Tools\code.exe --new-window", tile.ToolTip);
        Assert.Equal(PhosphorIcon.AppWindow, tile.Icon);
        Assert.False(tile.HasDescription);
        Assert.Equal(string.Empty, tile.Description);
    }

    [Fact]
    public async Task ATileWithADescription_ShowsIt_AndPutsItWholeInTheToolTip()
    {
        Shortcut code = PageHarness.Shortcut("Code", @"C:\Tools\code.exe", "--new-window") with { Description = "The editor" };
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [code])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];

        Assert.True(tile.HasDescription);
        Assert.Equal("The editor", tile.Description);
        Assert.Equal($@"C:\Tools\code.exe --new-window{Environment.NewLine}The editor", tile.ToolTip);
    }

    [Fact]
    public async Task EditingADescription_UpdatesTheTile_AndSaves()
    {
        Shortcut code = PageHarness.Shortcut("Code");
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [code])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel tile = page.Groups[0].Shortcuts[0];
        harness.Dialogs.ShortcutAnswers.Enqueue(code with { Description = "The editor" });

        await tile.EditCommand.ExecuteAsync(null);

        Assert.Equal("The editor", tile.Description);
        Assert.True(tile.HasDescription);
        Assert.Equal("The editor", Assert.Single(Assert.Single(await harness.SavedApplicationsAsync()).Shortcuts).Description);
    }
}
