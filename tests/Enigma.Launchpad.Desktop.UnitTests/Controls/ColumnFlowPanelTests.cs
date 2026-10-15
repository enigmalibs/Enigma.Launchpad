using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Launchpad.Desktop.Controls;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests.Controls;

/// <summary>
/// The panel measured and arranged on its own: where its children go, and the gap a child opens.
/// </summary>
[Collection(HeadlessCollection.Name)]
public sealed class ColumnFlowPanelTests
{
    // 924 wide in three columns 12 apart: each column is 300 wide, at 0, 312 and 624.
    private const double Width = 924;

    private readonly HeadlessAvaloniaFixture _fixture;

    public ColumnFlowPanelTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void Children_AreLaidOutInColumns_AtTheirSpans()
        => _fixture.Run(() =>
        {
            ColumnFlowPanel panel = Panel((1, 50), (2, 80), (3, 40));

            Layout(panel);

            Assert.Equal([new Rect(0, 0, 300, 50), new Rect(312, 0, 612, 80), new Rect(0, 92, 924, 40)], panel.Children.Select(child => child.Bounds));
            Assert.Equal(132, panel.DesiredSize.Height);
            Assert.Null(panel.GapBounds);
        });

    [Fact]
    public void AGapBeforeAChild_TakesItsColumnsInTheFlow_AndTheChildrenAfterItMakeRoom()
        => _fixture.Run(() =>
        {
            ColumnFlowPanel panel = Panel((1, 50), (1, 50), (1, 50));
            ColumnFlowPanel.SetGapBefore(panel.Children[1], 1);

            Layout(panel);

            Assert.Equal(new Rect(312, 0, 300, 48), panel.GapBounds);
            Assert.Equal([new Rect(0, 0, 300, 50), new Rect(624, 0, 300, 50), new Rect(0, 62, 300, 50)], panel.Children.Select(child => child.Bounds));
        });

    [Fact]
    public void AGapAfterAChild_ThatDoesNotFitTheLine_StartsTheNextOne()
        => _fixture.Run(() =>
        {
            ColumnFlowPanel panel = Panel((1, 50), (1, 50));
            ColumnFlowPanel.SetGapAfter(panel.Children[0], 3);

            Layout(panel);

            Assert.Equal(new Rect(0, 62, 924, 48), panel.GapBounds);
            Assert.Equal([new Rect(0, 0, 300, 50), new Rect(0, 122, 300, 50)], panel.Children.Select(child => child.Bounds));
        });

    [Fact]
    public void ClosingTheGap_PutsTheChildrenBack()
        => _fixture.Run(() =>
        {
            ColumnFlowPanel panel = Panel((1, 50), (1, 50));
            ColumnFlowPanel.SetGapBefore(panel.Children[0], 1);
            Layout(panel);

            ColumnFlowPanel.SetGapBefore(panel.Children[0], 0);
            Layout(panel);

            Assert.Null(panel.GapBounds);
            Assert.Equal([new Rect(0, 0, 300, 50), new Rect(312, 0, 300, 50)], panel.Children.Select(child => child.Bounds));
        });

    [Fact]
    public void Place_IsTheLayoutWithNoGapOpen()
        => _fixture.Run(() =>
        {
            ColumnFlowPanel panel = Panel((1, 50), (1, 70));
            ColumnFlowPanel.SetGapBefore(panel.Children[0], 2);
            Layout(panel);

            Assert.Equal(
                [new Rect(0, 0, 300, 70), new Rect(312, 0, 300, 70)],
                panel.Place(Width).Select(placement => placement.Cell));
        });

    [Fact]
    public void ATooNarrowPanel_GivesEveryChildAndTheGapTheWholeLine()
        => _fixture.Run(() =>
        {
            ColumnFlowPanel panel = Panel((1, 50), (2, 50));
            ColumnFlowPanel.SetGapAfter(panel.Children[0], 1);
            panel.MinColumnWidth = 400;

            Layout(panel);

            Assert.Equal(1, panel.ColumnsAt(Width));
            Assert.Equal(new Rect(0, 62, 924, 48), panel.GapBounds);
            Assert.Equal([new Rect(0, 0, 924, 50), new Rect(0, 122, 924, 50)], panel.Children.Select(child => child.Bounds));
        });

    private static ColumnFlowPanel Panel(params (int Span, double Height)[] children)
    {
        ColumnFlowPanel panel = new() { Columns = 3, ColumnSpacing = 12, RowSpacing = 12, GapHeight = 48, GapBrush = Brushes.Teal };

        foreach ((int span, double height) in children)
        {
            Border child = new() { Height = height };
            ColumnFlowPanel.SetSpan(child, span);
            panel.Children.Add(child);
        }

        return panel;
    }

    private static void Layout(ColumnFlowPanel panel)
    {
        panel.Measure(new Size(Width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, Width, panel.DesiredSize.Height));
    }
}
