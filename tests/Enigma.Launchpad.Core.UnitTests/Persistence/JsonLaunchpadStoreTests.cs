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

    private const string Version2File = """
        {
          "version": 2,
          "activeProfileId": "44444444-4444-4444-4444-444444444444",
          "profiles": [
            { "id": "44444444-4444-4444-4444-444444444444", "name": "Work",
              "applications": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Dev tools", "isCollapsed": true,
                "shortcuts": [ { "id": "22222222-2222-2222-2222-222222222222", "name": "Code", "target": "C:\\Code.exe", "arguments": "-n" } ] } ],
              "directories": [ { "id": "33333333-3333-3333-3333-333333333333", "name": "Dev tools", "shortcuts": [] } ],
              "urls": [ { "id": "55555555-5555-5555-5555-555555555555", "name": "Docs", "shortcuts": [] } ] },
            { "id": "66666666-6666-6666-6666-666666666666", "name": "Empty", "applications": [], "directories": [], "urls": [] }
          ]
        }
        """;

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
    public async Task SaveThenLoad_RoundTripsEveryProfileWorkspaceAndGroupInOrder()
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

        Assert.Contains("\"version\": 3", json, StringComparison.Ordinal);
        Assert.Contains("\"activeProfileId\": ", json, StringComparison.Ordinal);
        Assert.Contains("\"profiles\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Work\"", json, StringComparison.Ordinal);
        Assert.Contains("\"workspaces\": [", json, StringComparison.Ordinal);
        Assert.Contains("\"title\": \"Dev\"", json, StringComparison.Ordinal);
        Assert.Contains("\"icon\": \"Code\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"application\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"directory\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"url\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"document\"", json, StringComparison.Ordinal);
        Assert.Contains("\"description\": \"The editor\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"png\": \"{Convert.ToBase64String(SamplePng.Bytes)}\"", json, StringComparison.Ordinal);
        Assert.Contains("\"isCustom\": true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"activeProfile\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"applications\":", json, StringComparison.Ordinal);
        Assert.Contains("\"isCollapsed\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"arguments\": \"--new-window\"", json, StringComparison.Ordinal);
        Assert.Contains("\"width\": \"full\"", json, StringComparison.Ordinal);
        Assert.Contains("\"width\": \"twoThirds\"", json, StringComparison.Ordinal);
        Assert.Contains("\"width\": \"oneThird\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_ReplacesTheFileAndLeavesNoTemporaryFileBehind()
    {
        await _store.SaveAsync(SampleData(), TestContext.Current.CancellationToken);
        await _store.SaveAsync(LaunchpadData.Empty, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Data.ActiveProfile.Workspaces);
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
    [InlineData("{ \"version\": 4, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [] }")]
    [InlineData("{ \"applications\": [], \"directories\": [], \"urls\": [] }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [null] }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": null }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"applications\": [null], \"directories\": [], \"urls\": [] } ] }")]
    [InlineData("{ \"version\": 2, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"applications\": [], \"directories\": [], \"urls\": [] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [null] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"applications\": [], \"directories\": [], \"urls\": [] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"title\": \"Dev\", \"groups\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Tools\", \"kind\": \"spreadsheet\", \"shortcuts\": [] } ] } ] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"title\": \"Dev\", \"groups\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Tools\", \"shortcuts\": [] } ] } ] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"title\": \"Dev\", \"groups\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Tools\", \"kind\": 0, \"shortcuts\": [] } ] } ] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"title\": \"Dev\", \"groups\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Tools\", \"kind\": \"application\", \"width\": \"half\", \"shortcuts\": [] } ] } ] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"title\": \"Dev\", \"groups\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Tools\", \"kind\": \"application\", \"width\": 2, \"shortcuts\": [] } ] } ] } ] }")]
    [InlineData("{ \"version\": 3, \"activeProfileId\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"profiles\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"name\": \"Work\", \"workspaces\": [ { \"id\": \"8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d\", \"groups\": [] } ] } ] }")]
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
        ShortcutGroup group = Assert.Single(Assert.Single(result.Data.ActiveProfile.Workspaces).Groups);
        Assert.Equal(("Docs", ShortcutKind.Url), (group.Name, group.Kind));
        Assert.False(group.IsCollapsed);
    }

    [Fact]
    public async Task LoadAsync_ReadsAVersion1File_IntoOneDefaultProfile_InUse_WithOneWorkspace()
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

        Workspace workspace = Assert.Single(profile.Workspaces);
        Assert.Equal(Workspace.DefaultTitle, workspace.Title);
        Assert.Equal(
            [("Dev tools", ShortcutKind.Application), ("Work", ShortcutKind.Directory)],
            workspace.Groups.Select(group => (group.Name, group.Kind)));
        ShortcutGroup tools = workspace.Groups[0];
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), tools.Id);
        Assert.True(tools.IsCollapsed);
        Assert.Equal(new Shortcut(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Code", @"C:\Code.exe", "-n"), Assert.Single(tools.Shortcuts));
    }

    [Fact]
    public async Task LoadAsync_ReadsAVersion2File_EachProfilesGroupsInOneWorkspace_TypedByTheirCategory()
    {
        await File.WriteAllTextAsync(_store.FilePath, Version2File, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), result.Data.ActiveProfileId);
        Assert.Equal(["Work", "Empty"], result.Data.Profiles.Select(profile => profile.Name));

        Workspace workspace = Assert.Single(result.Data.Profiles[0].Workspaces);
        Assert.Equal(
            (Workspace.DefaultTitle, Workspace.MigratedDescription, Workspace.MigratedIcon),
            (workspace.Title, workspace.Description, workspace.Icon));
        Assert.Equal(
            [("Dev tools", ShortcutKind.Application), ("Dev tools", ShortcutKind.Directory), ("Docs", ShortcutKind.Url)],
            workspace.Groups.Select(group => (group.Name, group.Kind)));
        Assert.Equal(
            [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("55555555-5555-5555-5555-555555555555")],
            workspace.Groups.Select(group => group.Id));
        Assert.True(workspace.Groups[0].IsCollapsed);
        Assert.Equal("Code", Assert.Single(workspace.Groups[0].Shortcuts).Name);

        Assert.Empty(result.Data.Profiles[1].Workspaces);
    }

    [Fact]
    public async Task LoadAsync_ReadsAShortcutWithoutADescriptionOrAnIcon_AsHavingNone()
    {
        await File.WriteAllTextAsync(_store.FilePath, Version2File, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Shortcut shortcut = Assert.Single(result.Data.ActiveProfile.Workspaces[0].Groups[0].Shortcuts);
        Assert.Null(shortcut.Description);
        Assert.Null(shortcut.Icon);
    }

    [Fact]
    public async Task LoadAsync_ReadsAWorkspaceWithoutADescriptionOrAnIcon_AsHavingNone()
    {
        const string json = """
            { "version": 3, "activeProfileId": "44444444-4444-4444-4444-444444444444",
              "profiles": [ { "id": "44444444-4444-4444-4444-444444444444", "name": "Work",
                "workspaces": [ { "id": "11111111-1111-1111-1111-111111111111", "title": "Dev", "groups": [] } ] } ] }
            """;
        await File.WriteAllTextAsync(_store.FilePath, json, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Workspace workspace = Assert.Single(result.Data.ActiveProfile.Workspaces);
        Assert.Equal(("Dev", null, null), (workspace.Title, workspace.Description, workspace.Icon));
    }

    [Fact]
    public async Task LoadAsync_ReadsAGroupWithoutAWidth_AsFullWidth()
    {
        const string json = """
            { "version": 3, "activeProfileId": "44444444-4444-4444-4444-444444444444",
              "profiles": [ { "id": "44444444-4444-4444-4444-444444444444", "name": "Work",
                "workspaces": [ { "id": "11111111-1111-1111-1111-111111111111", "title": "Dev",
                  "groups": [ { "id": "22222222-2222-2222-2222-222222222222", "name": "Tools", "kind": "application", "shortcuts": [] } ] } ] } ] }
            """;
        await File.WriteAllTextAsync(_store.FilePath, json, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Assert.Equal(GroupWidth.Full, Assert.Single(Assert.Single(result.Data.ActiveProfile.Workspaces).Groups).Width);
    }

    [Fact]
    public async Task LoadAsync_ReadsAVersion2FilesGroups_AsFullWidth()
    {
        await File.WriteAllTextAsync(_store.FilePath, Version2File, TestContext.Current.CancellationToken);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.All(result.Data.ActiveProfile.Workspaces[0].Groups, group => Assert.Equal(GroupWidth.Full, group.Width));
    }

    [Theory]
    [InlineData(1, """{ "version": 1, "applications": [], "directories": [], "urls": [ { "id": "44444444-4444-4444-4444-444444444444", "name": "Docs", "shortcuts": [] } ] }""")]
    [InlineData(2, Version2File)]
    public async Task AnOlderFile_IsBackedUp_ThenWrittenBackAsTheCurrentVersion_OnTheNextSave(int version, string contents)
    {
        _temp.CreateFile(JsonLaunchpadStore.FileName, contents);
        LaunchpadLoadResult loaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        await _store.SaveAsync(loaded.Data, TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(_store.FilePath, TestContext.Current.CancellationToken);
        LaunchpadLoadResult reloaded = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(contents, await File.ReadAllTextAsync(_temp.Combine($"launchpad.v{version}.backup.json"), TestContext.Current.CancellationToken));
        Assert.Contains("\"version\": 3", json, StringComparison.Ordinal);
        Assert.Equal(loaded.Data.ActiveProfileId, reloaded.Data.ActiveProfileId);
        Assert.Equal(loaded.Data.ActiveProfile.Workspaces[0].Id, reloaded.Data.ActiveProfile.Workspaces[0].Id);
    }

    [Fact]
    public async Task ABackup_IsNeverReplaced()
    {
        string backup = _temp.CreateFile("launchpad.v2.backup.json", "kept from the first start");
        _temp.CreateFile(JsonLaunchpadStore.FileName, Version2File);

        LaunchpadLoadResult result = await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Null(result.UnreadableFileMovedTo);
        Assert.Equal("kept from the first start", await File.ReadAllTextAsync(backup, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACurrentFile_IsNotBackedUp()
    {
        await _store.SaveAsync(SampleData(), TestContext.Current.CancellationToken);

        await _store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([_store.FilePath], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task LoadAsync_GivesADocumentWithoutProfiles_ADefaultOne()
    {
        _temp.CreateFile(
            JsonLaunchpadStore.FileName,
            """{ "version": 3, "activeProfileId": "8c0e0f6e-55b7-4c4b-9a43-0f0c8a1b2c3d", "profiles": [] }""");

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
                new Workspace(
                    Guid.NewGuid(),
                    "Dev",
                    [
                        new ShortcutGroup(
                            Guid.NewGuid(),
                            "Dev tools",
                            ShortcutKind.Application,
                            [
                                new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "--new-window", "The editor", ShortcutIcon.FromBytes(SamplePng.Bytes, isCustom: true)),
                                new Shortcut(Guid.NewGuid(), "Terminal", @"C:\Tools\wt.exe"),
                            ],
                            IsCollapsed: true),
                        new ShortcutGroup(Guid.NewGuid(), "Work", ShortcutKind.Directory, [new Shortcut(Guid.NewGuid(), "Projects", @"C:\Projects")], Width: GroupWidth.TwoThirds),
                        new ShortcutGroup(Guid.NewGuid(), "Papers", ShortcutKind.Document, [new Shortcut(Guid.NewGuid(), "Report", @"C:\Docs\report.pdf")], Width: GroupWidth.OneThird),
                        new ShortcutGroup(
                            Guid.NewGuid(),
                            "Docs",
                            ShortcutKind.Url,
                            [new Shortcut(Guid.NewGuid(), "Avalonia", "https://docs.avaloniaui.net", Icon: ShortcutIcon.FromBytes(SamplePng.Bytes, isCustom: false))]),
                    ],
                    "Everything for coding",
                    "Code"),
                new Workspace(Guid.NewGuid(), "Empty", [new ShortcutGroup(Guid.NewGuid(), "Empty", ShortcutKind.Directory, [])]),
            ]);

        Profile home = new(
            Guid.NewGuid(),
            "Home",
            [new Workspace(Guid.NewGuid(), "News", [new ShortcutGroup(Guid.NewGuid(), "News", ShortcutKind.Url, [new Shortcut(Guid.NewGuid(), "Paper", "https://example.com")])])]);

        return new LaunchpadData(LaunchpadData.CurrentVersion, home.Id, [work, home]);
    }

    private static void AssertEquivalent(LaunchpadData expected, LaunchpadData actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.ActiveProfileId, actual.ActiveProfileId);
        Assert.Equal(expected.Profiles.Count, actual.Profiles.Count);

        foreach ((Profile left, Profile right) in expected.Profiles.Zip(actual.Profiles))
        {
            Assert.Equal((left.Id, left.Name), (right.Id, right.Name));
            Assert.Equal(left.Workspaces.Count, right.Workspaces.Count);

            foreach ((Workspace leftWorkspace, Workspace rightWorkspace) in left.Workspaces.Zip(right.Workspaces))
            {
                Assert.Equal(
                    (leftWorkspace.Id, leftWorkspace.Title, leftWorkspace.Description, leftWorkspace.Icon),
                    (rightWorkspace.Id, rightWorkspace.Title, rightWorkspace.Description, rightWorkspace.Icon));
                Assert.Equal(leftWorkspace.Groups.Count, rightWorkspace.Groups.Count);

                foreach ((ShortcutGroup leftGroup, ShortcutGroup rightGroup) in leftWorkspace.Groups.Zip(rightWorkspace.Groups))
                {
                    Assert.Equal(
                        (leftGroup.Id, leftGroup.Name, leftGroup.Kind, leftGroup.IsCollapsed, leftGroup.Width),
                        (rightGroup.Id, rightGroup.Name, rightGroup.Kind, rightGroup.IsCollapsed, rightGroup.Width));
                    Assert.Equal(leftGroup.Shortcuts, rightGroup.Shortcuts);
                }
            }
        }
    }
}
