using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One open workspace: its title, and its groups — each of one kind, of every kind together.
/// </summary>
/// <remarks>
/// <para>
/// The page's groups are the live state of the workspace. Every change is handed to the session as a fresh snapshot of
/// the workspace's groups, which the session saves in the background. The Workspaces page owns the open workspace: it
/// creates it, closes it, and has it read the workspace again when the session changes under it, so this page never
/// subscribes to the session itself and goes away with nothing left attached.
/// </para>
/// <para>
/// A group, and every shortcut in it, has one kind for its whole life; what can be done with a tile — run it with
/// arguments, open a terminal in it, find its icon — follows its group's kind.
/// </para>
/// </remarks>
public sealed class WorkspacePageViewModel : ObservableObject
{
    private readonly ILaunchpadSession _session;
    private readonly IShortcutValidator _validator;
    private readonly ILaunchpadDialogs _dialogs;
    private readonly IShortcutActions _actions;
    private readonly IShortcutIcons _icons;
    private readonly IUserNotifications _notifications;

    // Set while the groups are being built from the session, when the workspace is not whole yet.
    private bool _isReadingGroups;
    private bool _hasIconsToKeep;

    /// <summary>
    /// Initialises a new instance, showing a workspace of the profile in use.
    /// </summary>
    /// <param name="workspaceId">The workspace to show; the session must be loaded, and the workspace in it.</param>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    /// <param name="icons">The icons of the tiles: kept with the shortcuts, or found from their targets.</param>
    /// <param name="notifications">Where a failed icon lookup or a refused move is reported.</param>
    /// <param name="back">Goes back to the list of workspaces.</param>
    /// <exception cref="ArgumentException">The profile in use has no such workspace.</exception>
    public WorkspacePageViewModel(
        Guid workspaceId,
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutActions actions,
        IShortcutIcons icons,
        IUserNotifications notifications,
        Action back)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(back);

        WorkspaceId = workspaceId;
        _session = session;
        _validator = validator;
        _dialogs = dialogs;
        _actions = actions;
        _icons = icons;
        _notifications = notifications;

        Groups.CollectionChanged += OnGroupsChanged;
        NewGroupCommand = new AsyncRelayCommand<ShortcutKind>(NewGroupAsync);
        BackCommand = new RelayCommand(back);

