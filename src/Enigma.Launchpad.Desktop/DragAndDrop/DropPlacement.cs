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
    /// Lays rows out one after the other from a top, as they stand with no gap opened among them.
    /// </summary>
    /// <param name="top">Where the first row starts.</param>
    /// <param name="heights">Each row's height without an open gap: its card and the space below it.</param>
    /// <returns>Each row's top and height, in list order.</returns>
    /// <remarks>
    /// The slot is found in this geometry rather than in the rows as drawn: the dashed placeholder that marks the slot
    /// pushes the rows below it down, and reading their new places would move the slot, which would move the
    /// placeholder, and so on.
    /// </remarks>
    public static IReadOnlyList<(double Top, double Height)> Stack(double top, IReadOnlyList<double> heights)
    {
        ArgumentNullException.ThrowIfNull(heights);

        List<(double Top, double Height)> rows = new(heights.Count);

        foreach (double height in heights)
        {
            rows.Add((top, height));
            top += height;
        }

        return rows;
    }

    /// <summary>
    /// The gap a pointer is over among rows laid out top to bottom: the gap before the first row whose middle is below
    /// it.
    /// </summary>
    /// <param name="y">The pointer's position, in the rows' coordinates.</param>
    /// <param name="rows">The rows, in order (see <see cref="Stack"/>).</param>
    /// <returns>The gap's index: 0 before the first row, the row count after the last one.</returns>
    public static int SlotFor(double y, IReadOnlyList<(double Top, double Height)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        for (int index = 0; index < rows.Count; index++)
        {
            if (y < rows[index].Top + (rows[index].Height / 2))
            {
                return index;
            }
        }

        return rows.Count;
    }

    /// <summary>
    /// Where a horizontal wrap panel puts items of these sizes in a width, as they stand with no gap opened among them:
    /// left to right, an item that does not fit after another starting a new line.
    /// </summary>
    /// <param name="width">The width the panel lays its lines out in.</param>
    /// <param name="sizes">Each item's size, margins included, in order.</param>
    /// <returns>Each item's rectangle in the panel, in order; its height is its line's.</returns>
    /// <remarks>
    /// The same reason as <see cref="Stack"/>, and a stronger one: a dashed tile opened after the last tile of a line
    /// takes that tile to the next line with it, away from the pointer that put it there. The line break follows
    /// <see cref="Avalonia.Controls.WrapPanel"/>'s own test.
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

        int lineStart = 0;

        while (true)
        {
            int lineEnd = lineStart;

            while ((lineEnd + 1 < items.Count) && (items[lineEnd + 1].Top == items[lineStart].Top))
            {
                lineEnd++;
            }

            bool lastLine = lineEnd + 1 == items.Count;

            if (lastLine || (point.Y < items[lineStart].Bottom))
            {
                for (int index = lineStart; index <= lineEnd; index++)
                {
                    if (point.X < items[index].Center.X)
                    {
                        return (index, DropPosition.Before);
                    }
                }

                return (lineEnd, DropPosition.After);
            }

            lineStart = lineEnd + 1;
        }
    }
}
