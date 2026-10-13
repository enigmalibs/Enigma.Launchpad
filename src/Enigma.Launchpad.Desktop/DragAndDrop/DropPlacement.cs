using System;

namespace Enigma.Launchpad.Desktop.DragAndDrop;

/// <summary>
/// Which side of the item under the pointer a dragged item lands on.
/// </summary>
public enum DropPosition
{
    /// <summary>In front of the target: above a group, to the left of a tile.</summary>
    Before,

    /// <summary>Behind the target: below a group, to the right of a tile.</summary>
    After,
}

/// <summary>
/// What an item shows while something is dragged over it.
/// </summary>
public enum DropHint
{
    /// <summary>Nothing is dragged over it.</summary>
    None,

    /// <summary>The dragged item would land in front of it.</summary>
    Before,

    /// <summary>The dragged item would land behind it.</summary>
    After,

    /// <summary>The dragged item would land inside it, at the end (a group receiving a shortcut).</summary>
    Inside,
}

/// <summary>
/// The arithmetic of a drop: which side of the target, and where the dragged item ends up. Pure, so the
/// rules are tested without a pointer.
/// </summary>
public static class DropPlacement
{
    /// <summary>
    /// The side of a target laid out top to bottom (a group card): its upper half is before it.
    /// </summary>
    /// <param name="pointerY">The pointer's position from the target's top edge.</param>
    /// <param name="targetHeight">The target's height.</param>
    /// <returns>The side.</returns>
    public static DropPosition Vertical(double pointerY, double targetHeight) => pointerY < (targetHeight / 2) ? DropPosition.Before : DropPosition.After;

    /// <summary>
    /// The side of a target laid out left to right (a tile): its left half is before it.
    /// </summary>
    /// <param name="pointerX">The pointer's position from the target's left edge.</param>
    /// <param name="targetWidth">The target's width.</param>
    /// <returns>The side.</returns>
    public static DropPosition Horizontal(double pointerX, double targetWidth) => pointerX < (targetWidth / 2) ? DropPosition.Before : DropPosition.After;

    /// <summary>
    /// Where an item moved within one list ends up, as the index to pass to
    /// <see cref="System.Collections.ObjectModel.ObservableCollection{T}.Move"/>.
    /// </summary>
    /// <param name="sourceIndex">Where the dragged item is now.</param>
    /// <param name="targetIndex">Where the item it was dropped on is now.</param>
    /// <param name="position">Which side of that item it was dropped on.</param>
    /// <returns>The dragged item's new index; equal to <paramref name="sourceIndex"/> when nothing moves.</returns>
    public static int MoveIndex(int sourceIndex, int targetIndex, DropPosition position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(targetIndex);

        int insertion = position == DropPosition.Before ? targetIndex : targetIndex + 1;

        // Taking the item out first shifts everything after it one place back.
        return insertion > sourceIndex ? insertion - 1 : insertion;
    }

    /// <summary>
    /// Where an item dropped into another list goes, as the index to insert it at.
    /// </summary>
    /// <param name="targetIndex">Where the item it was dropped on is.</param>
    /// <param name="position">Which side of that item it was dropped on.</param>
    /// <returns>The insertion index.</returns>
    public static int InsertIndex(int targetIndex, DropPosition position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetIndex);
        return position == DropPosition.Before ? targetIndex : targetIndex + 1;
    }
}