        if (!Reload())
        {
            throw new ArgumentException("The profile in use has no such workspace.", nameof(workspaceId));
        }
    }

    /// <summary>
    /// Gets the workspace's identity.
    /// </summary>
    public Guid WorkspaceId { get; }

    /// <summary>
    /// Gets the workspace's title.
    /// </summary>
    public string Title { get; private set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>
    /// Gets what the workspace is for; empty when it does not say.
    /// </summary>
    public string Description
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasDescription));
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the header shows a description.
    /// </summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>
    /// Gets the workspace's glyph.
    /// </summary>
    public PhosphorIcon Glyph { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the workspace's groups, in display order.
    /// </summary>
    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the workspace has no group, which is when the page invites the user to create
    /// one.
    /// </summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>
    /// Gets the command creating a group of a kind, at the end of the workspace.
    /// </summary>
    public AsyncRelayCommand<ShortcutKind> NewGroupCommand { get; }

    /// <summary>
    /// Gets the command going back to the list of workspaces.
    /// </summary>
    public RelayCommand BackCommand { get; }

    /// <summary>
    /// Reads the workspace again from the session: its title, glyph and groups.
    /// </summary>
    /// <returns><see langword="false"/> when the profile in use no longer has the workspace.</returns>
    public bool Reload()
    {
        if (_session.Workspaces.FirstOrDefault(workspace => workspace.Id == WorkspaceId) is not { } workspace)
        {
            return false;
        }

        Title = workspace.Title;
        Description = workspace.Description ?? string.Empty;
        Glyph = Glyphs.OfWorkspace(workspace.Icon);
        ReadGroups(workspace.Groups);
        return true;
    }

    /// <summary>
    /// Hands the workspace's current groups to the session, which saves them.
    /// </summary>
    internal void Persist() => _session.UpdateGroups(WorkspaceId, Groups.Select(group => group.ToModel()).ToArray());

    /// <summary>
    /// Asks for a new name for a group and applies it.
    /// </summary>
    /// <param name="group">The group to rename.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task RenameGroupAsync(GroupViewModel group)
    {
        string? name = await _dialogs.PromptNameAsync(
            "Rename group",
            "Group name",
            "Rename",
            group.Name,
            candidate => _validator.ValidateGroupName(candidate, NamesOfKind(group.Kind, except: group)));

        if ((name is null) || (name == group.Name))
        {
            return;
        }

        group.Name = name;
        Persist();
    }

    /// <summary>
    /// Deletes a group and its shortcuts, once the user has confirmed.
    /// </summary>
    /// <param name="group">The group to delete.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task DeleteGroupAsync(GroupViewModel group)
    {
        string message = group.Count switch
        {
            0 => "This cannot be undone.",
            1 => "Its shortcut will be deleted with it. This cannot be undone.",
            _ => $"Its {group.Count} shortcuts will be deleted with it. This cannot be undone.",
        };

        if (!await _dialogs.ConfirmDeleteAsync($"Delete the group “{group.Name}”?", message, "Delete"))
        {
            return;
        }

        if (Groups.Remove(group))
        {
            Persist();
        }
    }

    /// <summary>
    /// Whether a group can move by an offset without leaving the page.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="offset">How many places, negative for earlier.</param>
    /// <returns><see langword="true"/> when the move stays on the page.</returns>
    internal bool CanMoveGroup(GroupViewModel group, int offset)
    {
        int index = Groups.IndexOf(group);
        int target = index + offset;

        return (index >= 0) && (offset != 0) && (target >= 0) && (target < Groups.Count);
    }

    /// <summary>
    /// Moves a group by an offset, and saves the new order.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="offset">How many places, negative for earlier.</param>
    internal void MoveGroup(GroupViewModel group, int offset)
    {
        if (!CanMoveGroup(group, offset))
        {
            return;
        }

        int index = Groups.IndexOf(group);
        Groups.Move(index, index + offset);
        Persist();
    }

    /// <summary>
    /// Sets how much of a line a group takes, and saves it when it changed.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="width">Its new width.</param>
    internal void SetGroupWidth(GroupViewModel group, GroupWidth width)
    {
        if (!Enum.IsDefined(width) || (group.Width == width))
        {
            return;
        }

        group.Width = width;
        Persist();
    }

    /// <summary>
    /// Asks which other workspace to move a group to, and moves it there, at the end. A workspace that already has a
    /// group of the same name and kind does not take it: the user is told, and nothing moves.
    /// </summary>
    /// <param name="group">The group to move.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task MoveGroupToWorkspaceAsync(GroupViewModel group)
    {
        Workspace[] others = _session.Workspaces.Where(workspace => workspace.Id != WorkspaceId).ToArray();

        if (others.Length == 0)
        {
            _notifications.ShowWarning("No other workspace", "Create another workspace first, then move the group to it.");
            return;
        }

        Workspace? target = await _dialogs.ChooseWorkspaceAsync(
            $"Move “{group.Name}”",
            $"Choose the workspace to move the group “{group.Name}” to, with its shortcuts.",
            "Move",
            others);

        // The group may have gone, or the workspace with it, while the question was open.
        if ((target is null) || !Groups.Contains(group))
        {
            return;
        }

        if (_session.Workspaces.FirstOrDefault(workspace => workspace.Id == target.Id) is not { } current)
        {
            return;
        }

        if (current.Groups.Any(existing => (existing.Kind == group.Kind) && SameName(existing.Name, group.Name)))
        {
            _notifications.ShowWarning(
                "The group was not moved",
                $"“{current.Title}” already has {GroupKindOption.Article(group.Kind)} group called “{group.Name}”. Rename one of them first.");
            return;
        }

        // The session moves it in one change, and the Workspaces page has this page read the workspace again.
        _session.MoveGroup(group.Id, WorkspaceId, current.Id);
    }

    /// <summary>
    /// Asks for a new shortcut and adds it at the end of a group, which is unfolded to show it.
    /// </summary>
    /// <param name="group">The group to add to; the shortcut is of its kind.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task AddShortcutAsync(GroupViewModel group)
    {
        Shortcut? shortcut = await _dialogs.EditShortcutAsync(group.Kind, null);

        if (shortcut is null)
        {
            return;
        }

        group.Shortcuts.Add(new ShortcutViewModel(this, group, shortcut));
        group.IsExpanded = true;
        Persist();
    }

    /// <summary>
    /// Asks for a shortcut's new name and target and applies them.
    /// </summary>
    /// <param name="shortcut">The shortcut to edit.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task EditShortcutAsync(ShortcutViewModel shortcut)
    {
        Shortcut? edited = await _dialogs.EditShortcutAsync(shortcut.Kind, shortcut.Model);

        if ((edited is null) || (edited == shortcut.Model))
        {
            return;
        }

        shortcut.Update(edited);
        Persist();
    }

    /// <summary>
    /// Deletes a shortcut, once the user has confirmed.
    /// </summary>
    /// <param name="shortcut">The shortcut to delete.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task DeleteShortcutAsync(ShortcutViewModel shortcut)
    {
        if (!await _dialogs.ConfirmDeleteAsync($"Delete the shortcut “{shortcut.Name}”?", "This cannot be undone.", "Delete"))
        {
            return;
        }

        if (shortcut.Group.Shortcuts.Remove(shortcut))
        {
            Persist();
        }
    }

    /// <summary>
    /// Whether a shortcut can move by an offset without leaving its group.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="offset">How many places, negative for earlier.</param>
    /// <returns><see langword="true"/> when the move stays in the group.</returns>
    internal bool CanMoveShortcut(ShortcutViewModel shortcut, int offset)
    {
        int index = shortcut.Group.Shortcuts.IndexOf(shortcut);
        int target = index + offset;

        return (index >= 0) && (offset != 0) && (target >= 0) && (target < shortcut.Group.Shortcuts.Count);
    }

    /// <summary>
    /// Moves a shortcut by an offset within its group, and saves the new order.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="offset">How many places, negative for earlier.</param>
    internal void MoveShortcut(ShortcutViewModel shortcut, int offset)
    {
        if (!CanMoveShortcut(shortcut, offset))
        {
            return;
        }

        int index = shortcut.Group.Shortcuts.IndexOf(shortcut);
        shortcut.Group.Shortcuts.Move(index, index + offset);
        Persist();
    }

    /// <summary>
    /// Opens a shortcut; a failure is shown to the user.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    internal void OpenShortcut(ShortcutViewModel shortcut) => _actions.Open(shortcut.Kind, shortcut.Model);

    /// <summary>
    /// Asks which arguments to run an application with this once — starting from its default arguments — and runs
    /// it with them. The shortcut itself is not changed.
    /// </summary>
    /// <param name="shortcut">The application's tile.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task RunWithArgumentsAsync(ShortcutViewModel shortcut)
    {
        if (shortcut.Kind != ShortcutKind.Application)
        {
            return;
        }

        Shortcut model = shortcut.Model;
        string? arguments = await _dialogs.PromptArgumentsAsync(model);

        if (arguments is null)
        {
            return;
        }

        _actions.Open(ShortcutKind.Application, model with { Arguments = arguments.Length == 0 ? null : arguments });
    }

    /// <summary>
    /// Opens a terminal in a directory shortcut's folder; a failure is shown to the user.
    /// </summary>
    /// <param name="shortcut">The folder's tile.</param>
    internal void OpenInTerminal(ShortcutViewModel shortcut)
    {
        if (shortcut.Kind == ShortcutKind.Directory)
        {
            _actions.OpenInTerminal(shortcut.Model);
        }
    }

    /// <summary>
    /// Shows a shortcut in its folder; a failure is shown to the user.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    internal void RevealShortcut(ShortcutViewModel shortcut) => _actions.Reveal(shortcut.Kind, shortcut.Model);

    /// <summary>
    /// Whether a kind of tile can have its icon found from its target.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for the kinds whose icons the sources can find.</returns>
    internal bool CanFindIcons(ShortcutKind kind) => _icons.CanFind(kind);

    /// <summary>
    /// Gets the image of an icon a shortcut keeps.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be shown.</returns>
    internal IImage? ShowIcon(ShortcutIcon icon) => _icons.Show(icon);

    /// <summary>
    /// Finds the icon of a shortcut's target — a program's, a website's — which its tile shows instead of its kind's
    /// glyph.
    /// </summary>
    /// <param name="kind">The shortcut's kind.</param>
    /// <param name="target">The shortcut's target.</param>
    /// <returns>The icon, or <see langword="null"/> when none was found or the kind has none to find.</returns>
    internal Task<IconImage?> FindIconAsync(ShortcutKind kind, string target) => _icons.FindAsync(kind, target);

    /// <summary>
    /// Looks a shortcut's icon up again — from the program, or from the website itself — and keeps it. An image the
    /// user chose is only replaced once they have said so; when nothing is found, the user is told and the icon
    /// stays as it was.
    /// </summary>
    /// <param name="shortcut">The tile.</param>
    /// <returns>A task that completes once the icon has been looked up, or the user declined.</returns>
    internal async Task FindShortcutIconAsync(ShortcutViewModel shortcut)
    {
        ShortcutKind kind = shortcut.Kind;

        if (!CanFindIcons(kind))
        {
            return;
        }

        string source = kind switch
        {
            ShortcutKind.Url => "website",
            ShortcutKind.Document => "file",
            _ => "program",
        };

        if ((shortcut.Model.Icon is { IsCustom: true })
            && !await _dialogs.ConfirmAsync(
                "Replace your icon?",
                $"“{shortcut.Name}” uses an image you chose. Look for the {source}'s own icon and use it instead?",
                "Replace"))
        {
            return;
        }

        Shortcut before = shortcut.Model;
        IconImage? found = await _icons.RefreshAsync(kind, before.Target);

        // The tile may have been edited, moved off the page or deleted while its icon was looked up.
        if ((FindShortcut(shortcut.Id) != shortcut) || (shortcut.Model != before))
        {
            return;
        }

        if (found is null)
        {
            _notifications.ShowWarning("No icon found", $"No icon could be found for “{shortcut.Name}”. Its icon is unchanged.");
            return;
        }

        shortcut.SetFoundIcon(found);
        Persist();
    }

    /// <summary>
    /// Saves an icon a tile has just found and taken into its shortcut, so the next session shows it without
    /// looking for it again.
    /// </summary>
    /// <param name="shortcut">The tile.</param>
    /// <remarks>
    /// While the groups are being built the workspace is not whole, and saving it then would drop the groups not
    /// built yet; the save waits for the end of the build. A tile no longer on the page — the workspace was read again
    /// while its icon was looked up — saves nothing: its icon is found again when it is shown again.
    /// </remarks>
    internal void KeepFoundIcon(ShortcutViewModel shortcut)
    {
        if (_isReadingGroups)
        {
            _hasIconsToKeep = true;
            return;
        }

        if (FindShortcut(shortcut.Id) == shortcut)
        {
            Persist();
        }
    }

    /// <summary>
    /// Copies a shortcut's target to the clipboard.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <returns>A task that completes once the clipboard has it.</returns>
    internal Task CopyShortcutTargetAsync(ShortcutViewModel shortcut) => _actions.CopyTargetAsync(shortcut.Model);

    /// <summary>
    /// Whether a dragged group may be dropped next to a group of this page.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="target">The group under the pointer.</param>
    /// <returns><see langword="true"/> for a group of this page other than the target itself, whatever its kind.</returns>
    internal bool CanDropGroup(DragPayload payload, GroupViewModel target)
        => (FindGroup(payload.Id) is { } source) && (source != target) && Groups.Contains(target);

    /// <summary>
    /// Moves a dragged group next to another one, and saves the new order.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="target">The group it was dropped on.</param>
    /// <param name="position">Which side of that group.</param>
    /// <returns><see langword="true"/> when the order changed.</returns>
    internal bool DropGroup(DragPayload payload, GroupViewModel target, DropPosition position)
    {
        if (!MovesGroup(payload, target, position) || (FindGroup(payload.Id) is not { } source))
        {
            return false;
        }

        int from = Groups.IndexOf(source);

        Groups.Move(from, DropPlacement.MoveIndex(from, Groups.IndexOf(target), position));
        Persist();
        return true;
    }

    /// <summary>
    /// Whether dropping a dragged group next to another one would change the order.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="target">The group it would be dropped next to.</param>
    /// <param name="position">Which side of that group.</param>
    /// <returns><see langword="true"/> when the drop is allowed and moves the group; <see langword="false"/> next to its
    /// own place, where nothing would move.</returns>
    internal bool MovesGroup(DragPayload payload, GroupViewModel target, DropPosition position)
    {
        if (!CanDropGroup(payload, target) || (FindGroup(payload.Id) is not { } source))
        {
            return false;
        }

        int from = Groups.IndexOf(source);

        return DropPlacement.MoveIndex(from, Groups.IndexOf(target), position) != from;
    }

    /// <summary>
    /// Whether a dragged shortcut may be dropped on a group of this page, or next to one of its tiles.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="targetGroup">The group under the pointer.</param>
    /// <param name="targetShortcut">The tile under the pointer; <see langword="null"/> for the group's empty space.</param>
    /// <returns><see langword="true"/> for a shortcut of this page dropped on a group of its own kind, anywhere but on
    /// its own tile.</returns>
    internal bool CanDropShortcut(DragPayload payload, GroupViewModel targetGroup, ShortcutViewModel? targetShortcut)
        => (FindShortcut(payload.Id) is { } source)
            && (source.Kind == targetGroup.Kind)
            && (source != targetShortcut)
            && Groups.Contains(targetGroup)
            && ((targetShortcut is null) || (targetShortcut.Group == targetGroup));

    /// <summary>
    /// Moves a dragged shortcut next to a tile, or to the end of a group — its own or another of its kind — and saves.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="targetGroup">The group it was dropped on.</param>
    /// <param name="targetShortcut">The tile it was dropped on; <see langword="null"/> to put it at the group's end.</param>
    /// <param name="position">Which side of the tile; ignored without one.</param>
    /// <returns><see langword="true"/> when anything moved.</returns>
    internal bool DropShortcut(DragPayload payload, GroupViewModel targetGroup, ShortcutViewModel? targetShortcut, DropPosition position)
    {
        if (!MovesShortcut(payload, targetGroup, targetShortcut, position) || (FindShortcut(payload.Id) is not { } source))
        {
            return false;
        }

        GroupViewModel sourceGroup = source.Group;
        int from = sourceGroup.Shortcuts.IndexOf(source);

        if (sourceGroup == targetGroup)
        {
            targetGroup.Shortcuts.Move(from, IndexWithinItsGroup(from, targetGroup, targetShortcut, position));
        }
        else
        {
            int insertAt = targetShortcut is null
                ? targetGroup.Shortcuts.Count
                : DropPlacement.InsertIndex(targetGroup.Shortcuts.IndexOf(targetShortcut), position);

            sourceGroup.Shortcuts.RemoveAt(from);
            source.Group = targetGroup;
            targetGroup.Shortcuts.Insert(insertAt, source);
            targetGroup.IsExpanded = true;
        }

        Persist();
        return true;
    }

    /// <summary>
    /// Whether dropping a dragged shortcut next to a tile, or at a group's end, would move it.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="targetGroup">The group it would be dropped on.</param>
    /// <param name="targetShortcut">The tile it would be dropped next to; <see langword="null"/> for the group's end.</param>
    /// <param name="position">Which side of the tile; ignored without one.</param>
    /// <returns><see langword="true"/> when the drop is allowed and moves the shortcut; <see langword="false"/> next to
    /// its own place, where nothing would move.</returns>
    internal bool MovesShortcut(DragPayload payload, GroupViewModel targetGroup, ShortcutViewModel? targetShortcut, DropPosition position)
    {
        if (!CanDropShortcut(payload, targetGroup, targetShortcut) || (FindShortcut(payload.Id) is not { } source))
        {
            return false;
        }

        if (source.Group != targetGroup)
        {
            return true;
        }

        int from = targetGroup.Shortcuts.IndexOf(source);

        return IndexWithinItsGroup(from, targetGroup, targetShortcut, position) != from;
    }

    /// <summary>
    /// Clears every drop hint and drag mark on the page, once a drag is over or has left.
    /// </summary>
    internal void ClearDragState()
    {
        ClearDropHints();

        foreach (GroupViewModel group in Groups)
        {
            group.IsDragging = false;

            foreach (ShortcutViewModel shortcut in group.Shortcuts)
            {
                shortcut.IsDragging = false;
            }
        }
    }

    /// <summary>
    /// Clears every drop hint on the page, before the one under the pointer is drawn again.
    /// </summary>
    internal void ClearDropHints()
    {
        foreach (GroupViewModel group in Groups)
        {
            group.DropHint = DropHint.None;

            foreach (ShortcutViewModel shortcut in group.Shortcuts)
            {
                shortcut.DropHint = DropHint.None;
            }
        }
    }

    /// <summary>
    /// Finds a group of this page by identity.
    /// </summary>
    /// <param name="id">The group's identity.</param>
    /// <returns>The group, or <see langword="null"/>.</returns>
    internal GroupViewModel? FindGroup(Guid id) => Groups.FirstOrDefault(group => group.Id == id);

    /// <summary>
    /// Finds a shortcut of this page by identity, in whichever group holds it.
    /// </summary>
    /// <param name="id">The shortcut's identity.</param>
    /// <returns>The shortcut, or <see langword="null"/>.</returns>
    internal ShortcutViewModel? FindShortcut(Guid id)
        => Groups.SelectMany(group => group.Shortcuts).FirstOrDefault(shortcut => shortcut.Id == id);

    /// <summary>
    /// Where a shortcut moved within its own group ends up: next to a tile, or last.
    /// </summary>
    private static int IndexWithinItsGroup(int from, GroupViewModel group, ShortcutViewModel? target, DropPosition position)
        => target is null
            ? group.Shortcuts.Count - 1
            : DropPlacement.MoveIndex(from, group.Shortcuts.IndexOf(target), position);

    private static bool SameName(string first, string second)
        => string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    private IEnumerable<string> NamesOfKind(ShortcutKind kind, GroupViewModel? except = null)
        => Groups.Where(group => (group.Kind == kind) && (group != except)).Select(group => group.Name).ToArray();

    private async Task NewGroupAsync(ShortcutKind kind)
    {
        string? name = await _dialogs.PromptNameAsync(
            $"New {GroupKindOption.Noun(kind)} group",
            "Group name",
            "Create",
            null,
            candidate => _validator.ValidateGroupName(candidate, NamesOfKind(kind)));

        if (name is null)
        {
            return;
        }

        Groups.Add(new GroupViewModel(this, new ShortcutGroup(Guid.NewGuid(), name, kind, [])));
        Persist();
    }

    private void ReadGroups(IReadOnlyList<ShortcutGroup> groups)
    {
        _isReadingGroups = true;

        try
        {
            Groups.Clear();

            foreach (ShortcutGroup group in groups)
            {
                Groups.Add(new GroupViewModel(this, group));
            }
        }
        finally
        {
            _isReadingGroups = false;
        }

        // Icons answered at once — already looked up this session — were taken into their shortcuts during the build.
        if (_hasIconsToKeep)
        {
            _hasIconsToKeep = false;
            Persist();
        }
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));

        foreach (GroupViewModel group in Groups)
        {
            group.RefreshPosition();
        }
    }
}

