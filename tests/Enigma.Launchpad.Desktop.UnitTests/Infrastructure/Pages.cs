using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// An open workspace wired to in-memory doubles, so its commands can be driven without a window. The workspace is
/// the first one of the profile in use; a document without one is given an empty one to open.
/// </summary>
internal sealed class PageHarness
{
    private readonly ShortcutValidator _validator = new();
    private WorkspacePageViewModel? _page;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="data">The document; an empty launchpad when omitted.</param>
    /// <param name="kind">The kind of the groups <see cref="SavedGroupsAsync"/> reads back.</param>
    public PageHarness(LaunchpadData? data = null, ShortcutKind kind = ShortcutKind.Application)
    {
        LaunchpadData document = data ?? LaunchpadData.Empty;

        if (document.ActiveProfile.Workspaces.Count == 0)
        {
            document = document.WithProfile(document.ActiveProfile.AddWorkspace(Workspace.Create(Workspace.DefaultTitle)));
        }

        Kind = kind;
        Store = new FakeLaunchpadStore(document);
        Session = new LaunchpadSession(Store, Notifications, NullLogger<LaunchpadSession>.Instance);
    }

    public ShortcutKind Kind { get; }

    public FakeLaunchpadStore Store { get; }

    public RecordingNotifications Notifications { get; } = new();

    public ScriptedDialogs Dialogs { get; } = new();

    public RecordingShortcutActions Actions { get; } = new();

    public FakeShortcutIcons Icons { get; } = new();

    public LaunchpadSession Session { get; }

    /// <summary>
    /// Gets the open workspace, once <see cref="LoadAsync"/> has opened it.
    /// </summary>
    public WorkspacePageViewModel Page => _page ?? throw new InvalidOperationException("The workspace is not open yet.");

    /// <summary>
    /// Gets how many times the page asked to go back to the list.
    /// </summary>
    public int BackRequests { get; private set; }

    /// <summary>
    /// Loads the document and opens the first workspace of the profile in use.
    /// </summary>
    public async Task<WorkspacePageViewModel> LoadAsync()
    {
        await Session.LoadAsync();

        _page = new WorkspacePageViewModel(Session.Workspaces[0].Id, Session, _validator, Dialogs, Actions, Icons, Notifications, () => BackRequests++);
        return _page;
    }

    /// <summary>
    /// The groups of <see cref="Kind"/> in the open workspace as last written, once every pending save has finished.
    /// </summary>
    public async Task<ShortcutGroup[]> SavedGroupsAsync()
    {
        await Session.FlushAsync();

        Guid id = Page.WorkspaceId;
        return Store.LastSaved?.ActiveProfile.Workspaces.Single(workspace => workspace.Id == id).Groups.Where(group => group.Kind == Kind).ToArray() ?? [];
    }

    /// <summary>
    /// The applications as last written, once every pending save has finished.
    /// </summary>
    public Task<ShortcutGroup[]> SavedApplicationsAsync() => SavedGroupsAsync();

    public static LaunchpadData Data(params string[] applicationGroups)
        => LaunchpadData.Empty.With(
            ShortcutKind.Application,
            applicationGroups.Select(name => new ShortcutGroup(Guid.NewGuid(), name, ShortcutKind.Application, [])).ToArray());

    public static Shortcut Shortcut(string name, string target = @"C:\Tools\tool.exe", string? arguments = null)
        => new(Guid.NewGuid(), name, target, arguments);

    /// <summary>
    /// A document with two profiles, "Work" in use and "Home", each with one workspace holding one application group
    /// of its own: "Dev tools" and "Games".
    /// </summary>
    public static LaunchpadData WorkAndHome()
    {
        Profile work = Profile.Create("Work").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Dev tools", ShortcutKind.Application, [])]);
        Profile home = Profile.Create("Home").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Games", ShortcutKind.Application, [])]);

        return new LaunchpadData(LaunchpadData.CurrentVersion, work.Id, [work, home]);
    }

    /// <summary>
    /// A document with one workspace holding one group of a kind per entry, each with the named shortcuts.
    /// </summary>
    public static LaunchpadData WithShortcuts(ShortcutKind kind, params (string Group, Shortcut[] Shortcuts)[] groups)
        => LaunchpadData.Empty.With(
            kind,
            groups.Select(group => new ShortcutGroup(Guid.NewGuid(), group.Group, kind, group.Shortcuts)).ToArray());
}
