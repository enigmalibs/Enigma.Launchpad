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
    public void Serialize_WritesTheMarker_TheVersion_TheProfileName_AndTheWorkspaces()
    {
        using JsonDocument json = JsonDocument.Parse(ShortcutExchange.Serialize(Sample()));
        JsonElement root = json.RootElement;

        Assert.Equal(["format", "version", "profile", "workspaces"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(ShortcutExchange.FormatName, root.GetProperty("format").GetString());
        Assert.Equal(2, root.GetProperty("version").GetInt32());
        Assert.Equal("Work", root.GetProperty("profile").GetString());
        Assert.Equal(2, root.GetProperty("workspaces").GetArrayLength());
    }

    [Fact]
    public void Serialize_LeavesOutIdentities_FoldState_AndAbsentDescriptionsArgumentsAndIcons()
    {
        using JsonDocument json = JsonDocument.Parse(ShortcutExchange.Serialize(Sample()));
        JsonElement[] workspaces = [.. json.RootElement.GetProperty("workspaces").EnumerateArray()];
        JsonElement tools = workspaces[0].GetProperty("groups")[0];
        JsonElement[] shortcuts = [.. tools.GetProperty("shortcuts").EnumerateArray()];

        Assert.Equal(["title", "description", "icon", "groups"], workspaces[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["title", "groups"], workspaces[1].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["name", "kind", "shortcuts"], tools.EnumerateObject().Select(property => property.Name));
        Assert.Equal("application", tools.GetProperty("kind").GetString());
        Assert.Equal("directory", workspaces[0].GetProperty("groups")[2].GetProperty("kind").GetString());
        Assert.Equal("url", workspaces[1].GetProperty("groups")[0].GetProperty("kind").GetString());
        Assert.Equal(["name", "target", "arguments", "description", "icon"], shortcuts[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(["name", "target"], shortcuts[1].EnumerateObject().Select(property => property.Name));
        Assert.Equal("--new-window", shortcuts[0].GetProperty("arguments").GetString());
        Assert.Equal("The editor", shortcuts[0].GetProperty("description").GetString());
        Assert.Equal(Convert.ToBase64String(SamplePng.Bytes), shortcuts[0].GetProperty("icon").GetProperty("png").GetString());
        Assert.True(shortcuts[0].GetProperty("icon").GetProperty("isCustom").GetBoolean());
    }

    [Fact]
    public async Task ExportThenRead_GivesBackEveryWorkspaceGroupAndShortcut_InOrder_UnderNewIdentities()
    {
        Profile profile = Sample();
        string path = _temp.Combine("Work shortcuts.json");

        await _exchange.ExportAsync(path, profile, TestContext.Current.CancellationToken);
        ShortcutImport import = await _exchange.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(0, import.SkippedEntries);
        Assert.Equal("Work", import.Content.Name);
        Assert.NotEqual(profile.Id, import.Content.Id);
        Assert.Equal(Describe(profile), Describe(import.Content));
        Assert.Empty(Identities(profile).Intersect(Identities(import.Content)));
    }

    [Fact]
    public async Task ExportAsync_ReplacesAnExistingFile()
    {
        string path = _temp.CreateFile("export.json", "previous contents that are much longer than the new document would ever be, surely");

        await _exchange.ExportAsync(path, Profile.Create("Empty"), TestContext.Current.CancellationToken);

        Assert.True(_exchange.TryParse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), out ShortcutImport? import, out _));
        Assert.Equal(0, import.Content.CountWorkspaces());
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
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 3 }""", "The file is in format version 3, which this version of the launchpad cannot read.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 0 }""", "The file is in format version 0, which this version of the launchpad cannot read.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [null] }""", "The file is damaged: a workspace, a group or a shortcut is missing.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [{ "name": "Docs", "shortcuts": [null] }] }""", "The file is damaged: a workspace, a group or a shortcut is missing.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 2, "workspaces": [null] }""", "The file is damaged: a workspace, a group or a shortcut is missing.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 2, "workspaces": [{ "title": "Dev", "groups": [null] }] }""", "The file is damaged: a workspace, a group or a shortcut is missing.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [{ "name": "Docs", "shortcuts": [{ "name": "No target" }] }] }""", "The file is damaged: a workspace, a group or a shortcut is incomplete.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": [{ "shortcuts": [] }] }""", "The file is damaged: a workspace, a group or a shortcut is incomplete.")]
    [InlineData("""{ "format": "enigma-launchpad-shortcuts", "version": 2, "workspaces": [{ "groups": [] }] }""", "The file is damaged: a workspace, a group or a shortcut is incomplete.")]
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
    public void TryParse_OfVersion1_PutsTheCategoriesInOneWorkspace_TypedByCategory()
    {
        string tool = JsonSerializer.Serialize(Absolute("tool.exe"));
        string folder = JsonSerializer.Serialize(Absolute("Projects"));
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "profile": "Old",
              "applications": [ { "name": "Tools", "shortcuts": [ { "name": "Tool", "target": {{tool}} } ] } ],
              "directories": [ { "name": "Tools", "shortcuts": [ { "name": "Projects", "target": {{folder}} } ] } ],
              "urls": [ { "name": "Docs", "shortcuts": [] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal("Old", import.Content.Name);
        Workspace workspace = Assert.Single(import.Content.Workspaces);
        Assert.Equal(Workspace.DefaultTitle, workspace.Title);
        Assert.Equal(
            [("Tools", ShortcutKind.Application), ("Tools", ShortcutKind.Directory), ("Docs", ShortcutKind.Url)],
            workspace.Groups.Select(group => (group.Name, group.Kind)));
    }

    [Fact]
    public void TryParse_OfVersion1_WithNoGroup_GivesNoWorkspace()
    {
        Assert.True(_exchange.TryParse("""{ "format": "enigma-launchpad-shortcuts", "version": 1 }""", out ShortcutImport? import, out _));

        Assert.Empty(import.Content.Workspaces);
    }

    [Fact]
    public void TryParse_ToleratesComments_TrailingCommas_AndMissingLists()
    {
        const string json = """
            {
              // Written by hand.
              "format": "enigma-launchpad-shortcuts",
              "version": 2,
              "workspaces": [
                { "title": "Reading", "groups": [
                  { "name": "Docs", "kind": "url", "shortcuts": [ { "name": "Avalonia", "target": "https://docs.avaloniaui.net", }, ], },
                  { "name": "Empty", "kind": "Directory" },
                ], },
                { "title": "Nothing in it" },
              ],
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(ShortcutExchange.DefaultContentName, import.Content.Name);
        Assert.Equal(["Reading", "Nothing in it"], import.Content.Workspaces.Select(workspace => workspace.Title));
        ShortcutGroup[] groups = [.. import.Content.Workspaces[0].Groups];
        Assert.Equal([ShortcutKind.Url, ShortcutKind.Directory], groups.Select(group => group.Kind));
        Assert.Equal("Avalonia", Assert.Single(groups[0].Shortcuts).Name);
        Assert.Empty(groups[1].Shortcuts);
        Assert.Empty(import.Content.Workspaces[1].Groups);
    }

    [Fact]
    public void TryParse_LeavesOutTheEntriesTheFormsWouldRefuse_AndCountsThem()
    {
        string tool = JsonSerializer.Serialize(Absolute("tool.exe"));
        string folder = JsonSerializer.Serialize(Absolute("Projects"));
        string relative = JsonSerializer.Serialize(Path.Combine("bin", "tool.exe"));
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 2,
              "workspaces": [
                { "title": "Kept", "groups": [
                  { "name": "Tools", "kind": "application", "shortcuts": [
                    { "name": "Kept", "target": {{tool}} },
                    { "name": "  ", "target": {{tool}} },
                    { "name": "{{new string('x', 101)}}", "target": {{tool}} },
                    { "name": "Relative", "target": {{relative}} },
                    { "name": "Empty", "target": "   " },
                    { "name": "Long description", "target": {{tool}}, "description": "{{new string('x', 201)}}" }
                  ] },
                  { "name": "", "kind": "application", "shortcuts": [ { "name": "Lost with its group", "target": {{tool}} } ] },
                  { "name": "Sheets", "kind": "spreadsheet", "shortcuts": [ { "name": "Lost with its unknown kind", "target": {{tool}} } ] },
                  { "name": "No kind", "shortcuts": [] },
                  { "name": "Work", "kind": "directory", "shortcuts": [ { "name": "Projects", "target": {{folder}} } ] },
                  { "name": "Links", "kind": "url", "shortcuts": [
                    { "name": "Kept", "target": "https://example.com" },
                    { "name": "Other scheme", "target": "ftp://example.com" },
                    { "name": "Script", "target": "javascript:alert(1)" }
                  ] }
                ] },
                { "title": "   ", "groups": [ { "name": "Lost", "kind": "url", "shortcuts": [ { "name": "Lost", "target": "https://example.com" } ] } ] },
                { "title": "Too long a description", "description": "{{new string('x', 201)}}" }
              ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        // Shortcuts: 5 in Tools and 2 in Links. Groups with their shortcuts: 2 (no name), 2 (unknown kind), 1 (no kind).
        // Workspaces with their content: 3 (no title), 1 (description too long).
        Assert.Equal(16, import.SkippedEntries);
        Workspace kept = Assert.Single(import.Content.Workspaces);
        Assert.Equal(["Tools", "Work", "Links"], kept.Groups.Select(group => group.Name));
        Assert.Equal(["Kept"], kept.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(["Projects"], kept.Groups[1].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Equal(["Kept"], kept.Groups[2].Shortcuts.Select(shortcut => shortcut.Name));
    }

    [Fact]
    public void TryParse_PutsEntriesInTheFormTheFormsStore()
    {
        string tool = JsonSerializer.Serialize(Absolute("tool.exe"));
        string folder = JsonSerializer.Serialize(Absolute("Projects"));
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 2,
              "profile": "  Home  ",
              "workspaces": [ { "title": "  Dev  ", "description": "   ", "icon": "  Code  ", "groups": [
                { "name": " Tools ", "kind": " APPLICATION ", "shortcuts": [
                  { "name": " Editor ", "target": {{tool}}, "arguments": "  --wait  ", "description": "  The editor  " },
                  { "name": "Plain", "target": {{tool}}, "arguments": "   ", "description": "   " }
                ] },
                { "name": "Work", "kind": "directory", "shortcuts": [ { "name": "Projects", "target": {{folder}}, "arguments": "--ignored" } ] },
                { "name": "Links", "kind": "url", "shortcuts": [ { "name": "Example", "target": " example.com/start ", "arguments": "--ignored" } ] }
              ] },
              { "title": "Described", "description": "  Words  ", "icon": "not a glyph name!" } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal("Home", import.Content.Name);
        Workspace dev = import.Content.Workspaces[0];
        Assert.Equal(("Dev", null, "Code"), (dev.Title, dev.Description, dev.Icon));
        Assert.Equal(("Words", null), (import.Content.Workspaces[1].Description, import.Content.Workspaces[1].Icon));

        ShortcutGroup tools = dev.Groups[0];
        Assert.Equal(("Tools", ShortcutKind.Application), (tools.Name, tools.Kind));
        Assert.Equal(("Editor", "--wait", "The editor"), (tools.Shortcuts[0].Name, tools.Shortcuts[0].Arguments, tools.Shortcuts[0].Description));
        Assert.Null(tools.Shortcuts[1].Arguments);
        Assert.Null(tools.Shortcuts[1].Description);
        Assert.Null(Assert.Single(dev.Groups[1].Shortcuts).Arguments);

        Shortcut link = Assert.Single(dev.Groups[2].Shortcuts);
        Assert.Equal("https://example.com/start", link.Target);
        Assert.Null(link.Arguments);
    }

    [Fact]
    public void TryParse_DropsAnIconThatIsNotAPngImage_AndKeepsItsShortcut()
    {
        string tool = JsonSerializer.Serialize(Absolute("tool.exe"));
        string png = Convert.ToBase64String(SamplePng.Bytes);
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "applications": [ { "name": "Tools", "shortcuts": [
                { "name": "Chosen", "target": {{tool}}, "icon": { "png": "{{png}}", "isCustom": true } },
                { "name": "Found", "target": {{tool}}, "icon": { "png": "{{png}}" } },
                { "name": "Not base64", "target": {{tool}}, "icon": { "png": "%%%" } },
                { "name": "Not a PNG", "target": {{tool}}, "icon": { "png": "R0lGODlhAQABAAAAACw=" } },
                { "name": "No image", "target": {{tool}}, "icon": { "isCustom": true } }
              ] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(0, import.SkippedEntries);
        Shortcut[] shortcuts = [.. Assert.Single(Groups(import)).Shortcuts];
        Assert.Equal(new ShortcutIcon(png, IsCustom: true), shortcuts[0].Icon);
        Assert.Equal(new ShortcutIcon(png, IsCustom: false), shortcuts[1].Icon);
        Assert.All(shortcuts[2..], shortcut => Assert.Null(shortcut.Icon));
    }

    [Fact]
    public void TryParse_AcceptsACommand_AsItIs_WhetherOrNotThisMachineHasIt()
    {
        string json = """
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "applications": [ { "name": "Tools", "shortcuts": [
                { "name": "Code", "target": " code ", "arguments": "--wait" },
                { "name": "Nowhere", "target": "no-such-command-anywhere" }
              ] } ],
              "directories": [ { "name": "Work", "shortcuts": [ { "name": "Relative", "target": "Projects" } ] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(1, import.SkippedEntries);
        ShortcutGroup[] groups = Groups(import);
        Shortcut[] applications = [.. groups[0].Shortcuts];
        Assert.Equal(("code", "--wait"), (applications[0].Target, applications[0].Arguments));
        Assert.Equal("no-such-command-anywhere", applications[1].Target);
        Assert.Empty(groups[1].Shortcuts);
    }

    [Fact]
    public void TryParse_AcceptsPathsThatDoNotExistOnThisMachine()
    {
        string missing = Absolute(Guid.NewGuid().ToString("N"), "tool.exe");
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 2,
              "workspaces": [ { "title": "Dev", "groups": [ { "name": "Tools", "kind": "application", "shortcuts": [ { "name": "Elsewhere", "target": {{JsonSerializer.Serialize(missing)}} } ] } ] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(0, import.SkippedEntries);
        Assert.Equal(missing, Assert.Single(Assert.Single(Groups(import)).Shortcuts).Target);
    }

    [Fact]
    public void TryParse_ReadsADocumentsGroup_WithFullPathsAndNoArguments()
    {
        string report = JsonSerializer.Serialize(Absolute("report.pdf"));
        string json = $$"""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 2,
              "workspaces": [ { "title": "Papers", "groups": [ { "name": "Reports", "kind": "document", "shortcuts": [
                { "name": "Report", "target": {{report}}, "arguments": "--ignored" },
                { "name": "Relative", "target": "report.pdf" },
                { "name": "Command", "target": "notepad" }
              ] } ] } ]
            }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(2, import.SkippedEntries);
        ShortcutGroup group = Assert.Single(Groups(import));
        Assert.Equal(ShortcutKind.Document, group.Kind);
        Shortcut kept = Assert.Single(group.Shortcuts);
        Assert.Equal((Absolute("report.pdf"), null), (kept.Target, kept.Arguments));
    }

    [Fact]
    public void Serialize_WritesADocumentsGroupsKind()
    {
        Profile profile = new(
            Guid.NewGuid(),
            "Work",
            [new Workspace(Guid.NewGuid(), "Papers", [new ShortcutGroup(Guid.NewGuid(), "Reports", ShortcutKind.Document, [])])]);

        using JsonDocument json = JsonDocument.Parse(ShortcutExchange.Serialize(profile));

        Assert.Equal("document", json.RootElement.GetProperty("workspaces")[0].GetProperty("groups")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void Serialize_WritesAGroupsWidth_OnlyWhenItIsNotTheFullWidth()
    {
        using JsonDocument json = JsonDocument.Parse(ShortcutExchange.Serialize(Sample()));
        JsonElement[] groups = [.. json.RootElement.GetProperty("workspaces")[0].GetProperty("groups").EnumerateArray()];

        Assert.False(groups[0].TryGetProperty("width", out _));
        Assert.Equal("oneThird", groups[1].GetProperty("width").GetString());
        Assert.Equal("twoThirds", groups[2].GetProperty("width").GetString());
    }

    [Theory]
    [InlineData(""" "width": "OneThird", """, GroupWidth.OneThird)]
    [InlineData(""" "width": " twothirds ", """, GroupWidth.TwoThirds)]
    [InlineData(""" "width": "full", """, GroupWidth.Full)]
    [InlineData(""" "width": "half", """, GroupWidth.Full)]
    [InlineData(""" "width": "", """, GroupWidth.Full)]
    [InlineData(""" "width": null, """, GroupWidth.Full)]
    [InlineData("", GroupWidth.Full)]
    public void TryParse_ReadsAGroupsWidth_IgnoringCase_AndAnyOtherAsTheFullWidth(string width, GroupWidth expected)
    {
        string json = $$"""
            { "format": "enigma-launchpad-shortcuts", "version": 2,
              "workspaces": [ { "title": "Dev", "groups": [ { "name": "Tools", "kind": "application", {{width}} "shortcuts": [] } ] } ] }
            """;

        Assert.True(_exchange.TryParse(json, out ShortcutImport? import, out _));

        Assert.Equal(0, import.SkippedEntries);
        Assert.Equal(expected, Assert.Single(Groups(import)).Width);
    }

    [Fact]
    public void TryParse_GivesEveryWorkspaceGroupAndShortcutANewIdentity()
    {
        string json = ShortcutExchange.Serialize(Sample());

        Assert.True(_exchange.TryParse(json, out ShortcutImport? first, out _));
        Assert.True(_exchange.TryParse(json, out ShortcutImport? second, out _));

        Assert.Empty(Identities(first.Content).Intersect(Identities(second.Content)));
    }

    private static ShortcutGroup[] Groups(ShortcutImport import)
        => [.. import.Content.Workspaces.SelectMany(workspace => workspace.Groups)];

    private static Profile Sample()
        => new(
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
                                new Shortcut(Guid.NewGuid(), "Code", Absolute("Code", "Code.exe"), "--new-window", "The editor", ShortcutIcon.FromBytes(SamplePng.Bytes, isCustom: true)),
                                new Shortcut(Guid.NewGuid(), "Terminal", Absolute("Terminal", "wt.exe")),
                            ],
                            IsCollapsed: true),
                        new ShortcutGroup(Guid.NewGuid(), "Empty", ShortcutKind.Application, [], Width: GroupWidth.OneThird),
                        new ShortcutGroup(Guid.NewGuid(), "Repos", ShortcutKind.Directory, [new Shortcut(Guid.NewGuid(), "Launchpad", Absolute("Repos", "Launchpad"))], Width: GroupWidth.TwoThirds),
                    ],
                    "Everything for coding",
                    "Code"),
                new Workspace(
                    Guid.NewGuid(),
                    "Reading",
                    [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [new Shortcut(Guid.NewGuid(), "Avalonia", "https://docs.avaloniaui.net")])]),
            ]);

    private static string Absolute(params string[] parts) => Path.Combine([Path.GetTempPath(), "Enigma.Launchpad.Exchange", .. parts]);

    private static string[] Describe(Profile profile)
        => profile.Workspaces
            .SelectMany(workspace => workspace.Groups
                .Select(group => $"{workspace.Title} ({workspace.Description}, {workspace.Icon}) / {group.Kind} {group.Name} [{group.Width}]: "
                    + string.Join(", ", group.Shortcuts.Select(shortcut => $"{shortcut.Name} → {shortcut.Target} {shortcut.Arguments} ({shortcut.Description}) [{shortcut.Icon}]")))
                .DefaultIfEmpty($"{workspace.Title} ({workspace.Description}, {workspace.Icon})"))
            .ToArray();

    private static Guid[] Identities(Profile profile)
        => profile.Workspaces
            .SelectMany(workspace => workspace.Groups
                .SelectMany(group => group.Shortcuts.Select(shortcut => shortcut.Id).Prepend(group.Id))
                .Prepend(workspace.Id))
            .ToArray();
}
