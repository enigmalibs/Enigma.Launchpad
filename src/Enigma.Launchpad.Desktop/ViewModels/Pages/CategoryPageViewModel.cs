using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One category's page: its title and the groups of shortcuts it holds.
/// </summary>
/// <remarks>
/// <para>
/// The three categories share one view and this base; each concrete ViewModel only says which category it
/// is, so the rail can resolve each page by its own type.
/// </para>
/// <para>
/// The page's groups are the live state of its category in the profile in use. Every change is handed to the
/// session as a fresh snapshot of the whole category, which the session saves in the background; when another
/// profile is put in use, a loaded page reads its groups again.
/// </para>
/// </remarks>
public abstract class CategoryPageViewModel : ObservableObject, INavigationViewModel
{
    private readonly ILaunchpadSession _session;
    private readonly IShortcutValidator _validator;
    private readonly ILaunchpadDialogs _dialogs;
    private readonly IShortcutActions _actions;
    private readonly IShortcutIcons _icons;
    private readonly IUserNotifications _notifications;

    // Set while the groups are being built from the session, when the category is not whole yet.
    private bool _isReadingGroups;
    private bool _hasIconsToKeep;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="kind">The category the page shows.</param>
    /// <param name="title">The page's title.</param>
    /// <param name="description">One line under the title saying what the category holds.</param>
    /// <param name="icon">The category's icon.</param>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    /// <param name="icons">The icons of the tiles: kept with the shortcuts, or found from their targets.</param>
    /// <param name="notifications">Where a failed icon lookup is reported.</param>
    protected CategoryPageViewModel(
        ShortcutKind kind,
        string title,
        string description,
        PhosphorIcon icon,
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutActions actions,
        IShortcutIcons icons,
        IUserNotifications notifications)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(notifications);

        Kind = kind;
        Title = title;
        Description = description;
        Icon = icon;
        _session = session;
        _validator = validator;
        _dialogs = dialogs;
        _actions = actions;
        _icons = icons;
        _notifications = notifications;

