using System;
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

public sealed class ProfilesPageViewModelTests
{
    private readonly FakeLaunchpadStore _store;
    private readonly LaunchpadSession _session;
    private readonly ScriptedDialogs _dialogs = new();
    private readonly ProfilesPageViewModel _page;

    public ProfilesPageViewModelTests()
    {
        LaunchpadData data = PageHarness.WorkAndHome();
        Profile work = data.Profiles[0].With(
            ShortcutKind.Url,
            [new ShortcutGroup(Guid.NewGuid(), "Docs", [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net"), PageHarness.Shortcut("Repo", "https://example.com")])]);

        _store = new FakeLaunchpadStore(data with { Profiles = [work, data.Profiles[1]] });
        _session = new LaunchpadSession(_store, new RecordingNotifications(), NullLogger<LaunchpadSession>.Instance);
        ShortcutValidator validator = new();
        _page = new ProfilesPageViewModel(_session, validator, _dialogs, new ShortcutExchange(validator), new FakePathPicker(), new RecordingNotifications());
    }

    private ProfileItemViewModel Work => _page.Profiles.Single(profile => profile.Name == "Work");

    private ProfileItemViewModel Home => _page.Profiles.Single(profile => profile.Name == "Home");

    [Fact]
    public async Task OnAppearing_ShowsEveryProfile_WithTheOneInUse_AndWhatEachHolds()
    {
        await _page.OnAppearingAsync();

        Assert.Equal(["Work", "Home"], _page.Profiles.Select(profile => profile.Name));
        Assert.True(Work.IsActive);
        Assert.False(Home.IsActive);
        Assert.Equal("2 groups · 2 shortcuts", Work.Summary);
        Assert.Equal("1 group · 0 shortcuts", Home.Summary);
    }

    [Fact]
    public async Task OnAppearing_AgainLater_PicksUpEditsMadeOnTheCategoryPages()
    {
        await _page.OnAppearingAsync();
        _session.Update(ShortcutKind.Directory, [new ShortcutGroup(Guid.NewGuid(), "Projects", [])]);

        await _page.OnAppearingAsync();

        Assert.Equal("3 groups · 2 shortcuts", Work.Summary);
    }

    [Theory]
    [InlineData(0, 0, "No groups yet")]
    [InlineData(1, 1, "1 group · 1 shortcut")]
    [InlineData(4, 12, "4 groups · 12 shortcuts")]
    public void Summarise_CountsInWords(int groups, int shortcuts, string expected)
        => Assert.Equal(expected, ProfileItemViewModel.Summarise(groups, shortcuts));

    [Fact]
    public async Task NewProfile_IsAddedAtTheEnd_WithoutSwitching()
    {
        await _page.OnAppearingAsync();
        _dialogs.NameAnswers.Enqueue("Lab");

        await _page.NewProfileCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Equal(["Work", "Home", "Lab"], _page.Profiles.Select(profile => profile.Name));
        Assert.Equal(["Work", "Home", "Lab"], _store.LastSaved?.Profiles.Select(profile => profile.Name));
        Assert.Equal("Work", _session.ActiveProfile.Name);
    }

    [Fact]
    public async Task NewProfile_RefusesTheNameOfAnotherProfile()
    {
        await _page.OnAppearingAsync();

        await _page.NewProfileCommand.ExecuteAsync(null);

        ScriptedDialogs.NamePrompt prompt = Assert.Single(_dialogs.NamePrompts);
        Assert.Equal("New profile", prompt.Title);
        Assert.Equal("A profile with this name already exists.", prompt.Validate(" home "));
        Assert.Null(prompt.Validate("Lab"));
        Assert.Equal(2, _session.Profiles.Count);
    }

    [Fact]
    public async Task Rename_StartsFromTheName_RefusesAnotherProfilesName_AndIsSaved()
    {
        await _page.OnAppearingAsync();
        _dialogs.NameAnswers.Enqueue("Family");

        await Home.RenameCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        ScriptedDialogs.NamePrompt prompt = Assert.Single(_dialogs.NamePrompts);
        Assert.Equal("Home", prompt.InitialName);
        Assert.Equal("A profile with this name already exists.", prompt.Validate("WORK"));
        Assert.Null(prompt.Validate("Home"));
        Assert.Equal(["Work", "Family"], _store.LastSaved?.Profiles.Select(profile => profile.Name));
    }

    [Fact]
    public async Task Rename_Cancelled_ChangesNothing()
    {
        await _page.OnAppearingAsync();

        await Home.RenameCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Duplicate_ProposesAFreeName_AndPutsTheCopyAfterTheOriginal()
    {
        await _page.OnAppearingAsync();
        _dialogs.NameAnswers.Enqueue("Work (copy)");

        await Work.DuplicateCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        Assert.Equal("Work (copy)", Assert.Single(_dialogs.NamePrompts).InitialName);
        Assert.Equal(["Work", "Work (copy)", "Home"], _page.Profiles.Select(profile => profile.Name));
        Assert.Equal("2 groups · 2 shortcuts", _page.Profiles[1].Summary);
        Assert.False(_page.Profiles[1].IsActive);
    }

    [Fact]
    public void CopyName_SkipsNamesAlreadyTaken()
        => Assert.Equal("Work (copy 3)", ProfilesPageViewModel.CopyName("Work", ["Work", "work (COPY)", "Work (copy 2)"]));

    [Fact]
    public async Task Use_PutsTheProfileInUse_AndOnlyOtherProfilesOfferIt()
    {
        await _page.OnAppearingAsync();

        Assert.False(Work.UseCommand.CanExecute(null));
        Assert.True(Home.UseCommand.CanExecute(null));

        Home.UseCommand.Execute(null);

        Assert.Equal("Home", _session.ActiveProfile.Name);
        Assert.True(Home.IsActive);
        Assert.False(Work.IsActive);
    }

    [Fact]
    public async Task Delete_AsksFirst_NamingWhatIsLost()
    {
        await _page.OnAppearingAsync();

        await Work.DeleteCommand.ExecuteAsync(null);

        (string title, string message, string confirm) = Assert.Single(_dialogs.Confirmations);
        Assert.Equal("Delete the profile “Work”?", title);
        Assert.Equal("Its 2 groups · 2 shortcuts will be deleted with it. This cannot be undone. Another profile will be put in use.", message);
        Assert.Equal("Delete", confirm);
        Assert.Equal(2, _session.Profiles.Count);
    }

    [Fact]
    public async Task Delete_Confirmed_OfTheProfileInUse_PutsItsNeighbourInUse()
    {
        await _page.OnAppearingAsync();
        _dialogs.ConfirmAnswers.Enqueue(true);

        await Work.DeleteCommand.ExecuteAsync(null);
        await _session.FlushAsync();

        ProfileItemViewModel home = Assert.Single(_page.Profiles);
        Assert.True(home.IsActive);
        Assert.Equal("Home", _store.LastSaved?.ActiveProfile.Name);
    }

    [Fact]
    public async Task Delete_IsNotOffered_ForTheOnlyProfile()
    {
        await _page.OnAppearingAsync();
        _dialogs.ConfirmAnswers.Enqueue(true);
        await Home.DeleteCommand.ExecuteAsync(null);

        ProfileItemViewModel last = Assert.Single(_page.Profiles);

        Assert.False(last.DeleteCommand.CanExecute(null));
        await _page.DeleteAsync(last);
        Assert.Single(_dialogs.Confirmations);
        Assert.Single(_session.Profiles);
    }

    [Fact]
    public async Task Move_ReordersTheProfiles_WithinTheList()
    {
        await _page.OnAppearingAsync();

        Assert.False(Work.MoveUpCommand.CanExecute(null));
        Assert.True(Work.MoveDownCommand.CanExecute(null));
        Assert.False(Home.MoveDownCommand.CanExecute(null));

        Home.MoveUpCommand.Execute(null);
        await _session.FlushAsync();

        Assert.Equal(["Home", "Work"], _page.Profiles.Select(profile => profile.Name));
        Assert.Equal(["Home", "Work"], _store.LastSaved?.Profiles.Select(profile => profile.Name));
    }

    [Fact]
    public async Task TheCards_FollowASwitchMadeFromTheProfileList()
    {
        await _page.OnAppearingAsync();

        _session.SwitchProfile(_session.Profiles[1].Id);

        Assert.True(Home.IsActive);
        Assert.False(Work.IsActive);
    }
}
