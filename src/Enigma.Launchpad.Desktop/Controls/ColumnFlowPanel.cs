using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Enigma.Launchpad.Desktop.Controls;

/// <summary>
/// Lays its children out in lines of equal columns, in order, each child spanning some of a line's columns: children
/// whose spans fit together share a line, top-aligned, and a child that does not fit in what is left of a line starts
/// the next one (see <see cref="ColumnFlow"/>).
/// </summary>
/// <remarks>
/// When a column would be narrower than <see cref="MinColumnWidth"/>, every child takes a whole line. Given an infinite
/// width — which a scroll viewer that only scrolls vertically never gives — the lines are as wide as the widest child.
/// </remarks>
public sealed class ColumnFlowPanel : Panel
{
    /// <summary>
    /// Defines the <see cref="Columns"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<ColumnFlowPanel, int>(nameof(Columns), 3, validate: columns => columns >= 1);

    /// <summary>
    /// Defines the <see cref="ColumnSpacing"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<ColumnFlowPanel, double>(nameof(ColumnSpacing));

    /// <summary>
    /// Defines the <see cref="RowSpacing"/> property.
    /// </summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<ColumnFlowPanel, double>(nameof(RowSpacing));

    /// <summary>
    /// Defines the <see cref="MinColumnWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<ColumnFlowPanel, double>(nameof(MinColumnWidth));

    /// <summary>
    /// Defines the attached property saying how many columns a child spans; all of them unless set.
    /// </summary>
    public static readonly AttachedProperty<int> SpanProperty =
        AvaloniaProperty.RegisterAttached<ColumnFlowPanel, Control, int>("Span", int.MaxValue, validate: span => span >= 1);

    static ColumnFlowPanel()
    {
        AffectsMeasure<ColumnFlowPanel>(ColumnsProperty, ColumnSpacingProperty, RowSpacingProperty, MinColumnWidthProperty);
        AffectsParentMeasure<ColumnFlowPanel>(SpanProperty);
    }

    /// <summary>
    /// Gets or sets the number of columns in a line.
    /// </summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the space between two columns.
    /// </summary>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the space between two lines.
    /// </summary>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the narrowest a column may be before every child takes a whole line.
    /// </summary>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>
    /// Gets how many columns a child spans.
    /// </summary>
    /// <param name="control">The child.</param>
    /// <returns>Its span; more than the line has means the whole line.</returns>
    public static int GetSpan(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(SpanProperty);
    }

    /// <summary>
    /// Sets how many columns a child spans.
    /// </summary>
    /// <param name="control">The child.</param>
    /// <param name="span">Its span, at least 1.</param>
    public static void SetSpan(Control control, int span)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(SpanProperty, span);
    }

    /// <summary>
    /// The number of columns the children are laid out in at a width: <see cref="Columns"/>, or 1 when a column would
    /// be narrower than <see cref="MinColumnWidth"/>.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <returns>The number of columns.</returns>
    public int ColumnsAt(double width)
        => double.IsFinite(width) ? ColumnFlow.ColumnsFor(width, Columns, ColumnSpacing, MinColumnWidth) : 1;

    /// <summary>
    /// Where the children go at a width, at the heights they last measured at.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <returns>Each child's placement, in order.</returns>
    public IReadOnlyList<ColumnFlowPlacement> Place(double width)
    {
        List<(int Span, double Height)> items = new(Children.Count);

        foreach (Control child in Children)
        {
            items.Add((GetSpan(child), child.DesiredSize.Height));
        }

        return ColumnFlow.Layout(width, items, ColumnsAt(width), ColumnSpacing, RowSpacing);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = availableSize.Width;

        if (!double.IsFinite(width))
        {
            width = 0;

            foreach (Control child in Children)
            {
                child.Measure(Size.Infinity);
                width = Math.Max(width, child.DesiredSize.Width);
            }
        }

        int columns = ColumnsAt(width);

        foreach (Control child in Children)
        {
            child.Measure(new Size(ColumnFlow.SpanWidth(width, columns, ColumnSpacing, GetSpan(child)), double.PositiveInfinity));
        }

        return new Size(width, ColumnFlow.Height(Place(width)));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        IReadOnlyList<ColumnFlowPlacement> placements = Place(finalSize.Width);

        for (int index = 0; index < placements.Count; index++)
        {
            Children[index].Arrange(placements[index].Bounds);
        }

        return finalSize;
    }
}
