using System;
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

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public WorkspacePageView()
    {
        InitializeComponent();

        // Attached in code: XAML has no syntax for a handler on an attached routed event, nor for a
        // tunnelling one.
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
    /// <returns><see langword="true"/> when the drop is accepted there.</returns>
    internal bool Resolve(IDataTransfer data, object? source, Func<Visual, Point> pointer, bool drop)
    {
        if (Page is not { } page)
        {
            return false;
        }

        page.ClearDropHints();

        if (FindAncestor(source, "groupCard") is not { DataContext: GroupViewModel group } card)
        {
            return false;
        }

        if (DragPayload.TryParse(data.TryGetValue(GroupFormat), out DragPayload? groupPayload))
        {
            if (!page.CanDropGroup(groupPayload, group))
            {
                return false;
            }

            DropPosition position = DropPlacement.Vertical(pointer(card).Y, card.Bounds.Height);

            if (drop)
            {
                page.DropGroup(groupPayload, group, position);
            }
            else
            {
                group.DropHint = position == DropPosition.Before ? DropHint.Before : DropHint.After;
            }

            return true;
        }

        if (DragPayload.TryParse(data.TryGetValue(ShortcutFormat), out DragPayload? shortcutPayload))
        {
            Control? tile = FindAncestor(source, "tile");
            ShortcutViewModel? target = tile?.DataContext as ShortcutViewModel;

            if (!page.CanDropShortcut(shortcutPayload, group, target))
            {
                return false;
            }

            DropPosition position = (tile is not null) ? DropPlacement.Horizontal(pointer(tile).X, tile.Bounds.Width) : DropPosition.After;

            if (drop)
            {
                page.DropShortcut(shortcutPayload, group, target, position);
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
                break;

            case ShortcutViewModel shortcut:
                data.Add(DataTransferItem.Create(ShortcutFormat, new DragPayload(shortcut.Kind, shortcut.Id).Format()));
                shortcut.IsDragging = true;

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
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = Resolve(e.DataTransfer, e.Source, e.GetPosition, drop: false) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => Page?.ClearDropHints();

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.DragEffects = Resolve(e.DataTransfer, e.Source, e.GetPosition, drop: true) ? DragDropEffects.Move : DragDropEffects.None;
        Page?.ClearDropHints();
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
}
