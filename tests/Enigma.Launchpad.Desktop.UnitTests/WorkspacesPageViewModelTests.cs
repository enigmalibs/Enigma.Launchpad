using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WorkspacesPageViewModelTests
{
    private readonly FakeLaunchpadStore _store = new(Data());
    private readonly LaunchpadSession _session;
    private readonly ScriptedDialogs _dialogs = new();
    private readonly WorkspacesPageViewModel _page;

    public WorkspacesPageViewModelTests()
    {
        _session = new LaunchpadSession(_store, new RecordingNotifications(), NullLogger<LaunchpadSession>.Instance);
        _page = new WorkspacesPageViewModel(_session, new ShortcutValidator(), _dialogs, new RecordingShortcutActions(), new FakeShortcutIcons(), new RecordingNotifications());
    }

    private WorkspaceItemViewModel Dev => _page.Workspaces.Single(workspace => workspace.Title == "Dev");

    [Fact]
    public async Task OnAppearing_ShowsEveryWorkspace_WithItsLooks_AndWhatItHolds()
    {
        Assert.False(_page.NewWorkspaceCommand.CanExecute(null));

        await _page.OnAppearingAsync();

        Assert.Equal(["Dev", "Reading", "Spare"], _page.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(("Coding and tools", true, PhosphorIcon.Code), (Dev.Description, Dev.HasDescription, Dev.Glyph));
        Assert.Equal(["2 groups · 3 shortcuts", "1 group · 0 shortcuts", "Empty"], _page.Workspaces.Select(workspace => workspace.Summary));
        Assert.Equal(PhosphorIcon.SquaresFour, _page.Workspaces[1].Glyph);
        Assert.False(_page.Workspaces[1].HasDescription);
        Assert.True(_page.HasWorkspaces);
        Assert.False(_page.IsEmpty);
        Assert.False(_page.HasNoMatch);
        Assert.True(_page.NewWorkspaceCommand.CanExecute(null));
    }

    [Fact]
    public async Task AProfileWithoutWorkspaces_IsEmpty()
    {
        WorkspacesPageViewModel page = await EmptyPageAsync();

        Assert.True(page.IsEmpty);
        Assert.False(page.HasWorkspaces);
        Assert.False(page.HasNoMatch);
        Assert.Empty(page.Workspaces);
    }

    [Fact]
    public async Task OnlyTheFirstAndLastCards_CannotMoveUpAndDown()
    {
        await _page.OnAppearingAsync();

        Assert.Equal([false, true, true], _page.Workspaces.Select(workspace => workspace.MoveUpCommand.CanExecute(null)));
        Assert.Equal([true, true, false], _page.Workspaces.Select(workspace => workspace.MoveDownCommand.CanExecute(null)));
    }

    [Theory]
    [InlineData("dev", new[] { "Dev" })]
    [InlineData("  READ  ", new[] { "Reading" })]
    [InlineData("tools", new[] { "Dev" })]
    [InlineData("", new[] { "Dev", "Reading", "Spare" })]
    [InlineData("e", new[] { "Dev", "Reading", "Spare" })]
    public async Task TheSearch_ShowsTheCardsWhoseTitleOrDescriptionHoldsIt(string search, string[] expected)
    {
        await _page.OnAppearingAsync();

        _page.SearchText = search;

        Assert.Equal(expected, _page.Workspaces.Select(workspace => workspace.Title));
        Assert.False(_page.HasNoMatch);
    }

    [Fact]
    public async Task ASearchWithNoMatch_SaysSo_AndClearingItShowsEverythingAgain()
    {
        await _page.OnAppearingAsync();

        _page.SearchText = " games ";

        Assert.Empty(_page.Workspaces);
        Assert.True(_page.HasNoMatch);
        Assert.False(_page.IsEmpty);
        Assert.Equal("No workspace matches \u201Cgames\u201D.", _page.NoMatchText);

        _page.SearchText = string.Empty;

        Assert.Equal(3, _page.Workspaces.Count);
        Assert.False(_page.HasNoMatch);
    }

    [Fact]
    public async Task TheSearch_StillApplies_AfterAChange()
    {
        await _page.OnAppearingAsync();
        _page.SearchText = "dev";
        _dialogs.WorkspaceAnswers.Enqueue(ScriptedDialogs.FillWorkspace("Devices"));

        await _page.NewWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal(["Dev", "Devices"], _page.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public async Task NewWorkspace_AddsWhatTheFormSays_AtTheEnd_AndSavesIt()
    {
        await _page.OnAppearingAsync();
        _dialogs.WorkspaceAnswers.Enqueue(ScriptedDialogs.FillWorkspace("  Lab  ", " Experiments ", PhosphorIcon.Flask));

        await _page.NewWorkspaceCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        (Workspace? existing, string[] titles) = Assert.Single(_dialogs.WorkspaceForms);
        Assert.Null(existing);
        Assert.Equal(["Dev", "Reading", "Spare"], titles);

        Workspace lab = _store.LastSaved?.ActiveProfile.Workspaces[^1] ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal(("Lab", "Experiments", "Flask"), (lab.Title, lab.Description, lab.Icon));
        Assert.Equal("Lab", _page.Workspaces[^1].Title);
    }

    [Fact]
    public async Task NewWorkspace_Cancelled_ChangesNothing()
    {
        await _page.OnAppearingAsync();

        await _page.NewWorkspaceCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Single(_dialogs.WorkspaceForms);
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task NewWorkspace_RefusesATitleAnotherWorkspaceHas()
    {
        await _page.OnAppearingAsync();
        _dialogs.WorkspaceAnswers.Enqueue(form =>
        {
            form.Title = "DEV";
            Assert.Equal("A workspace with this title already exists.", form.VisibleTitleError);
            Assert.False(form.IsValid);
            return null;
        });

        await _page.NewWorkspaceCommand.ExecuteAsync(null);

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Edit_StartsFromTheWorkspace_ChecksTheTitleAgainstTheOthers_AndSavesTheChange()
    {
        await _page.OnAppearingAsync();
        _dialogs.WorkspaceAnswers.Enqueue(form =>
        {
            Assert.Equal(("Dev", "Coding and tools", PhosphorIcon.Code), (form.Title, form.Description, form.SelectedGlyph.Glyph));
            return ScriptedDialogs.FillWorkspace("Development", string.Empty, PhosphorIcon.TerminalWindow)(form);
        });

        await Dev.EditCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        (Workspace? existing, string[] titles) = Assert.Single(_dialogs.WorkspaceForms);
        Assert.Equal("Dev", existing?.Title);
        Assert.Equal(["Reading", "Spare"], titles);

        Workspace edited = _store.LastSaved?.ActiveProfile.Workspaces[0] ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal(("Development", null, "TerminalWindow"), (edited.Title, edited.Description, edited.Icon));
        Assert.Equal(2, edited.Groups.Count);
        Assert.Equal("Development", _page.Workspaces[0].Title);
    }

    [Fact]
    public async Task Edit_WithNothingChanged_SavesNothing()
    {
        await _page.OnAppearingAsync();
        _dialogs.WorkspaceAnswers.Enqueue(form => form.ToDetails());

        await _page.Workspaces[1].EditCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Duplicate_ProposesAFreeTitle_AndPutsTheCopyAfterTheOriginal()
    {
        await _page.OnAppearingAsync();
        _dialogs.NameAnswers.Enqueue("Dev (copy)");

        await Dev.DuplicateCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        ScriptedDialogs.NamePrompt prompt = Assert.Single(_dialogs.NamePrompts);
        Assert.Equal(("Duplicate workspace", "Dev (copy)"), (prompt.Title, prompt.InitialName));
        Assert.Equal("A workspace with this title already exists.", prompt.Validate("reading"));
        Assert.Null(prompt.Validate("Dev 2"));

        Assert.Equal(["Dev", "Dev (copy)", "Reading", "Spare"], _page.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal("2 groups · 3 shortcuts", _page.Workspaces[1].Summary);
        Assert.Equal(4, _store.LastSaved?.ActiveProfile.Workspaces.Count);
    }

    [Fact]
    public async Task Delete_AsksFirst_SayingWhatIsLost_AndDeletesOnlyOnceConfirmed()
    {
        await _page.OnAppearingAsync();

        await Dev.DeleteCommand.ExecuteAsync(null);

        (string title, string message, string confirm) = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("Delete the workspace \u201CDev\u201D?", title);
        Assert.Equal("Its 2 groups · 3 shortcuts will be deleted with it. This cannot be undone.", message);
        Assert.Equal("Delete", confirm);
        Assert.Equal(3, _session.Workspaces.Count);

        _dialogs.ConfirmAnswers.Enqueue(true);
        await Dev.DeleteCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Equal(["Reading", "Spare"], _page.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(["Reading", "Spare"], _store.LastSaved?.ActiveProfile.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public async Task Delete_OfAnEmptyWorkspace_OnlySaysItCannotBeUndone()
    {
        await _page.OnAppearingAsync();

        await _page.Workspaces[2].DeleteCommand.ExecuteAsync(null);

        Assert.Equal("This cannot be undone.", Assert.Single(_dialogs.Confirmations).Message);
    }

    [Fact]
    public async Task MoveDown_ChangesTheOrder_AndSavesIt()
    {
        await _page.OnAppearingAsync();

        Dev.MoveDownCommand.Execute(null);
        await _session.FlushAsync();

        Assert.Equal(["Reading", "Dev", "Spare"], _page.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(["Reading", "Dev", "Spare"], _store.LastSaved?.ActiveProfile.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public async Task AnotherProfileInUse_ShowsItsWorkspaces()
    {
        await _page.OnAppearingAsync();

        _session.SwitchProfile(_session.Profiles[1].Id);

        Assert.Equal(["Garden"], _page.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public async Task AChangeMadeElsewhere_IsShown()
    {
        await _page.OnAppearingAsync();

        _session.AddWorkspace("Lab", null, "Flask");

        Assert.Equal("Lab", _page.Workspaces[^1].Title);
        Assert.Equal(PhosphorIcon.Flask, _page.Workspaces[^1].Glyph);
    }

    [Fact]
    public async Task Open_ShowsTheWorkspaceInPlaceOfTheList_AndBackShowsTheListAgain_WithWhatChanged()
    {
        await _page.OnAppearingAsync();

        Dev.OpenCommand.Execute(null);

        WorkspacePageViewModel open = _page.OpenWorkspace ?? throw new InvalidOperationException("Nothing open.");
        Assert.True(_page.IsWorkspaceOpen);
        Assert.Equal(("Dev", 2), (open.Title, open.Groups.Count));

        _dialogs.NameAnswers.Enqueue("Docs");
        await open.NewGroupCommand.ExecuteAsync(ShortcutKind.Url);
        open.BackCommand.Execute(null);

        Assert.Null(_page.OpenWorkspace);
        Assert.False(_page.IsWorkspaceOpen);
        Assert.Equal("3 groups · 3 shortcuts", Dev.Summary);
    }

    [Fact]
    public async Task AnOpenWorkspace_DeletedElsewhere_GoesBackToTheList()
    {
        await _page.OnAppearingAsync();
        Dev.OpenCommand.Execute(null);

        _session.DeleteWorkspace(_session.Workspaces[0].Id);

        Assert.Null(_page.OpenWorkspace);
        Assert.Equal(["Reading", "Spare"], _page.Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public async Task AnOpenWorkspace_ReadsItselfAgain_WhenAGroupIsMovedOut()
    {
        await _page.OnAppearingAsync();
        Dev.OpenCommand.Execute(null);
        WorkspacePageViewModel open = _page.OpenWorkspace ?? throw new InvalidOperationException("Nothing open.");
        _dialogs.WorkspaceChoiceAnswers.Enqueue("Spare");

        await open.Groups[1].MoveToWorkspaceCommand.ExecuteAsync(null);

        Assert.Same(open, _page.OpenWorkspace);
        Assert.Equal(["Tools"], open.Groups.Select(group => group.Name));
        Assert.Equal(["Repos"], _session.Workspaces[2].Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task AnOpenWorkspace_FollowsAnEditOfItsTitle()
    {
        await _page.OnAppearingAsync();
        Dev.OpenCommand.Execute(null);

        _session.UpdateWorkspace(_session.Workspaces[0].Id, "Development", null, "Code");

        Assert.Equal(("Development", false), (_page.OpenWorkspace?.Title, _page.OpenWorkspace?.HasDescription));
    }

    [Fact]
    public async Task ACardWhoseWorkspaceIsGone_OpensNothing()
    {
        await _page.OnAppearingAsync();
        WorkspaceItemViewModel dev = Dev;

        Profile profile = _session.ActiveProfile;
        _session.SwitchProfile(_session.Profiles[1].Id);
        dev.OpenCommand.Execute(null);

        Assert.Null(_page.OpenWorkspace);
        Assert.Equal(profile.Workspaces[0].Id, dev.Id);
    }

    [Theory]
    [InlineData(0, 0, "Empty")]
    [InlineData(1, 1, "1 group · 1 shortcut")]
    [InlineData(3, 12, "3 groups · 12 shortcuts")]
    public void Summarise_CountsInWords(int groups, int shortcuts, string expected)
        => Assert.Equal(expected, WorkspaceItemViewModel.Summarise(groups, shortcuts));

    private static async Task<WorkspacesPageViewModel> EmptyPageAsync()
    {
        LaunchpadSession session = new(new FakeLaunchpadStore(), new RecordingNotifications(), NullLogger<LaunchpadSession>.Instance);
        WorkspacesPageViewModel page = new(session, new ShortcutValidator(), new ScriptedDialogs(), new RecordingShortcutActions(), new FakeShortcutIcons(), new RecordingNotifications());
        await page.OnAppearingAsync();
        return page;
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
                    [
                        new ShortcutGroup(Guid.NewGuid(), "Tools", ShortcutKind.Application, [PageHarness.Shortcut("Code"), PageHarness.Shortcut("Shell")]),
                        new ShortcutGroup(Guid.NewGuid(), "Repos", ShortcutKind.Directory, [PageHarness.Shortcut("Launchpad", @"C:\Repos\Launchpad")]),
                    ],
                    "Coding and tools",
                    "Code"),
                new Workspace(Guid.NewGuid(), "Reading", [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [])]),
                new Workspace(Guid.NewGuid(), "Spare", [], "Nothing here yet", "Star"),
            ]);
        Profile home = new(Guid.NewGuid(), "Home", [Workspace.Create("Garden")]);

        return new LaunchpadData(LaunchpadData.CurrentVersion, work.Id, [work, home]);
    }
}
