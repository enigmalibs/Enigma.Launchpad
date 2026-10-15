using System;
using System.Collections.Generic;
using Avalonia;

namespace Enigma.Launchpad.Desktop.Controls;

/// <summary>
/// Where one item of a <see cref="ColumnFlow"/> goes.
/// </summary>
/// <param name="Bounds">The item's rectangle: its span's width, its own height, at the top of its line.</param>
/// <param name="Cell">The same columns over the whole height of the item's line.</param>
public readonly record struct ColumnFlowPlacement(Rect Bounds, Rect Cell);

/// <summary>
/// The arithmetic of a flow of items spanning columns: lines of equal columns filled in order, an item that does not fit
/// in what is left of a line starting the next one. Pure, so the layout is tested without a window, and the same
/// layout is computed wherever it is needed.
/// </summary>
public static class ColumnFlow
{
    /// <summary>
    /// How many columns a width is laid out in: all of them, or one when a column would be narrower than a minimum.
    /// </summary>
    /// <param name="width">The width the lines are laid out in.</param>
    /// <param name="columns">The columns of a line.</param>
    /// <param name="columnSpacing">The space between two columns.</param>
    /// <param name="minColumnWidth">The narrowest a column may be.</param>
    /// <returns><paramref name="columns"/>, or 1.</returns>
    public static int ColumnsFor(double width, int columns, double columnSpacing, double minColumnWidth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);

        return (columns > 1) && (ColumnWidth(width, columns, columnSpacing) >= minColumnWidth) ? columns : 1;
    }

    /// <summary>
    /// How wide an item spanning some columns is.
    /// </summary>
    /// <param name="width">The width the lines are laid out in.</param>
    /// <param name="columns">The columns of a line, as <see cref="ColumnsFor"/> gives them.</param>
    /// <param name="columnSpacing">The space between two columns.</param>
    /// <param name="span">The columns the item spans; clamped to the line.</param>
    /// <returns>The span's columns and the spacing between them; never negative.</returns>
    public static double SpanWidth(double width, int columns, double columnSpacing, int span)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);

        int clamped = Math.Clamp(span, 1, columns);
        return Math.Max(0, (clamped * ColumnWidth(width, columns, columnSpacing)) + ((clamped - 1) * columnSpacing));
    }

    /// <summary>
    /// Lays items out in lines of columns, in order: left to right, an item whose span does not fit in what is left of
    /// a line starting the next one.
    /// </summary>
    /// <param name="width">The width the lines are laid out in.</param>
    /// <param name="items">Each item's span, in columns — clamped to the line — and height, in order.</param>
    /// <param name="columns">The columns of a line, as <see cref="ColumnsFor"/> gives them.</param>
    /// <param name="columnSpacing">The space between two columns.</param>
    /// <param name="rowSpacing">The space between two lines.</param>
    /// <returns>Where each item goes, in order. A line is as high as its highest item; every item sits at its top.</returns>
    public static IReadOnlyList<ColumnFlowPlacement> Layout(
        double width,
        IReadOnlyList<(int Span, double Height)> items,
        int columns,
        double columnSpacing,
        double rowSpacing)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);

        double column = ColumnWidth(width, columns, columnSpacing);
        List<(int Line, double X, double Width, double Height)> placed = new(items.Count);
        List<double> lineHeights = [];
        int used = 0;

        foreach ((int span, double height) in items)
        {
            int clamped = Math.Clamp(span, 1, columns);

            if ((lineHeights.Count == 0) || (used + clamped > columns))
            {
                lineHeights.Add(0);
                used = 0;
            }

            placed.Add((lineHeights.Count - 1, used * (column + columnSpacing), SpanWidth(width, columns, columnSpacing, clamped), height));
            lineHeights[^1] = Math.Max(lineHeights[^1], height);
            used += clamped;
        }

        double[] lineTops = new double[lineHeights.Count];

        for (int line = 1; line < lineTops.Length; line++)
        {
            lineTops[line] = lineTops[line - 1] + lineHeights[line - 1] + rowSpacing;
        }

        ColumnFlowPlacement[] placements = new ColumnFlowPlacement[placed.Count];

        for (int index = 0; index < placed.Count; index++)
        {
            (int line, double x, double itemWidth, double height) = placed[index];
            placements[index] = new ColumnFlowPlacement(
                new Rect(x, lineTops[line], itemWidth, height),
                new Rect(x, lineTops[line], itemWidth, lineHeights[line]));
        }

        return placements;
    }

    /// <summary>
    /// The height of laid-out items: from the first line's top to the last line's bottom.
    /// </summary>
    /// <param name="placements">The items, as <see cref="Layout"/> placed them.</param>
    /// <returns>The height; 0 for no item.</returns>
    public static double Height(IReadOnlyList<ColumnFlowPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        double bottom = 0;

        foreach (ColumnFlowPlacement placement in placements)
        {
            bottom = Math.Max(bottom, placement.Cell.Bottom);
        }

        return bottom;
    }

    private static double ColumnWidth(double width, int columns, double columnSpacing)
        => (width - ((columns - 1) * columnSpacing)) / columns;
}
