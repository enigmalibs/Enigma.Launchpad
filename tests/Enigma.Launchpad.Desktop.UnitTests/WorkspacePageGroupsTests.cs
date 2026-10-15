using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WorkspacePageGroupsTests
{
    [Fact]
    public async Task Opening_ShowsTheWorkspace_WithItsGroupsOfEveryKind_InOrder()
    {
        LaunchpadData data = PageHarness.Data("Dev tools", "Games")
            .With(ShortcutKind.Url, [new ShortcutGroup(System.Guid.NewGuid(), "Docs", ShortcutKind.Url, [])]);
        Workspace workspace = data.ActiveProfile.Workspaces[0] with { Title = "Dev", Description = "Coding", Icon = "Code" };
        PageHarness harness = new(data.WithProfile(data.ActiveProfile.ReplaceWorkspace(workspace)));

        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.Equal(workspace.Id, page.WorkspaceId);
        Assert.Equal(("Dev", "Coding", true, Enigma.Icons.Phosphor.PhosphorIcon.Code), (page.Title, page.Description, page.HasDescription, page.Glyph));
        Assert.False(page.IsEmpty);
        Assert.Equal(["Dev tools", "Games", "Docs"], page.Groups.Select(group => group.Name));
        Assert.Equal([ShortcutKind.Application, ShortcutKind.Application, ShortcutKind.Url], page.Groups.Select(group => group.Kind));
    }

    [Fact]
    public async Task AWorkspaceWithoutGroups_IsEmpty()
    {
        PageHarness harness = new();

        await harness.LoadAsync();

        Assert.True(harness.Page.IsEmpty);
        Assert.True(harness.Page.NewGroupCommand.CanExecute(ShortcutKind.Application));
        Assert.False(harness.Page.HasDescription);
    }

    [Fact]
    public async Task Back_AsksTheListToShowAgain()
    {
        PageHarness harness = new();
        WorkspacePageViewModel page = await harness.LoadAsync();

        page.BackCommand.Execute(null);

        Assert.Equal(1, harness.BackRequests);
    }

    [Fact]
    public async Task NewGroup_AddsItAtTheEnd_Expanded_AndSaves()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.NameAnswers.Enqueue("Games");

        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Application);

        Assert.Equal(["Dev tools", "Games"], page.Groups.Select(group => group.Name));
        Assert.True(page.Groups[1].IsExpanded);
        ShortcutGroup[] saved = await harness.SavedApplicationsAsync();
        Assert.Equal(["Dev tools", "Games"], saved.Select(group => group.Name));
        Assert.False(saved[1].IsCollapsed);
    }

    [Fact]
    public async Task NewGroup_ChecksTheNameAgainstEveryGroup()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Application);

        ScriptedDialogs.NamePrompt prompt = Assert.Single(harness.Dialogs.NamePrompts);
        Assert.Null(prompt.InitialName);
        Assert.NotNull(prompt.Validate(" DEV TOOLS "));
        Assert.NotNull(prompt.Validate(""));
        Assert.Null(prompt.Validate("Games"));
    }

    [Fact]
    public async Task NewGroup_Cancelled_ChangesNothing()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Application);
        await harness.Session.FlushAsync();

        Assert.Single(page.Groups);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task Rename_AppliesTheNewName_AndSaves()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools", "Games"));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.NameAnswers.Enqueue("Tools");

        await page.Groups[0].RenameCommand.ExecuteAsync(null);

        Assert.Equal("Tools", page.Groups[0].Name);
        Assert.Equal(["Tools", "Games"], (await harness.SavedApplicationsAsync()).Select(group => group.Name));
    }

    [Fact]
    public async Task Rename_AcceptsTheGroupsOwnName_ButNotASiblings()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools", "Games"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        await page.Groups[0].RenameCommand.ExecuteAsync(null);

        ScriptedDialogs.NamePrompt prompt = Assert.Single(harness.Dialogs.NamePrompts);
        Assert.Equal("Dev tools", prompt.InitialName);
        Assert.Null(prompt.Validate("dev TOOLS"));
        Assert.NotNull(prompt.Validate("games"));
    }

    [Fact]
    public async Task Rename_ToTheSameName_DoesNotSave()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.NameAnswers.Enqueue("Dev tools");

        await page.Groups[0].RenameCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesTheGroup_AndSaves()
    {
        LaunchpadData data = LaunchpadData.Empty.With(
            ShortcutKind.Application,
            [
                new ShortcutGroup(System.Guid.NewGuid(), "Dev tools", ShortcutKind.Application, [new Shortcut(System.Guid.NewGuid(), "Code", @"C:\code.exe"), new Shortcut(System.Guid.NewGuid(), "Git", @"C:\git.exe")]),
                new ShortcutGroup(System.Guid.NewGuid(), "Games", ShortcutKind.Application, []),
            ]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.ConfirmAnswers.Enqueue(true);

        await page.Groups[0].DeleteCommand.ExecuteAsync(null);

        (string title, string message, string _) = Assert.Single(harness.Dialogs.Confirmations);
        Assert.Contains("Dev tools", title, System.StringComparison.Ordinal);
        Assert.Contains("2 shortcuts", message, System.StringComparison.Ordinal);
        Assert.Equal(["Games"], page.Groups.Select(group => group.Name));
        Assert.Equal(["Games"], (await harness.SavedApplicationsAsync()).Select(group => group.Name));
    }

    [Fact]
    public async Task Delete_Declined_KeepsTheGroup()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.ConfirmAnswers.Enqueue(false);

        await page.Groups[0].DeleteCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Single(page.Groups);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task DeletingTheLastGroup_LeavesThePageEmpty()
    {
        PageHarness harness = new(PageHarness.Data("Dev tools"));
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.ConfirmAnswers.Enqueue(true);

        await page.Groups[0].DeleteCommand.ExecuteAsync(null);

        Assert.True(page.IsEmpty);
    }

    [Fact]
    public async Task Saving_KeepsEachGroupsWidth()
    {
        LaunchpadData data = PageHarness.Data().With(
            ShortcutKind.Application,
            [
                new ShortcutGroup(System.Guid.NewGuid(), "A", ShortcutKind.Application, [], Width: GroupWidth.OneThird),
                new ShortcutGroup(System.Guid.NewGuid(), "B", ShortcutKind.Application, [], Width: GroupWidth.TwoThirds),
                new ShortcutGroup(System.Guid.NewGuid(), "C", ShortcutKind.Application, []),
            ]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.Equal([GroupWidth.OneThird, GroupWidth.TwoThirds, GroupWidth.Full], page.Groups.Select(group => group.Width));

        page.MoveGroup(page.Groups[2], -2);

        ShortcutGroup[] saved = await harness.SavedGroupsAsync();
        Assert.Equal(["C", "A", "B"], saved.Select(group => group.Name));
        Assert.Equal([GroupWidth.Full, GroupWidth.OneThird, GroupWidth.TwoThirds], saved.Select(group => group.Width));
    }

    [Fact]
    public async Task MoveUpAndDown_AreOnlyAvailableWhereTheGroupCanGo()
    {
        PageHarness harness = new(PageHarness.Data("A", "B", "C"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.False(page.Groups[0].MoveUpCommand.CanExecute(null));
        Assert.True(page.Groups[0].MoveDownCommand.CanExecute(null));
        Assert.True(page.Groups[1].MoveUpCommand.CanExecute(null));
        Assert.True(page.Groups[1].MoveDownCommand.CanExecute(null));
        Assert.True(page.Groups[2].MoveUpCommand.CanExecute(null));
        Assert.False(page.Groups[2].MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public async Task MoveDown_ReordersTheGroups_AndSaves()
    {
        PageHarness harness = new(PageHarness.Data("A", "B", "C"));
        WorkspacePageViewModel page = await harness.LoadAsync();
        GroupViewModel first = page.Groups[0];

        first.MoveDownCommand.Execute(null);

        Assert.Equal(["B", "A", "C"], page.Groups.Select(group => group.Name));
        Assert.True(first.MoveUpCommand.CanExecute(null));
        Assert.Equal(["B", "A", "C"], (await harness.SavedApplicationsAsync()).Select(group => group.Name));
    }

    [Fact]
    public async Task MoveUp_FromTheTop_DoesNothing()
    {
        PageHarness harness = new(PageHarness.Data("A", "B"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        page.Groups[0].MoveUpCommand.Execute(null);
        await harness.Session.FlushAsync();

        Assert.Equal(["A", "B"], page.Groups.Select(group => group.Name));
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task Folding_AGroup_IsSaved()
    {
        PageHarness harness = new(PageHarness.Data("A", "B"));
        WorkspacePageViewModel page = await harness.LoadAsync();

        page.Groups[1].IsExpanded = false;

        ShortcutGroup[] saved = await harness.SavedApplicationsAsync();
        Assert.False(saved[0].IsCollapsed);
        Assert.True(saved[1].IsCollapsed);
    }

    [Fact]
    public async Task LoadingAFoldedGroup_DoesNotSave()
    {
        LaunchpadData data = LaunchpadData.Empty.With(
            ShortcutKind.Application,
            [new ShortcutGroup(System.Guid.NewGuid(), "A", ShortcutKind.Application, [], IsCollapsed: true)]);
        PageHarness harness = new(data);

        WorkspacePageViewModel page = await harness.LoadAsync();
        await harness.Session.FlushAsync();

        Assert.False(page.Groups[0].IsExpanded);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task AGroupKeepsItsShortcuts_WhenTheCategoryIsSaved()
    {
        Shortcut code = new(System.Guid.NewGuid(), "Code", @"C:\code.exe", "--new-window");
        LaunchpadData data = LaunchpadData.Empty.With(
            ShortcutKind.Application,
            [new ShortcutGroup(System.Guid.NewGuid(), "Dev tools", ShortcutKind.Application, [code])]);
        PageHarness harness = new(data);
        WorkspacePageViewModel page = await harness.LoadAsync();
        harness.Dialogs.NameAnswers.Enqueue("Tools");

        await page.Groups[0].RenameCommand.ExecuteAsync(null);

        ShortcutGroup saved = Assert.Single(await harness.SavedApplicationsAsync());
        Assert.Equal(code, Assert.Single(saved.Shortcuts));
        Assert.Equal(1, page.Groups[0].Count);
        Assert.True(page.Groups[0].HasShortcuts);
    }
}
