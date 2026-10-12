using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Launchpad.Core.Shortcuts;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Shortcuts;

public sealed class LaunchpadDataTests
{
    private static readonly Profile Work = Profile.Create("Work");
    private static readonly Profile Home = Profile.Create("Home");
    private static readonly Profile Lab = Profile.Create("Lab");

    [Fact]
    public void Empty_HasOneEmptyDefaultProfile_InUse_AndTheCurrentVersion()
    {
        Assert.Equal(LaunchpadData.CurrentVersion, LaunchpadData.Empty.Version);

        Profile profile = Assert.Single(LaunchpadData.Empty.Profiles);
        Assert.Equal(Profile.DefaultName, profile.Name);
        Assert.Same(profile, LaunchpadData.Empty.ActiveProfile);

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            Assert.Empty(LaunchpadData.Empty.GroupsOf(kind));
        }
    }

    [Theory]
    [InlineData(ShortcutKind.Application)]
    [InlineData(ShortcutKind.Directory)]
    [InlineData(ShortcutKind.Url)]
    public void With_ReplacesOnlyTheGivenCategory_OfTheProfileInUse(ShortcutKind kind)
    {
        IReadOnlyList<ShortcutGroup> groups = [new ShortcutGroup(Guid.NewGuid(), "Group", [])];

        LaunchpadData data = ThreeProfiles(active: Home).With(kind, groups);

        foreach (ShortcutKind other in Enum.GetValues<ShortcutKind>())
        {
            if (other == kind)
            {
                Assert.Same(groups, data.GroupsOf(other));
                Assert.Same(groups, data.FindProfile(Home.Id)?.GroupsOf(other));
            }
            else
            {
                Assert.Empty(data.GroupsOf(other));
            }
        }

        Assert.Empty(data.FindProfile(Work.Id)?.GroupsOf(kind) ?? throw new InvalidOperationException());
        Assert.Equal([Work.Id, Home.Id, Lab.Id], data.Profiles.Select(profile => profile.Id));
    }

    [Fact]
    public void GroupsOf_RefusesAnUnknownKind()
        => Assert.Throws<ArgumentOutOfRangeException>(() => LaunchpadData.Empty.GroupsOf((ShortcutKind)42));

    [Fact]
    public void ActiveProfile_FallsBackToTheFirst_WhenTheIdentityIsUnknown()
        => Assert.Same(Work, (ThreeProfiles(active: Home) with { ActiveProfileId = Guid.NewGuid() }).ActiveProfile);

    [Fact]
    public void ActiveProfile_OfADocumentWithoutProfiles_Throws()
        => Assert.Throws<InvalidOperationException>(() => new LaunchpadData(LaunchpadData.CurrentVersion, Guid.NewGuid(), []).ActiveProfile);

    [Fact]
    public void WithActiveProfile_SwitchesTheProfileInUse()
    {
        LaunchpadData data = ThreeProfiles(active: Work).WithActiveProfile(Lab.Id);

        Assert.Same(Lab, data.ActiveProfile);
    }

    [Fact]
    public void WithActiveProfile_RefusesAnUnknownProfile()
        => Assert.Throws<ArgumentException>(() => ThreeProfiles(active: Work).WithActiveProfile(Guid.NewGuid()));

    [Fact]
    public void AddProfile_AppendsByDefault_AndKeepsTheProfileInUse()
    {
        Profile travel = Profile.Create("Travel");

        LaunchpadData data = ThreeProfiles(active: Home).AddProfile(travel);

        Assert.Equal(["Work", "Home", "Lab", "Travel"], data.Profiles.Select(profile => profile.Name));
        Assert.Same(Home, data.ActiveProfile);
    }

    [Fact]
    public void AddProfile_InsertsAtAnIndex()
    {
        LaunchpadData data = ThreeProfiles(active: Home).AddProfile(Profile.Create("Travel"), 1);

        Assert.Equal(["Work", "Travel", "Home", "Lab"], data.Profiles.Select(profile => profile.Name));
    }

    [Fact]
    public void AddProfile_RefusesADuplicateIdentity_AndAnIndexOutsideTheList()
    {
        LaunchpadData data = ThreeProfiles(active: Home);

        Assert.Throws<ArgumentException>(() => data.AddProfile(Work with { Name = "Copy" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.AddProfile(Profile.Create("Travel"), 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => data.AddProfile(Profile.Create("Travel"), -1));
    }

    [Fact]
    public void RenameProfile_ChangesOnlyItsName()
    {
        LaunchpadData data = ThreeProfiles(active: Home).RenameProfile(Lab.Id, "Workshop");

        Assert.Equal(["Work", "Home", "Workshop"], data.Profiles.Select(profile => profile.Name));
        Assert.Equal(Lab.Id, data.Profiles[2].Id);
    }

    [Fact]
    public void ImportInto_ChangesOnlyTheNamedProfile_AndKeepsTheProfileInUse()
    {
        Profile source = Profile.Create("Shared").With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", [])]);

        LaunchpadData data = ThreeProfiles(active: Work).ImportInto(Home.Id, source);

        Assert.Equal(Work.Id, data.ActiveProfile.Id);
        Assert.Equal(["Work", "Home", "Lab"], data.Profiles.Select(profile => profile.Name));
        Assert.Equal("Docs", Assert.Single(data.Profiles[1].Urls).Name);
        Assert.Same(Work, data.Profiles[0]);
        Assert.Same(Lab, data.Profiles[2]);
    }

    [Fact]
    public void ImportInto_RefusesAnUnknownProfile()
        => Assert.Throws<ArgumentException>(() => ThreeProfiles(active: Work).ImportInto(Guid.NewGuid(), Profile.Create("Shared")));

    [Fact]
    public void RemoveProfile_ThatIsNotInUse_KeepsTheProfileInUse()
    {
        LaunchpadData data = ThreeProfiles(active: Home).RemoveProfile(Work.Id);

        Assert.Equal(["Home", "Lab"], data.Profiles.Select(profile => profile.Name));
        Assert.Same(Home, data.ActiveProfile);
    }

    [Fact]
    public void RemoveProfile_InUse_UsesTheOneThatTookItsPlace()
    {
        LaunchpadData data = ThreeProfiles(active: Home).RemoveProfile(Home.Id);

        Assert.Same(Lab, data.ActiveProfile);
    }

    [Fact]
    public void RemoveProfile_InUse_AtTheEnd_UsesTheOneBeforeIt()
    {
        LaunchpadData data = ThreeProfiles(active: Lab).RemoveProfile(Lab.Id);

        Assert.Same(Home, data.ActiveProfile);
    }

    [Fact]
    public void RemoveProfile_RefusesTheLastOne_AndAnUnknownOne()
    {
        Assert.Throws<InvalidOperationException>(() => LaunchpadData.Of(Work).RemoveProfile(Work.Id));
        Assert.Throws<ArgumentException>(() => ThreeProfiles(active: Work).RemoveProfile(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(0, 2, true)]
    [InlineData(0, -1, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, -1, true)]
    public void CanMoveProfile_StaysInsideTheList(int index, int offset, bool expected)
    {
        LaunchpadData data = ThreeProfiles(active: Work);

        Assert.Equal(expected, data.CanMoveProfile(data.Profiles[index].Id, offset));
    }

    [Fact]
    public void MoveProfile_ReordersTheList_AndKeepsTheProfileInUse()
    {
        LaunchpadData data = ThreeProfiles(active: Home).MoveProfile(Lab.Id, -2);

        Assert.Equal(["Lab", "Work", "Home"], data.Profiles.Select(profile => profile.Name));
        Assert.Same(Home, data.ActiveProfile);
    }

    [Fact]
    public void MoveProfile_RefusesAMoveOutOfTheList()
        => Assert.Throws<ArgumentException>(() => ThreeProfiles(active: Home).MoveProfile(Work.Id, -1));

    [Fact]
    public void Normalised_GivesADocumentWithoutProfiles_ADefaultOne_InUse()
    {
        LaunchpadData data = new LaunchpadData(LaunchpadData.CurrentVersion, Guid.NewGuid(), []).Normalised();

        Profile profile = Assert.Single(data.Profiles);
        Assert.Equal(Profile.DefaultName, profile.Name);
        Assert.Equal(profile.Id, data.ActiveProfileId);
    }

    [Fact]
    public void Normalised_PointsAnUnknownIdentity_AtTheFirstProfile()
        => Assert.Equal(Work.Id, (ThreeProfiles(active: Lab) with { ActiveProfileId = Guid.NewGuid() }).Normalised().ActiveProfileId);

    [Fact]
    public void Normalised_LeavesAUsableDocumentAsItIs()
    {
        LaunchpadData data = ThreeProfiles(active: Lab);

        Assert.Same(data, data.Normalised());
    }

    private static LaunchpadData ThreeProfiles(Profile active)
        => new(LaunchpadData.CurrentVersion, active.Id, [Work, Home, Lab]);
}
