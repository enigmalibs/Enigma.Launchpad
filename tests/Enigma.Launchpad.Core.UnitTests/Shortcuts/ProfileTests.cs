using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
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
        Assert.Equal(0, first.CountWorkspaces());
        Assert.Equal(0, first.CountGroups());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_RefusesABlankName(string name)
        => Assert.Throws<ArgumentException>(() => Profile.Create(name));

    [Fact]
    public void AddWorkspace_AppendsOrInserts_AndRefusesADuplicateIdentity()
    {
        Workspace first = Workspace.Create("First");
        Workspace second = Workspace.Create("Second");
        Workspace between = Workspace.Create("Between");

        Profile profile = Profile.Create("Work").AddWorkspace(first).AddWorkspace(second).AddWorkspace(between, 1);

        Assert.Equal(["First", "Between", "Second"], profile.Workspaces.Select(workspace => workspace.Title));
        Assert.Throws<ArgumentException>(() => profile.AddWorkspace(first));
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.AddWorkspace(Workspace.Create("Far"), 4));
    }

    [Fact]
    public void ReplaceWorkspace_KeepsItsPlace()
    {
        Workspace first = Workspace.Create("First");
        Workspace second = Workspace.Create("Second");
        Profile profile = Profile.Create("Work").AddWorkspace(first).AddWorkspace(second);

        Profile result = profile.ReplaceWorkspace(first with { Title = "Renamed", Description = "Now with words", Icon = "Code" });

        Assert.Equal(["Renamed", "Second"], result.Workspaces.Select(workspace => workspace.Title));
        Assert.Equal(("Now with words", "Code"), (result.Workspaces[0].Description, result.Workspaces[0].Icon));
        Assert.Throws<ArgumentException>(() => profile.ReplaceWorkspace(Workspace.Create("Unknown")));
    }

    [Fact]
    public void RemoveWorkspace_TakesItOut()
    {
        Workspace first = Workspace.Create("First");
        Profile profile = Profile.Create("Work").AddWorkspace(first).AddWorkspace(Workspace.Create("Second"));

        Assert.Equal(["Second"], profile.RemoveWorkspace(first.Id).Workspaces.Select(workspace => workspace.Title));
        Assert.Throws<ArgumentException>(() => profile.RemoveWorkspace(Guid.NewGuid()));
    }

    [Fact]
    public void MoveWorkspace_StaysInTheList()
    {
        Workspace first = Workspace.Create("First");
        Workspace last = Workspace.Create("Last");
        Profile profile = Profile.Create("Work").AddWorkspace(first).AddWorkspace(Workspace.Create("Middle")).AddWorkspace(last);

        Assert.Equal(["Middle", "Last", "First"], profile.MoveWorkspace(first.Id, 2).Workspaces.Select(workspace => workspace.Title));
        Assert.False(profile.CanMoveWorkspace(first.Id, -1));
        Assert.False(profile.CanMoveWorkspace(last.Id, 1));
        Assert.False(profile.CanMoveWorkspace(first.Id, 0));
        Assert.False(profile.CanMoveWorkspace(Guid.NewGuid(), 1));
        Assert.Throws<ArgumentException>(() => profile.MoveWorkspace(last.Id, 1));
    }

    [Fact]
    public void Duplicate_CopiesEverything_UnderNewIdentities()
    {
        Profile source = Sample();

        Profile copy = source.Duplicate("Work (copy)");

        Assert.Equal("Work (copy)", copy.Name);
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(Describe(source), Describe(copy));
        Assert.Empty(Identities(source).Intersect(Identities(copy)));
        Assert.Equal(
            source.Workspaces.SelectMany(workspace => workspace.Groups).Select(group => group.IsCollapsed),
            copy.Workspaces.SelectMany(workspace => workspace.Groups).Select(group => group.IsCollapsed));
    }

    [Fact]
    public void Counts_CoverEveryWorkspace()
    {
        Profile profile = Sample();

        Assert.Equal(2, profile.CountWorkspaces());
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
        Assert.Equal(Describe(source), Describe(result));
        Assert.Empty(Identities(source).Intersect(Identities(result)));
        Assert.All(result.Workspaces.SelectMany(workspace => workspace.Groups), group => Assert.False(group.IsCollapsed));
    }

    [Fact]
    public void Import_MergesAWorkspaceWithTheSameTitle_IgnoringCase_AndKeepsItsOwnLooks()
    {
        Shortcut code = new(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe");
        ShortcutGroup tools = new(Guid.NewGuid(), "Dev tools", ShortcutKind.Application, [code], IsCollapsed: true);
        Workspace dev = new(Guid.NewGuid(), "Dev", [tools, Group("Games", ShortcutKind.Application)], "Mine", "Code");
        Profile target = new(Guid.NewGuid(), "Home", [dev]);
        Profile source = new(
            Guid.NewGuid(),
            "Work",
            [
                new Workspace(
                    Guid.NewGuid(),
                    " DEV ",
                    [
                        new ShortcutGroup(Guid.NewGuid(), "DEV TOOLS", ShortcutKind.Application, [new Shortcut(Guid.NewGuid(), "Shell", @"C:\Tools\sh.exe")]),
                        Group("Office", ShortcutKind.Application),
                    ],
                    "Theirs",
                    "Globe"),
                new Workspace(Guid.NewGuid(), "Writing", [Group("Notes", ShortcutKind.Directory)], "Words", "PencilSimple"),
            ]);

        Profile result = target.Import(source);

        Assert.Equal(["Dev", "Writing"], result.Workspaces.Select(workspace => workspace.Title));
        Workspace merged = result.Workspaces[0];
        Assert.Equal((dev.Id, "Mine", "Code"), (merged.Id, merged.Description, merged.Icon));
        Assert.Equal(["Dev tools", "Games", "Office"], merged.Groups.Select(group => group.Name));
        Assert.Equal(tools.Id, merged.Groups[0].Id);
        Assert.True(merged.Groups[0].IsCollapsed);
        Assert.Equal(["Code", "Shell"], merged.Groups[0].Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Same(code, merged.Groups[0].Shortcuts[0]);
        Assert.Equal(("Words", "PencilSimple"), (result.Workspaces[1].Description, result.Workspaces[1].Icon));
    }

    [Fact]
    public void Import_LeavesOutAShortcutAlreadyInTheGroup_ButNotOneWithOtherArguments()
    {
        Profile target = One("Home", new ShortcutGroup(Guid.NewGuid(), "Tools", ShortcutKind.Application, [new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "-n")]));
        Profile source = One(
            "Work",
            new ShortcutGroup(
                Guid.NewGuid(),
                "tools",
                ShortcutKind.Application,
                [
                    new Shortcut(Guid.NewGuid(), "code", @"c:\tools\code.exe", " -n "),
                    new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "--wait"),
                    new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "--wait"),
                ]));

        Profile result = target.Import(source);

        Assert.Equal(["-n", "--wait"], Assert.Single(Assert.Single(result.Workspaces).Groups).Shortcuts.Select(shortcut => shortcut.Arguments));
    }

    [Fact]
    public void Import_OfTheSameContentTwice_AddsNothingTheSecondTime()
    {
        Profile once = Profile.Create("Home").Import(Sample());

        Profile twice = once.Import(Sample());

        Assert.Equal(Identities(once), Identities(twice));
    }

    [Fact]
    public void Import_KeepsKindsApart()
    {
        Profile source = One("Work", Group("Tools", ShortcutKind.Url));
        Profile target = One("Home", Group("Tools", ShortcutKind.Application));

        Profile result = target.Import(source);

        Assert.Equal(
            [ShortcutKind.Application, ShortcutKind.Url],
            Assert.Single(result.Workspaces).Groups.Select(group => group.Kind));
    }

    private static ShortcutGroup Group(string name, ShortcutKind kind) => new(Guid.NewGuid(), name, kind, []);

    private static Profile One(string name, ShortcutGroup group)
        => new(Guid.NewGuid(), name, [new Workspace(Guid.NewGuid(), "Shared", [group])]);

    private static string[] Describe(Profile profile)
        => profile.Workspaces
            .SelectMany(workspace => workspace.Groups
                .Select(group => $"{workspace.Title} ({workspace.Description}, {workspace.Icon}) / {group.Kind} {group.Name}: "
                    + string.Join(", ", group.Shortcuts.Select(shortcut => $"{shortcut.Name} → {shortcut.Target} {shortcut.Arguments} ({shortcut.Description}) {shortcut.Icon?.Png.Length}")))
                .DefaultIfEmpty($"{workspace.Title} ({workspace.Description}, {workspace.Icon})"))
            .ToArray();

    private static Guid[] Identities(Profile profile)
        => profile.Workspaces
            .SelectMany(workspace => workspace.Groups
                .SelectMany(group => group.Shortcuts.Select(shortcut => shortcut.Id).Prepend(group.Id))
                .Prepend(workspace.Id))
            .ToArray();

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
                            [new Shortcut(Guid.NewGuid(), "Code", @"C:\Tools\Code.exe", "-n", "The editor", ShortcutIcon.FromBytes(SamplePng.Bytes, isCustom: true)), new Shortcut(Guid.NewGuid(), "Shell", @"C:\Tools\sh.exe")],
                            IsCollapsed: true),
                        new ShortcutGroup(Guid.NewGuid(), "Work", ShortcutKind.Directory, []),
                    ],
                    "Coding",
                    "Code"),
                new Workspace(
                    Guid.NewGuid(),
                    "Reading",
                    [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [new Shortcut(Guid.NewGuid(), "Avalonia", "https://docs.avaloniaui.net")])]),
            ]);
}
