using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Exchange;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Enigma.Launchpad.Core.Validation;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Exchange;

public sealed class ShortcutExchangeTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ShortcutExchange _exchange = new(new ShortcutValidator());

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Serialize_WritesTheMarker_TheVersion_TheProfileName_AndEveryCategory()
    {
        using JsonDocument json = JsonDocument.Parse(ShortcutExchange.Serialize(Sample()));
        JsonElement root = json.RootElement;

        Assert.Equal(["format", "version", "profile", "applications", "directories", "urls"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(ShortcutExchange.FormatName, root.GetProperty("format").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("Work", root.GetProperty("profile").GetString());
        Assert.Equal(0, root.GetProperty("directories").GetArrayLength());
    }

    [Fact]
    public void Serialize_LeavesOutIdentities_FoldState_AndAbsentArguments()
    {
        using JsonDocument json = JsonDocument.Parse(ShortcutExchange.Serialize(Sample()));
        JsonElement tools = json.RootElement.GetProperty("applications")[0];
        JsonElement[] shortcuts = [.. tools.GetProperty("shortcuts").EnumerateArray()];

        Assert.Equal(["name", "shortcuts"], tools.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["name", "target", "arguments"], shortcuts[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["name", "target"], shortcuts[1].EnumerateObject().Select(property => property.Name));
        Assert.Equal("--new-window", shortcuts[0].GetProperty("arguments").GetString());
    }

    [Fact]
    public async Task ExportThenRead_GivesBackEveryGroupAndShortcut_InOrder_UnderNewIdentities()
    {
        Profile profile = Sample();
        string path = _temp.Combine("Work shortcuts.json");

        await _exchange.ExportAsync(path, profile, TestContext.Current.CancellationToken);
        ShortcutImport import = await _exchange.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(0, import.SkippedEntries);
        Assert.Equal("Work", import.Content.Name);
        Assert.NotEqual(profile.Id, import.Content.Id);

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            Assert.Equal(Describe(profile.GroupsOf(kind)), Describe(import.Content.GroupsOf(kind)));
            Assert.Empty(Identities(profile.GroupsOf(kind)).Intersect(Identities(import.Content.GroupsOf(kind))));
        }
    }

    [Fact]
    public async Task ExportAsync_ReplacesAnExistingFile()
    {
        string path = _temp.CreateFile("export.json", "previous contents that are much longer than the new document would ever be, surely");

        await _exchange.ExportAsync(path, Profile.Create("Empty"), TestContext.Current.CancellationToken);

        Assert.True(_exchange.TryParse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), out ShortcutImport? import, out _));
        Assert.Equal(0, import.Content.CountGroups());
    }

    [Theory]
    [InlineData("", "The file is not valid JSON.")]
    [InlineData("this is not JSON", "The file is not valid JSON.")]
    [InlineData("{ \"format\": ", "The file is not valid JSON.")]
    [InlineData("[1, 2]", "The file is not a launchpad shortcuts export.")]
    [InlineData("null", "The file is not a launchpad shortcuts export.")]
    [InlineData("{}", "The file is not a launchpad shortcuts export.")]
    [InlineData("""{ "format": "something-else", "version": 1 }""", "The file is not a launchpad shortcuts export.")]
    [InlineData("""{ "version": 2, "activeProfileId": "00000000-0000-0000-0000-000000000000", "profiles": [] }""", "The file is not a launchpad shortcuts export.")]
    [InlineData("""{ "format": 5, "version": 1 }""", "The file is not a launchpad shortcuts export.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts" }""", "The file does not say which format version it is in.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": "1" }""", "The file does not say which format version it is in.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 2 }""", "The file is in format version 2, which this version of the launchpad cannot read.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [null] }""", "The file is damaged: a group or a shortcut is missing.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [{ "name": "Docs", "shortcuts": [null] }] }""", "The file is damaged: a group or a shortcut is missing.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [{ "name": "Docs", "shortcuts": [{ "name": "No target" }] }] }""", "The file is damaged: a group or a shortcut is incomplete.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [{ "shortcuts": [] }] }""", "The file is damaged: a group or a shortcut is incomplete.")]
    public void TryParse_RefusesWhatIsNotAReadableExport_SayingWhy(string json, string expectedStart)
    {
        Assert.False(_exchange.TryParse(json, out ShortcutImport? import, out string? reason));

        Assert.Null(import);
        Assert.StartsWith(expectedStart, reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_OfAFileThatIsNotAnExport_ThrowsInvalidData_WithTheReason()
    {
        string path = _temp.CreateFile("settings.json", """{ "theme": "dark" }""");

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => _exchange.ReadAsync(path, TestContext.Current.CancellationToken));

        Assert.Equal("The file is not a launchpad shortcuts export.", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_OfAMissingFile_LetsTheIoErrorThrough()
        => await Assert.ThrowsAsync<FileNotFoundException>(
            () => _exchange.ReadAsync(_temp.Combine("missing.json"), TestContext.Current.CancellationToken));

    [Fact]
    public void TryParse_ToleratesComments_TrailingCommas_AndMissingCategories()
    {
        const string json = """
            {
              // Written by hand.
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "urls": [
                { "name": "Docs", "shortcuts": [ { "name": "Avalonia", "target": "https://docs.avaloniaui.net", }, ], },
              ],
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(ShortcutExchange.DefaultContentName, import.Content.Name);
        Assert.Empty(import.Content.Applications);
        Assert.Empty(import.Content.Directories);
        Assert.Equal("Avalonia", Assert.Single(Assert.Single(import.Content.Urls).Shortcuts).Name);
    }

    [Fact]
    public void TryParse_LeavesOutTheEntriesTheFormWouldRefuse_AndCountsThem()
    {
        string tool = JsonSerializer.Serialize(Absolute("tool.exe"));
        string folder = JsonSerializer.Serialize(Absolute("Projects"));
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "applications": [
                { "name": "Tools", "shortcuts": [
                  { "name": "Kept", "target": {{tool}} },
                  { "name": "  ", "target": {{tool}} },
                  { "name": "{{new string('x', 101)}}", "target": {{tool}} },
                  { "name": "Relative", "target": "bin\\tool.exe" },
                  { "name": "Empty", "target": "   " }
                ] },
                { "name": "", "shortcuts": [ { "name": "Lost with its group", "target": {{tool}} } ] }
              ],
              "directories": [
                { "name": "Work", "shortcuts": [ { "name": "Projects", "target": {{folder}} } ] }
              ],
              "urls": [
                { "name": "Links", "shortcuts": [
                  { "name": "Kept", "target": "https://example.com" },
                  { "name": "Other scheme", "target": "ftp://example.com" },
                  { "name": "Script", "target": "javascript:alert(1)" }
                ] }
              ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(8, import.SkippedEntries);
        Assert.Equal(["Kept"], Assert.Single(import.Content.Applications).Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(["Projects"], Assert.Single(import.Content.Directories).Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(["Kept"], Assert.Single(import.Content.Urls).Shortcuts.Select(shortcut => shortcut.Name));
    }

    [Fact]
    public void TryParse_PutsEntriesInTheFormTheShortcutFormStores()
    {
        string tool = JsonSerializer.Serialize(Absolute("tool.exe"));
        string folder = JsonSerializer.Serialize(Absolute("Projects"));
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "profile": "  Home  ",
              "applications": [ { "name": " Tools ", "shortcuts": [
                { "name": " Editor ", "target": {{tool}}, "arguments": "  --wait  " },
                { "name": "Plain", "target": {{tool}}, "arguments": "   " }
              ] } ],
              "directories": [ { "name": "Work", "shortcuts": [ { "name": "Projects", "target": {{folder}}, "arguments": "--ignored" } ] } ],
              "urls": [ { "name": "Links", "shortcuts": [ { "name": "Example", "target": " example.com/start ", "arguments": "--ignored" } ] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        ShortcutGroup tools = Assert.Single(import.Content.Applications);
        Assert.Equal("Home", import.Content.Name);
        Assert.Equal("Tools", tools.Name);
        Assert.Equal(("Editor", "--wait"), (tools.Shortcuts[0].Name, tools.Shortcuts[0].Arguments));
        Assert.Null(tools.Shortcuts[1].Arguments);
        Assert.Null(Assert.Single(Assert.Single(import.Content.Directories).Shortcuts).Arguments);

        Shortcut link = Assert.Single(Assert.Single(import.Content.Urls).Shortcuts);
        Assert.Equal("https://example.com/start", link.Target);
        Assert.Null(link.Arguments);
    }

    [Fact]
    public void TryParse_AcceptsPathsThatDoNotExistOnThisMachine()
    {
        string missing = Absolute(Guid.NewGuid().ToString("N"), "tool.exe");
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "applications": [ { "name": "Tools", "shortcuts": [ { "name": "Elsewhere", "target": {{JsonSerializer.Serialize(missing)}} } ] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(0, import.SkippedEntries);
        Assert.Equal(missing, Assert.Single(Assert.Single(import.Content.Applications).Shortcuts).Target);
    }

    [Fact]
    public void TryParse_GivesEveryGroupAndShortcutANewIdentity()
    {
        string json = ShortcutExchange.Serialize(Sample());

        Assert.True(_exchange.TryParse(json, out ShortcutImport? first, out _));
        Assert.True(_exchange.TryParse(json, out ShortcutImport? second, out _));

        Assert.Empty(Identities(first.Content.Urls).Intersect(Identities(second.Content.Urls)));
    }

    private static Profile Sample()
        => Profile.Create("Work")
            .With(ShortcutKind.Application,
            [
                new ShortcutGroup(
                    Guid.NewGuid(),
                    "Dev tools",
                    [
                        new Shortcut(Guid.NewGuid(), "Code", Absolute("Code", "Code.exe"), "--new-window"),
                        new Shortcut(Guid.NewGuid(), "Terminal", Absolute("Terminal", "wt.exe")),
                    ],
                    IsCollapsed: true),
                new ShortcutGroup(Guid.NewGuid(), "Empty", []),
            ])
            .With(ShortcutKind.Url,
            [
                new ShortcutGroup(Guid.NewGuid(), "Docs", [new Shortcut(Guid.NewGuid(), "Avalonia", "https://docs.avaloniaui.net")]),
            ]);

    private static string Absolute(params string[] parts) => Path.Combine([Path.GetTempPath(), "Enigma.Launchpad.Exchange", .. parts]);

    private static string[] Describe(IEnumerable<ShortcutGroup> groups)
        => groups
            .Select(group => $"{group.Name}: {string.Join(", ", group.Shortcuts.Select(shortcut => $"{shortcut.Name} → {shortcut.Target} {shortcut.Arguments}"))}")
            .ToArray();

    private static Guid[] Identities(IEnumerable<ShortcutGroup> groups)
        => groups.SelectMany(group => group.Shortcuts.Select(shortcut => shortcut.Id).Prepend(group.Id)).ToArray();
}
