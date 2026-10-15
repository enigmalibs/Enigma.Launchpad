using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Enigma.Launchpad.Desktop.Controls;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests.Controls;

public sealed class ColumnFlowTests
{
    // 924 wide in three columns 12 apart: each column is 300 wide, at 0, 312 and 624.
    private const double Width = 924;
    private const double Spacing = 12;

    [Fact]
    public void ThreeOneThirdItems_ShareALine_OneColumnEach()
        => Assert.Equal(
            [new Rect(0, 0, 300, 50), new Rect(312, 0, 300, 80), new Rect(624, 0, 300, 40)],
            Bounds((1, 50), (1, 80), (1, 40)));

    [Fact]
    public void TwoThirdsThenOneThird_ShareALine()
        => Assert.Equal([new Rect(0, 0, 612, 50), new Rect(624, 0, 300, 60)], Bounds((2, 50), (1, 60)));

    [Fact]
    public void AnItemThatDoesNotFitWhatIsLeftOfTheLine_StartsTheNextOne()
        => Assert.Equal(
            [new Rect(0, 0, 300, 50), new Rect(312, 0, 300, 70), new Rect(0, 82, 612, 40), new Rect(624, 82, 300, 30)],
            Bounds((1, 50), (1, 70), (2, 40), (1, 30)));

    [Fact]
    public void AFullWidthItem_IsAloneOnItsLine()
        => Assert.Equal(
            [new Rect(0, 0, 300, 50), new Rect(0, 62, 924, 90), new Rect(0, 164, 300, 20)],
            Bounds((1, 50), (3, 90), (1, 20)));

    [Fact]
    public void EveryItem_SitsAtTheTopOfItsLine_WhoseCellIsAsHighAsItsHighestItem()
    {
        IReadOnlyList<ColumnFlowPlacement> placements = ColumnFlow.Layout(Width, [(1, 50), (2, 120), (3, 10)], 3, Spacing, Spacing);

        Assert.Equal([50.0, 120, 10], placements.Select(placement => placement.Bounds.Height));
        Assert.Equal([new Rect(0, 0, 300, 120), new Rect(312, 0, 612, 120), new Rect(0, 132, 924, 10)], placements.Select(placement => placement.Cell));
        Assert.Equal(142, ColumnFlow.Height(placements));
    }

    [Fact]
    public void Spans_AreClampedToTheLine()
        => Assert.Equal([new Rect(0, 0, 924, 50), new Rect(0, 62, 300, 20)], Bounds((int.MaxValue, 50), (0, 20)));

    [Theory]
    [InlineData(924, 276, 3)]
    [InlineData(852, 276, 3)]  // exactly 276 a column
    [InlineData(851, 276, 1)]
    [InlineData(100, 0, 3)]
    [InlineData(0, 276, 1)]
    public void ColumnsFor_FallsToOneColumn_WhenAColumnWouldBeTooNarrow(double width, double minColumnWidth, int expected)
        => Assert.Equal(expected, ColumnFlow.ColumnsFor(width, 3, Spacing, minColumnWidth));

    [Fact]
    public void InOneColumn_EveryItemTakesTheWholeWidth()
        => Assert.Equal(
            [new Rect(0, 0, 600, 50), new Rect(0, 62, 600, 30)],
            ColumnFlow.Layout(600, [(1, 50), (2, 30)], 1, Spacing, Spacing).Select(placement => placement.Bounds));

    [Theory]
    [InlineData(1, 300)]
    [InlineData(2, 612)]
    [InlineData(3, 924)]
    [InlineData(7, 924)]
    public void SpanWidth_IsItsColumnsAndTheSpacingBetweenThem(int span, double expected)
        => Assert.Equal(expected, ColumnFlow.SpanWidth(Width, 3, Spacing, span));

    [Fact]
    public void SpanWidth_IsNeverNegative()
        => Assert.Equal(0, ColumnFlow.SpanWidth(10, 3, Spacing, 1));

    [Fact]
    public void NoItems_LayOutToNothing()
    {
        Assert.Empty(ColumnFlow.Layout(Width, [], 3, Spacing, Spacing));
        Assert.Equal(0, ColumnFlow.Height([]));
    }

    [Fact]
    public void ALineWithoutColumns_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnFlow.ColumnsFor(Width, 0, Spacing, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnFlow.Layout(Width, [], 0, Spacing, Spacing));
    }

    private static Rect[] Bounds(params (int Span, double Height)[] items)
        => [.. ColumnFlow.Layout(Width, items, 3, Spacing, Spacing).Select(placement => placement.Bounds)];
}