/// <summary>
/// A kind of group, as the New group menu and the group cards name it.
/// </summary>
/// <param name="Kind">The kind.</param>
public sealed record GroupKindOption(ShortcutKind Kind)
{
    /// <summary>
    /// Gets what the menu calls the kind: "Applications", "Folders", "Links", "Documents".
    /// </summary>
    public string Title => Kind switch
    {
        ShortcutKind.Application => "Applications",
        ShortcutKind.Directory => "Folders",
        ShortcutKind.Document => "Documents",
        _ => "Links",
    };

    /// <summary>
    /// Gets the kind's glyph.
    /// </summary>
    public PhosphorIcon Glyph => Glyphs.OfKind(Kind);

    /// <summary>
    /// Gets the kind in lower case, as it reads inside a sentence: "applications", "folders", "links", "documents".
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The word.</returns>
    public static string Noun(ShortcutKind kind) => new GroupKindOption(kind).Title.ToLowerInvariant();

    /// <summary>
    /// Gets "a" or "an" and the kind's noun, as in "an applications group".
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The words.</returns>
    public static string Article(ShortcutKind kind)
    {
        string noun = Noun(kind);
        return ("aeiou".Contains(noun[0], StringComparison.Ordinal) ? "an " : "a ") + noun;
    }
}
