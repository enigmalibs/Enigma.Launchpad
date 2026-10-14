using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// The Workspaces page: the workspaces of the profile in use, as cards, a search over them, and the ways to create,
/// edit, copy, reorder, delete and open them. An open workspace takes the page's place until the user goes back.
/// </summary>
/// <remarks>
/// <para>
/// The page keeps no state of its own beyond the cards, the search and the open workspace: every change goes to the
/// session, and the cards are built again from the session whenever the workspaces change, another profile is put in
/// use, or the page appears.
/// </para>
/// <para>
/// The open workspace is this page's: it is created when a card is opened, told to read its workspace again whenever
/// the session changes under it, and closed when the workspace is gone — deleted, or left behind by a profile switch.
/// </para>
/// </remarks>
public sealed class WorkspacesPageViewModel : ObservableObject, INavigationViewModel
{
    private readonly ILaunchpadSession _session;
    private readonly IShortcutValidator _validator;
    private readonly ILaunchpadDialogs _dialogs;
    private readonly IShortcutActions _actions;
    private readonly IShortcutIcons _icons;
    private readonly IUserNotifications _notifications;

    private WorkspaceItemViewModel[] _all = [];

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names, titles and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    /// <param name="icons">The icons of the tiles: kept with the shortcuts, or found from their targets.</param>
    /// <param name="notifications">Where a failed icon lookup or a refused move is reported.</param>
    public WorkspacesPageViewModel(
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutActions actions,
        IShortcutIcons icons,
        IUserNotifications notifications)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(notifications);

        _session = session;
        _validator = validator;
        _dialogs = dialogs;
        _actions = actions;
        _icons = icons;
        _notifications = notifications;

