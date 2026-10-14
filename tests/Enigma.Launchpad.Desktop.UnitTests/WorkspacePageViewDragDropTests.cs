using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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

            // The group's end, drawn where the tile goes: a dashed tile after its last one.
            Assert.True(tools.Shortcuts[0].IsDropAfter);
            Assert.True(Placeholder(view, tools.Shortcuts[0], "after").IsVisible);
            Assert.Equal(DropHint.None, tools.DropHint);

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
    public void AGroupDragged_OpensADashedBarThatMakesRoom_AndTheGapStaysPutUnderThePointer()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", []), ("B", []), ("C", []), ("D", [])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel b = page.Groups[1];
            Control card = Card(view, b);
            Point topOfB = At(harness, card, card.Bounds.Width / 2, 4);
            double top = At(harness, card, 0, 0).Y;
            DataTransfer data = Carrying(WorkspacePageView.GroupFormat, ShortcutKind.Application, page.Groups[3].Id);

            Raise(harness, topOfB, RawDragEventType.DragEnter, data);
            Raise(harness, topOfB, RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            Panel bar = Placeholder(view, b, "before");
            Assert.True(b.IsDropBefore);
            Assert.True(bar.IsVisible);
            Assert.True(At(harness, card, 0, 0).Y >= top + bar.Bounds.Height);

            // B has moved down out from under the pointer, which is now over the dashed bar: the gap stays.
            Raise(harness, topOfB, RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            Assert.True(b.IsDropBefore);
            Assert.All(page.Groups.Where(group => group != b), group => Assert.Equal(DropHint.None, group.DropHint));

            Raise(harness, topOfB, RawDragEventType.Drop, data);

            Assert.Equal(["A", "D", "B", "C"], page.Groups.Select(group => group.Name));
            Assert.False(bar.IsVisible);
        });

    [Fact]
    public void AGroupDraggedNextToItsOwnPlace_OpensNothing_AndIsNotRefused()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", []), ("B", []), ("C", [])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel b = page.Groups[1];
            Control card = Card(view, b);
            DataTransfer data = Carrying(WorkspacePageView.GroupFormat, ShortcutKind.Application, b.Id);

            foreach (double y in new[] { 4, card.Bounds.Height - 4 })
            {
                Point point = new(card.Bounds.Width / 2, y);

                Assert.True(view.Resolve(data, card, visual => card.TranslatePoint(point, visual) ?? default, drop: false));
                Assert.All(page.Groups, group => Assert.Equal(DropHint.None, group.DropHint));
            }

            Assert.True(view.Resolve(data, card, visual => card.TranslatePoint(new Point(10, 4), visual) ?? default, drop: true));
            Assert.Equal(["A", "B", "C"], page.Groups.Select(group => group.Name));
        });

    [Fact]
    public void ATileDragged_OpensADashedTileThatMakesRoom()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B", "C"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ShortcutViewModel b = page.Groups[0].Shortcuts[1];
            Control tileA = Tile(view, page.Groups[0].Shortcuts[0]);
            Control tileB = Tile(view, b);
            Point leftOfB = At(harness, tileB, 0, 0);
            Point rightHalfOfA = At(harness, tileA, tileA.Bounds.Width - 10, tileA.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, page.Groups[0].Shortcuts[2].Id);

            Raise(harness, rightHalfOfA, RawDragEventType.DragEnter, data);
            Raise(harness, rightHalfOfA, RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            // Within a line, the gap between A and B is B's: the dashed tile opens in front of B, which moves on.
            Panel dashed = Placeholder(view, b, "before");
            Assert.True(b.IsDropBefore);
            Assert.True(dashed.IsVisible);
            Assert.Equal(tileA.Bounds.Width, dashed.Bounds.Width);
            Assert.NotEqual(leftOfB, At(harness, tileB, 0, 0));

            Raise(harness, rightHalfOfA, RawDragEventType.Drop, data);

            Assert.Equal(["A", "C", "B"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
        });

    [Fact]
    public void ADashedTileOpenedAtTheEndOfALine_StaysThereWhenItsTileWrapsAway()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B", "C", "D", "E", "F", "G", "H"]), ("Tools", ["X"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ShortcutViewModel[] dev = [.. page.Groups[0].Shortcuts];
            double firstLine = At(harness, Tile(view, dev[0]), 0, 0).Y;
            ShortcutViewModel lineEnd = dev.TakeWhile(shortcut => At(harness, Tile(view, shortcut), 0, 0).Y == firstLine).Last();
            Assert.NotEqual(dev[^1], lineEnd);
            Control tile = Tile(view, lineEnd);
            Point rightHalf = At(harness, tile, tile.Bounds.Width - 10, tile.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, page.Groups[1].Shortcuts[0].Id);

            Raise(harness, rightHalf, RawDragEventType.DragEnter, data);
            Raise(harness, rightHalf, RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            Assert.True(lineEnd.IsDropAfter);
            Assert.True(At(harness, tile, 0, 0).Y > firstLine);

            // The tile and its dashed tile went down a line, leaving the pointer over the first line's empty end.
            Raise(harness, rightHalf, RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            Assert.True(lineEnd.IsDropAfter);
            Assert.Single(page.Groups.SelectMany(group => group.Shortcuts), shortcut => shortcut.DropHint != DropHint.None);
            Assert.Equal(DropHint.None, page.Groups[0].DropHint);
        });

    [Fact]
    public void AGroupShowingNoTile_ReceivingOne_IsFramedInDashes()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A"]), ("Tools", [])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel tools = page.Groups[1];
            Control card = Card(view, tools);
            Point point = At(harness, card, card.Bounds.Width / 2, card.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, page.Groups[0].Shortcuts[0].Id);

            Raise(harness, point, RawDragEventType.DragEnter, data);
            Raise(harness, point, RawDragEventType.DragOver, data);

            Assert.True(tools.IsDropInside);
            Assert.True(view.GetVisualDescendants().OfType<Rectangle>().Single(frame => frame.Classes.Contains("dropFrame") && frame.DataContext == tools).IsVisible);

            Raise(harness, point, RawDragEventType.Drop, data);

            Assert.Equal(["A"], tools.Shortcuts.Select(shortcut => shortcut.Name));
            Assert.False(tools.IsDropInside);
        });

    [Fact]
    public void ATileDraggedNextToItsOwnPlace_OpensNothing_AndIsNotRefused()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ShortcutViewModel a = page.Groups[0].Shortcuts[0];
            Control tileA = Tile(view, a);
            Control tileB = Tile(view, page.Groups[0].Shortcuts[1]);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, a.Id);

            foreach ((Control tile, double x) in new[] { (tileA, 10.0), (tileA, tileA.Bounds.Width - 10), (tileB, 10.0) })
            {
                Point point = new(x, tile.Bounds.Height / 2);

                Assert.True(view.Resolve(data, tile, visual => tile.TranslatePoint(point, visual) ?? default, drop: false));
                Assert.All(page.Groups[0].Shortcuts, shortcut => Assert.Equal(DropHint.None, shortcut.DropHint));
                Assert.Equal(DropHint.None, page.Groups[0].DropHint);
            }
        });

    [Fact]
    public void ADraggedTile_IsCarriedUnderThePointer_InBothDirections_NeitherFadedNorOpened()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"]), ("Tools", ["X"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ShortcutViewModel a = page.Groups[0].Shortcuts[0];
            a.IsDragging = true;
            Canvas layer = Layer(view);
            ContentControl copy = Copy(view, "TileGhost");
            Point middle = new(layer.Bounds.Width / 2, layer.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, a.Id);

            Raise(harness, At(harness, layer, middle.X, middle.Y), RawDragEventType.DragEnter, data);
            Raise(harness, At(harness, layer, middle.X, middle.Y), RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            // A drag the page did not start holds the copy by its middle.
            Assert.True(copy.IsVisible);
            Assert.Same(a, copy.Content);
            Assert.Equal(middle.X - (copy.Bounds.Width / 2), Canvas.GetLeft(copy), 3);
            Assert.Equal(middle.Y - (copy.Bounds.Height / 2), Canvas.GetTop(copy), 3);
            Assert.Equal(Tile(view, page.Groups[0].Shortcuts[1]).Bounds.Width, copy.Bounds.Width);
            Assert.False(Copy(view, "GroupGhost").IsVisible);

            Button tile = copy.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("tile"));
            Button original = view.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Classes.Contains("tile") && (button.DataContext == a) && !copy.IsVisualAncestorOf(button));
            Assert.Equal(1, tile.Opacity);
            Assert.Equal(0.45, original.Opacity, 3);
            Assert.All(copy.GetVisualDescendants().OfType<Panel>().Where(panel => panel.Classes.Contains("placeholder")), panel => Assert.False(panel.IsVisible));

            double left = Canvas.GetLeft(copy);
            double top = Canvas.GetTop(copy);

            Raise(harness, At(harness, layer, middle.X + 37, middle.Y + 23), RawDragEventType.DragOver, data);

            Assert.Equal(left + 37, Canvas.GetLeft(copy), 3);
            Assert.Equal(top + 23, Canvas.GetTop(copy), 3);
        });

    [Fact]
    public void ADraggedTilesCopy_StaysInsideTheLayer()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            Canvas layer = Layer(view);
            ContentControl copy = Copy(view, "TileGhost");
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, page.Groups[0].Shortcuts[0].Id);

            Raise(harness, At(harness, layer, 2, 2), RawDragEventType.DragEnter, data);
            Raise(harness, At(harness, layer, 2, 2), RawDragEventType.DragOver, data);

            Assert.Equal(0, Canvas.GetLeft(copy));
            Assert.Equal(0, Canvas.GetTop(copy));

            Raise(harness, At(harness, layer, layer.Bounds.Width - 2, layer.Bounds.Height - 2), RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            Assert.Equal(layer.Bounds.Width - copy.Bounds.Width, Canvas.GetLeft(copy), 3);
            Assert.Equal(layer.Bounds.Height - copy.Bounds.Height, Canvas.GetTop(copy), 3);
        });

    [Fact]
    public void ADraggedGroup_IsCarriedAsItsHeader_AlignedWithTheCards_UpAndDownOnly()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", ["One", "Two"]), ("B", []), ("C", ["Three"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel c = page.Groups[2];
            Control card = Card(view, page.Groups[0]);
            Canvas layer = Layer(view);
            ContentControl copy = Copy(view, "GroupGhost");
            Point point = At(harness, card, card.Bounds.Width / 2, card.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.GroupFormat, ShortcutKind.Application, c.Id);

            Raise(harness, point, RawDragEventType.DragEnter, data);
            Raise(harness, point, RawDragEventType.DragOver, data);
            harness.Window.UpdateLayout();

            Assert.True(copy.IsVisible);
            Assert.Same(c, copy.Content);
            Assert.False(Copy(view, "TileGhost").IsVisible);
            Assert.Equal(card.TranslatePoint(default, layer)!.Value.X, Canvas.GetLeft(copy), 3);
            Assert.Equal(card.Bounds.Width, copy.Bounds.Width, 3);
            Assert.True(copy.Bounds.Height < card.Bounds.Height);
            Assert.Contains(copy.GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("groupName") && text.Text == "C");
            Assert.DoesNotContain(copy.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("tile"));

            double left = Canvas.GetLeft(copy);
            double top = Canvas.GetTop(copy);

            Raise(harness, new Point(point.X + 50, point.Y + 30), RawDragEventType.DragOver, data);

            Assert.Equal(left, Canvas.GetLeft(copy));
            Assert.Equal(top + 30, Canvas.GetTop(copy), 3);
        });

    [Fact]
    public void TheCopy_HidesWhileTheDragIsAway_AndIsGoneAfterTheDrop()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("Dev", ["A", "B"])));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            Control tileB = Tile(view, page.Groups[0].Shortcuts[1]);
            ContentControl copy = Copy(view, "TileGhost");
            Point point = At(harness, tileB, tileB.Bounds.Width - 10, tileB.Bounds.Height / 2);
            DataTransfer data = Carrying(WorkspacePageView.ShortcutFormat, ShortcutKind.Application, page.Groups[0].Shortcuts[0].Id);

            Raise(harness, point, RawDragEventType.DragEnter, data);
            Raise(harness, point, RawDragEventType.DragOver, data);
            Assert.True(copy.IsVisible);

            Raise(harness, point, RawDragEventType.DragLeave, data);
            Assert.False(copy.IsVisible);

            Raise(harness, point, RawDragEventType.DragEnter, data);
            Raise(harness, point, RawDragEventType.DragOver, data);
            Assert.True(copy.IsVisible);

            Raise(harness, point, RawDragEventType.Drop, data);

            Assert.False(copy.IsVisible);
            Assert.Null(copy.Content);
            Assert.Equal(["B", "A"], page.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
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

                Assert.False(Copy(view, "TileGhost").IsVisible);
                Assert.False(Copy(view, "GroupGhost").IsVisible);

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

    private static Canvas Layer(WorkspacePageView view)
        => view.FindControl<Canvas>("DragLayer") ?? throw new InvalidOperationException("No drag layer.");

    private static ContentControl Copy(WorkspacePageView view, string name)
        => view.FindControl<ContentControl>(name) ?? throw new InvalidOperationException($"No {name}.");

    private static Control Card(WorkspacePageView view, GroupViewModel group)
        => view.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("groupCard") && border.DataContext == group);

    private static Panel Placeholder(WorkspacePageView view, object item, string side)
        => view.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Classes.Contains("placeholder") && panel.Classes.Contains(side) && panel.DataContext == item);

    private static void Raise(WindowHarness harness, Point point, RawDragEventType type, DataTransfer data)
        => harness.Window.DragDrop(point, type, data, DragDropEffects.Move, RawInputModifiers.None);

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
