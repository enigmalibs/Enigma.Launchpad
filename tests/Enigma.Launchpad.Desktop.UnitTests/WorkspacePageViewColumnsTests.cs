using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Controls;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The groups of an open workspace laid out in lines of three columns, on the real page.
/// </summary>
[Collection(HeadlessCollection.Name)]
public sealed class WorkspacePageViewColumnsTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public WorkspacePageViewColumnsTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void GroupsWhoseWidthsFit_ShareALine_SideBySide_AndAFullWidthGroupTakesTheNext()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", GroupWidth.OneThird), ("B", GroupWidth.OneThird), ("C", GroupWidth.OneThird), ("D", GroupWidth.Full)));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ColumnFlowPanel panel = Panel(view);
            Assert.Equal(3, panel.ColumnsAt(panel.Bounds.Width));
            Rect[] cards = [.. page.Groups.Select(group => CardIn(view, group, panel))];
            double third = (panel.Bounds.Width - 24) / 3;

            Assert.All(cards[..3], card => Assert.Equal(cards[0].Top, card.Top));
            Assert.Equal([0, third + 12, (2 * third) + 24], cards[..3].Select(card => card.Left), new Tolerance());
            Assert.All(cards[..3], card => Assert.Equal(third, card.Width, 3));
            Assert.Equal(0, cards[3].Left, 3);
            Assert.Equal(panel.Bounds.Width, cards[3].Width, 3);
            Assert.True(cards[3].Top >= cards[..3].Max(card => card.Bottom) + 12 - 0.5);
        });

    [Fact]
    public void TwoThirdsAndOneThird_ShareALine_AndCardsOfALineAreTopAligned_WithTheirOwnHeights()
        => _fixture.RunAsync(async () =>
        {
            LaunchpadData data = PageHarness.Data().With(
                ShortcutKind.Application,
                [
                    new ShortcutGroup(
                        Guid.NewGuid(),
                        "Wide",
                        ShortcutKind.Application,
                        [PageHarness.Shortcut("One"), PageHarness.Shortcut("Two"), PageHarness.Shortcut("Three"), PageHarness.Shortcut("Four")],
                        Width: GroupWidth.TwoThirds),
                    new ShortcutGroup(Guid.NewGuid(), "Narrow", ShortcutKind.Application, [], Width: GroupWidth.OneThird),
                ]);
            using WindowHarness harness = new(data);
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ColumnFlowPanel panel = Panel(view);
            Rect wide = CardIn(view, page.Groups[0], panel);
            Rect narrow = CardIn(view, page.Groups[1], panel);

            Assert.Equal(wide.Top, narrow.Top);
            Assert.Equal(panel.Bounds.Width, narrow.Right, 3);
            Assert.Equal((narrow.Width * 2) + 12, wide.Width, 3);
            Assert.True(wide.Height > narrow.Height);
        });

    [Fact]
    public void APageTooNarrowForATileInAThird_ShowsEveryGroupAtFullWidth()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", GroupWidth.OneThird), ("B", GroupWidth.TwoThirds)));
            harness.Window.Width = 760;
            WorkspacePageView view = await harness.LoadedPageAsync();
            harness.Window.UpdateLayout();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            ColumnFlowPanel panel = Panel(view);

            Assert.Equal(1, panel.ColumnsAt(panel.Bounds.Width));
            Rect a = CardIn(view, page.Groups[0], panel);
            Rect b = CardIn(view, page.Groups[1], panel);
            Assert.Equal((0, panel.Bounds.Width), (a.Left, a.Width));
            Assert.Equal((0, panel.Bounds.Width), (b.Left, b.Width));
            Assert.True(b.Top > a.Bottom);
            Assert.Equal(GroupWidth.OneThird, page.Groups[0].Width);
        });

    [Fact]
    public void TheWidthMenu_ChecksTheCurrentWidth_AndAChoiceRelaysThePageOut_AndIsSaved()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data(("A", GroupWidth.Full), ("B", GroupWidth.OneThird)));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            GroupViewModel a = page.Groups[0];
            ColumnFlowPanel panel = Panel(view);
            Button actions = view.GetVisualDescendants().OfType<Button>()
                .First(button => (AutomationProperties.GetName(button) == "Group actions") && (button.DataContext == a));
            MenuFlyout menu = Assert.IsType<MenuFlyout>(actions.Flyout);

            string[] entries = [.. menu.Items.Select(item => item is MenuItem entry ? entry.Header?.ToString() ?? string.Empty : "—")];
            Assert.Equal(["Rename…", "Width", "Move earlier", "Move later", "Move to workspace…", "—", "Delete…"], entries);

            menu.ShowAt(actions);
            await Task.Delay(20);
            MenuItem[] widths = [.. menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Width").Items.OfType<MenuItem>()];
            Assert.Equal(["Full width", "Two thirds", "One third"], widths.Select(item => item.Header?.ToString()));
            Assert.All(widths, item => Assert.Equal(MenuItemToggleType.Radio, item.ToggleType));
            Assert.Equal([true, false, false], widths.Select(item => item.IsChecked));

            widths[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            harness.Window.UpdateLayout();

            Assert.Equal(GroupWidth.TwoThirds, a.Width);
            Assert.Equal([false, true, false], widths.Select(item => item.IsChecked));
            Rect cardA = CardIn(view, a, panel);
            Rect cardB = CardIn(view, page.Groups[1], panel);
            Assert.Equal(cardA.Top, cardB.Top);
            Assert.True(cardB.Left > cardA.Right);

            widths[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            harness.Window.UpdateLayout();

            Assert.Equal([true, false, false], widths.Select(item => item.IsChecked));
            Assert.True(CardIn(view, page.Groups[1], panel).Top > CardIn(view, a, panel).Bottom);
            menu.Hide();

            ILaunchpadSession session = harness.Provider.GetRequiredService<ILaunchpadSession>();
            await session.FlushAsync();
            Assert.Equal([GroupWidth.Full, GroupWidth.OneThird], session.GroupsOf(ShortcutKind.Application).Select(group => group.Width));
        });

    [Fact]
    public void ALongNameInANarrowCard_IsTrimmed_AndItsCountStaysInsideTheCard()
        => _fixture.RunAsync(async () =>
        {
            const string name = "A group name far too long to fit in one third of the line, whatever the window";
            using WindowHarness harness = new(Data((name, GroupWidth.OneThird), ("Short", GroupWidth.OneThird)));
            WorkspacePageView view = await harness.LoadedPageAsync();
            WorkspacePageViewModel page = Assert.IsAssignableFrom<WorkspacePageViewModel>(view.DataContext);
            Border card = Card(view, page.Groups[0]);
            TextBlock title = card.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("groupName"));
            Border badge = card.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("countBadge"));
            Button add = card.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Add a shortcut");

            Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
            Assert.True(title.Bounds.Width < title.DesiredSize.Width + 1);
            Assert.True(Right(badge, card) <= Left(add, card));

            // A short name keeps its count right after it.
            Border shortCard = Card(view, page.Groups[1]);
            TextBlock shortTitle = shortCard.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("groupName"));
            Border shortBadge = shortCard.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("countBadge"));
            Assert.Equal(Right(shortTitle, shortCard) + 10, Left(shortBadge, shortCard), 1);
        });

    private static ColumnFlowPanel Panel(WorkspacePageView view)
        => view.GetVisualDescendants().OfType<ColumnFlowPanel>().Single();

    private static Border Card(WorkspacePageView view, GroupViewModel group)
        => view.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("groupCard") && border.DataContext == group);

    private static Rect CardIn(WorkspacePageView view, GroupViewModel group, Visual panel)
    {
        Border card = Card(view, group);
        Point topLeft = card.TranslatePoint(default, panel) ?? throw new InvalidOperationException("Not on the panel.");
        return new Rect(topLeft, card.Bounds.Size);
    }

    private static double Left(Visual visual, Visual relativeTo)
        => (visual.TranslatePoint(default, relativeTo) ?? throw new InvalidOperationException("Not in the card.")).X;

    private static double Right(Visual visual, Visual relativeTo) => Left(visual, relativeTo) + visual.Bounds.Width;

    private static LaunchpadData Data(params (string Name, GroupWidth Width)[] groups)
        => PageHarness.Data().With(
            ShortcutKind.Application,
            groups.Select(group => new ShortcutGroup(Guid.NewGuid(), group.Name, ShortcutKind.Application, [], Width: group.Width)).ToArray());

    /// <summary>
    /// Compares positions to within layout rounding.
    /// </summary>
    private sealed class Tolerance : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 0.5;

        public int GetHashCode(double obj) => 0;
    }
}
