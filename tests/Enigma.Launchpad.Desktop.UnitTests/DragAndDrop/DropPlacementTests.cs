using System;
using Avalonia;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests.DragAndDrop;

public sealed class DropPlacementTests
{
    // Over A B C D: where the dragged item ends up, as ObservableCollection.Move's new index.
    [Theory]
    [InlineData(0, 2, DropPosition.Before, 1)] // A before C  -> B A C D
    [InlineData(0, 2, DropPosition.After, 2)]  // A after C   -> B C A D
    [InlineData(3, 1, DropPosition.Before, 1)] // D before B  -> A D B C
    [InlineData(3, 0, DropPosition.Before, 0)] // D before A  -> D A B C
    [InlineData(0, 3, DropPosition.After, 3)]  // A after D   -> B C D A
    [InlineData(1, 0, DropPosition.After, 1)]  // B after A   -> unchanged
    [InlineData(1, 2, DropPosition.Before, 1)] // B before C  -> unchanged
    public void MoveIndex_AccountsForTheItemLeavingItsPlace(int source, int target, DropPosition position, int expected)
        => Assert.Equal(expected, DropPlacement.MoveIndex(source, target, position));

    [Theory]
    [InlineData(0, DropPosition.Before, 0)]
    [InlineData(0, DropPosition.After, 1)]
    [InlineData(2, DropPosition.After, 3)]
    public void InsertIndex_IsTheTargetsSlotOrTheNextOne(int target, DropPosition position, int expected)
        => Assert.Equal(expected, DropPlacement.InsertIndex(target, position));

