using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class LaunchpadSessionTests
{
    private readonly FakeLaunchpadStore _store = new(PageHarness.Data("Dev tools"));
    private readonly RecordingNotifications _notifications = new();
    private readonly LaunchpadSession _session;

    public LaunchpadSessionTests()
        => _session = new LaunchpadSession(_store, _notifications, NullLogger<LaunchpadSession>.Instance);

    private Guid WorkspaceId => _session.Workspaces[0].Id;

    [Fact]
    public async Task LoadAsync_ReadsTheStoreOnlyOnce()
    {
        await _session.LoadAsync();
        await _session.LoadAsync();

        Assert.Equal(1, _store.LoadCount);
        Assert.Equal("Dev tools", Assert.Single(_session.Workspaces[0].Groups).Name);
        Assert.Empty(_notifications.Shown);
    }

    [Fact]
    public async Task LoadAsync_WarnsWhenTheFileWasMovedAside()
    {
        _store.UnreadableFileMovedTo = "launchpad.corrupt-20261012083015.json";

        await _session.LoadAsync();

        (string severity, string _, string message) = Assert.Single(_notifications.Shown);
        Assert.Equal("Warning", severity);
        Assert.Contains("launchpad.corrupt-20261012083015.json", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ThatFails_ReportsIt_AndNeverSaves()
    {
        _store.LoadFailure = new IOException("The file is locked.");

        await _session.LoadAsync();
        _session.AddWorkspace("Lab", null, "Flask");
        await _session.FlushAsync();

        Assert.Equal("Error", Assert.Single(_notifications.Shown).Severity);
        Assert.Empty(_store.Saved);
        Assert.Equal(["Lab"], _session.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public void UpdateGroups_BeforeLoading_Throws()
        => Assert.Throws<InvalidOperationException>(() => _session.UpdateGroups(Guid.NewGuid(), []));

    [Fact]
    public async Task UpdateGroups_OfAnUnknownWorkspace_IsRefused_AndSavesNothing()
    {
        await _session.LoadAsync();

        Assert.Throws<ArgumentException>(() => _session.UpdateGroups(Guid.NewGuid(), []));
        await _session.FlushAsync();

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task UpdateGroups_ReplacesTheWorkspacesGroups_SavesTheDocument_AndAnnouncesNothing()
    {
        await _session.LoadAsync();
        int events = 0;
        _session.WorkspacesChanged += (_, _) => events++;
        _session.ActiveProfileChanged += (_, _) => events++;
        ShortcutGroup docs = new(Guid.NewGuid(), "Docs", ShortcutKind.Url, []);
        ShortcutGroup tools = _session.Workspaces[0].Groups[0];

        _session.UpdateGroups(WorkspaceId, [tools, docs]);
        await _session.FlushAsync();

        LaunchpadData saved = Assert.Single(_store.Saved);
        Assert.Equal([tools, docs], saved.ActiveProfile.Workspaces[0].Groups);
        Assert.Same(docs, _session.Workspaces[0].Groups[1]);
        Assert.Equal(0, events);
    }

    [Fact]
    public async Task UpdatesDuringASave_AreFoldedIntoOneMoreSave_OfTheLatestDocument()
    {
        await _session.LoadAsync();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.SaveGate = gate.Task;

        for (int index = 1; index <= 5; index++)
        {
            _session.UpdateGroups(WorkspaceId, Enumerable.Range(0, index).Select(n => new ShortcutGroup(Guid.NewGuid(), $"G{n}", ShortcutKind.Url, [])).ToArray());
        }

        gate.SetResult();
        await _session.FlushAsync();

        Assert.InRange(_store.Saved.Count, 1, 2);
        Assert.Equal(5, _store.LastSaved?.ActiveProfile.Workspaces[0].Groups.Count);
    }

    [Fact]
    public async Task AFailedSave_IsReported_AndTheNextOneStillRuns()
    {
        await _session.LoadAsync();
        _store.SaveFailure = new UnauthorizedAccessException("Access denied.");

        _session.UpdateGroups(WorkspaceId, []);
        await _session.FlushAsync();

        (string severity, string title, string _) = Assert.Single(_notifications.Shown);
        Assert.Equal("Error", severity);
        Assert.Equal("Your changes could not be saved", title);

        _store.SaveFailure = null;
        _session.UpdateGroups(WorkspaceId, []);
        await _session.FlushAsync();

        Assert.Single(_store.Saved);
    }

    [Fact]
    public async Task MoveGroup_TakesItFromOneWorkspace_ToTheEndOfAnother_InOneSave_AndSaysSo()
    {
        await _session.LoadAsync();
        Workspace lab = _session.AddWorkspace("Lab", null, "Flask");
        _session.UpdateGroups(lab.Id, [new ShortcutGroup(Guid.NewGuid(), "Existing", ShortcutKind.Url, [])]);
        await _session.FlushAsync();
        int saves = _store.Saved.Count;
        int events = 0;
        _session.WorkspacesChanged += (_, _) => events++;
        ShortcutGroup tools = _session.Workspaces[0].Groups[0];

        _session.MoveGroup(tools.Id, WorkspaceId, lab.Id);
        await _session.FlushAsync();

        Assert.Empty(_session.Workspaces[0].Groups);
        Assert.Equal(["Existing", "Dev tools"], _session.Workspaces[1].Groups.Select(group => group.Name));
        Assert.Same(tools, _session.Workspaces[1].Groups[1]);
        Assert.Equal(saves + 1, _store.Saved.Count);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task MoveGroup_IsRefused_WhenTheTargetHasAGroupOfTheSameNameAndKind()
    {
        await _session.LoadAsync();
        Workspace lab = _session.AddWorkspace("Lab", null, "Flask");
        _session.UpdateGroups(lab.Id, [new ShortcutGroup(Guid.NewGuid(), " DEV TOOLS ", ShortcutKind.Application, [])]);
        ShortcutGroup tools = _session.Workspaces[0].Groups[0];

        Assert.Throws<InvalidOperationException>(() => _session.MoveGroup(tools.Id, WorkspaceId, lab.Id));

        Assert.Same(tools, Assert.Single(_session.Workspaces[0].Groups));
    }

    [Fact]
    public async Task MoveGroup_ToAGroupOfTheSameNameButAnotherKind_IsFine()
    {
        await _session.LoadAsync();
        Workspace lab = _session.AddWorkspace("Lab", null, "Flask");
        _session.UpdateGroups(lab.Id, [new ShortcutGroup(Guid.NewGuid(), "Dev tools", ShortcutKind.Directory, [])]);

        _session.MoveGroup(_session.Workspaces[0].Groups[0].Id, WorkspaceId, lab.Id);

        Assert.Equal([ShortcutKind.Directory, ShortcutKind.Application], _session.Workspaces[1].Groups.Select(group => group.Kind));
    }

    [Fact]
    public async Task MoveGroup_WithinOneWorkspace_OrOfAnUnknownGroup_IsRefused()
    {
        await _session.LoadAsync();
        Workspace lab = _session.AddWorkspace("Lab", null, "Flask");

        Assert.Throws<ArgumentException>(() => _session.MoveGroup(_session.Workspaces[0].Groups[0].Id, WorkspaceId, WorkspaceId));
        Assert.Throws<ArgumentException>(() => _session.MoveGroup(Guid.NewGuid(), WorkspaceId, lab.Id));
        Assert.Throws<ArgumentException>(() => _session.MoveGroup(_session.Workspaces[0].Groups[0].Id, WorkspaceId, Guid.NewGuid()));
    }
}
