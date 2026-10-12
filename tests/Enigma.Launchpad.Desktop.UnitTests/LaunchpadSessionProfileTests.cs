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

public sealed class LaunchpadSessionProfileTests
{
    private readonly FakeLaunchpadStore _store = new(PageHarness.WorkAndHome());
    private readonly LaunchpadSession _session;
    private readonly List<string> _events = [];

    public LaunchpadSessionProfileTests()
    {
        _session = new LaunchpadSession(_store, new RecordingNotifications(), NullLogger<LaunchpadSession>.Instance);
        _session.ProfilesChanged += (_, _) => _events.Add("Profiles");
        _session.ActiveProfileChanged += (_, _) => _events.Add("Active");
    }

    private Guid WorkId => _session.Profiles[0].Id;

    private Guid HomeId => _session.Profiles[1].Id;

    [Fact]
    public async Task LoadAsync_ExposesTheProfiles_AndTheOneInUse()
    {
        await _session.LoadAsync();

        Assert.Equal(["Work", "Home"], _session.Profiles.Select(profile => profile.Name));
        Assert.Equal("Work", _session.ActiveProfile.Name);
        Assert.Equal("Dev tools", Assert.Single(_session.GroupsOf(ShortcutKind.Application)).Name);
        Assert.Empty(_events);
    }

    [Fact]
    public void ProfileOperations_BeforeLoading_Throw()
        => Assert.Throws<InvalidOperationException>(() => _session.AddProfile("Lab"));

    [Fact]
    public async Task SwitchProfile_PutsItInUse_SavesIt_AndSaysSo()
    {
        await _session.LoadAsync();

        _session.SwitchProfile(HomeId);
        await _session.FlushAsync();

        Assert.Equal("Games", Assert.Single(_session.GroupsOf(ShortcutKind.Application)).Name);
        Assert.Equal(HomeId, _store.LastSaved?.ActiveProfileId);
        Assert.Equal(["Profiles", "Active"], _events);
    }

    [Fact]
    public async Task SwitchProfile_ToTheOneInUse_DoesNothing()
    {
        await _session.LoadAsync();

        _session.SwitchProfile(WorkId);
        await _session.FlushAsync();

        Assert.Empty(_events);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Update_AfterASwitch_ChangesOnlyTheProfileInUse()
    {
        await _session.LoadAsync();
        _session.SwitchProfile(HomeId);

        _session.Update(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "News", [])]);
        await _session.FlushAsync();

