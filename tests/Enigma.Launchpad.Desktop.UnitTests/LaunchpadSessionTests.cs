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

    [Fact]
    public async Task LoadAsync_ReadsTheStoreOnlyOnce()
    {
        await _session.LoadAsync();
        await _session.LoadAsync();

        Assert.Equal(1, _store.LoadCount);
        Assert.Equal("Dev tools", Assert.Single(_session.GroupsOf(ShortcutKind.Application)).Name);
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
        _session.Update(ShortcutKind.Url, []);
        await _session.FlushAsync();

        Assert.Equal("Error", Assert.Single(_notifications.Shown).Severity);
        Assert.Empty(_store.Saved);
        Assert.Empty(_session.GroupsOf(ShortcutKind.Application));
    }

    [Fact]
    public void Update_BeforeLoading_Throws()
        => Assert.Throws<InvalidOperationException>(() => _session.Update(ShortcutKind.Url, []));

    [Fact]
    public async Task Update_ReplacesOnlyItsCategory_AndSavesTheDocument()
    {
        await _session.LoadAsync();
        ShortcutGroup docs = new(Guid.NewGuid(), "Docs", []);

        _session.Update(ShortcutKind.Url, [docs]);
        await _session.FlushAsync();

        LaunchpadData saved = Assert.Single(_store.Saved);
        Assert.Same(docs, Assert.Single(saved.GroupsOf(ShortcutKind.Url)));
        Assert.Equal("Dev tools", Assert.Single(saved.GroupsOf(ShortcutKind.Application)).Name);
        Assert.Same(docs, Assert.Single(_session.GroupsOf(ShortcutKind.Url)));
    }

    [Fact]
    public async Task UpdatesDuringASave_AreFoldedIntoOneMoreSave_OfTheLatestDocument()
    {
        await _session.LoadAsync();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.SaveGate = gate.Task;

        for (int index = 1; index <= 5; index++)
        {
            _session.Update(ShortcutKind.Url, Enumerable.Range(0, index).Select(n => new ShortcutGroup(Guid.NewGuid(), $"G{n}", [])).ToArray());
        }

        gate.SetResult();
        await _session.FlushAsync();

        Assert.InRange(_store.Saved.Count, 1, 2);
        Assert.Equal(5, _store.LastSaved?.GroupsOf(ShortcutKind.Url).Count);
    }

    [Fact]
    public async Task AFailedSave_IsReported_AndTheNextOneStillRuns()
    {
        await _session.LoadAsync();
        _store.SaveFailure = new UnauthorizedAccessException("Access denied.");

        _session.Update(ShortcutKind.Url, []);
        await _session.FlushAsync();

        (string severity, string title, string _) = Assert.Single(_notifications.Shown);
        Assert.Equal("Error", severity);
        Assert.Equal("Your changes could not be saved", title);

        _store.SaveFailure = null;
        _session.Update(ShortcutKind.Directory, []);
        await _session.FlushAsync();

        Assert.Single(_store.Saved);
    }
}