    [Fact]
    public void Indexes_RefuseNegativePositions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DropPlacement.MoveIndex(-1, 0, DropPosition.Before));
        Assert.Throws<ArgumentOutOfRangeException>(() => DropPlacement.InsertIndex(-1, DropPosition.Before));
    }

    [Fact]
    public void Flow_StartsANewLineWithTheItemThatDoesNotFit()
        => Assert.Equal(
            [new Rect(0, 0, 256, 70), new Rect(256, 0, 256, 70), new Rect(0, 70, 256, 70), new Rect(256, 70, 256, 70), new Rect(0, 140, 256, 70)],
            DropPlacement.Flow(600, [Tile, Tile, Tile, Tile, Tile]));

    [Fact]
    public void Flow_KeepsAnItemThatFillsTheLineExactly()
        => Assert.Equal(
            [new Rect(0, 0, 256, 70), new Rect(256, 0, 256, 70), new Rect(0, 70, 256, 70)],
            DropPlacement.Flow(512, [Tile, Tile, Tile]));

    [Fact]
    public void Flow_GivesEveryItemItsLinesHeight()
        => Assert.Equal(
            [new Rect(0, 0, 256, 90), new Rect(256, 0, 256, 90), new Rect(0, 90, 256, 70)],
            DropPlacement.Flow(600, [Tile, new Size(256, 90), Tile]));

    [Fact]
    public void Flow_PutsAnItemWiderThanTheWidthOnALineOfItsOwn()
        => Assert.Equal(
            [new Rect(0, 0, 100, 50), new Rect(0, 50, 256, 70), new Rect(0, 120, 100, 50), new Rect(100, 120, 80, 50)],
            DropPlacement.Flow(200, [new Size(100, 50), Tile, new Size(100, 50), new Size(80, 40)]));

    [Fact]
    public void Flow_OfNothing_IsEmpty()
        => Assert.Empty(DropPlacement.Flow(600, []));

    // Over the lines A B / C D / E, each item 256 by 70: middles at 128 and 384.
    [Theory]
    [InlineData(10, 10, 0, DropPosition.Before)]
    [InlineData(200, 10, 1, DropPosition.Before)]
    [InlineData(500, 10, 1, DropPosition.After)]
    [InlineData(580, 69, 1, DropPosition.After)]
    [InlineData(10, 100, 2, DropPosition.Before)]
    [InlineData(300, 100, 3, DropPosition.Before)]
    [InlineData(500, 139, 3, DropPosition.After)]
    [InlineData(10, -40, 0, DropPosition.Before)]
    [InlineData(10, 400, 4, DropPosition.Before)]
    [InlineData(200, 400, 4, DropPosition.After)]
    public void FlowSlot_FindsTheGapOnTheLineUnderThePointer(double x, double y, int index, DropPosition position)
        => Assert.Equal((index, position), DropPlacement.FlowSlot(new Point(x, y), DropPlacement.Flow(600, [Tile, Tile, Tile, Tile, Tile])));

    [Fact]
    public void FlowSlot_RefusesNoItems()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DropPlacement.FlowSlot(default, []));

    // Cells in lines 924 wide: A and B, a third each, at 0 and 312; C alone over the whole line; D alone in a third.
    private static readonly Rect[] Cells =
    [
        new Rect(0, 0, 300, 62),
        new Rect(312, 0, 300, 62),
        new Rect(0, 62, 924, 62),
        new Rect(0, 124, 300, 62),
    ];

    [Theory]
    [InlineData(10, 10, 0, DropPosition.Before)]   // A's left half
    [InlineData(200, 10, 1, DropPosition.Before)]  // A's right half: the gap between A and B
    [InlineData(700, 10, 1, DropPosition.After)]   // the line's empty end
    [InlineData(10, -50, 0, DropPosition.Before)]  // above every line
    [InlineData(900, 70, 2, DropPosition.Before)]  // C's upper half, whatever the side
    [InlineData(10, 100, 2, DropPosition.After)]   // C's lower half
    [InlineData(10, 130, 3, DropPosition.Before)]  // D, alone but a third: its sides
    [InlineData(500, 130, 3, DropPosition.After)]
    [InlineData(500, 900, 3, DropPosition.After)]  // below every line
    public void ColumnSlot_GoesAcrossALine_ButAboveOrBelowACardTakingTheWholeLine(double x, double y, int index, DropPosition position)
        => Assert.Equal((index, position), DropPlacement.ColumnSlot(new Point(x, y), Cells, 924));

    [Fact]
    public void ColumnSlot_InOneColumn_IsAboveOrBelowEveryCard()
    {
        Rect[] column = [new Rect(0, 0, 500, 60), new Rect(0, 60, 500, 60)];

        Assert.Equal((0, DropPosition.Before), DropPlacement.ColumnSlot(new Point(490, 10), column, 500));
        Assert.Equal((0, DropPosition.After), DropPlacement.ColumnSlot(new Point(10, 50), column, 500));
        Assert.Equal((1, DropPosition.Before), DropPlacement.ColumnSlot(new Point(10, 61), column, 500));
    }

    [Fact]
    public void ColumnSlot_RefusesNoCells()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DropPlacement.ColumnSlot(default, [], 924));

    // A copy 60 high, held 20 below its top, on a layer 300 high.
    [Theory]
    [InlineData(100, 20, 60, 300, 80)]
    [InlineData(10, 20, 60, 300, 0)]
    [InlineData(290, 20, 60, 300, 240)]
    [InlineData(50, 20, 400, 300, 0)]
    public void GhostOffset_HoldsTheCopyWhereItIsGrabbed_InsideTheLayer(double pointer, double grab, double size, double layer, double expected)
        => Assert.Equal(expected, DropPlacement.GhostOffset(pointer, grab, size, layer));

    private static Size Tile => new(256, 70);
}

public sealed class DragPayloadTests
{
    [Theory]
    [InlineData(ShortcutKind.Application)]
    [InlineData(ShortcutKind.Directory)]
    [InlineData(ShortcutKind.Url)]
    public void Format_RoundTrips(ShortcutKind kind)
    {
        DragPayload payload = new(kind, Guid.NewGuid());

        Assert.True(DragPayload.TryParse(payload.Format(), out DragPayload? parsed));
        Assert.Equal(payload, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("0:not-a-guid")]
    [InlineData("7:8c0e0f6e55b74c4b9a430f0c8a1b2c3d")]
    [InlineData("-1:8c0e0f6e55b74c4b9a430f0c8a1b2c3d")]
    [InlineData(":8c0e0f6e55b74c4b9a430f0c8a1b2c3d")]
    public void TryParse_RefusesAnythingElse(string? text)
        => Assert.False(DragPayload.TryParse(text, out _));
}