        Groups.CollectionChanged += OnGroupsChanged;
        _session.ActiveProfileChanged += OnActiveProfileChanged;
        NewGroupCommand = new AsyncRelayCommand(NewGroupAsync, () => IsLoaded);
    }

    /// <summary>
    /// Gets the category the page shows.
    /// </summary>
    public ShortcutKind Kind { get; }

    /// <summary>
    /// Gets the page's title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the line under the title saying what the category holds.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the category's icon.
    /// </summary>
    public PhosphorIcon Icon { get; }

    /// <summary>
    /// Gets the category's groups, in display order.
    /// </summary>
    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the category has been read from the session.
    /// </summary>
    public bool IsLoaded
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                NewGroupCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the category is loaded and has no group, which is when the page invites
    /// the user to create one.
    /// </summary>
    public bool IsEmpty => IsLoaded && (Groups.Count == 0);

    /// <summary>
    /// Gets the command creating a group, at the end of the page.
    /// </summary>
    public AsyncRelayCommand NewGroupCommand { get; }

    /// <inheritdoc />
    public async Task OnAppearingAsync(object? parameter = null)
    {
        if (IsLoaded)
        {
            return;
        }

        await _session.LoadAsync();

        ReadGroups();
        IsLoaded = true;
    }

    /// <inheritdoc />
    public Task<bool> OnDisappearingAsync() => Task.FromResult(true);

    /// <summary>
    /// Hands the category's current state to the session, which saves it.
    /// </summary>
    internal void Persist() => _session.Update(Kind, Groups.Select(group => group.ToModel()).ToArray());

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
            candidate => _validator.ValidateGroupName(candidate, Groups.Where(other => other != group).Select(other => other.Name)));

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
    /// <param name="offset">How many places, negative for up.</param>
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
    /// <param name="offset">How many places, negative for up.</param>
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
    /// Asks for a new shortcut and adds it at the end of a group, which is unfolded to show it.
    /// </summary>
    /// <param name="group">The group to add to.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task AddShortcutAsync(GroupViewModel group)
    {
        Shortcut? shortcut = await _dialogs.EditShortcutAsync(Kind, null);

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
        Shortcut? edited = await _dialogs.EditShortcutAsync(Kind, shortcut.Model);

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
        if (!await _dialogs.ConfirmDeleteAsync($"Delete the shortcut \u201C{shortcut.Name}\u201D?", "This cannot be undone.", "Delete"))
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
    internal void OpenShortcut(ShortcutViewModel shortcut) => _actions.Open(Kind, shortcut.Model);

    /// <summary>
    /// Asks which arguments to run an application with this once — starting from its default arguments — and runs
    /// it with them. The shortcut itself is not changed.
    /// </summary>
    /// <param name="shortcut">The application's tile.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task RunWithArgumentsAsync(ShortcutViewModel shortcut)
    {
        if (Kind != ShortcutKind.Application)
        {
            return;
        }

        Shortcut model = shortcut.Model;
        string? arguments = await _dialogs.PromptArgumentsAsync(model);

        if (arguments is null)
        {
            return;
        }

        _actions.Open(Kind, model with { Arguments = arguments.Length == 0 ? null : arguments });
    }

    /// <summary>
    /// Opens a terminal in a directory shortcut's folder; a failure is shown to the user.
    /// </summary>
    /// <param name="shortcut">The folder's tile.</param>
    internal void OpenInTerminal(ShortcutViewModel shortcut)
    {
        if (Kind == ShortcutKind.Directory)
        {
            _actions.OpenInTerminal(shortcut.Model);
        }
    }

    /// <summary>
    /// Shows a shortcut in its folder; a failure is shown to the user.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    internal void RevealShortcut(ShortcutViewModel shortcut) => _actions.Reveal(Kind, shortcut.Model);

    /// <summary>
    /// Gets a value indicating whether the category's tiles can have their icon found from their targets.
    /// </summary>
    internal bool CanFindIcons => _icons.CanFind(Kind);

    /// <summary>
    /// Gets the image of an icon a shortcut keeps.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be shown.</returns>
    internal IImage? ShowIcon(ShortcutIcon icon) => _icons.Show(icon);

    /// <summary>
    /// Finds the icon of a shortcut's target — a program's, a website's — which its tile shows instead of the
    /// category's glyph.
    /// </summary>
    /// <param name="target">The shortcut's target.</param>
    /// <returns>The icon, or <see langword="null"/> when none was found or the category has none to find.</returns>
    internal Task<IconImage?> FindIconAsync(string target) => _icons.FindAsync(Kind, target);

    /// <summary>
    /// Looks a shortcut's icon up again — from the program, or from the website itself — and keeps it. An image the
    /// user chose is only replaced once they have said so; when nothing is found, the user is told and the icon
    /// stays as it was.
    /// </summary>
    /// <param name="shortcut">The tile.</param>
    /// <returns>A task that completes once the icon has been looked up, or the user declined.</returns>
    internal async Task FindShortcutIconAsync(ShortcutViewModel shortcut)
    {
        if (!CanFindIcons)
        {
            return;
        }

        string source = Kind == ShortcutKind.Url ? "website" : "program";

        if ((shortcut.Model.Icon is { IsCustom: true })
            && !await _dialogs.ConfirmAsync(
                "Replace your icon?",
                $"\u201C{shortcut.Name}\u201D uses an image you chose. Look for the {source}'s own icon and use it instead?",
                "Replace"))
        {
            return;
        }

        Shortcut before = shortcut.Model;
        IconImage? found = await _icons.RefreshAsync(Kind, before.Target);

        // The tile may have been edited, moved off the page or deleted while its icon was looked up.
        if ((FindShortcut(shortcut.Id) != shortcut) || (shortcut.Model != before))
        {
            return;
        }

        if (found is null)
        {
            _notifications.ShowWarning("No icon found", $"No icon could be found for \u201C{shortcut.Name}\u201D. Its icon is unchanged.");
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
    /// While the groups are being built the category is not whole, and saving it then would drop the groups not
    /// built yet; the save waits for the end of the build. A tile no longer on the page — the profile changed while
    /// its icon was looked up — saves nothing: its icon is found again when the profile is shown again.
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
    /// <returns><see langword="true"/> for a group of this category other than the target itself.</returns>
    internal bool CanDropGroup(DragPayload payload, GroupViewModel target)
        => (payload.Kind == Kind) && (FindGroup(payload.Id) is { } source) && (source != target) && Groups.Contains(target);

    /// <summary>
    /// Moves a dragged group next to another one, and saves the new order.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="target">The group it was dropped on.</param>
    /// <param name="position">Which side of that group.</param>
    /// <returns><see langword="true"/> when the order changed.</returns>
    internal bool DropGroup(DragPayload payload, GroupViewModel target, DropPosition position)
    {
        if (!CanDropGroup(payload, target) || (FindGroup(payload.Id) is not { } source))
        {
            return false;
        }

        int from = Groups.IndexOf(source);
        int to = DropPlacement.MoveIndex(from, Groups.IndexOf(target), position);

        if (to == from)
        {
            return false;
        }

        Groups.Move(from, to);
        Persist();
        return true;
    }

    /// <summary>
    /// Whether a dragged shortcut may be dropped on a group of this page, or next to one of its tiles.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="targetGroup">The group under the pointer.</param>
    /// <param name="targetShortcut">The tile under the pointer; <see langword="null"/> for the group's empty space.</param>
    /// <returns><see langword="true"/> for a shortcut of this category dropped anywhere but on its own tile.</returns>
    internal bool CanDropShortcut(DragPayload payload, GroupViewModel targetGroup, ShortcutViewModel? targetShortcut)
        => (payload.Kind == Kind)
            && (FindShortcut(payload.Id) is { } source)
            && (source != targetShortcut)
            && Groups.Contains(targetGroup)
            && ((targetShortcut is null) || (targetShortcut.Group == targetGroup));

    /// <summary>
    /// Moves a dragged shortcut next to a tile, or to the end of a group — its own or another of this
    /// category — and saves.
    /// </summary>
    /// <param name="payload">What the drag carries.</param>
    /// <param name="targetGroup">The group it was dropped on.</param>
    /// <param name="targetShortcut">The tile it was dropped on; <see langword="null"/> to put it at the group's end.</param>
    /// <param name="position">Which side of the tile; ignored without one.</param>
    /// <returns><see langword="true"/> when anything moved.</returns>
    internal bool DropShortcut(DragPayload payload, GroupViewModel targetGroup, ShortcutViewModel? targetShortcut, DropPosition position)
    {
        if (!CanDropShortcut(payload, targetGroup, targetShortcut) || (FindShortcut(payload.Id) is not { } source))
        {
            return false;
        }

        GroupViewModel sourceGroup = source.Group;
        int from = sourceGroup.Shortcuts.IndexOf(source);

        if (sourceGroup == targetGroup)
        {
            int to = targetShortcut is null
                ? targetGroup.Shortcuts.Count - 1
                : DropPlacement.MoveIndex(from, targetGroup.Shortcuts.IndexOf(targetShortcut), position);

            if (to == from)
            {
                return false;
            }

            targetGroup.Shortcuts.Move(from, to);
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

    private async Task NewGroupAsync()
    {
        string? name = await _dialogs.PromptNameAsync(
            "New group",
            "Group name",
            "Create",
            null,
            candidate => _validator.ValidateGroupName(candidate, Groups.Select(group => group.Name)));

        if (name is null)
        {
            return;
        }

        Groups.Add(new GroupViewModel(this, new ShortcutGroup(Guid.NewGuid(), name, [])));
        Persist();
    }

    private void ReadGroups()
    {
        _isReadingGroups = true;

        try
        {
            Groups.Clear();

            foreach (ShortcutGroup group in _session.GroupsOf(Kind))
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

    // A page not shown yet reads the profile in use when it first appears; only a loaded one has stale groups.
    private void OnActiveProfileChanged(object? sender, EventArgs e)
    {
        if (IsLoaded)
        {
            ReadGroups();
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
