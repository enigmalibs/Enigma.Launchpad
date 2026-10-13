using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Launchpad.Core.Shortcuts;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Shortcuts;

public sealed class ProfileTests
{
    [Fact]
    public void Create_GivesANewEmptyProfile()
    {
        Profile first = Profile.Create("Work");
        Profile second = Profile.Create("Work");

        Assert.Equal("Work", first.Name);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(0, first.CountGroups());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_RefusesABlankName(string name)
        => Assert.Throws<ArgumentException>(() => Profile.Create(name));

    [Theory]
    [InlineData(ShortcutKind.Application)]
    [InlineData(ShortcutKind.Directory)]
    [InlineData(ShortcutKind.Url)]
    public void With_ReplacesOnlyTheGivenCategory(ShortcutKind kind)
    {
        ShortcutGroup[] groups = [new ShortcutGroup(Guid.NewGuid(), "Group", [])];

        Profile profile = Profile.Create("Work").With(kind, groups);

        foreach (ShortcutKind other in Enum.GetValues<ShortcutKind>())
        {
            Assert.Equal(other == kind ? 1 : 0, profile.GroupsOf(other).Count);
        }
    }

    [Fact]
    public void Duplicate_CopiesEverything_UnderNewIdentities()
    {
        Profile source = Sample();

        Profile copy = source.Duplicate("Work (copy)");

        Assert.Equal("Work (copy)", copy.Name);
        Assert.NotEqual(source.Id, copy.Id);

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            Assert.Equal(source.GroupsOf(kind).Select(group => group.Name), copy.GroupsOf(kind).Select(group => group.Name));
            Assert.Equal(source.GroupsOf(kind).Select(group => group.IsCollapsed), copy.GroupsOf(kind).Select(group => group.IsCollapsed));

            foreach ((ShortcutGroup original, ShortcutGroup copied) in source.GroupsOf(kind).Zip(copy.GroupsOf(kind)))
            {
                Assert.NotEqual(original.Id, copied.Id);
                Assert.Equal(
                    original.Shortcuts.Select(shortcut => (shortcut.Name, shortcut.Target, shortcut.Arguments, shortcut.Description)),
                    copied.Shortcuts.Select(shortcut => (shortcut.Name, shortcut.Target, shortcut.Arguments, shortcut.Description)));
                Assert.Empty(original.Shortcuts.Select(shortcut => shortcut.Id).Intersect(copied.Shortcuts.Select(shortcut => shortcut.Id)));
            }
        }
    }

    [Fact]
    public void Counts_CoverEveryCategory()
    {
        Profile profile = Sample();

        Assert.Equal(3, profile.CountGroups());
        Assert.Equal(3, profile.CountShortcuts());
    }

    [Fact]
    public void Import_IntoAnEmptyProfile_AddsEverything_InOrder_UnderNewIdentities()
    {
        Profile source = Sample();
        Profile target = Profile.Create("Home");

        Profile result = target.Import(source);

        Assert.Equal((target.Id, "Home"), (result.Id, result.Name));

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            Assert.Equal(Describe(source.GroupsOf(kind)), Describe(result.GroupsOf(kind)));
            Assert.Empty(Identities(source.GroupsOf(kind)).Intersect(Identities(result.GroupsOf(kind))));
        }

        Assert.All(result.Applications, group => Assert.False(group.IsCollapsed));
    }

    [Fact]
    public void Import_AddsTheShortcutsOfAGroupWithTheSameName_ToItsEnd_IgnoringCase()
    {
        Shortcut code = new(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe");
        ShortcutGroup tools = new(Guid.NewGuid(), "Dev tools", [code], IsCollapsed: true);
        Profile target = Profile.Create("Home").With(ShortcutKind.Application, [tools, new ShortcutGroup(Guid.NewGuid(), "Games", [])]);
        Profile source = Profile.Create("Work").With(
            ShortcutKind.Application,
            [
                new ShortcutGroup(Guid.NewGuid(), "DEV TOOLS", [new Shortcut(Guid.NewGuid(), "Shell", @"C:\Tools\sh.exe")]),
                new ShortcutGroup(Guid.NewGuid(), "Office", []),
            ]);

        Profile result = target.Import(source);

        Assert.Equal(["Dev tools", "Games", "Office"], result.Applications.Select(group => group.Name));
        Assert.Equal(tools.Id, result.Applications[0].Id);
        Assert.True(result.Applications[0].IsCollapsed);
        Assert.Equal(["Code", "Shell"], result.Applications[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Same(code, result.Applications[0].Shortcuts[0]);
    }

    [Fact]
    public void Import_LeavesOutAShortcutAlreadyInTheGroup_ButNotOneWithOtherArguments()
    {
        Profile target = Profile.Create("Home").With(
            ShortcutKind.Application,
            [new ShortcutGroup(Guid.NewGuid(), "Tools", [new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "-n")])]);
        Profile source = Profile.Create("Work").With(
            ShortcutKind.Application,
            [
                new ShortcutGroup(
                    Guid.NewGuid(),
                    "tools",
                    [
                        new Shortcut(Guid.NewGuid(), "code", @"c:\tools\code.exe", " -n "),
                        new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "--wait"),
                        new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "--wait"),
                    ]),
            ]);

        Profile result = target.Import(source);

        Assert.Equal(["-n", "--wait"], Assert.Single(result.Applications).Shortcuts.Select(shortcut => shortcut.Arguments));
    }

    [Fact]
    public void Import_OfTheSameContentTwice_AddsNothingTheSecondTime()
    {
        Profile once = Profile.Create("Home").Import(Sample());

        Profile twice = once.Import(Sample());

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            Assert.Equal(Identities(once.GroupsOf(kind)), Identities(twice.GroupsOf(kind)));
        }
    }

    [Fact]
    public void Import_KeepsCategoriesApart()
    {
        Profile source = Profile.Create("Work").With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Tools", [])]);
        Profile target = Profile.Create("Home").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Tools", [])]);

        Profile result = target.Import(source);

        Assert.Single(result.Applications);
        Assert.Equal("Tools", Assert.Single(result.Urls).Name);
    }

    private static string[] Describe(IEnumerable<ShortcutGroup> groups)
        => groups
            .Select(group => $"{group.Name}: {string.Join(", ", group.Shortcuts.Select(shortcut => $"{shortcut.Name} → {shortcut.Target} {shortcut.Arguments} ({shortcut.Description})"))}")
            .ToArray();

    private static Guid[] Identities(IEnumerable<ShortcutGroup> groups)
        => groups.SelectMany(group => group.Shortcuts.Select(shortcut => shortcut.Id).Prepend(group.Id)).ToArray();

    private static Profile Sample()
        => new(
            Guid.NewGuid(),
            "Work",
            [
                new ShortcutGroup(
                    Guid.NewGuid(),
                    "Dev tools",
                    [new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "-n", "The editor"), new Shortcut(Guid.NewGuid(), "Shell", @"C:\Tools\sh.exe")],
                    IsCollapsed: true),
            ],
            [new ShortcutGroup(Guid.NewGuid(), "Work", [])],
            [new ShortcutGroup(Guid.NewGuid(), "Docs", [new Shortcut(Guid.NewGuid(), "Avalonia", "https://docs.avaloniaui.net")])]);
}
