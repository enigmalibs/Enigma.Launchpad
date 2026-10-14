using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Enigma.Launchpad.Desktop.ViewModels.Pages;

namespace Enigma.Launchpad.Desktop.Views.Pages;

/// <summary>
/// An open workspace: its groups of every kind, and their tiles.
/// </summary>
/// <remarks>
/// <para>
/// The code here is the drag and drop, which a binding cannot express: a drag is a sequence of pointer
/// events. No decision is taken here — what may be dropped where, and what a drop does, is the
/// ViewModel's to say; the view only finds what is under the pointer and which side of it.
/// </para>
/// <para>
/// Groups are dragged by their grip, shortcuts by their tile. The pointer handlers tunnel, because a
/// button marks the press as handled to capture the pointer, and a bubbling handler would never see the
/// press a drag starts from; none of them marks anything handled, so clicks work exactly as before. A press on
/// one of a tile's own buttons is a click on that button, never the start of a drag.
/// </para>
/// <para>
/// Where a drop would land, a dashed placeholder opens in the gap and the cards or tiles after it make room.
/// The gap is therefore found among the cards and tiles as they stand with no placeholder open
/// (<see cref="DropPlacement.Stack"/>, <see cref="DropPlacement.Flow"/>): read from the moved items, it would
/// move with them.
/// </para>
/// <para>
/// While a drag is over the page, a copy of what it carries follows the pointer on a layer above the groups,
/// and the original stays faded in its place. The copy is drawn from the drag's own payload, so it shows for
/// whatever item of the page a drag carries; a drag the page started also says where the item was taken hold of.
/// </para>
/// </remarks>
public partial class WorkspacePageView : UserControl
{
    /// <summary>
    /// The format a dragged group travels in: its kind and identity.
    /// </summary>
    internal static readonly DataFormat<string> GroupFormat = DataFormat.CreateStringApplicationFormat("enigma-launchpad.group");

    /// <summary>
    /// The format a dragged shortcut travels in: its kind and identity.
    /// </summary>
    internal static readonly DataFormat<string> ShortcutFormat = DataFormat.CreateStringApplicationFormat("enigma-launchpad.shortcut");

    /// <summary>
    /// How far the pointer travels with the button down before a press becomes a drag rather than a click
    /// that wobbled.
    /// </summary>
    private const double DragThreshold = 4;

    private object? _dragCandidate;
    private PointerPressedEventArgs? _dragTrigger;
    private Point _dragOrigin;
    private bool _suppressNextClick;
    private Lift? _lift;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public WorkspacePageView()
    {
        InitializeComponent();

        // Attached in code: XAML has no syntax for a handler on an attached routed event, nor for a
        // tunnelling one. A drag entering answers as one moving over: whenever the element under the pointer
        // changes, the old one sees the drag leave and the new one sees it enter, in the same input pass, and
        // answering the enter reopens the gap the leave closed before anything is drawn.
        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(Button.ClickEvent, OnClick, RoutingStrategies.Bubble);
    }

    private WorkspacePageViewModel? Page => DataContext as WorkspacePageViewModel;

    /// <summary>
    /// Finds what a drop at a point of the page lands on, and acts on it.
    /// </summary>
    /// <param name="data">What the drag carries.</param>
    /// <param name="source">The element under the pointer.</param>
    /// <param name="pointer">The pointer's position relative to a given control.</param>
    /// <param name="drop"><see langword="true"/> to carry the drop out; <see langword="false"/> to only show where it would land.</param>
    /// <returns><see langword="true"/> when the drop is accepted there — including next to the dragged item's own
    /// place, where it is accepted and changes nothing.</returns>
    internal bool Resolve(IDataTransfer data, object? source, Func<Visual, Point> pointer, bool drop)
    {
        if (Page is not { } page)
        {
            return false;
        }

        page.ClearDropHints();

        if (DragPayload.TryParse(data.TryGetValue(GroupFormat), out DragPayload? groupPayload))
        {
            return ResolveGroup(page, groupPayload, pointer, drop);
        }

        if (DragPayload.TryParse(data.TryGetValue(ShortcutFormat), out DragPayload? shortcutPayload))
        {
            return ResolveShortcut(page, shortcutPayload, source, pointer, drop);
        }

        return false;
    }

