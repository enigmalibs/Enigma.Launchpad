using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ProfileSwitchingTests
{
    [Fact]
    public async Task ALoadedPage_ShowsTheGroupsOfTheProfilePutInUse()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        CategoryPageViewModel page = await harness.LoadAsync();

        harness.Session.SwitchProfile(harness.Session.Profiles[1].Id);

        Assert.Equal(["Games"], page.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task APageNotShownYet_ReadsTheProfileInUse_WhenItAppears()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        await harness.Session.LoadAsync();
        harness.Session.SwitchProfile(harness.Session.Profiles[1].Id);

        CategoryPageViewModel page = await harness.LoadAsync();

        Assert.Equal(["Games"], page.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task AGroupCreatedAfterASwitch_LandsInTheProfileInUse_Only()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        CategoryPageViewModel page = await harness.LoadAsync();
        Guid home = harness.Session.Profiles[1].Id;
        harness.Session.SwitchProfile(home);

        harness.Dialogs.NameAnswers.Enqueue("Puzzles");
        await page.NewGroupCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        LaunchpadData saved = harness.Store.LastSaved ?? throw new InvalidOperationException("Nothing saved.");
        Assert.Equal(["Games", "Puzzles"], saved.FindProfile(home)?.Applications.Select(group => group.Name));
        Assert.Equal(["Dev tools"], saved.Profiles[0].Applications.Select(group => group.Name));
    }

    [Fact]
    public async Task SwitchingBack_ShowsTheFirstProfileAsItWasLeft()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        CategoryPageViewModel page = await harness.LoadAsync();
        Guid work = harness.Session.Profiles[0].Id;

        harness.Dialogs.NameAnswers.Enqueue("Editors");
        await page.NewGroupCommand.ExecuteAsync(null);
        harness.Session.SwitchProfile(harness.Session.Profiles[1].Id);
        harness.Session.SwitchProfile(work);

        Assert.Equal(["Dev tools", "Editors"], page.Groups.Select(group => group.Name));
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
        CategoryPageViewModel page = await harness.LoadAsync();
        ProfileSwitcherViewModel switcher = new(harness.Session);

        switcher.SelectedProfile = switcher.Profiles[1];

        Assert.Equal("Home", harness.Session.ActiveProfile.Name);
        Assert.Equal(["Games"], page.Groups.Select(group => group.Name));
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
    public async Task ALoadedPage_ShowsWhatWasImportedIntoTheProfileInUse_AndKeepsItOnTheNextEdit()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        CategoryPageViewModel page = await harness.LoadAsync();
        Profile content = Profile.Create("Shared").With(
            ShortcutKind.Application,
            [new ShortcutGroup(Guid.NewGuid(), "Editors", [PageHarness.Shortcut("Code")])]);

        harness.Session.ImportIntoProfile(harness.Session.ActiveProfile.Id, content);

        Assert.Equal(["Dev tools", "Editors"], page.Groups.Select(group => group.Name));

        harness.Dialogs.NameAnswers.Enqueue("Browsers");
        await page.NewGroupCommand.ExecuteAsync(null);

        Assert.Equal(["Dev tools", "Editors", "Browsers"], (await harness.SavedApplicationsAsync()).Select(group => group.Name));
    }

    [Fact]
    public async Task ALoadedPage_IsLeftAlone_ByAnImportIntoAnotherProfile()
    {
        PageHarness harness = new(PageHarness.WorkAndHome());
        CategoryPageViewModel page = await harness.LoadAsync();
        GroupViewModel shown = Assert.Single(page.Groups);

        harness.Session.ImportIntoProfile(
            harness.Session.Profiles[1].Id,
            Profile.Create("Shared").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Editors", [])]));

        Assert.Same(shown, Assert.Single(page.Groups));
    }
}
