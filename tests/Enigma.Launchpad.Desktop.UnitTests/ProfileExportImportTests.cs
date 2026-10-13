using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Exchange;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ProfileExportImportTests : IDisposable
{
    private static readonly string CodePath = Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tools", "Code.exe");

    private readonly TempDirectory _temp = new();
    private readonly FakeLaunchpadStore _store;
    private readonly LaunchpadSession _session;
    private readonly ScriptedDialogs _dialogs = new();
    private readonly FakePathPicker _picker = new();
    private readonly RecordingNotifications _notifications = new();
    private readonly ShortcutExchange _exchange = new(new ShortcutValidator());
    private readonly ProfilesPageViewModel _page;

    public ProfileExportImportTests()
    {
        LaunchpadData data = PageHarness.WorkAndHome();
        Profile work = data.Profiles[0]
            .With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Dev tools", [PageHarness.Shortcut("Code", CodePath, "--new-window")])])
            .With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net")])]);

        _store = new FakeLaunchpadStore(data with { Profiles = [work, data.Profiles[1]] });
        _session = new LaunchpadSession(_store, new RecordingNotifications(), NullLogger<LaunchpadSession>.Instance);
        _page = new ProfilesPageViewModel(_session, new ShortcutValidator(), _dialogs, _exchange, _picker, _notifications);
    }

    private ProfileItemViewModel Work => _page.Profiles.Single(profile => profile.Name == "Work");

    private ProfileItemViewModel Home => _page.Profiles.Single(profile => profile.Name == "Home");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Export_WritesTheTickedPart_ToThePickedFile_AndSaysWhere()
    {
        await _page.OnAppearingAsync();
        string path = _temp.Combine("Work shortcuts.json");
        _picker.NextJsonFileToSave = path;
        _dialogs.SelectionAnswers.Enqueue(dialog =>
        {
            dialog.Categories[1].IsChecked = false;
            return dialog.ToProfile();
        });

        await Work.ExportCommand.ExecuteAsync(null);

        (string title, string message, string confirm, Profile content) = Assert.Single(_dialogs.Selections);
        Assert.Equal("Export “Work”", title);
        Assert.Equal("Tick the groups and shortcuts of “Work” to write to the file.", message);
        Assert.Equal("Export…", confirm);
        Assert.Equal(_session.Profiles[0].Id, content.Id);
        Assert.Equal(("Export “Work”", "Work shortcuts.json"), Assert.Single(_picker.JsonRequests));

        ShortcutImport written = await _exchange.ReadAsync(path, TestContext.Current.CancellationToken);
        Shortcut code = Assert.Single(Assert.Single(written.Content.Applications).Shortcuts);
        Assert.Equal(("Code", CodePath, "--new-window"), (code.Name, code.Target, code.Arguments));
        Assert.Empty(written.Content.Urls);

        Assert.Equal(("Success", "“Work” exported", $"1 group · 1 shortcut written to {path}."), Assert.Single(_notifications.Shown));
    }

    [Fact]
    public async Task Export_CancelledInTheDialog_DoesNotAskForAFile()
    {
        await _page.OnAppearingAsync();

        await Work.ExportCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Selections);
        Assert.Empty(_picker.JsonRequests);
        Assert.Empty(_notifications.Shown);
    }

    [Fact]
    public async Task Export_CancelledInThePicker_WritesNothing()
    {
        await _page.OnAppearingAsync();
        _dialogs.SelectionAnswers.Enqueue(ScriptedDialogs.SelectEverything);

        await Work.ExportCommand.ExecuteAsync(null);

        Assert.Single(_picker.JsonRequests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.Path));
        Assert.Empty(_notifications.Shown);
    }

    [Fact]
    public async Task Export_IsNotOffered_ForAProfileWithNoGroup()
    {
        await _page.OnAppearingAsync();
        _session.AddProfile("Lab");
        ProfileItemViewModel lab = _page.Profiles.Single(profile => profile.Name == "Lab");

        Assert.False(lab.ExportCommand.CanExecute(null));
        Assert.True(Home.ExportCommand.CanExecute(null));

        await _page.ExportAsync(lab);
        Assert.Empty(_dialogs.Selections);
    }

    [Fact]
    public async Task Export_ThatCannotBeWritten_SaysSo()
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToSave = _temp.Combine("missing folder", "Work shortcuts.json");
        _dialogs.SelectionAnswers.Enqueue(ScriptedDialogs.SelectEverything);

        await Work.ExportCommand.ExecuteAsync(null);

        (string severity, string title, _) = Assert.Single(_notifications.Shown);
        Assert.Equal(("Error", "The export could not be written"), (severity, title));
    }

    [Fact]
    public async Task Import_AddsTheTickedPart_ToTheChosenProfile_AndSaysWhatWasAdded()
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToOpen = await ExportFileAsync(Profile.Create("Shared")
            .With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "games", [PageHarness.Shortcut("Chess", CodePath)]), new ShortcutGroup(Guid.NewGuid(), "Emulators", [])])
            .With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "News", [PageHarness.Shortcut("Example", "https://example.com")])]));
        _dialogs.SelectionAnswers.Enqueue(dialog =>
        {
            dialog.Categories[1].IsChecked = false;
            return dialog.ToProfile();
        });

        await Home.ImportCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Equal<(string, string?)>(("Import into “Home”", null), Assert.Single(_picker.JsonRequests));
        (string title, string message, string confirm, _) = Assert.Single(_dialogs.Selections);
        Assert.Equal("Import into “Home”", title);
        Assert.Equal(
            "Tick what to add to “Home” from “Shared”. A group named like one of its own adds its shortcuts to it, and shortcuts it already has are skipped.",
            message);
        Assert.Equal("Import", confirm);

        LaunchpadData saved = _store.LastSaved ?? throw new InvalidOperationException("Nothing saved.");
        Profile home = saved.Profiles[1];
        Assert.Equal(["Games", "Emulators"], home.Applications.Select(group => group.Name));
        Assert.Equal(["Chess"], home.Applications[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Empty(home.Urls);
        Assert.Equal(["Dev tools"], saved.Profiles[0].Applications.Select(group => group.Name));
        Assert.Equal("Work", saved.ActiveProfile.Name);

        Assert.Equal(("Success", "Imported into “Home”", "1 shortcut and 1 new group added."), Assert.Single(_notifications.Shown));
        Assert.Equal("2 groups · 1 shortcut", Home.Summary);
    }

    [Fact]
    public async Task Import_OfAFileAlreadyImported_SaysEverythingWasAlreadyThere()
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToOpen = await ExportFileAsync(_session.Profiles[0]);
        _dialogs.SelectionAnswers.Enqueue(ScriptedDialogs.SelectEverything);

        await Work.ImportCommand.ExecuteAsync(null);

        Assert.Equal(("Success", "Imported into “Work”", "Everything ticked was already there."), Assert.Single(_notifications.Shown));
        Assert.Equal(2, _session.ActiveProfile.CountGroups());
        Assert.Equal(2, _session.ActiveProfile.CountShortcuts());
    }

    [Fact]
    public async Task Import_SaysHowManyEntriesOfTheFileWereLeftOut()
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToOpen = WriteFile("""
            {
              "format": "enigma-launchpad-shortcuts",
              "version": 1,
              "profile": "Shared",
              "urls": [ { "name": "Links", "shortcuts": [
                { "name": "Kept", "target": "https://example.com" },
                { "name": "Script", "target": "javascript:alert(1)" }
              ] } ]
            }
            """);

        await Home.ImportCommand.ExecuteAsync(null);

        Assert.EndsWith(" 1 entry of the file was left out because it is not valid.", Assert.Single(_dialogs.Selections).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_OfAFileThatIsNotAnExport_SaysWhy_AndChangesNothing()
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToOpen = WriteFile("""{ "theme": "dark" }""");

        await Home.ImportCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Equal(("Error", "This file cannot be imported", "The file is not a launchpad shortcuts export."), Assert.Single(_notifications.Shown));
        Assert.Empty(_dialogs.Selections);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Import_OfAFileThatCannotBeRead_SaysSo()
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToOpen = _temp.Combine("gone.json");

        await Home.ImportCommand.ExecuteAsync(null);

        (string severity, string title, _) = Assert.Single(_notifications.Shown);
        Assert.Equal(("Error", "The file could not be read"), (severity, title));
        Assert.Empty(_dialogs.Selections);
    }

    [Theory]
    [InlineData("[]", "The file holds no group.")]
    [InlineData("""[{ "name": "", "shortcuts": [{ "name": "Lost", "target": "https://example.com" }] }]""", "2 entries of the file were left out because they are not valid. Nothing else is in it.")]
    public async Task Import_OfAFileWithNothingToTake_Warns_WithoutAsking(string urls, string expected)
    {
        await _page.OnAppearingAsync();
        _picker.NextJsonFileToOpen = WriteFile($$"""{ "format": "enigma-launchpad-shortcuts", "version": 1, "urls": {{urls}} }""");

        await Home.ImportCommand.ExecuteAsync(null);

        Assert.Equal(("Warning", "Nothing to import", expected), Assert.Single(_notifications.Shown));
        Assert.Empty(_dialogs.Selections);
    }

    [Fact]
    public async Task Import_CancelledInThePicker_OrTheDialog_ChangesNothing()
    {
        await _page.OnAppearingAsync();

        await Home.ImportCommand.ExecuteAsync(null);
        Assert.Empty(_dialogs.Selections);

        _picker.NextJsonFileToOpen = await ExportFileAsync(_session.Profiles[0]);
        await Home.ImportCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Single(_dialogs.Selections);
        Assert.Empty(_store.Saved);
        Assert.Empty(_notifications.Shown);
    }

    [Theory]
    [InlineData("Work", "Work shortcuts.json")]
    [InlineData("  Work  ", "Work shortcuts.json")]
    [InlineData("Work/Home", "Work_Home shortcuts.json")]
    public void ExportFileName_IsNamedAfterTheProfile_AndAlwaysAFileName(string profile, string expected)
        => Assert.Equal(expected, ProfilesPageViewModel.ExportFileName(profile));

    [Theory]
    [InlineData(0, 0, "Everything ticked was already there.")]
    [InlineData(0, 1, "1 shortcut added.")]
    [InlineData(2, 0, "2 new groups added.")]
    [InlineData(1, 3, "3 shortcuts and 1 new group added.")]
    public void Added_WordsWhatAnImportAdded(int groups, int shortcuts, string expected)
        => Assert.Equal(expected, ProfilesPageViewModel.Added(groups, shortcuts));

    private async Task<string> ExportFileAsync(Profile content)
    {
        string path = _temp.Combine($"{Guid.NewGuid():N}.json");
        await _exchange.ExportAsync(path, content, TestContext.Current.CancellationToken);
        return path;
    }

    private string WriteFile(string json)
    {
        string path = _temp.Combine($"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
