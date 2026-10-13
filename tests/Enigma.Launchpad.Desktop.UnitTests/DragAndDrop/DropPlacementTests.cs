using System;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests.DragAndDrop;

public sealed class DropPlacementTests
{
    [Theory]
    [InlineData(0, 100, DropPosition.Before)]
    [InlineData(49.9, 100, DropPosition.Before)]
    [InlineData(50, 100, DropPosition.After)]
    [InlineData(100, 100, DropPosition.After)]
    public void Vertical_SplitsTheTargetAtHalfItsHeight(double y, double height, DropPosition expected)
        => Assert.Equal(expected, DropPlacement.Vertical(y, height));

    [Theory]
    [InlineData(10, 248, DropPosition.Before)]
    [InlineData(200, 248, DropPosition.After)]
    public void Horizontal_SplitsTheTargetAtHalfItsWidth(double x, double width, DropPosition expected)
        => Assert.Equal(expected, DropPlacement.Horizontal(x, width));

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
