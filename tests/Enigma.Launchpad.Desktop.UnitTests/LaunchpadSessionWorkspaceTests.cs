using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class LaunchpadSessionWorkspaceTests
{
    private readonly FakeLaunchpadStore _store = new(Data());
    private readonly LaunchpadSession _session;
    private readonly List<string> _events = [];

    public LaunchpadSessionWorkspaceTests()
    {
        _session = new LaunchpadSession(_store, new RecordingNotifications(), NullLogger<LaunchpadSession>.Instance);
        _session.ProfilesChanged += (_, _) => _events.Add("Profiles");
        _session.ActiveProfileChanged += (_, _) => _events.Add("Active");
        _session.WorkspacesChanged += (_, _) => _events.Add("Workspaces");
    }

    private Guid DevId => _session.Workspaces[0].Id;

    [Fact]
    public async Task Workspaces_AreThoseOfTheProfileInUse()
    {
        await _session.LoadAsync();

        Assert.Equal(["Dev", "Reading"], _session.Workspaces.Select(workspace => workspace.Title));

        _session.SwitchProfile(_session.Profiles[1].Id);

        Assert.Equal(["Garden"], _session.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public void WorkspaceOperations_BeforeLoading_Throw()
        => Assert.Throws<InvalidOperationException>(() => _session.AddWorkspace("Lab", null, "Flask"));

    [Fact]
    public async Task AddWorkspace_AppendsAnEmptyOne_SavesIt_AndSaysSo()
    {
        await _session.LoadAsync();

        Workspace lab = _session.AddWorkspace("Lab", "  Experiments  ", "Flask");
        await _session.FlushAsync();

        Assert.Equal(("Lab", "Experiments", "Flask"), (lab.Title, lab.Description, lab.Icon));
        Assert.Empty(lab.Groups);
        Assert.Equal(["Dev", "Reading", "Lab"], _store.LastSaved?.ActiveProfile.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(["Workspaces"], _events);
    }

    [Fact]
    public async Task AddWorkspace_WithABlankDescription_HasNone()
    {
        await _session.LoadAsync();

        Assert.Null(_session.AddWorkspace("Lab", "   ", "Flask").Description);
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesItsLooks_AndKeepsItsGroups()
    {
        await _session.LoadAsync();
        Workspace before = _session.Workspaces[0];

        _session.UpdateWorkspace(before.Id, "Development", null, "Code");
        await _session.FlushAsync();

        Workspace after = _store.LastSaved?.ActiveProfile.Workspaces[0] ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal((before.Id, "Development", null, "Code"), (after.Id, after.Title, after.Description, after.Icon));
        Assert.Same(before.Groups, after.Groups);
        Assert.Equal(["Workspaces"], _events);
    }

    [Fact]
    public async Task UpdateWorkspace_OfAnUnknownOne_IsRefused_AndChangesNothing()
    {
        await _session.LoadAsync();

        Assert.Throws<ArgumentException>(() => _session.UpdateWorkspace(Guid.NewGuid(), "Lost", null, "Flask"));
        await _session.FlushAsync();

        Assert.Empty(_store.Saved);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task DuplicateWorkspace_InsertsACopyRightAfterIt_UnderNewIdentities()
    {
        await _session.LoadAsync();
        Workspace dev = _session.Workspaces[0];

        Workspace copy = _session.DuplicateWorkspace(dev.Id, "Dev (copy)");

        Assert.Equal(["Dev", "Dev (copy)", "Reading"], _session.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal((dev.Description, dev.Icon), (copy.Description, copy.Icon));
        Assert.NotEqual(dev.Id, copy.Id);
        Assert.Equal(dev.Groups.Select(group => (group.Name, group.Kind)), copy.Groups.Select(group => (group.Name, group.Kind)));
        Assert.NotEqual(dev.Groups[0].Id, copy.Groups[0].Id);
        Assert.NotEqual(dev.Groups[0].Shortcuts[0].Id, copy.Groups[0].Shortcuts[0].Id);
        Assert.Equal(["Workspaces"], _events);
    }

    [Fact]
    public async Task DeleteWorkspace_TakesItOut_AndSavesIt()
    {
        await _session.LoadAsync();

        _session.DeleteWorkspace(DevId);
        await _session.FlushAsync();

        Assert.Equal(["Reading"], _store.LastSaved?.ActiveProfile.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(["Workspaces"], _events);
    }

    [Fact]
    public async Task MoveWorkspace_ChangesTheOrder_WithinTheList()
    {
        await _session.LoadAsync();
        Guid dev = DevId;

        Assert.False(_session.CanMoveWorkspace(dev, -1));
        Assert.True(_session.CanMoveWorkspace(dev, 1));

        _session.MoveWorkspace(dev, 1);

        Assert.Equal(["Reading", "Dev"], _session.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(["Workspaces"], _events);
    }

    [Fact]
    public async Task WorkspaceOperations_LeaveTheOtherProfilesAlone()
    {
        await _session.LoadAsync();
        Profile home = _session.Profiles[1];

        _session.AddWorkspace("Lab", null, "Flask");
        _session.DeleteWorkspace(DevId);

        Assert.Same(home, _session.Profiles[1]);
    }

    private static LaunchpadData Data()
    {
        Profile work = new(
            Guid.NewGuid(),
            "Work",
            [
                new Workspace(
                    Guid.NewGuid(),
                    "Dev",
                    [new ShortcutGroup(Guid.NewGuid(), "Tools", ShortcutKind.Application, [PageHarness.Shortcut("Code")])],
                    "Coding",
                    "Code"),
                new Workspace(Guid.NewGuid(), "Reading", [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [])]),
            ]);
        Profile home = new(Guid.NewGuid(), "Home", [Workspace.Create("Garden")]);

        return new LaunchpadData(LaunchpadData.CurrentVersion, work.Id, [work, home]);
    }
}
