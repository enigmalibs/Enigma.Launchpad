using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ShortcutSelectionViewModelTests
{
    private readonly Profile _content = Content();
    private readonly ShortcutSelectionViewModel _dialog;

    public ShortcutSelectionViewModelTests() => _dialog = new ShortcutSelectionViewModel("Choose what to export.", _content);

    private SelectionNodeViewModel Dev => _dialog.Workspaces[0];

    private SelectionNodeViewModel Tools => Dev.Children[0];

    private SelectionNodeViewModel Empty => Dev.Children[1];

    private SelectionNodeViewModel Reading => _dialog.Workspaces[1];

    private SelectionNodeViewModel Spare => _dialog.Workspaces[2];

    [Fact]
    public void Starts_WithEveryWorkspace_EverythingChecked_AndEverythingOpen()
    {
        Assert.Equal("Choose what to export.", _dialog.Message);
        Assert.Equal(["Dev", "Reading", "Spare"], _dialog.Workspaces.Select(workspace => workspace.Title));
        Assert.All(Lines(), line => Assert.True(line.IsChecked));
        Assert.All(Lines(), line => Assert.True(line.IsExpanded));
        Assert.True(_dialog.HasSelection);
        Assert.Equal("3 workspaces · 3 shortcuts selected", _dialog.Summary);
    }

    [Fact]
    public void Lines_ShowNames_Details_AndIcons()
    {
        Assert.Equal(["Dev tools", "Empty", "Repos"], Dev.Children.Select(group => group.Title));
        Assert.Equal(["2 shortcuts", "empty", "empty"], Dev.Children.Select(group => group.Detail));
        Assert.Equal("1 shortcut", Reading.Children[0].Detail);
        Assert.Equal(["Code", "Shell"], Tools.Children.Select(shortcut => shortcut.Title));
        Assert.Equal(@"C:\Tools\Code.exe", Tools.Children[0].Detail);
        Assert.Null(Dev.Detail);
        Assert.Equal(PhosphorIcon.Code, Dev.Icon);
        Assert.Equal(PhosphorIcon.SquaresFour, Reading.Icon);
        Assert.Equal(PhosphorIcon.AppWindow, Tools.Icon);
        Assert.Equal(PhosphorIcon.AppWindow, Tools.Children[0].Icon);
        Assert.Equal(PhosphorIcon.Folder, Dev.Children[2].Icon);
        Assert.Equal(PhosphorIcon.Globe, Reading.Children[0].Children[0].Icon);
        Assert.Empty(Tools.Children[0].Children);
        Assert.Empty(Spare.Children);
    }

    [Fact]
    public void UncheckingAShortcut_LeavesItsGroupAndWorkspacePartlyChecked()
    {
        Tools.Children[1].IsChecked = false;

        Assert.Null(Tools.IsChecked);
        Assert.Null(Dev.IsChecked);
        Assert.True(Reading.IsChecked);
        Assert.Equal("3 workspaces · 2 shortcuts selected", _dialog.Summary);
    }

    [Fact]
    public void UncheckingEveryShortcutOfAGroup_UnchecksTheGroup()
    {
        Tools.Children[0].IsChecked = false;
        Tools.Children[1].IsChecked = false;

        Assert.False(Tools.IsChecked);
        Assert.Null(Dev.IsChecked);
    }

    [Fact]
    public void CheckingAPartlyCheckedWorkspace_ChecksEverythingUnderIt()
    {
        Tools.Children[0].IsChecked = false;
        Empty.IsChecked = false;

        Dev.IsChecked = true;

        Assert.All(Walk(Dev), line => Assert.True(line.IsChecked));
    }

    [Fact]
    public void UncheckingAWorkspace_UnchecksEverythingUnderIt_AndLeavesTheOthers()
    {
        Dev.IsChecked = false;

        Assert.All(Walk(Dev), line => Assert.False(line.IsChecked));
        Assert.True(Reading.IsChecked);
        Assert.Equal("2 workspaces · 1 shortcut selected", _dialog.Summary);
    }

    [Fact]
    public void IndeterminateIsShownButNeverChosen()
    {
        Tools.Children[0].IsChecked = false;

        Tools.IsChecked = null;

        Assert.Null(Tools.IsChecked);
        Assert.False(Tools.Children[0].IsChecked);
        Assert.True(Tools.Children[1].IsChecked);
    }

    [Fact]
    public void UncheckingEverything_LeavesNothingToConfirm_AndSaysSo()
    {
        List<string?> changes = [];
        _dialog.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Dev.IsChecked = false;
        Reading.IsChecked = false;
        Spare.IsChecked = false;

        Assert.False(_dialog.HasSelection);
        Assert.Equal("Nothing selected", _dialog.Summary);
        Assert.Contains(nameof(ShortcutSelectionViewModel.HasSelection), changes);

        Reading.Children[0].Children[0].IsChecked = true;

        Assert.True(_dialog.HasSelection);
        Assert.True(Reading.IsChecked);
    }

    [Fact]
    public void ACheckboxChange_IsAnnouncedOnEveryLineItChanged()
    {
        List<SelectionNodeViewModel> announced = [];

        foreach (SelectionNodeViewModel line in Lines())
        {
            line.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectionNodeViewModel.IsChecked))
                {
                    announced.Add((SelectionNodeViewModel)sender!);
                }
            };
        }

        Tools.Children[0].IsChecked = false;

        Assert.Equal([Tools.Children[0], Tools, Dev], announced);
    }

    [Fact]
    public void ToProfile_WithEverythingChecked_IsTheContentAsItWas()
    {
        Profile chosen = _dialog.ToProfile();

        Assert.Equal((_content.Id, _content.Name), (chosen.Id, chosen.Name));
        Assert.Equal(Describe(_content), Describe(chosen));
    }

    [Fact]
    public void ToProfile_KeepsOnlyWhatIsChecked_InOrder()
    {
        Tools.Children[0].IsChecked = false;
        Empty.IsChecked = false;
        Dev.Children[2].IsChecked = false;
        Reading.IsChecked = false;

        Profile chosen = _dialog.ToProfile();

        Assert.Equal(["Dev", "Spare"], chosen.Workspaces.Select(workspace => workspace.Title));
        Workspace dev = chosen.Workspaces[0];
        Assert.Equal((_content.Workspaces[0].Id, "Coding", "Code"), (dev.Id, dev.Description, dev.Icon));
        ShortcutGroup tools = Assert.Single(dev.Groups);
        Assert.Equal(_content.Workspaces[0].Groups[0].Id, tools.Id);
        Assert.Equal(["Shell"], tools.Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Same(_content.Workspaces[0].Groups[0].Shortcuts[1], tools.Shortcuts[0]);
    }

    [Fact]
    public void ToProfile_KeepsAnEmptyGroupOrWorkspace_OnlyWhenItIsChecked()
    {
        Tools.IsChecked = false;
        Dev.Children[2].IsChecked = false;

        Assert.Equal(["Empty"], _dialog.ToProfile().Workspaces[0].Groups.Select(group => group.Name));
        Assert.Contains("Spare", _dialog.ToProfile().Workspaces.Select(workspace => workspace.Title));

        Empty.IsChecked = false;
        Spare.IsChecked = false;

        Assert.Equal(["Reading"], _dialog.ToProfile().Workspaces.Select(workspace => workspace.Title));
    }

    [Fact]
    public void AProfileWithNoWorkspace_OffersNothing()
    {
        ShortcutSelectionViewModel dialog = new("Choose.", Profile.Create("Empty"));

        Assert.Empty(dialog.Workspaces);
        Assert.False(dialog.HasSelection);
        Assert.Equal("Nothing selected", dialog.Summary);
    }

    private static string[] Describe(Profile profile)
        => profile.Workspaces
            .Select(workspace => $"{workspace.Id} {workspace.Title}: "
                + string.Join(" | ", workspace.Groups.Select(group => $"{group.Id} {group.Name}: {string.Join(", ", group.Shortcuts.Select(shortcut => shortcut.Id))}")))
            .ToArray();

    private static Profile Content()
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
                            [PageHarness.Shortcut("Code", @"C:\Tools\Code.exe"), PageHarness.Shortcut("Shell", @"C:\Tools\sh.exe")]),
                        new ShortcutGroup(Guid.NewGuid(), "Empty", ShortcutKind.Application, []),
                        new ShortcutGroup(Guid.NewGuid(), "Repos", ShortcutKind.Directory, []),
                    ],
                    "Coding",
                    "Code"),
                new Workspace(
                    Guid.NewGuid(),
                    "Reading",
                    [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net")])]),
                Workspace.Create("Spare"),
            ]);

    private static IEnumerable<SelectionNodeViewModel> Walk(SelectionNodeViewModel line) => line.Children.SelectMany(Walk).Prepend(line);

    private IEnumerable<SelectionNodeViewModel> Lines() => _dialog.Workspaces.SelectMany(Walk);
}