        _session.WorkspacesChanged += OnWorkspacesChanged;
        _session.ActiveProfileChanged += OnWorkspacesChanged;
        NewWorkspaceCommand = new AsyncRelayCommand(NewWorkspaceAsync, () => IsLoaded);
    }

    /// <summary>
    /// Gets the cards the search lets through, in the profile's order.
    /// </summary>
    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    /// <summary>
    /// Gets or sets the search: the cards shown are those whose title or description holds it, ignoring case.
    /// </summary>
    public string SearchText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ShowMatches();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the workspaces have been read from the session.
    /// </summary>
    public bool IsLoaded
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                NewWorkspaceCommand.NotifyCanExecuteChanged();
                RaiseStates();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the profile in use has any workspace, which is when there is something to
    /// search.
    /// </summary>
    public bool HasWorkspaces => _all.Length > 0;

    /// <summary>
    /// Gets a value indicating whether the profile in use has no workspace, which is when the page invites the user to
    /// create one.
    /// </summary>
    public bool IsEmpty => IsLoaded && !HasWorkspaces;

    /// <summary>
    /// Gets a value indicating whether the search lets no card through.
    /// </summary>
    public bool HasNoMatch => HasWorkspaces && (Workspaces.Count == 0);

    /// <summary>
    /// Gets what the page says when the search lets no card through.
    /// </summary>
    public string NoMatchText => $"No workspace matches “{SearchText.Trim()}”.";

    /// <summary>
    /// Gets the command creating a workspace at the end of the list.
    /// </summary>
    public AsyncRelayCommand NewWorkspaceCommand { get; }

    /// <summary>
    /// Gets the workspace open in place of the list; <see langword="null"/> while the list is shown.
    /// </summary>
    public WorkspacePageViewModel? OpenWorkspace
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsWorkspaceOpen));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a workspace is open in place of the list.
    /// </summary>
    public bool IsWorkspaceOpen => OpenWorkspace is not null;

    /// <inheritdoc />
    public async Task OnAppearingAsync(object? parameter = null)
    {
        await _session.LoadAsync();

        ReadWorkspaces();
        IsLoaded = true;
    }

    /// <summary>
    /// Opens a workspace in place of the list.
    /// </summary>
    /// <param name="workspace">The workspace's card.</param>
    internal void Open(WorkspaceItemViewModel workspace)
    {
        // A card can outlive its workspace for as long as an event takes to arrive; there is nothing to open then.
        if (_session.Workspaces.All(candidate => candidate.Id != workspace.Id))
        {
            return;
        }

        OpenWorkspace = new WorkspacePageViewModel(workspace.Id, _session, _validator, _dialogs, _actions, _icons, _notifications, Close);
    }

    /// <summary>
    /// Goes back from the open workspace to the list, whose cards then show what the workspace holds now.
    /// </summary>
    internal void Close()
    {
        OpenWorkspace = null;
        ReadWorkspaces();
    }

    /// <inheritdoc />
    public Task<bool> OnDisappearingAsync() => Task.FromResult(true);

    /// <summary>
    /// Asks for a workspace's new title, description and glyph, and applies them.
    /// </summary>
    /// <param name="workspace">The workspace to edit.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task EditAsync(WorkspaceItemViewModel workspace)
    {
        if (_session.Workspaces.FirstOrDefault(candidate => candidate.Id == workspace.Id) is not { } stored)
        {
            return;
        }

        WorkspaceDetails? details = await _dialogs.EditWorkspaceAsync(stored, TitlesExcept(stored.Id));

        if ((details is null) || (details == new WorkspaceDetails(stored.Title, stored.Description, Glyphs.OfWorkspace(stored.Icon).ToString())))
        {
            return;
        }

        _session.UpdateWorkspace(stored.Id, details.Title, details.Description, details.Icon);
    }

    /// <summary>
    /// Asks for a title and copies a workspace under it, right after the original.
    /// </summary>
    /// <param name="workspace">The workspace to copy.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task DuplicateAsync(WorkspaceItemViewModel workspace)
    {
        string[] titles = [.. _session.Workspaces.Select(candidate => candidate.Title)];

        string? title = await _dialogs.PromptNameAsync(
            "Duplicate workspace",
            "Title of the copy",
            "Duplicate",
            ProfilesPageViewModel.CopyName(workspace.Title, titles),
            candidate => _validator.ValidateWorkspaceTitle(candidate, titles));

        if (title is null)
        {
            return;
        }

        _session.DuplicateWorkspace(workspace.Id, title);
    }

    /// <summary>
    /// Deletes a workspace with everything in it, once the user has confirmed.
    /// </summary>
    /// <param name="workspace">The workspace to delete.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task DeleteAsync(WorkspaceItemViewModel workspace)
    {
        string message = workspace.GroupCount == 0
            ? "This cannot be undone."
            : $"Its {WorkspaceItemViewModel.Summarise(workspace.GroupCount, workspace.ShortcutCount)} will be deleted with it. This cannot be undone.";

        if (!await _dialogs.ConfirmDeleteAsync($"Delete the workspace “{workspace.Title}”?", message, "Delete"))
        {
            return;
        }

        // It may have gone while the question was open — deleted, or its profile put away.
        if (_session.Workspaces.Any(candidate => candidate.Id == workspace.Id))
        {
            _session.DeleteWorkspace(workspace.Id);
        }
    }

    /// <summary>
    /// Moves a workspace by an offset in the list.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="offset">How many places, negative for up.</param>
    internal void Move(WorkspaceItemViewModel workspace, int offset)
    {
        if (_session.CanMoveWorkspace(workspace.Id, offset))
        {
            _session.MoveWorkspace(workspace.Id, offset);
        }
    }

    private async Task NewWorkspaceAsync()
    {
        WorkspaceDetails? details = await _dialogs.EditWorkspaceAsync(null, TitlesExcept(Guid.Empty));

        if (details is null)
        {
            return;
        }

        _session.AddWorkspace(details.Title, details.Description, details.Icon);
    }

    private IEnumerable<string> TitlesExcept(Guid id)
        => _session.Workspaces.Where(workspace => workspace.Id != id).Select(workspace => workspace.Title).ToArray();

    private void ReadWorkspaces()
    {
        IReadOnlyList<Workspace> workspaces = _session.Workspaces;

        _all = workspaces
            .Select(workspace => new WorkspaceItemViewModel(
                this,
                workspace,
                canMoveUp: _session.CanMoveWorkspace(workspace.Id, -1),
                canMoveDown: _session.CanMoveWorkspace(workspace.Id, 1)))
            .ToArray();

        ShowMatches();
    }

    private void ShowMatches()
    {
        string search = SearchText.Trim();

        Workspaces.Clear();

        foreach (WorkspaceItemViewModel workspace in _all.Where(workspace => workspace.Matches(search)))
        {
            Workspaces.Add(workspace);
        }

        RaiseStates();
    }

    private void RaiseStates()
    {
        OnPropertyChanged(nameof(HasWorkspaces));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoMatch));
        OnPropertyChanged(nameof(NoMatchText));
    }

    // A page not shown yet reads the session when it first appears.
    private void OnWorkspacesChanged(object? sender, EventArgs e)
    {
        if (IsLoaded)
        {
            ReadWorkspaces();
        }

        if ((OpenWorkspace is { } open) && !open.Reload())
        {
            OpenWorkspace = null;
        }
    }
}