        LaunchpadData saved = _store.LastSaved ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal("News", Assert.Single(saved.FindProfile(HomeId)?.Urls ?? []).Name);
        Assert.Empty(saved.FindProfile(WorkId)?.Urls ?? throw new InvalidOperationException());
        Assert.Equal("Dev tools", Assert.Single(saved.FindProfile(WorkId)?.Applications ?? []).Name);
    }

    [Fact]
    public async Task AddProfile_AppendsAnEmptyOne_WithoutSwitching()
    {
        await _session.LoadAsync();

        Profile lab = _session.AddProfile("Lab");
        await _session.FlushAsync();

        Assert.Equal(["Work", "Home", "Lab"], _store.LastSaved?.Profiles.Select(profile => profile.Name));
        Assert.Equal(0, lab.CountGroups());
        Assert.Equal("Work", _session.ActiveProfile.Name);
        Assert.Equal(["Profiles"], _events);
    }

    [Fact]
    public async Task DuplicateProfile_InsertsTheCopyRightAfterItsSource()
    {
        await _session.LoadAsync();

        Profile copy = _session.DuplicateProfile(WorkId, "Work (copy)");
        await _session.FlushAsync();

        Assert.Equal(["Work", "Work (copy)", "Home"], _session.Profiles.Select(profile => profile.Name));
        Assert.Equal("Dev tools", Assert.Single(copy.Applications).Name);
        Assert.NotEqual(_session.Profiles[0].Applications[0].Id, copy.Applications[0].Id);
        Assert.Equal(3, _store.LastSaved?.Profiles.Count);
    }

    [Fact]
    public async Task RenameProfile_IsSaved_AndAnnounced()
    {
        await _session.LoadAsync();

        _session.RenameProfile(HomeId, "Family");
        await _session.FlushAsync();

        Assert.Equal("Family", _store.LastSaved?.FindProfile(HomeId)?.Name);
        Assert.Equal(["Profiles"], _events);
    }

    [Fact]
    public async Task DeleteProfile_InUse_SwitchesToItsNeighbour_AndSaysSo()
    {
        await _session.LoadAsync();
        Guid home = HomeId;

        _session.DeleteProfile(WorkId);
        await _session.FlushAsync();

        Assert.Equal(home, _session.ActiveProfile.Id);
        Assert.Equal("Games", Assert.Single(_session.GroupsOf(ShortcutKind.Application)).Name);
        Assert.Equal(["Profiles", "Active"], _events);
        Assert.Single(_store.LastSaved?.Profiles ?? []);
    }

    [Fact]
    public async Task DeleteProfile_OfTheLastOne_IsRefused_AndChangesNothing()
    {
        await _session.LoadAsync();
        _session.DeleteProfile(HomeId);
        _events.Clear();

        Assert.Throws<InvalidOperationException>(() => _session.DeleteProfile(WorkId));

        Assert.Equal("Work", Assert.Single(_session.Profiles).Name);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task MoveProfile_ReordersAndSaves()
    {
        await _session.LoadAsync();

        Assert.True(_session.CanMoveProfile(HomeId, -1));
        Assert.False(_session.CanMoveProfile(HomeId, 1));

        _session.MoveProfile(HomeId, -1);
        await _session.FlushAsync();

        Assert.Equal(["Home", "Work"], _store.LastSaved?.Profiles.Select(profile => profile.Name));
        Assert.Equal("Work", _session.ActiveProfile.Name);
    }

    [Fact]
    public async Task ImportIntoProfile_NotInUse_MergesAndSaves_WithoutTellingThePages()
    {
        await _session.LoadAsync();
        Profile content = Profile.Create("Shared").With(
            ShortcutKind.Application,
            [new ShortcutGroup(Guid.NewGuid(), "games", [PageHarness.Shortcut("Chess")]), new ShortcutGroup(Guid.NewGuid(), "Emulators", [])]);

        _session.ImportIntoProfile(HomeId, content);
        await _session.FlushAsync();

        LaunchpadData saved = _store.LastSaved ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal(["Games", "Emulators"], saved.FindProfile(HomeId)?.Applications.Select(group => group.Name));
        Assert.Equal(["Chess"], saved.FindProfile(HomeId)?.Applications[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(["Dev tools"], saved.FindProfile(WorkId)?.Applications.Select(group => group.Name));
        Assert.Equal("Work", _session.ActiveProfile.Name);
        Assert.Equal(["Profiles"], _events);
    }

    [Fact]
    public async Task ImportIntoProfile_InUse_TellsThePagesItsGroupsChanged()
    {
        await _session.LoadAsync();

        _session.ImportIntoProfile(WorkId, Profile.Create("Shared").With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", [])]));

        Assert.Equal("Docs", Assert.Single(_session.GroupsOf(ShortcutKind.Url)).Name);
        Assert.Equal(["Profiles", "Active"], _events);
    }

    [Fact]
    public async Task ImportIntoProfile_OfAnUnknownProfile_IsRefused_AndChangesNothing()
    {
        await _session.LoadAsync();

        Assert.Throws<ArgumentException>(() => _session.ImportIntoProfile(Guid.NewGuid(), Profile.Create("Shared")));
        await _session.FlushAsync();

        Assert.Empty(_store.Saved);
        Assert.Empty(_events);
    }
}
