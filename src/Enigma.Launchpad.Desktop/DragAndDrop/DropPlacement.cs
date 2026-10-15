using System;
using System.Collections.Generic;
using Avalonia;

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
    /// The gap between 1 and the next double: what Avalonia's layout treats as rounding when it compares sizes.
    /// </summary>
    private const double MachineEpsilon = 2.220446049250313E-16;

    /// <summary>
    /// How far apart two layout positions may be and still be the same: well under a pixel.
    /// </summary>
    private const double LayoutRounding = 0.5;

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

    /// <summary>
    /// Places the copy of a dragged item along one axis: where the pointer holds it, kept inside the layer it is drawn
    /// on.
    /// </summary>
    /// <param name="pointer">Where the pointer is, in the layer's coordinates.</param>
    /// <param name="grab">How far into the item it is held.</param>
    /// <param name="size">The copy's extent along the axis.</param>
    /// <param name="layer">The layer's extent along the axis.</param>
    /// <returns>The copy's offset, in the layer's coordinates.</returns>
    public static double GhostOffset(double pointer, double grab, double size, double layer)
        => Math.Clamp(pointer - grab, 0, Math.Max(0, layer - size));

    /// <summary>
    /// Where a horizontal wrap panel puts items of these sizes in a width, as they stand with no gap opened among them:
    /// left to right, an item that does not fit after another starting a new line.
    /// </summary>
    /// <param name="width">The width the panel lays its lines out in.</param>
    /// <param name="sizes">Each item's size, margins included, in order.</param>
    /// <returns>Each item's rectangle in the panel, in order; its height is its line's.</returns>
    /// <remarks>
    /// The slot is found in this geometry rather than in the items as drawn: the dashed placeholder that marks the slot
    /// moves the items after it, and reading their new places would move the slot, which would move the placeholder,
    /// and so on. Worse, a dashed tile opened after the last tile of a line takes that tile to the next line with it,
    /// away from the pointer that put it there. The line break follows <see cref="Avalonia.Controls.WrapPanel"/>'s own
    /// test.
    /// </remarks>
    public static IReadOnlyList<Rect> Flow(double width, IReadOnlyList<Size> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);

        List<Rect> items = new(sizes.Count);
        int lineStart = 0;
        double x = 0;
        double top = 0;
        double lineHeight = 0;

        for (int index = 0; index < sizes.Count; index++)
        {
            Size size = sizes[index];

            if ((index > lineStart) && Overflows(x + size.Width, width))
            {
                EndLine(items, lineStart, lineHeight);
                lineStart = index;
                top += lineHeight;
                x = 0;
                lineHeight = 0;
            }

            items.Add(new Rect(x, top, size.Width, size.Height));
            x += size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        EndLine(items, lineStart, lineHeight);
        return items;

        // A line is as high as its highest item, and so is every item of it: the panel arranges them that way.
        static void EndLine(List<Rect> items, int start, double height)
        {
            for (int index = start; index < items.Count; index++)
            {
                items[index] = items[index].WithHeight(height);
            }
        }

        // The panel's own test, Avalonia's internal MathUtilities.GreaterThan: past the width by more than rounding.
        static bool Overflows(double extent, double width)
            => (extent - width) >= ((Math.Abs(extent) + Math.Abs(width) + 10.0) * MachineEpsilon);
    }

    /// <summary>
    /// The gap a pointer is over among items laid out by <see cref="Flow"/>: on the line under it — the first line when
    /// it is above them all, the last one when below — before the first item whose middle is right of it, else after
    /// that line's last item.
    /// </summary>
    /// <param name="point">The pointer's position, in the panel's coordinates.</param>
    /// <param name="items">The items' rectangles, in order; at least one.</param>
    /// <returns>The item next to the gap, and which side of it.</returns>
    /// <remarks>
    /// The end of a line is the gap after its last item, never the one before the next line's first: the gap is drawn
    /// where the pointer is.
    /// </remarks>
    public static (int Index, DropPosition Position) FlowSlot(Point point, IReadOnlyList<Rect> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfZero(items.Count);

        (int lineStart, int lineEnd) = LineAt(point.Y, items);
        return SlotAcross(point.X, items, lineStart, lineEnd);
    }

    /// <summary>
    /// The gap a pointer is over among cards laid out in lines of columns: on the line under it — the first line when it
    /// is above them all, the last one when below — the gap across the line as <see cref="FlowSlot"/> finds it; but for
    /// a card alone on its line and as wide as the line, before it in its upper half and after it in its lower half.
    /// </summary>
    /// <param name="point">The pointer's position, in the panel's coordinates.</param>
    /// <param name="cells">Each card's cell, in order: its columns over its line's height and the spacing below it (see
    /// <see cref="Enigma.Launchpad.Desktop.Controls.ColumnFlowPlacement.Cell"/>); at least one.</param>
    /// <param name="lineWidth">The width of a line.</param>
    /// <returns>The card next to the gap, and which side of it.</returns>
    /// <remarks>
    /// A card spanning the whole line has no side to drop beside — anything dropped next to it goes above or below — so
    /// its halves are the upper and the lower one, as in a single column of cards.
    /// </remarks>
    public static (int Index, DropPosition Position) ColumnSlot(Point point, IReadOnlyList<Rect> cells, double lineWidth)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentOutOfRangeException.ThrowIfZero(cells.Count);

        (int lineStart, int lineEnd) = LineAt(point.Y, cells);

        if ((lineStart == lineEnd) && (cells[lineStart].Width >= lineWidth - LayoutRounding))
        {
            return (lineStart, point.Y < cells[lineStart].Center.Y ? DropPosition.Before : DropPosition.After);
        }

        return SlotAcross(point.X, cells, lineStart, lineEnd);
    }

    /// <summary>
    /// The line under a height among items laid out in lines — items of one line share their top: the first line above
    /// them all, the last one below.
    /// </summary>
    private static (int Start, int End) LineAt(double y, IReadOnlyList<Rect> items)
    {
        int lineStart = 0;

        while (true)
        {
            int lineEnd = lineStart;

            while ((lineEnd + 1 < items.Count) && (items[lineEnd + 1].Top == items[lineStart].Top))
            {
                lineEnd++;
            }

            if ((lineEnd + 1 == items.Count) || (y < items[lineStart].Bottom))
            {
                return (lineStart, lineEnd);
            }

            lineStart = lineEnd + 1;
        }
    }

    /// <summary>
    /// The gap across one line: before the first item whose middle is right of the pointer, else after the line's last.
    /// </summary>
    private static (int Index, DropPosition Position) SlotAcross(double x, IReadOnlyList<Rect> items, int lineStart, int lineEnd)
    {
        for (int index = lineStart; index <= lineEnd; index++)
        {
            if (x < items[index].Center.X)
            {
                return (index, DropPosition.Before);
            }
        }

        return (lineEnd, DropPosition.After);
    }
}
