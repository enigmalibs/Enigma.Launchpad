using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// An open workspace holding groups of every kind: each group, and each tile in it, behaves as its kind says.
/// </summary>
public sealed class WorkspacePageKindsTests
{
    [Theory]
    [InlineData(ShortcutKind.Application, "New applications group", PhosphorIcon.AppWindow, "No applications in this group yet. Add one with +.")]
    [InlineData(ShortcutKind.Directory, "New folders group", PhosphorIcon.Folder, "No folders in this group yet. Add one with +.")]
    [InlineData(ShortcutKind.Url, "New links group", PhosphorIcon.Globe, "No links in this group yet. Add one with +.")]
    [InlineData(ShortcutKind.Document, "New documents group", PhosphorIcon.FileText, "No documents in this group yet. Add one with +.")]
    public async Task NewGroup_IsOfTheKindChosen(ShortcutKind kind, string title, PhosphorIcon glyph, string emptyText)
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"), kind);
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.NameAnswers.Enqueue("New");

        await page.NewGroupCommand.ExecuteAsync(kind);

        Assert.Equal(title, harness.Dialogs.NamePrompts[0].Title);
        GroupViewModel group = page.Groups[^1];
        Assert.Equal((kind, glyph, emptyText), (group.Kind, group.KindGlyph, group.EmptyText));
        Assert.Equal(("New", kind), (await harness.SavedGroupsAsync()).Select(saved => (saved.Name, saved.Kind)).Last());
    }

    [Fact]
    public async Task AGroupsName_IsCheckedOnlyAgainstGroupsOfItsKind()
    {
        PageHarness harness = new(PageHarness.Data("Tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Directory);
        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Application);

        Assert.Null(harness.Dialogs.NamePrompts[0].Validate("tools"));
        Assert.Equal("A group with this name already exists.", harness.Dialogs.NamePrompts[1].Validate("tools"));
    }

    [Fact]
    public async Task Renaming_IsCheckedOnlyAgainstGroupsOfItsKind()
    {
        LaunchpadData data = PageHarness.Data("Tools", "Games").With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[2].RenameCommand.ExecuteAsync(null);
        await page.Groups[0].RenameCommand.ExecuteAsync(null);

        Assert.Null(harness.Dialogs.NamePrompts[0].Validate("Tools"));
        Assert.Equal("A group with this name already exists.", harness.Dialogs.NamePrompts[1].Validate("games"));
        Assert.Null(harness.Dialogs.NamePrompts[1].Validate("docs"));
    }

    [Fact]
    public async Task ATile_TakesItsKindFromItsGroup()
    {
        LaunchpadData data = PageHarness.WithShortcuts(ShortcutKind.Application, ("Tools", [PageHarness.Shortcut("Code")]))
            .With(ShortcutKind.Directory, [new ShortcutGroup(Guid.NewGuid(), "Repos", ShortcutKind.Directory, [PageHarness.Shortcut("Repo", @"C:\Repo")])])
            .With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [PageHarness.Shortcut("Docs", "https://docs.example.com/start")])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();

        ShortcutViewModel code = page.Groups[0].Shortcuts[0];
        ShortcutViewModel repo = page.Groups[1].Shortcuts[0];
        ShortcutViewModel docs = page.Groups[2].Shortcuts[0];

        Assert.Equal([PhosphorIcon.AppWindow, PhosphorIcon.Folder, PhosphorIcon.Globe], new[] { code, repo, docs }.Select(tile => tile.Icon));
        Assert.Equal([true, false, false], new[] { code, repo, docs }.Select(tile => tile.CanRunWithArguments));
        Assert.Equal([false, true, false], new[] { code, repo, docs }.Select(tile => tile.CanOpenInTerminal));
        Assert.Equal([true, true, false], new[] { code, repo, docs }.Select(tile => tile.CanReveal));
        Assert.Equal("docs.example.com", docs.Subtitle);

        repo.OpenCommand.Execute(null);
        await docs.EditCommand.ExecuteAsync(null);

        Assert.Equal(ShortcutKind.Directory, Assert.Single(harness.Actions.Performed).Kind);
        Assert.Equal(ShortcutKind.Url, Assert.Single(harness.Dialogs.ShortcutForms).Kind);
    }

    [Fact]
    public async Task ADocumentTile_CopiesItsPath_ShowsItInItsFolder_AndFindsItsIcon()
    {
        LaunchpadData data = PageHarness.Data("Tools")
            .With(ShortcutKind.Document, [new ShortcutGroup(Guid.NewGuid(), "Papers", ShortcutKind.Document, [PageHarness.Shortcut("Report", @"C:\Docs\report.pdf")])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        ShortcutViewModel report = page.Groups[1].Shortcuts[0];

        Assert.Equal(PhosphorIcon.FileText, report.Icon);
        Assert.Equal((false, false), (report.CanRunWithArguments, report.CanOpenInTerminal));
        Assert.Equal((true, "Copy path", true, true), (report.CanCopyFromTile, report.CopyTargetLabel, report.CanReveal, report.CanFindIcon));
        Assert.Equal(@"C:\Docs\report.pdf", report.Subtitle);

        report.OpenCommand.Execute(null);
        report.RevealCommand.Execute(null);

        Assert.Equal([("Open", ShortcutKind.Document), ("Reveal", ShortcutKind.Document)], harness.Actions.Performed.Select(action => (action.Action, action.Kind)));
    }

    [Fact]
    public async Task TryToGetIcon_OfADocument_SaysItLooksForTheFilesIcon()
    {
        Shortcut chosen = PageHarness.Shortcut("Report", @"C:\Docs\report.pdf") with { Icon = new ShortcutIcon("iVBORw0KGgo=", IsCustom: true) };
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Document, ("Papers", [chosen])));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[0].Shortcuts[0].FindIconCommand.ExecuteAsync(null);

        Assert.Equal("\u201CReport\u201D uses an image you chose. Look for the file's own icon and use it instead?", Assert.Single(harness.Dialogs.Questions).Message);
    }

    [Fact]
    public async Task AShortcutAdded_IsOfItsGroupsKind()
    {
        LaunchpadData data = PageHarness.Data("Tools").With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[1].AddShortcutCommand.ExecuteAsync(null);

        Assert.Equal(ShortcutKind.Url, Assert.Single(harness.Dialogs.ShortcutForms).Kind);
    }

    [Fact]
    public async Task MoveToWorkspace_MovesTheGroupWithItsShortcuts_ToTheChosenWorkspace()
    {
        PageHarness harness = new(TwoWorkspaces());
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.WorkspaceChoiceAnswers.Enqueue("Reading");

        await page.Groups[0].MoveToWorkspaceCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        (string title, string message, Workspace[] offered) = Assert.Single(harness.Dialogs.WorkspaceChoices);
        Assert.Equal("Move \u201CTools\u201D", title);
        Assert.Contains("\u201CTools\u201D", message, StringComparison.Ordinal);
        Assert.Equal(["Reading"], offered.Select(workspace => workspace.Title));

        Workspace[] saved = [.. harness.Store.LastSaved?.ActiveProfile.Workspaces ?? []];
        Assert.Equal(["Links"], saved[0].Groups.Select(group => group.Name));
        Assert.Equal(["Docs", "Tools"], saved[1].Groups.Select(group => group.Name));
        Assert.Equal(["Code"], saved[1].Groups[1].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Empty(harness.Notifications.Shown);
    }

    [Fact]
    public async Task MoveToWorkspace_IsRefused_WhenTheTargetHasAGroupOfTheSameNameAndKind()
    {
        LaunchpadData data = TwoWorkspaces();
        Workspace reading = data.ActiveProfile.Workspaces[1];
        data = data.WithProfile(data.ActiveProfile.ReplaceWorkspace(reading with
        {
            Groups = [.. reading.Groups, new ShortcutGroup(Guid.NewGuid(), "tools", ShortcutKind.Application, [])],
        }));
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.WorkspaceChoiceAnswers.Enqueue("Reading");

        await page.Groups[0].MoveToWorkspaceCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        (string severity, string title, string message) = Assert.Single(harness.Notifications.Shown);
        Assert.Equal(("Warning", "The group was not moved"), (severity, title));
        Assert.Equal("\u201CReading\u201D already has an applications group called \u201CTools\u201D. Rename one of them first.", message);
        Assert.Equal(["Tools", "Links"], page.Groups.Select(group => group.Name));
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task MoveToWorkspace_Cancelled_ChangesNothing()
    {
        PageHarness harness = new(TwoWorkspaces());
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[0].MoveToWorkspaceCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Single(harness.Dialogs.WorkspaceChoices);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task MoveToWorkspace_WithNoOtherWorkspace_SaysToCreateOne()
    {
        PageHarness harness = new(PageHarness.Data("Tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[0].MoveToWorkspaceCommand.ExecuteAsync(null);

        Assert.Empty(harness.Dialogs.WorkspaceChoices);
        (string severity, string title, _) = Assert.Single(harness.Notifications.Shown);
        Assert.Equal(("Warning", "No other workspace"), (severity, title));
    }

    [Fact]
    public void TheChoiceDialog_StartsWithNothingChosen_AndFollowsTheChoice()
    {
        Workspace dev = new(Guid.NewGuid(), "Dev", [], Icon: "Code");
        WorkspaceChoiceViewModel dialog = new("Choose.", [dev, Workspace.Create("Reading")]);

        Assert.Equal("Choose.", dialog.Message);
        Assert.Equal(["Dev", "Reading"], dialog.Choices.Select(choice => choice.Title));
        Assert.Equal(PhosphorIcon.Code, dialog.Choices[0].Glyph);
        Assert.False(dialog.HasSelection);

        dialog.Selected = dialog.Choices[0];

        Assert.True(dialog.HasSelection);
        Assert.Same(dev, dialog.Selected.Workspace);
    }

    [Theory]
    [InlineData(ShortcutKind.Application, "Applications", "an applications")]
    [InlineData(ShortcutKind.Directory, "Folders", "a folders")]
    [InlineData(ShortcutKind.Url, "Links", "a links")]
    [InlineData(ShortcutKind.Document, "Documents", "a documents")]
    public void AKind_IsNamedForPeople(ShortcutKind kind, string title, string article)
    {
        Assert.Equal(title, new GroupKindOption(kind).Title);
        Assert.Equal(article, GroupKindOption.Article(kind));
    }

    // "Dev" (open first): an applications group "Tools" with "Code", and a links group "Links"; "Reading": "Docs".
    private static LaunchpadData TwoWorkspaces()
    {
        Profile profile = new(
            Guid.NewGuid(),
            "Work",
            [
                new Workspace(
                    Guid.NewGuid(),
                    "Dev",
                    [
                        new ShortcutGroup(Guid.NewGuid(), "Tools", ShortcutKind.Application, [PageHarness.Shortcut("Code")]),
                        new ShortcutGroup(Guid.NewGuid(), "Links", ShortcutKind.Url, []),
                    ]),
                new Workspace(Guid.NewGuid(), "Reading", [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [])]),
            ]);

        return LaunchpadData.Of(profile);
    }
}
