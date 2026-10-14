using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ProfileSwitchingTests
{
    [Fact]
    public async Task AnOpenWorkspace_Closes_WhenAnotherProfileIsPutInUse_AndTheListShowsThatProfilesWorkspaces()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        WorkspacesPageViewModel list = await OpenFirstAsync(harness);

        harness.Session.SwitchProfile(harness.Session.Profiles[1].Id);

        Assert.Null(list.OpenWorkspace);
        Assert.False(list.IsWorkspaceOpen);
        Assert.Equal("1 group · 0 shortcuts", Assert.Single(list.Workspaces).Summary);

        list.Workspaces[0].OpenCommand.Execute(null);

        Assert.Equal(["Games"], list.OpenWorkspace?.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task APageNotShownYet_ReadsTheProfileInUse_WhenItAppears()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        await harness.Session.LoadAsync();
        harness.Session.SwitchProfile(harness.Session.Profiles[1].Id);

        WorkspacePageViewModel page = await harness.LoadAsync();

        Assert.Equal(["Games"], page.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task AGroupCreatedAfterASwitch_LandsInTheProfileInUse_Only()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        WorkspacesPageViewModel list = await OpenFirstAsync(harness);
        Guid home = harness.Session.Profiles[1].Id;
        harness.Session.SwitchProfile(home);
        list.Workspaces[0].OpenCommand.Execute(null);
        WorkspacePageViewModel page = list.OpenWorkspace ?? throw new InvalidOperationException("Nothing open.");

        harness.Dialogs.NameAnswers.Enqueue("Puzzles");
        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Application);
        await harness.Session.FlushAsync();

        LaunchpadData saved = harness.Store.LastSaved ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal(["Games", "Puzzles"], saved.FindProfile(home)?.GroupsOf(ShortcutKind.Application).Select(group => group.Name));
        Assert.Equal(["Dev tools"], saved.Profiles[0].GroupsOf(ShortcutKind.Application).Select(group => group.Name));
    }

    [Fact]
    public async Task SwitchingBack_ShowsTheFirstProfileAsItWasLeft()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        WorkspacesPageViewModel list = await OpenFirstAsync(harness);
        Guid work = harness.Session.Profiles[0].Id;

        harness.Dialogs.NameAnswers.Enqueue("Editors");
        await (list.OpenWorkspace ?? throw new InvalidOperationException("Nothing open.")).NewGroupCommand.ExecuteAsync(ShortcutKind.Application);
        harness.Session.SwitchProfile(harness.Session.Profiles[1].Id);
        harness.Session.SwitchProfile(work);
        list.Workspaces[0].OpenCommand.Execute(null);

        Assert.Equal(["Dev tools", "Editors"], list.OpenWorkspace?.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task TheSwitcher_ListsTheProfiles_WithTheOneInUseSelected()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        await harness.Session.LoadAsync();

        ProfileSwitcherViewModel switcher = new(harness.Session);

        Assert.Equal(["Work", "Home"], switcher.Profiles.Select(option => option.Name));
        Assert.Equal("Work", switcher.SelectedProfile?.Name);
    }

    [Fact]
    public async Task ChoosingInTheSwitcher_PutsTheProfileInUse_AndThePageFollows()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        WorkspacesPageViewModel list = await OpenFirstAsync(harness);
        ProfileSwitcherViewModel switcher = new(harness.Session);

        switcher.SelectedProfile = switcher.Profiles[1];

        Assert.Equal("Home", harness.Session.ActiveProfile.Name);
        Assert.Null(list.OpenWorkspace);
        Assert.Equal(harness.Session.ActiveProfile.Workspaces[0].Id, Assert.Single(list.Workspaces).Id);
        Assert.Same(switcher.Profiles[1], switcher.SelectedProfile);
    }

    [Fact]
    public async Task TheSwitcher_FollowsChangesMadeElsewhere_WithoutSwitching()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        await harness.Session.LoadAsync();
        ProfileSwitcherViewModel switcher = new(harness.Session);
        Guid home = harness.Session.Profiles[1].Id;

        harness.Session.RenameProfile(home, "Family");
        harness.Session.AddProfile("Lab");

        Assert.Equal(["Work", "Family", "Lab"], switcher.Profiles.Select(option => option.Name));
        Assert.Equal("Work", switcher.SelectedProfile?.Name);
        Assert.Equal("Work", harness.Session.ActiveProfile.Name);

        harness.Session.SwitchProfile(home);

        Assert.Equal("Family", switcher.SelectedProfile?.Name);
    }

    [Fact]
    public async Task ANullSelection_FromTheListBeingRebuilt_IsNotAChoice()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        await harness.Session.LoadAsync();
        ProfileSwitcherViewModel switcher = new(harness.Session);

        switcher.SelectedProfile = null;

        Assert.Equal("Work", harness.Session.ActiveProfile.Name);
    }

    [Fact]
    public async Task AnOpenWorkspace_ShowsWhatWasImportedIntoIt_AndKeepsItOnTheNextEdit()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        WorkspacesPageViewModel list = await OpenFirstAsync(harness);
        WorkspacePageViewModel page = list.OpenWorkspace ?? throw new InvalidOperationException("Nothing open.");
        Profile content = Profile.Create("Shared").With(
            ShortcutKind.Application,
            [new ShortcutGroup(Guid.NewGuid(), "Editors", ShortcutKind.Application, [PageHarness.Shortcut("Code")])]);

        harness.Session.ImportIntoProfile(harness.Session.ActiveProfile.Id, content);

        Assert.Same(page, list.OpenWorkspace);
        Assert.Equal(["Dev tools", "Editors"], page.Groups.Select(group => group.Name));

        harness.Dialogs.NameAnswers.Enqueue("Browsers");
        await page.NewGroupCommand.ExecuteAsync(ShortcutKind.Application);

        await harness.Session.FlushAsync();
        Assert.Equal(["Dev tools", "Editors", "Browsers"], harness.Store.LastSaved?.ActiveProfile.Workspaces[0].Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task AnOpenWorkspace_IsLeftAlone_ByAnImportIntoAnotherProfile()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        WorkspacesPageViewModel list = await OpenFirstAsync(harness);
        WorkspacePageViewModel page = list.OpenWorkspace ?? throw new InvalidOperationException("Nothing open.");
        GroupViewModel shown = Assert.Single(page.Groups);

        harness.Session.ImportIntoProfile(
            harness.Session.Profiles[1].Id,
            Profile.Create("Shared").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Editors", ShortcutKind.Application, [])]));

        Assert.Same(shown, Assert.Single(page.Groups));
    }

    // The Workspaces page, with the first workspace of the profile in use open in its place.
    private static async Task<WorkspacesPageViewModel> OpenFirstAsync(PageHarness harness)
    {
        WorkspacesPageViewModel list = new(harness.Session, new ShortcutValidator(), harness.Dialogs, harness.Actions, harness.Icons, harness.Notifications);
        await list.OnAppearingAsync();
        list.Workspaces[0].OpenCommand.Execute(null);
        return list;
    }
}
