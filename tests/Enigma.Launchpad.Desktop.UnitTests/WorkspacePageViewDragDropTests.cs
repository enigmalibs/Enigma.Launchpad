using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// Drops simulated on the real page, through the headless platform: the view must find what is under the
/// pointer, show where the item would land, and hand the drop to the ViewModel.
/// </summary>
[Collection(HeadlessCollection.Name)]
public sealed class WorkspacePageViewDragDropTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public WorkspacePageViewDragDropTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void ATileDraggedOntoAnotherGroup_ShowsWhereItLands_ThenMovesThere()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"]), ("Tools", ["X", "Y"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ShortcutViewModel a = page.Groups[0].Shortcuts[0];
            ShortcutViewModel y = page.Groups[1].Shortcuts[1];
            Control tile = Tile(view, y);
            Point rightHalf = At(harness, tile, tile.Bounds.Width - 10, tile.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, a.Id);

            harness.Window.DragDrop(rightHalf, RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
            harness.Window.DragDrop(rightHalf, RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);

            Assert.True(y.IsDropAfter);

            harness.Window.DragDrop(rightHalf, RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);

            Assert.Equal(["B"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
            Assert.Equal(["X", "Y", "A"], page.Groups[1].Shortcuts.Select(shortcut => shortcut.Name));
            Assert.Equal(DropHint.None, y.DropHint);

            ILaunchpadSession session = harness.Provider.GetRequiredService<ILaunchpadSession>();
            await session.FlushAsync();
            Assert.Equal(["X", "Y", "A"], session.GroupsOf(ShortcutKind.Application)[1].Shortcuts.Select(shortcut => shortcut.Name));
        });

    [Fact]
    public void ATileDraggedOverAGroupsHeader_LandsAtTheGroupsEnd()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"]), ("Tools", ["X"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel tools = page.Groups[1];
            Control name = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("groupName") && text.Text == "Tools");
            Point point = At(harness, name, 2, name.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, page.Groups[0].Shortcuts[0].Id);

            harness.Window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
            harness.Window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);

            Assert.True(tools.IsDropInside);

            harness.Window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);

            Assert.Equal(["X", "A"], tools.Shortcuts.Select(shortcut => shortcut.Name));
        });

    [Fact]
    public void AGroupDraggedOntoTheTopHalfOfAnother_LandsAboveIt()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", []), ("B", []), ("C", [])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel first = page.Groups[0];
            Control card = view.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("groupCard") && border.DataContext == first);
            Point topHalf = At(harness, card, card.Bounds.Width / 2, 4);
            DataTransfer data = Carrying(WorkspacePageView.GroupFormat, ShortcutKind.Application, page.Groups[2].Id);

            harness.Window.DragDrop(topHalf, RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
            harness.Window.DragDrop(topHalf, RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);

            Assert.True(first.IsDropBefore);

            harness.Window.DragDrop(topHalf, RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);

            Assert.Equal(["C", "A", "B"], page.Groups.Select(group => group.Name));
        });

    [Fact]
    public void ForeignData_IsRefused_AndChangesNothing()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            Control tile = Tile(view, page.Groups[0].Shortcuts[0]);
            Point point = At(harness, tile, 5, tile.Bounds.Height / 2);
            DataTransfer text = new();
            text.Add(DataTransferItem.CreateText("C:\\Windows"));
            DataTransfer unknown = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, Guid.NewGuid());

            foreach (DataTransfer data in new[] { text, unknown })
            {
                harness.Window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
                harness.Window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);
                harness.Window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);
            }

            Assert.Equal(["A", "B"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
            Assert.All(page.Groups[0].Shortcuts, shortcut => Assert.Equal(DropHint.None, shortcut.DropHint));
        });

    [Fact]
    public void EveryGroup_HasAGripToDragItBy()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", []), ("B", [])));
            WorkspacePageView view = await harness.LoadedPageAsync();

            Assert.Equal(2, view.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("grip")));
            Assert.True(DragDrop.GetAllowDrop(view));
        });

    private static Control Tile(WorkspacePageView view, ShortcutViewModel shortcut)
        => view.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("tile") && button.DataContext == shortcut);

    private static Point At(WindowHarness harness, Visual control, double x, double y)
        => control.TranslatePoint(new Point(x, y), harness.Window) ?? throw new InvalidOperationException("Not in the window.");

    private static DataTransfer Carrying(DataFormat<string> format, ShortcutKind kind, Guid id)
    {
        DataTransfer data = new();
        data.Add(DataTransferItem.Create(format, new DragPayload(kind, id).Format()));
        return data;
    }

    private static LaunchpadData Data(params (string Group, string[] Names)[] groups)
        => PageHarness.WithShortcuts(
            ShortcutKind.Application,
            groups.Select(group => (group.Group, group.Names.Select(name => PageHarness.Shortcut(name)).ToArray())).ToArray());
}
