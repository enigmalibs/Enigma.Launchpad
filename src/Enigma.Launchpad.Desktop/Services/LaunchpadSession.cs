using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// The launchpad document the running application works on: loaded once, and saved after every change.
/// </summary>
/// <remarks>
/// The workspace accessors act on the profile in use. The profile and workspace operations change the document like
/// any other edit, and announce themselves through <see cref="ProfilesChanged"/>, <see cref="ActiveProfileChanged"/>
/// and <see cref="WorkspacesChanged"/>, raised on the thread that made the change once it is in place. An open
/// workspace saving its own groups (<see cref="UpdateGroups"/>) announces nothing: it is the one that changed them.
/// </remarks>
public interface ILaunchpadSession
{
    /// <summary>
    /// Raised after a profile was added, renamed, moved, deleted or imported into, or another one was put in use.
    /// </summary>
    event EventHandler? ProfilesChanged;

    /// <summary>
    /// Raised after the profile in use changed under the pages: another one was put in use — chosen, or because the
    /// one in use was deleted — or shortcuts were imported into it. Raised after <see cref="ProfilesChanged"/>.
    /// </summary>
    event EventHandler? ActiveProfileChanged;

    /// <summary>
    /// Raised after a workspace of the profile in use was added, edited, copied, moved or deleted, or a group was moved
    /// from one workspace to another.
    /// </summary>
    event EventHandler? WorkspacesChanged;

    /// <summary>
    /// Gets the profiles, in display order.
    /// </summary>
    IReadOnlyList<Profile> Profiles { get; }

    /// <summary>
    /// Gets the profile in use.
    /// </summary>
    Profile ActiveProfile { get; }

    /// <summary>
    /// Gets the workspaces of the profile in use, in display order.
    /// </summary>
    IReadOnlyList<Workspace> Workspaces { get; }

    /// <summary>
    /// Loads the document. Only the first call reads the file; later ones return the same task.
    /// </summary>
    /// <returns>A task that completes once the document is available, even when it could not be read.</returns>
    Task LoadAsync();

    /// <summary>
    /// Replaces a workspace's groups and saves the document in the background.
    /// </summary>
    /// <param name="workspaceId">The workspace, in the profile in use.</param>
    /// <param name="groups">Its groups now, in display order.</param>
    /// <exception cref="ArgumentException">The profile in use has no such workspace.</exception>
    void UpdateGroups(Guid workspaceId, IReadOnlyList<ShortcutGroup> groups);

    /// <summary>
    /// Moves a group, with its shortcuts, from one workspace of the profile in use to the end of another.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="fromWorkspaceId">The workspace holding it.</param>
    /// <param name="toWorkspaceId">The workspace taking it.</param>
    /// <exception cref="ArgumentException">A workspace or the group is unknown, or the two workspaces are the same.</exception>
    /// <exception cref="InvalidOperationException">The target already has a group of the same name and kind.</exception>
    void MoveGroup(Guid groupId, Guid fromWorkspaceId, Guid toWorkspaceId);

    /// <summary>
    /// Adds an empty workspace at the end of the profile in use.
    /// </summary>
    /// <param name="title">Its title, already validated.</param>
    /// <param name="description">What it is for, already validated; <see langword="null"/> or blank for nothing.</param>
    /// <param name="icon">The name of its glyph.</param>
    /// <returns>The new workspace.</returns>
    Workspace AddWorkspace(string title, string? description, string icon);

    /// <summary>
    /// Changes a workspace's title, description and glyph; its groups stay as they are.
    /// </summary>
    /// <param name="id">The workspace, in the profile in use.</param>
    /// <param name="title">Its new title, already validated.</param>
    /// <param name="description">What it is for, already validated; <see langword="null"/> or blank for nothing.</param>
    /// <param name="icon">The name of its glyph.</param>
    /// <exception cref="ArgumentException">The profile in use has no such workspace.</exception>
    void UpdateWorkspace(Guid id, string title, string? description, string icon);

    /// <summary>
    /// Adds a copy of a workspace — its groups and shortcuts under new identities — right after it.
    /// </summary>
    /// <param name="id">The workspace to copy, in the profile in use.</param>
    /// <param name="title">The copy's title, already validated.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">The profile in use has no such workspace.</exception>
    Workspace DuplicateWorkspace(Guid id, string title);

    /// <summary>
    /// Deletes a workspace with its groups and shortcuts.
    /// </summary>
    /// <param name="id">The workspace, in the profile in use.</param>
    /// <exception cref="ArgumentException">The profile in use has no such workspace.</exception>
    void DeleteWorkspace(Guid id);

