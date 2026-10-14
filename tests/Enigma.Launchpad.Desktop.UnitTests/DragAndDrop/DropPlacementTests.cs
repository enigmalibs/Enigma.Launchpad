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
    public void Stack_LaysTheRowsOneAfterTheOther()
    {
        Assert.Equal(new (double Top, double Height)[] { (10, 30), (40, 50), (90, 20) }, DropPlacement.Stack(10, [30, 50, 20]));
        Assert.Empty(DropPlacement.Stack(0, []));
    }

    // Three rows of 40 from 0: their middles are at 20, 60 and 100.
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(19.9, 0)]
    [InlineData(20, 1)]
    [InlineData(59, 1)]
    [InlineData(60, 2)]
    [InlineData(100, 3)]
    [InlineData(500, 3)]
    public void SlotFor_IsTheGapBeforeTheFirstRowWhoseMiddleIsBelowThePointer(double y, int expected)
        => Assert.Equal(expected, DropPlacement.SlotFor(y, DropPlacement.Stack(0, [40, 40, 40])));

    [Fact]
    public void SlotFor_WithoutRows_IsTheOnlyGap()
        => Assert.Equal(0, DropPlacement.SlotFor(10, []));

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

    // A copy 60 high, held 20 below its top, on a layer 300 high.
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