    /// <summary>
    /// The nearest control, from an element up, that carries a style class.
    /// </summary>
    private static Control? FindAncestor(object? source, string styleClass)
    {
        for (Visual? visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if ((visual is Control control) && control.Classes.Contains(styleClass))
            {
                return control;
            }
        }

        return null;
    }

    /// <summary>
    /// The first control under an element that carries a style class.
    /// </summary>
    private static T? FindDescendant<T>(Visual root, string styleClass)
        where T : Control
        => root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Classes.Contains(styleClass));

    /// <summary>
    /// A dragged group: it lands in the gap between two cards the pointer is nearest to, wherever the pointer is on the
    /// page.
    /// </summary>
    private bool ResolveGroup(WorkspacePageViewModel page, DragPayload payload, Func<Visual, Point> pointer, bool drop)
    {
        if ((page.FindGroup(payload.Id) is null) || (GroupRows(page.Groups.Count) is not { } rows))
        {
            return false;
        }

        int slot = DropPlacement.SlotFor(pointer(GroupList).Y, rows);
        (GroupViewModel target, DropPosition position) = slot < page.Groups.Count
            ? (page.Groups[slot], DropPosition.Before)
            : (page.Groups[^1], DropPosition.After);

        if (!page.MovesGroup(payload, target, position))
        {
            // Its own place: a drop there moves nothing, so nothing opens; nor is it refused.
            return true;
        }

        if (drop)
        {
            page.DropGroup(payload, target, position);
        }
        else
        {
            target.DropHint = position == DropPosition.Before ? DropHint.Before : DropHint.After;
        }

        return true;
    }

    /// <summary>
    /// The group rows as they stand with no dashed bar open among them: from the first row's top, each card's height
    /// and the spacing below it.
    /// </summary>
    /// <remarks>
    /// The first row's top is where the rows start whatever is open: a dashed bar is laid out inside a row, never above
    /// the first one.
    /// </remarks>
    private IReadOnlyList<(double Top, double Height)>? GroupRows(int count)
    {
        if ((count == 0) || (GroupList.ContainerFromIndex(0) is not { } first))
        {
            return null;
        }

        double spacing = (GroupList.ItemsPanelRoot as StackPanel)?.Spacing ?? 0;
        List<double> heights = new(count);

        for (int index = 0; index < count; index++)
        {
            if ((GroupList.ContainerFromIndex(index) is not { } row) || (FindDescendant<Border>(row, "groupCard") is not { } card))
            {
                return null;
            }

            heights.Add(card.Bounds.Height + spacing);
        }

        return DropPlacement.Stack(first.TranslatePoint(default, GroupList)?.Y ?? 0, heights);
    }

    /// <summary>
    /// A dragged shortcut: it lands on the group under the pointer, in the gap of its tiles the pointer is over, or at
    /// its end.
    /// </summary>
    private static bool ResolveShortcut(WorkspacePageViewModel page, DragPayload payload, object? source, Func<Visual, Point> pointer, bool drop)
    {
        if ((FindAncestor(source, "groupCard") is not { DataContext: GroupViewModel group } card)
            || !page.CanDropShortcut(payload, group, null))
        {
            return false;
        }

        (ShortcutViewModel? target, DropPosition position) = TileSlot(card, group, pointer);

        if (!page.MovesShortcut(payload, group, target, position))
        {
            // Its own place: a drop there moves nothing, so nothing opens; nor is it refused.
            return true;
        }

        if (drop)
        {
            page.DropShortcut(payload, group, target, position);
        }
        else if (target is not null)
        {
            target.DropHint = position == DropPosition.Before ? DropHint.Before : DropHint.After;
        }
        else
        {
            group.DropHint = DropHint.Inside;
        }

        return true;
    }

    /// <summary>
    /// Where on a group a dragged tile would land: in the gap of its tiles under the pointer, found as they stand with no
    /// dashed tile open among them; elsewhere on the card, at the group's end — after its last tile when it shows any,
    /// else in the group itself.
    /// </summary>
    private static (ShortcutViewModel? Target, DropPosition Position) TileSlot(Control card, GroupViewModel group, Func<Visual, Point> pointer)
    {
        if ((FindDescendant<ItemsControl>(card, "tiles") is not { IsEffectivelyVisible: true, ItemsPanelRoot: { } flow } tiles)
            || (group.Shortcuts.Count == 0))
        {
            return (null, DropPosition.After);
        }

        Point at = pointer(flow);

        if ((at.Y < 0) || (at.Y > flow.Bounds.Height) || (TileSizes(tiles, group.Shortcuts.Count) is not { } sizes))
        {
            return (group.Shortcuts[^1], DropPosition.After);
        }

        (int index, DropPosition position) = DropPlacement.FlowSlot(at, DropPlacement.Flow(flow.Bounds.Width, sizes));
        return (group.Shortcuts[index], position);
    }

    /// <summary>
    /// The size each tile takes in its group's flow with no dashed tile open beside it: the tile and its slot's margins.
    /// </summary>
    private static List<Size>? TileSizes(ItemsControl tiles, int count)
    {
        List<Size> sizes = new(count);

        for (int index = 0; index < count; index++)
        {
            if ((tiles.ContainerFromIndex(index) is not { } container)
                || (FindDescendant<StackPanel>(container, "tileSlot") is not { } slot)
                || (FindDescendant<Button>(slot, "tile") is not { } tile))
            {
                return null;
            }

            sizes.Add(new Size(
                tile.DesiredSize.Width + slot.Margin.Left + slot.Margin.Right,
                tile.DesiredSize.Height + slot.Margin.Top + slot.Margin.Bottom));
        }

        return sizes;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _suppressNextClick = false;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ForgetDrag();
            return;
        }

        if (FindAncestor(e.Source, "tileAction") is not null)
        {
            ForgetDrag();
            return;
        }

        // Remembered, not acted on: every click starts this way, and only the movement that follows tells
        // the two apart.
        _dragCandidate = FindAncestor(e.Source, "tile")?.DataContext as ShortcutViewModel
            ?? (object?)(FindAncestor(e.Source, "grip")?.DataContext as GroupViewModel);
        _dragTrigger = _dragCandidate is null ? null : e;
        _dragOrigin = e.GetPosition(this);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => ForgetDrag();

    // The one kind of place an async void is allowed: a UI event handler.
    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if ((_dragCandidate is not { } candidate)
            || (_dragTrigger is not { } trigger)
            || (Page is not { } page)
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point position = e.GetPosition(this);

        if ((Math.Abs(position.X - _dragOrigin.X) < DragThreshold) && (Math.Abs(position.Y - _dragOrigin.Y) < DragThreshold))
        {
            return;
        }

        // Cleared before the drag, not after: the drag runs its own event loop, and a second drag started
        // from inside the first would be a drag of a drag.
        ForgetDrag();

        DataTransfer data = new();

        switch (candidate)
        {
            case GroupViewModel group:
                data.Add(DataTransferItem.Create(GroupFormat, new DragPayload(group.Kind, group.Id).Format()));
                group.IsDragging = true;
                _lift = new Lift(group.Id, GrabIn(trigger.Source, "groupCard"));
                break;

            case ShortcutViewModel shortcut:
                data.Add(DataTransferItem.Create(ShortcutFormat, new DragPayload(shortcut.Kind, shortcut.Id).Format()));
                shortcut.IsDragging = true;
                _lift = new Lift(shortcut.Id, GrabIn(trigger.Source, "tile"));

                // A tile dropped where it started would otherwise read the release as a click and open.
                _suppressNextClick = true;
                break;
        }

        try
        {
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move);
        }
        catch (Exception)
        {
            // Swallowed deliberately: DoDragDrop goes through platform code whose failures are not a
            // documented set, and a drag that never started is a gesture that did not happen — nothing has
            // moved, nothing is half-done — whereas an exception escaping an async void handler would take
            // the application down.
        }
        finally
        {
            page.ClearDragState();
            PutTheCopyDown();
            _lift = null;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = Resolve(e.DataTransfer, e.Source, e.GetPosition, drop: false) ? DragDropEffects.Move : DragDropEffects.None;
        CarryTheCopy(e.DataTransfer, e.GetPosition);
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        Page?.ClearDropHints();
        HideTheCopy();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.DragEffects = Resolve(e.DataTransfer, e.Source, e.GetPosition, drop: true) ? DragDropEffects.Move : DragDropEffects.None;
        Page?.ClearDropHints();
        PutTheCopyDown();
        e.Handled = true;
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        if (_suppressNextClick && (FindAncestor(e.Source, "tile") is not null))
        {
            // Marked handled before the button runs its command, so the tile does not open.
            e.Handled = true;
        }

        _suppressNextClick = false;
    }

    private void ForgetDrag()
    {
        _dragCandidate = null;
        _dragTrigger = null;
    }

    /// <summary>
    /// Where the press that starts a drag took hold of the item: the offset from its top-left corner.
    /// </summary>
    private Point GrabIn(object? source, string styleClass)
        => (FindAncestor(source, styleClass) is { } item) && (this.TranslatePoint(_dragOrigin, item) is { } grab) ? grab : default;

    /// <summary>
    /// Shows the copy of what a drag carries under the pointer: a tile, where it is held, in both directions; a group's
    /// header up and down only, at the cards' left and width. A drag carrying nothing of this page shows none.
    /// </summary>
    private void CarryTheCopy(IDataTransfer data, Func<Visual, Point> pointer)
    {
        Point at = pointer(DragLayer);
        Size layer = DragLayer.Bounds.Size;

        if (DragPayload.TryParse(data.TryGetValue(GroupFormat), out DragPayload? groupPayload)
            && (Page?.FindGroup(groupPayload.Id) is { } group))
        {
            Size size = ShowTheCopy(GroupGhost, group, GroupList.Bounds.Width);
            Point grab = GrabFor(group.Id, size);

            Canvas.SetLeft(GroupGhost, GroupList.TranslatePoint(default, DragLayer)?.X ?? 0);
            Canvas.SetTop(GroupGhost, DropPlacement.GhostOffset(at.Y, grab.Y, size.Height, layer.Height));
            TileGhost.IsVisible = false;
        }
        else if (DragPayload.TryParse(data.TryGetValue(ShortcutFormat), out DragPayload? shortcutPayload)
            && (Page?.FindShortcut(shortcutPayload.Id) is { } shortcut))
        {
            Size size = ShowTheCopy(TileGhost, shortcut, double.NaN);
            Point grab = GrabFor(shortcut.Id, size);

            Canvas.SetLeft(TileGhost, DropPlacement.GhostOffset(at.X, grab.X, size.Width, layer.Width));
            Canvas.SetTop(TileGhost, DropPlacement.GhostOffset(at.Y, grab.Y, size.Height, layer.Height));
            GroupGhost.IsVisible = false;
        }
        else
        {
            HideTheCopy();
        }
    }

    /// <summary>
    /// Puts an item in a copy and shows it.
    /// </summary>
    /// <returns>The copy's size.</returns>
    /// <remarks>
    /// Measured here rather than on the next layout pass: the copy is placed by its size before it has ever been laid
    /// out.
    /// </remarks>
    private static Size ShowTheCopy(ContentControl copy, object item, double width)
    {
        copy.Content = item;
        copy.Width = width;
        copy.IsVisible = true;
        copy.Measure(Size.Infinity);
        return copy.DesiredSize;
    }

    /// <summary>
    /// Where the pointer holds the copy: where the press took hold of the item, for a drag this page started; its
    /// middle otherwise.
    /// </summary>
    private Point GrabFor(Guid id, Size size)
        => (_lift is { } lift) && (lift.Id == id)
            ? new Point(Math.Clamp(lift.Grab.X, 0, size.Width), Math.Clamp(lift.Grab.Y, 0, size.Height))
            : new Point(size.Width / 2, size.Height / 2);

    /// <summary>
    /// Hides the copy while the drag is away from the page, keeping its content for when it comes back.
    /// </summary>
    private void HideTheCopy()
    {
        TileGhost.IsVisible = false;
        GroupGhost.IsVisible = false;
    }

    /// <summary>
    /// Takes the copy away once the drag is over, emptied so that no second set of the item's controls stays on the
    /// page between drags.
    /// </summary>
    private void PutTheCopyDown()
    {
        HideTheCopy();
        TileGhost.Content = null;
        GroupGhost.Content = null;
    }

    /// <summary>
    /// What a drag the page started carries, and where the press took hold of it.
    /// </summary>
    /// <param name="Id">The carried group's or shortcut's identity.</param>
    /// <param name="Grab">The press's offset from the item's top-left corner.</param>
    private readonly record struct Lift(Guid Id, Point Grab);
}