    /// <summary>
    /// Whether a workspace can move by an offset without leaving the list.
    /// </summary>
    /// <param name="id">The workspace.</param>
    /// <param name="offset">How many places, negative for up.</param>
    /// <returns><see langword="true"/> when the move stays in the list.</returns>
    bool CanMoveWorkspace(Guid id, int offset);

    /// <summary>
    /// Moves a workspace by an offset in the list.
    /// </summary>
    /// <param name="id">The workspace.</param>
    /// <param name="offset">How many places, negative for up.</param>
    void MoveWorkspace(Guid id, int offset);

    /// <summary>
    /// Puts another profile in use. Nothing happens when it already is.
    /// </summary>
    /// <param name="id">The profile to use.</param>
    void SwitchProfile(Guid id);

    /// <summary>
    /// Adds an empty profile at the end of the list. The profile in use does not change.
    /// </summary>
    /// <param name="name">Its name, already validated.</param>
    /// <returns>The new profile.</returns>
    Profile AddProfile(string name);

    /// <summary>
    /// Adds a copy of a profile — its groups and shortcuts under new identities — right after it.
    /// </summary>
    /// <param name="sourceId">The profile to copy.</param>
    /// <param name="name">The copy's name, already validated.</param>
    /// <returns>The copy.</returns>
    Profile DuplicateProfile(Guid sourceId, string name);

    /// <summary>
    /// Renames a profile.
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="name">Its new name, already validated.</param>
    void RenameProfile(Guid id, string name);

    /// <summary>
    /// Deletes a profile with its groups and shortcuts; when it was in use, its neighbour takes over.
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <exception cref="InvalidOperationException">It is the only profile.</exception>
    void DeleteProfile(Guid id);

    /// <summary>
    /// Whether a profile can move by an offset without leaving the list.
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="offset">How many places, negative for up.</param>
    /// <returns><see langword="true"/> when the move stays in the list.</returns>
    bool CanMoveProfile(Guid id, int offset);

    /// <summary>
    /// Moves a profile by an offset in the list.
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="offset">How many places, negative for up.</param>
    void MoveProfile(Guid id, int offset);

    /// <summary>
    /// Imports workspaces, groups and shortcuts into a profile, merged as <see cref="Profile.Import"/> does.
    /// </summary>
    /// <param name="id">The profile receiving them.</param>
    /// <param name="content">What to import.</param>
    void ImportIntoProfile(Guid id, Profile content);

    /// <summary>
    /// Waits for every change made so far to be written.
    /// </summary>
    /// <returns>A task that completes once the last pending save has finished, successfully or not.</returns>
    Task FlushAsync();
}

/// <summary>
/// Default <see cref="ILaunchpadSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// Saves run one after the other on the thread pool, chained, and each writes the <em>latest</em> document
/// rather than the one current when it was asked for: changes made while a save is under way are folded into
/// a single save after it, so the file never goes back in time and a burst of changes costs two writes, not
/// one per change. Because the chain never needs the UI thread, the application can wait for it at exit.
/// </para>
/// <para>
/// The session is deliberately thread-agnostic — its state is behind a lock, and its notifications marshal
/// to the UI thread themselves — which is why it awaits with <c>ConfigureAwait(false)</c> where the rest of
/// the application does not.
/// </para>
/// <para>
/// A document that could not be read at all — the file exists but cannot be opened — puts the session in a
/// read-only state: the user can work, but nothing is written, so a transient error can never replace their
/// shortcuts with an empty document.
/// </para>
/// </remarks>
public sealed class LaunchpadSession : ILaunchpadSession
{
    private readonly ILaunchpadStore _store;
    private readonly IUserNotifications _notifications;
    private readonly ILogger<LaunchpadSession> _logger;
    private readonly object _sync = new();

