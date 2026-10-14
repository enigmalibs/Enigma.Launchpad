using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WorkspacePageDragDropTests
{
    [Fact]
    public async Task DropGroup_MovesItNextToTheTarget_AndSaves()
    {
        PageHarness harness = new(PageHarness.Data("A", "B", "C", "D"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.True(page.DropGroup(Payload(page, page.Groups[0].Id), page.Groups[2], DropPosition.After));

        Assert.Equal(["B", "C", "A", "D"], Names(page));
        Assert.Equal(["B", "C", "A", "D"], (await harness.SavedGroupsAsync()).Select(group => group.Name));
        Assert.True(page.Groups[0].MoveUpCommand.CanExecute(null) is false);
    }

    [Fact]
    public async Task DropGroup_WhereItAlreadyIs_DoesNothing()
    {
        PageHarness harness = new(PageHarness.Data("A", "B", "C"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.False(page.DropGroup(Payload(page, page.Groups[1].Id), page.Groups[0], DropPosition.After));
        await harness.Session.FlushAsync();

        Assert.Equal(["A", "B", "C"], Names(page));
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task AGroup_CannotBeDroppedOnItself_OrWhenUnknown_ButMovesPastAGroupOfAnyKind()
    {
        LaunchpadData data = PageHarness.Data("A", "B").With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "C", ShortcutKind.Url, [])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        Guid a = page.Groups[0].Id;

        Assert.False(page.CanDropGroup(Payload(page, a), page.Groups[0]));
        Assert.False(page.CanDropGroup(Payload(page, Guid.NewGuid()), page.Groups[1]));
        Assert.True(page.CanDropGroup(Payload(page, a), page.Groups[1]));
        Assert.True(page.CanDropGroup(Payload(page, a), page.Groups[2]));

        Assert.True(page.DropGroup(Payload(page, a), page.Groups[2], DropPosition.After));

        Assert.Equal(["B", "C", "A"], Names(page));
    }

    [Fact]
    public async Task DropShortcut_OnATileOfItsOwnGroup_Reorders()
    {
        PageHarness harness = new(Shortcuts(("Dev", ["A", "B", "C"])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel dev = page.Groups[0];

        Assert.True(page.DropShortcut(Payload(page, dev.Shortcuts[2].Id), dev, dev.Shortcuts[0], DropPosition.Before));

        Assert.Equal(["C", "A", "B"], Names(dev));
        Assert.Equal(["C", "A", "B"], (await harness.SavedGroupsAsync())[0].Shortcuts.Select(shortcut => shortcut.Name));
    }

    [Fact]
    public async Task DropShortcut_OnItsOwnGroupsEmptySpace_MovesItToTheEnd()
    {
        PageHarness harness = new(Shortcuts(("Dev", ["A", "B", "C"])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel dev = page.Groups[0];

        Assert.True(page.DropShortcut(Payload(page, dev.Shortcuts[0].Id), dev, null, DropPosition.After));

        Assert.Equal(["B", "C", "A"], Names(dev));
    }

    [Fact]
    public async Task DropShortcut_OnTheLastPlaceItAlreadyHas_DoesNothing()
    {
        PageHarness harness = new(Shortcuts(("Dev", ["A", "B"])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel dev = page.Groups[0];

        Assert.False(page.DropShortcut(Payload(page, dev.Shortcuts[1].Id), dev, null, DropPosition.After));
        await harness.Session.FlushAsync();

        Assert.Empty(harness.Store.Saved);
    }

    [Theory]
    [InlineData(DropPosition.Before, new[] { "X", "A", "Y" })]
    [InlineData(DropPosition.After, new[] { "X", "Y", "A" })]
    public async Task DropShortcut_OnATileOfAnotherGroup_MovesItThere(DropPosition position, string[] expected)
    {
        PageHarness harness = new(Shortcuts(("Dev", ["A", "B"]), ("Tools", ["X", "Y"])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel dev = page.Groups[0];
        GroupViewModel tools = page.Groups[1];
        ShortcutViewModel a = dev.Shortcuts[0];

        Assert.True(page.DropShortcut(Payload(page, a.Id), tools, tools.Shortcuts[1], position));

        Assert.Equal(["B"], Names(dev));
        Assert.Equal(expected, Names(tools));
        Assert.Same(tools, a.Group);
        ShortcutGroup[] saved = await harness.SavedGroupsAsync();
        Assert.Equal(["B"], saved[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(expected, saved[1].Shortcuts.Select(shortcut => shortcut.Name));
    }

    [Fact]
    public async Task DropShortcut_IntoAnEmptyFoldedGroup_AppendsIt_AndUnfoldsTheGroup()
    {
        LaunchpadData data = LaunchpadData.Empty.With(
            ShortcutKind.Application,
            [
                new ShortcutGroup(Guid.NewGuid(), "Dev", ShortcutKind.Application, [PageHarness.Shortcut("A")]),
                new ShortcutGroup(Guid.NewGuid(), "Empty", ShortcutKind.Application, [], IsCollapsed: true),
            ]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel empty = page.Groups[1];

        Assert.True(page.DropShortcut(Payload(page, page.Groups[0].Shortcuts[0].Id), empty, null, DropPosition.After));

        Assert.Empty(page.Groups[0].Shortcuts);
        Assert.Equal(["A"], Names(empty));
        Assert.True(empty.IsExpanded);
        Assert.False((await harness.SavedGroupsAsync())[1].IsCollapsed);
    }

    [Fact]
    public async Task AMovedShortcut_ThenMovesWithinItsNewGroup()
    {
        PageHarness harness = new(Shortcuts(("Dev", ["A"]), ("Tools", ["X", "Y"])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel a = page.Groups[0].Shortcuts[0];
        page.DropShortcut(Payload(page, a.Id), page.Groups[1], null, DropPosition.After);

        Assert.True(a.MoveUpCommand.CanExecute(null));
        Assert.False(a.MoveDownCommand.CanExecute(null));

        a.MoveUpCommand.Execute(null);

        Assert.Equal(["X", "A", "Y"], Names(page.Groups[1]));
    }

    [Fact]
    public async Task AShortcut_IsRefused_OnItsOwnTile_InAGroupOfAnotherKind_OrOnATileOfAnotherGroup()
    {
        LaunchpadData data = Shortcuts(("Dev", ["A", "B"]), ("Tools", ["X"]))
            .With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Links", ShortcutKind.Url, [PageHarness.Shortcut("L", "https://example.com")])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel dev = page.Groups[0];
        GroupViewModel links = page.Groups[2];
        ShortcutViewModel a = dev.Shortcuts[0];

        Assert.False(page.CanDropShortcut(Payload(page, a.Id), dev, a));
        Assert.False(page.CanDropShortcut(Payload(page, a.Id), dev, page.Groups[1].Shortcuts[0]));
        Assert.False(page.CanDropShortcut(Payload(page, Guid.NewGuid()), dev, null));
        Assert.False(page.CanDropShortcut(Payload(page, a.Id), links, null));
        Assert.False(page.CanDropShortcut(new DragPayload(ShortcutKind.Url, a.Id), links, links.Shortcuts[0]));
        Assert.False(page.DropShortcut(Payload(page, a.Id), links, null, DropPosition.After));
        Assert.True(page.CanDropShortcut(Payload(page, a.Id), dev, dev.Shortcuts[1]));
        Assert.True(page.CanDropShortcut(Payload(page, a.Id), page.Groups[1], null));
    }

    [Fact]
    public async Task ClearDragState_ClearsEveryHintAndMark()
    {
        PageHarness harness = new(Shortcuts(("Dev", ["A", "B"])));
        WorkspacePageViewModel page = await harness.LoadAsync();
        page.Groups[0].DropHint = DropHint.Inside;
        page.Groups[0].IsDragging = true;
        page.Groups[0].Shortcuts[1].DropHint = DropHint.After;
        page.Groups[0].Shortcuts[0].IsDragging = true;

        page.ClearDragState();

        Assert.Equal(DropHint.None, page.Groups[0].DropHint);
        Assert.False(page.Groups[0].IsDragging);
        Assert.All(page.Groups[0].Shortcuts, shortcut => Assert.Equal(DropHint.None, shortcut.DropHint));
        Assert.All(page.Groups[0].Shortcuts, shortcut => Assert.False(shortcut.IsDragging));
    }

    [Fact]
    public async Task DropHint_DrivesTheIndicatorFlags()
    {
        WorkspacePageViewModel page = await new PageHarness().LoadAsync();
        GroupViewModel group = new(page, new ShortcutGroup(Guid.NewGuid(), "G", ShortcutKind.Application, []));

        group.DropHint = DropHint.Before;
        Assert.True(group.IsDropBefore);
        Assert.False(group.IsDropAfter || group.IsDropInside);

        group.DropHint = DropHint.Inside;
        Assert.True(group.IsDropInside);
        Assert.False(group.IsDropBefore || group.IsDropAfter);
    }

    private static DragPayload Payload(WorkspacePageViewModel page, Guid id) => new(ShortcutKind.Application, id);

    private static string[] Names(WorkspacePageViewModel page) => page.Groups.Select(group => group.Name).ToArray();

    private static string[] Names(GroupViewModel group) => group.Shortcuts.Select(shortcut => shortcut.Name).ToArray();

    private static LaunchpadData Shortcuts(params (string Group, string[] Names)[] groups)
        => PageHarness.WithShortcuts(
            ShortcutKind.Application,
            groups.Select(group => (group.Group, group.Names.Select(name => PageHarness.Shortcut(name)).ToArray())).ToArray());
}
