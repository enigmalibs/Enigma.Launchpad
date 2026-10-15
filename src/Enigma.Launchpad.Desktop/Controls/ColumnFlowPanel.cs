using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Enigma.Launchpad.Desktop.Controls;

/// <summary>
/// Lays its children out in lines of equal columns, in order, each child spanning some of a line's columns: children
/// whose spans fit together share a line, top-aligned, and a child that does not fit in what is left of a line starts
/// the next one (see <see cref="ColumnFlow"/>).
/// </summary>
/// <remarks>
/// <para>
/// When a column would be narrower than <see cref="MinColumnWidth"/>, every child takes a whole line. Given an infinite
/// width — which a scroll viewer that only scrolls vertically never gives — the lines are as wide as the widest child.
/// </para>
/// <para>
/// A child can open a gap before or after itself (<see cref="GapBeforeProperty"/>, <see cref="GapAfterProperty"/>):
/// a dashed slot of some columns and <see cref="GapHeight"/>, laid out in the flow like a child of its own — so the
/// children after it make room, and wrap as they would — and drawn by the panel. It cannot belong to the child itself:
/// the gap before a child may sit at the end of the line above it. One gap is drawn: the first a child opens.
/// </para>
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
    /// Defines the <see cref="GapHeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> GapHeightProperty =
        AvaloniaProperty.Register<ColumnFlowPanel, double>(nameof(GapHeight));

    /// <summary>
    /// Defines the <see cref="GapBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> GapBrushProperty =
        AvaloniaProperty.Register<ColumnFlowPanel, IBrush?>(nameof(GapBrush));

    /// <summary>
    /// Defines the attached property saying how many columns a child spans; all of them unless set.
    /// </summary>
    public static readonly AttachedProperty<int> SpanProperty =
        AvaloniaProperty.RegisterAttached<ColumnFlowPanel, Control, int>("Span", int.MaxValue, validate: span => span >= 1);

    /// <summary>
    /// Defines the attached property saying how many columns the gap a child opens before itself spans; 0 for none.
    /// </summary>
    public static readonly AttachedProperty<int> GapBeforeProperty =
        AvaloniaProperty.RegisterAttached<ColumnFlowPanel, Control, int>("GapBefore", 0, validate: span => span >= 0);

    /// <summary>
    /// Defines the attached property saying how many columns the gap a child opens after itself spans; 0 for none.
    /// </summary>
    public static readonly AttachedProperty<int> GapAfterProperty =
        AvaloniaProperty.RegisterAttached<ColumnFlowPanel, Control, int>("GapAfter", 0, validate: span => span >= 0);

    private readonly GapMark _gap = new() { IsHitTestVisible = false, IsVisible = false };

    static ColumnFlowPanel()
    {
        AffectsMeasure<ColumnFlowPanel>(ColumnsProperty, ColumnSpacingProperty, RowSpacingProperty, MinColumnWidthProperty, GapHeightProperty);
        AffectsArrange<ColumnFlowPanel>(GapBrushProperty);
        AffectsParentMeasure<ColumnFlowPanel>(SpanProperty, GapBeforeProperty, GapAfterProperty);
    }

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <remarks>
    /// The gap is drawn by a visual of the panel's own, not one of its <see cref="Panel.Children"/>, which belong to
    /// the items control hosting it. It is the last visual child, and stays last: the panel keeps its visual children
    /// in step with its children by index, inserting, moving and replacing among the first ones, and removing by
    /// identity. (<see cref="Panel"/>'s own rendering is sealed, so the panel cannot draw the gap itself.)
    /// </remarks>
    public ColumnFlowPanel() => VisualChildren.Add(_gap);

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
    /// Gets or sets how high a gap opened by a child is.
    /// </summary>
    public double GapHeight
    {
        get => GetValue(GapHeightProperty);
        set => SetValue(GapHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush a gap is drawn with: its outline, and faintly its inside; no gap is drawn without one.
    /// </summary>
    public IBrush? GapBrush
    {
        get => GetValue(GapBrushProperty);
        set => SetValue(GapBrushProperty, value);
    }

    /// <summary>
    /// Gets where the gap a child opens is, as last arranged; <see langword="null"/> when none is open.
    /// </summary>
    public Rect? GapBounds => _gap.IsVisible ? _gap.Bounds : null;

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
    /// Gets how many columns the gap a child opens before itself spans.
    /// </summary>
    /// <param name="control">The child.</param>
    /// <returns>The gap's span; 0 when it opens none.</returns>
    public static int GetGapBefore(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(GapBeforeProperty);
    }

    /// <summary>
    /// Sets how many columns the gap a child opens before itself spans.
    /// </summary>
    /// <param name="control">The child.</param>
    /// <param name="span">The gap's span; 0 for none.</param>
    public static void SetGapBefore(Control control, int span)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(GapBeforeProperty, span);
    }

    /// <summary>
    /// Gets how many columns the gap a child opens after itself spans.
    /// </summary>
    /// <param name="control">The child.</param>
    /// <returns>The gap's span; 0 when it opens none.</returns>
    public static int GetGapAfter(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(GapAfterProperty);
    }

    /// <summary>
    /// Sets how many columns the gap a child opens after itself spans.
    /// </summary>
    /// <param name="control">The child.</param>
    /// <param name="span">The gap's span; 0 for none.</param>
    public static void SetGapAfter(Control control, int span)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(GapAfterProperty, span);
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
    /// Where the children go at a width, at the heights they last measured at, as they stand with no gap open.
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

        _gap.Measure(Size.Infinity);

        return new Size(width, ColumnFlow.Height(LayOut(width, out _)));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        IReadOnlyList<ColumnFlowPlacement> placements = LayOut(finalSize.Width, out List<int> gapIndexes);
        int next = 0;

        for (int index = 0; index < placements.Count; index++)
        {
            if (!gapIndexes.Contains(index))
            {
                Children[next++].Arrange(placements[index].Bounds);
            }
        }

        _gap.Brush = GapBrush;
        _gap.IsVisible = gapIndexes.Count > 0;
        _gap.Arrange(gapIndexes.Count > 0 ? placements[gapIndexes[0]].Bounds : default);
        _gap.InvalidateVisual();

        return finalSize;
    }

    /// <summary>
    /// Lays the children out with the gaps they open, each gap an item of the flow.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="gapIndexes">Which of the placements are gaps.</param>
    /// <returns>The placements, children and gaps in flow order.</returns>
    private IReadOnlyList<ColumnFlowPlacement> LayOut(double width, out List<int> gapIndexes)
    {
        List<(int Span, double Height)> items = new(Children.Count + 1);
        gapIndexes = [];

        foreach (Control child in Children)
        {
            AddGap(items, gapIndexes, GetGapBefore(child));
            items.Add((GetSpan(child), child.DesiredSize.Height));
            AddGap(items, gapIndexes, GetGapAfter(child));
        }

        return ColumnFlow.Layout(width, items, ColumnsAt(width), ColumnSpacing, RowSpacing);

        void AddGap(List<(int Span, double Height)> flow, List<int> gaps, int span)
        {
            if (span > 0)
            {
                gaps.Add(flow.Count);
                flow.Add((span, GapHeight));
            }
        }
    }

    /// <summary>
    /// The gap as drawn: the dashed placeholders' look (<c>Panel.placeholder</c> in <c>Styles.axaml</c>) — a faint fill
    /// and a dashed outline in the brush, with the corners of the group cards it stands among.
    /// </summary>
    private sealed class GapMark : Control
    {
        private const double FillOpacity = 0.08;
        private const double StrokeThickness = 1.5;
        private const double CornerRadius = 10;
        private static readonly DashStyle Dashes = new([4, 3], 0);

        public IBrush? Brush { get; set; }

        public override void Render(DrawingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (Brush is not { } brush)
            {
                return;
            }

            Rect area = new(Bounds.Size);

            using (context.PushOpacity(FillOpacity))
            {
                context.DrawRectangle(brush, null, area, CornerRadius, CornerRadius);
            }

            context.DrawRectangle(null, new Pen(brush, StrokeThickness, Dashes), area.Deflate(StrokeThickness / 2), CornerRadius, CornerRadius);
        }
    }
}
