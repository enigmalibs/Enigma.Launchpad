using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Persistence;

public sealed class JsonLaunchpadStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 30, 15, TimeSpan.Zero);

    private readonly TempDirectory _temp = new();
    private readonly JsonLaunchpadStore _store;

    public JsonLaunchpadStoreTests()
        => _store = new JsonLaunchpadStore(new AppPaths(_temp.Path), new FixedTimeProvider(Now), NullLogger<JsonLaunchpadStore>.Instance);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FilePath_IsTheDocumentInTheDataDirectory()
        => Assert.Equal(Path.Combine(_temp.Path, JsonLaunchpadStore.FileName), _store.FilePath);

    [Fact]
    public async Task LoadAsync_WithoutAFile_ReturnsAnEmptyDocument()
    {
        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Same(LaunchpadData.Empty, result.Data);
        Assert.Null(result.UnreadableFileMovedTo);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryProfileAndCategoryInOrder()
    {
        LaunchpadData data = SampleData();

        await _store.SaveAsync(data, TestContext.Current.CancellationToken);
        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        AssertEquivalent(data, result.Data);
        Assert.Null(result.UnreadableFileMovedTo);
    }

    [Fact]
    public async Task SaveAsync_WritesReadableCamelCaseJson()
    {
        await _store.SaveAsync(SampleData(), TestContext.Current.CancellationToken);

        string json = await File.ReadAllTextAsync(_store.FilePath, TestContext.Current.CancellationToken);

        Assert.Contains("\"version\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"activeProfileId\": ", json, StringComparison.Ordinal);
        Assert.Contains("\"profiles\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Work\"", json, StringComparison.Ordinal);
        Assert.Contains("\"description\": \"The editor\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"png\": \"{Convert.ToBase64String(SamplePng.Bytes)}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"isCustom\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"applications\": [", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"activeProfile\":", json, StringComparison.Ordinal);
        Assert.Contains("\"isCollapsed\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"arguments\": \"--new-window\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_ReplacesTheFileAndLeavesNoTemporaryFileBehind()
    {
        await _store.SaveAsync(SampleData(), TestContext.Current.CancellationToken);
        await _store.SaveAsync(LaunchpadData.Empty, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Data.GroupsOf(ShortcutKind.Application));
        Assert.Equal([_store.FilePath], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task SaveAsync_AlwaysWritesTheCurrentVersion()
    {
        await _store.SaveAsync(LaunchpadData.Empty with { Version = 0 }, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LaunchpadData.CurrentVersion, result.Data.Version);
        Assert.Null(result.UnreadableFileMovedTo);
    }

    [Fact]
    public async Task ConcurrentSaves_AllComplete_AndLeaveAReadableDocument()
    {
        Task[] saves = Enumerable.Range(0, 20)
            .Select(_ => _store.SaveAsync(SampleData(), TestContext.Current.CancellationToken))
            .ToArray();

        await Task.WhenAll(saves);
        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Assert.Equal(2, result.Data.Profiles.Count);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{ \"version\": 1, \"applications\": [], \"directories\": [] }")]
    [InlineData("{ \"version\": 1, \"applications\": null, \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 1, \"applications\": [null], \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 1, \"applications\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"shortcuts\": [] } ], \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 2, \"applications\": [], \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 0, \"applications\": [], \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [] }")]
    [InlineData("{ \"applications\": [], \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [null] }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": null }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"applications\": [null], \"directories\": [], \"urls\": [] } ] }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"applications\": [], \"directories\": [], \"urls\": [] } ] }")]
    public async Task LoadAsync_MovesAnUnreadableFileAsideAndStartsEmpty(string contents)
    {
        await File.WriteAllTextAsync(_store.FilePath, contents, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        string expected = Path.Combine(_temp.Path, "launchpad.corrupt-20261012083015.json");
        Assert.Same(LaunchpadData.Empty, result.Data);
        Assert.Equal(expected, result.UnreadableFileMovedTo);
        Assert.False(File.Exists(_store.FilePath));
        Assert.Equal(contents, await File.ReadAllTextAsync(expected, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_NeverOverwritesAnEarlierMovedAsideFile()
    {
        string first = _temp.CreateFile("launchpad.corrupt-20261012083015.json", "first");
        await File.WriteAllTextAsync(_store.FilePath, "{ broken", TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_temp.Path, "launchpad.corrupt-20261012083015-2.json"), result.UnreadableFileMovedTo);
        Assert.Equal("first", await File.ReadAllTextAsync(first, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadAsync_AcceptsCommentsAndTrailingCommas()
    {
        const string json = """
            {
              // written by hand
              "version": 1,
              "applications": [],
              "directories": [],
              "urls": [ { "id": "8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d", "name": "Docs", "shortcuts": [], }, ],
            }
            """;
        await File.WriteAllTextAsync(_store.FilePath, json, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        ShortcutGroup group = Assert.Single(result.Data.GroupsOf(ShortcutKind.Url));
        Assert.Equal("Docs", group.Name);
        Assert.False(group.IsCollapsed);
    }

    [Fact]
    public async Task LoadAsync_ReadsAVersion1File_IntoOneDefaultProfile_InUse()
    {
        const string json = """
            {
              "version": 1,
              "applications": [
                { "id": "11111111-1111-1111-1111-111111111111", "name": "Dev tools", "isCollapsed": true,
                  "shortcuts": [ { "id": "22222222-2222-2222-2222-222222222222", "name": "Code", "target": "C:\\Code.exe", "arguments": "-n" } ] }
              ],
              "directories": [ { "id": "33333333-3333-3333-3333-333333333333", "name": "Work", "shortcuts": [] } ],
              "urls": []
            }
            """;
        await File.WriteAllTextAsync(_store.FilePath, json, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Profile profile = Assert.Single(result.Data.Profiles);
        Assert.Equal(Profile.DefaultName, profile.Name);
        Assert.Equal(profile.Id, result.Data.ActiveProfileId);
        Assert.Equal(LaunchpadData.CurrentVersion, result.Data.Version);

        ShortcutGroup tools = Assert.Single(profile.Applications);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), tools.Id);
        Assert.True(tools.IsCollapsed);
        Assert.Equal(new Shortcut(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Code", @"C:\Code.exe", "-n"), Assert.Single(tools.Shortcuts));
        Assert.Equal("Work", Assert.Single(profile.Directories).Name);
        Assert.Empty(profile.Urls);
    }

    [Fact]
    public async Task LoadAsync_ReadsAShortcutWithoutADescriptionOrAnIcon_AsHavingNone()
    {
        const string json = """
            {
              "version": 2,
              "activeProfileId": "44444444-4444-4444-4444-444444444444",
              "profiles": [ { "id": "44444444-4444-4444-4444-444444444444", "name": "Work", "directories": [], "urls": [],
                "applications": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Dev tools",
                  "shortcuts": [ { "id": "22222222-2222-2222-2222-222222222222", "name": "Code", "target": "C:\\Code.exe" } ] } ] } ]
            }
            """;
        await File.WriteAllTextAsync(_store.FilePath, json, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Shortcut shortcut = Assert.Single(Assert.Single(result.Data.GroupsOf(ShortcutKind.Application)).Shortcuts);
        Assert.Null(shortcut.Description);
        Assert.Null(shortcut.Icon);
    }

    [Fact]
    public async Task AVersion1File_IsWrittenBackAsVersion2_OnTheNextSave()
    {
        _temp.CreateFile(
            JsonLaunchpadStore.FileName,
            """{ "version": 1, "applications": [], "directories": [], "urls": [ { "id": "44444444-4444-4444-4444-444444444444", "name": "Docs", "shortcuts": [] } ] }""");
        LaunchpadLoadResult loaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        await _store.SaveAsync(loaded.Data, TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(_store.FilePath, TestContext.Current.CancellationToken);
        LaunchpadLoadResult reloaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"version\": 2", json, StringComparison.Ordinal);
        Assert.Equal(loaded.Data.ActiveProfileId, reloaded.Data.ActiveProfileId);
        Assert.Equal("Docs", Assert.Single(reloaded.Data.GroupsOf(ShortcutKind.Url)).Name);
    }

    [Fact]
    public async Task LoadAsync_GivesADocumentWithoutProfiles_ADefaultOne()
    {
        _temp.CreateFile(
            JsonLaunchpadStore.FileName,
            """{ "version": 2, "activeProfileId": "8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d", "profiles": [] }""");

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Profile profile = Assert.Single(result.Data.Profiles);
        Assert.Equal(Profile.DefaultName, profile.Name);
        Assert.Equal(profile.Id, result.Data.ActiveProfileId);
    }

    [Fact]
    public async Task LoadAsync_UsesTheFirstProfile_WhenTheOneInUseIsUnknown()
    {
        LaunchpadData data = SampleData() with { ActiveProfileId = Guid.NewGuid() };
        await _store.SaveAsync(data, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Assert.Equal(data.Profiles[0].Id, result.Data.ActiveProfileId);
    }

    private static LaunchpadData SampleData()
    {
        Profile work = new(
            Guid.NewGuid(),
            "Work",
            [
                new ShortcutGroup(
                    Guid.NewGuid(),
                    "Dev tools",
                    [
                        new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "--new-window", "The editor", ShortcutIcon.FromBytes(SamplePng.Bytes, isCustom: true)),
                        new Shortcut(Guid.NewGuid(), "Terminal", @"C:\Tools\wt.exe"),
                    ],
                    IsCollapsed: true),
            ],
            [
                new ShortcutGroup(Guid.NewGuid(), "Work", [new Shortcut(Guid.NewGuid(), "Projects", @"C:\Projects")]),
                new ShortcutGroup(Guid.NewGuid(), "Empty", []),
            ],
            [
                new ShortcutGroup(
                    Guid.NewGuid(),
                    "Docs",
                    [new Shortcut(Guid.NewGuid(), "Avalonia", "https://docs.avaloniaui.net", Icon: ShortcutIcon.FromBytes(SamplePng.Bytes, isCustom: false))]),
            ]);

        Profile home = Profile.Create("Home") with
        {
            Urls = [new ShortcutGroup(Guid.NewGuid(), "News", [new Shortcut(Guid.NewGuid(), "Paper", "https://example.com")])],
        };

        return new LaunchpadData(LaunchpadData.CurrentVersion, home.Id, [work, home]);
    }

    private static void AssertEquivalent(LaunchpadData expected, LaunchpadData actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.ActiveProfileId, actual.ActiveProfileId);
        Assert.Equal(expected.Profiles.Count, actual.Profiles.Count);

        foreach ((Profile left, Profile right) in expected.Profiles.Zip(actual.Profiles))
        {
            Assert.Equal(left.Id, right.Id);
            Assert.Equal(left.Name, right.Name);

            foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
            {
                Assert.Equal(left.GroupsOf(kind).Count, right.GroupsOf(kind).Count);

                foreach ((ShortcutGroup leftGroup, ShortcutGroup rightGroup) in left.GroupsOf(kind).Zip(right.GroupsOf(kind)))
                {
                    Assert.Equal(leftGroup.Id, rightGroup.Id);
                    Assert.Equal(leftGroup.Name, rightGroup.Name);
                    Assert.Equal(leftGroup.IsCollapsed, rightGroup.IsCollapsed);
                    Assert.Equal(leftGroup.Shortcuts, rightGroup.Shortcuts);
                }
            }
        }
    }
}