    private LaunchpadData _current = LaunchpadData.Empty;
    private Task? _load;
    private Task _saving = Task.CompletedTask;
    private bool _saveQueued;
    private bool _isReadOnly;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="store">Where the document lives.</param>
    /// <param name="notifications">Where the user is told about a file that could not be read or written.</param>
    /// <param name="logger">Where the same is logged.</param>
    public LaunchpadSession(ILaunchpadStore store, IUserNotifications notifications, ILogger<LaunchpadSession> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? ProfilesChanged;

    /// <inheritdoc />
    public event EventHandler? ActiveProfileChanged;

    /// <inheritdoc />
    public event EventHandler? WorkspacesChanged;

    /// <inheritdoc />
    public IReadOnlyList<Profile> Profiles
    {
        get
        {
            lock (_sync)
            {
                return _current.Profiles;
            }
        }
    }

    /// <inheritdoc />
    public Profile ActiveProfile
    {
        get
        {
            lock (_sync)
            {
                return _current.ActiveProfile;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Workspace> Workspaces
    {
        get
        {
            lock (_sync)
            {
                return _current.ActiveProfile.Workspaces;
            }
        }
    }

    /// <inheritdoc />
    public Task LoadAsync()
    {
        lock (_sync)
        {
            return _load ??= LoadCoreAsync();
        }
    }

    /// <inheritdoc />
    public void UpdateGroups(Guid workspaceId, IReadOnlyList<ShortcutGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        lock (_sync)
        {
            EnsureLoaded();

            Profile profile = _current.ActiveProfile;
            Workspace workspace = RequireWorkspace(profile, workspaceId);

            _current = _current.WithProfile(profile.ReplaceWorkspace(workspace with { Groups = groups }));
            QueueSave();
        }
    }

    /// <inheritdoc />
    public void MoveGroup(Guid groupId, Guid fromWorkspaceId, Guid toWorkspaceId)
    {
        if (fromWorkspaceId == toWorkspaceId)
        {
            throw new ArgumentException("A group is moved to another workspace.", nameof(toWorkspaceId));
        }

        ChangeWorkspaces(profile =>
        {
            Workspace source = RequireWorkspace(profile, fromWorkspaceId);
            Workspace target = RequireWorkspace(profile, toWorkspaceId);
            ShortcutGroup group = source.Groups.FirstOrDefault(candidate => candidate.Id == groupId)
                ?? throw new ArgumentException("No group of this workspace has this identity.", nameof(groupId));

            if (target.Groups.Any(existing => (existing.Kind == group.Kind)
                && string.Equals(existing.Name.Trim(), group.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The workspace already has a group of this name and kind.");
            }

            return profile
                .ReplaceWorkspace(source with { Groups = source.Groups.Where(candidate => candidate.Id != groupId).ToArray() })
                .ReplaceWorkspace(target with { Groups = [.. target.Groups, group] });
        });
    }

    /// <inheritdoc />
    public Workspace AddWorkspace(string title, string? description, string icon)
    {
        Workspace workspace = Workspace.Create(title, Blank(description), icon);
        ChangeWorkspaces(profile => profile.AddWorkspace(workspace));
        return workspace;
    }

    /// <inheritdoc />
    public void UpdateWorkspace(Guid id, string title, string? description, string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        ChangeWorkspaces(profile => profile.ReplaceWorkspace(
            (profile.FindWorkspace(id) ?? throw new ArgumentException("No workspace has this identity.", nameof(id))) with
            {
                Title = title,
                Description = Blank(description),
                Icon = icon,
            }));
    }

    /// <inheritdoc />
    public Workspace DuplicateWorkspace(Guid id, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Workspace? copy = null;

        ChangeWorkspaces(profile =>
        {
            Workspace source = profile.FindWorkspace(id) ?? throw new ArgumentException("No workspace has this identity.", nameof(id));
            copy = Profile.Copy(source) with { Title = title };

            int index = 0;

            while (profile.Workspaces[index].Id != id)
            {
                index++;
            }

            return profile.AddWorkspace(copy, index + 1);
        });

        return copy ?? throw new InvalidOperationException("The workspace was not copied.");
    }

    /// <inheritdoc />
    public void DeleteWorkspace(Guid id) => ChangeWorkspaces(profile => profile.RemoveWorkspace(id));

    /// <inheritdoc />
    public bool CanMoveWorkspace(Guid id, int offset)
    {
        lock (_sync)
        {
            return _current.ActiveProfile.CanMoveWorkspace(id, offset);
        }
    }

    /// <inheritdoc />
    public void MoveWorkspace(Guid id, int offset) => ChangeWorkspaces(profile => profile.MoveWorkspace(id, offset));

    /// <inheritdoc />
    public void SwitchProfile(Guid id)
    {
        lock (_sync)
        {
            EnsureLoaded();

            if (_current.ActiveProfile.Id == id)
            {
                return;
            }
        }

        ChangeProfiles(data => data.WithActiveProfile(id));
    }

    /// <inheritdoc />
    public Profile AddProfile(string name)
    {
        Profile profile = Profile.Create(name);
        ChangeProfiles(data => data.AddProfile(profile));
        return profile;
    }

    /// <inheritdoc />
    public Profile DuplicateProfile(Guid sourceId, string name)
    {
        Profile? copy = null;

        ChangeProfiles(data =>
        {
            Profile source = data.FindProfile(sourceId) ?? throw new ArgumentException("No profile has this identity.", nameof(sourceId));
            copy = source.Duplicate(name);

            int index = 0;

            while (data.Profiles[index].Id != sourceId)
            {
                index++;
            }

            return data.AddProfile(copy, index + 1);
        });

        return copy ?? throw new InvalidOperationException("The profile was not copied.");
    }

    /// <inheritdoc />
    public void RenameProfile(Guid id, string name) => ChangeProfiles(data => data.RenameProfile(id, name));

    /// <inheritdoc />
    public void DeleteProfile(Guid id) => ChangeProfiles(data => data.RemoveProfile(id));

    /// <inheritdoc />
    public bool CanMoveProfile(Guid id, int offset)
    {
        lock (_sync)
        {
            return _current.CanMoveProfile(id, offset);
        }
    }

    /// <inheritdoc />
    public void MoveProfile(Guid id, int offset) => ChangeProfiles(data => data.MoveProfile(id, offset));

    /// <inheritdoc />
    public void ImportIntoProfile(Guid id, Profile content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ChangeProfiles(data => data.ImportInto(id, content), groupsChangedIn: id);
    }

    /// <inheritdoc />
    public Task FlushAsync()
    {
        lock (_sync)
        {
            return _saving;
        }
    }

    /// <summary>
    /// Applies a change to the profiles, saves, and tells the listeners once the change is in place. A change that
    /// throws leaves the document as it was.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="groupsChangedIn">
    /// The profile whose workspaces the change replaced, if any. When it is the profile in use, the pages are told as
    /// for a switch: an open workspace holds its groups, and would otherwise write its stale copy back on the next edit.
    /// </param>
    private void ChangeProfiles(Func<LaunchpadData, LaunchpadData> change, Guid? groupsChangedIn = null)
    {
        bool activeChanged;

        lock (_sync)
        {
            EnsureLoaded();

            LaunchpadData before = _current;
            _current = change(before);
            activeChanged = (_current.ActiveProfile.Id != before.ActiveProfile.Id) || (groupsChangedIn == _current.ActiveProfile.Id);
            QueueSave();
        }

        // Outside the lock: a listener reads the session back, and may change it again.
        ProfilesChanged?.Invoke(this, EventArgs.Empty);

        if (activeChanged)
        {
            ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static Workspace RequireWorkspace(Profile profile, Guid id)
        => profile.FindWorkspace(id) ?? throw new ArgumentException("No workspace has this identity.", nameof(id));

    /// <summary>
    /// Applies a change to the workspaces of the profile in use, saves, and tells the listeners once the change is in
    /// place. A change that throws leaves the document as it was.
    /// </summary>
    /// <param name="change">The change, given the profile in use and returning its new version.</param>
    private void ChangeWorkspaces(Func<Profile, Profile> change)
    {
        lock (_sync)
        {
            EnsureLoaded();
            _current = _current.WithProfile(change(_current.ActiveProfile));
            QueueSave();
        }

        // Outside the lock: a listener reads the session back.
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Refuses a change before the document has been read, which would be lost on the read. Called under the lock.
    /// </summary>
    private void EnsureLoaded()
    {
        if (_load is not { IsCompleted: true })
        {
            throw new InvalidOperationException("The launchpad is changed before it was loaded.");
        }
    }

    /// <summary>
    /// Chains a save of the latest document, unless one is already waiting or the session is read-only. Called
    /// under the lock.
    /// </summary>
    private void QueueSave()
    {
        if (_isReadOnly || _saveQueued)
        {
            return;
        }

        _saveQueued = true;
        _saving = _saving.ContinueWith(_ => SaveLatestAsync(), TaskScheduler.Default).Unwrap();
    }

    private async Task LoadCoreAsync()
    {
        try
        {
            LaunchpadLoadResult result = await _store.LoadAsync().ConfigureAwait(false);

            lock (_sync)
            {
                _current = result.Data;
            }

            if (result.UnreadableFileMovedTo is { } movedTo)
            {
                _notifications.ShowWarning(
                    "Your shortcuts could not be read",
                    $"The file was kept as {movedTo}, and the launchpad starts empty.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "The launchpad file {Path} could not be read", _store.FilePath);

            lock (_sync)
            {
                _isReadOnly = true;
            }

            _notifications.ShowError(
                "Your shortcuts could not be read",
                $"{exception.Message} Nothing will be saved until the application is restarted.");
        }
    }

    private async Task SaveLatestAsync()
    {
        LaunchpadData snapshot;

        lock (_sync)
        {
            _saveQueued = false;
            snapshot = _current;
        }

        try
        {
            await _store.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "The launchpad file {Path} could not be written", _store.FilePath);
            _notifications.ShowError("Your changes could not be saved", exception.Message);
        }
    }
}
