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

    private SelectionNodeViewModel Applications => _dialog.Categories[0];

    private SelectionNodeViewModel Tools => Applications.Children[0];

    private SelectionNodeViewModel Empty => Applications.Children[1];

    private SelectionNodeViewModel Urls => _dialog.Categories[1];

    [Fact]
    public void Starts_WithTheCategoriesThatHaveGroups_EverythingChecked_AndEverythingOpen()
    {
        Assert.Equal("Choose what to export.", _dialog.Message);
        Assert.Equal(["Applications", "URLs"], _dialog.Categories.Select(category => category.Title));
        Assert.All(Lines(), line => Assert.True(line.IsChecked));
        Assert.All(Lines(), line => Assert.True(line.IsExpanded));
        Assert.True(_dialog.HasSelection);
        Assert.Equal("3 groups · 3 shortcuts selected", _dialog.Summary);
    }

    [Fact]
    public void Lines_ShowNames_Details_AndIcons()
    {
        Assert.Equal(["Dev tools", "Empty"], Applications.Children.Select(group => group.Title));
        Assert.Equal(["2 shortcuts", "empty"], Applications.Children.Select(group => group.Detail));
        Assert.Equal("1 shortcut", Urls.Children[0].Detail);
        Assert.Equal(["Code", "Shell"], Tools.Children.Select(shortcut => shortcut.Title));
        Assert.Equal(@"C:\Tools\Code.exe", Tools.Children[0].Detail);
        Assert.Null(Applications.Detail);
        Assert.Equal(PhosphorIcon.AppWindow, Applications.Icon);
        Assert.Equal(PhosphorIcon.AppWindow, Tools.Children[0].Icon);
        Assert.Equal(PhosphorIcon.Globe, Urls.Children[0].Children[0].Icon);
        Assert.Empty(Tools.Children[0].Children);
    }

    [Fact]
    public void UncheckingAShortcut_LeavesItsGroupAndCategoryPartlyChecked()
    {
        Tools.Children[1].IsChecked = false;

        Assert.Null(Tools.IsChecked);
        Assert.Null(Applications.IsChecked);
        Assert.True(Urls.IsChecked);
        Assert.Equal("3 groups · 2 shortcuts selected", _dialog.Summary);
    }

    [Fact]
    public void UncheckingEveryShortcutOfAGroup_UnchecksTheGroup()
    {
        Tools.Children[0].IsChecked = false;
        Tools.Children[1].IsChecked = false;

        Assert.False(Tools.IsChecked);
        Assert.Null(Applications.IsChecked);
    }

    [Fact]
    public void CheckingAPartlyCheckedCategory_ChecksEverythingUnderIt()
    {
        Tools.Children[0].IsChecked = false;
        Empty.IsChecked = false;

        Applications.IsChecked = true;

        Assert.All(new[] { Applications, Tools, Empty }.Concat(Tools.Children), line => Assert.True(line.IsChecked));
    }

    [Fact]
    public void UncheckingACategory_UnchecksEverythingUnderIt_AndLeavesTheOthers()
    {
        Applications.IsChecked = false;

        Assert.All(new[] { Applications, Tools, Empty }.Concat(Tools.Children), line => Assert.False(line.IsChecked));
        Assert.True(Urls.IsChecked);
        Assert.Equal("1 group · 1 shortcut selected", _dialog.Summary);
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

        Applications.IsChecked = false;
        Urls.IsChecked = false;

        Assert.False(_dialog.HasSelection);
        Assert.Equal("Nothing selected", _dialog.Summary);
        Assert.Contains(nameof(ShortcutSelectionViewModel.HasSelection), changes);

        Urls.Children[0].Children[0].IsChecked = true;

        Assert.True(_dialog.HasSelection);
        Assert.True(Urls.IsChecked);
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

        Assert.Equal([Tools.Children[0], Tools, Applications], announced);
    }

    [Fact]
    public void ToProfile_WithEverythingChecked_IsTheContentAsItWas()
    {
        Profile chosen = _dialog.ToProfile();

        Assert.Equal((_content.Id, _content.Name), (chosen.Id, chosen.Name));

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            Assert.Equal(_content.GroupsOf(kind).Select(Describe), chosen.GroupsOf(kind).Select(Describe));
        }
    }

    [Fact]
    public void ToProfile_KeepsOnlyWhatIsChecked_InOrder()
    {
        Tools.Children[0].IsChecked = false;
        Empty.IsChecked = false;
        Urls.IsChecked = false;

        Profile chosen = _dialog.ToProfile();

        ShortcutGroup tools = Assert.Single(chosen.Applications);
        Assert.Equal(_content.Applications[0].Id, tools.Id);
        Assert.Equal(["Shell"], tools.Shortcuts.Select(shortcut => shortcut.Name));
        Assert.Same(_content.Applications[0].Shortcuts[1], tools.Shortcuts[0]);
        Assert.Empty(chosen.Urls);
    }

    [Fact]
    public void ToProfile_KeepsAnEmptyGroup_OnlyWhenItIsChecked()
    {
        Tools.IsChecked = false;

        Assert.Equal(["Empty"], _dialog.ToProfile().Applications.Select(group => group.Name));

        Empty.IsChecked = false;

        Assert.Empty(_dialog.ToProfile().Applications);
    }

    [Fact]
    public void AProfileWithNoGroup_OffersNothing()
    {
        ShortcutSelectionViewModel dialog = new("Choose.", Profile.Create("Empty"));

        Assert.Empty(dialog.Categories);
        Assert.False(dialog.HasSelection);
        Assert.Equal("Nothing selected", dialog.Summary);
    }

    private static string Describe(ShortcutGroup group)
        => $"{group.Id} {group.Name}: {string.Join(", ", group.Shortcuts.Select(shortcut => shortcut.Id))}";

    private static Profile Content()
        => Profile.Create("Work")
            .With(
                ShortcutKind.Application,
                [
                    new ShortcutGroup(
                        Guid.NewGuid(),
                        "Dev tools",
                        [PageHarness.Shortcut("Code", @"C:\Tools\Code.exe"), PageHarness.Shortcut("Shell", @"C:\Tools\sh.exe")]),
                    new ShortcutGroup(Guid.NewGuid(), "Empty", []),
                ])
            .With(
                ShortcutKind.Url,
                [new ShortcutGroup(Guid.NewGuid(), "Docs", [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net")])]);

    private IEnumerable<SelectionNodeViewModel> Lines()
    {
        IEnumerable<SelectionNodeViewModel> Walk(SelectionNodeViewModel line) => line.Children.SelectMany(Walk).Prepend(line);

        return _dialog.Categories.SelectMany(Walk);
    }
}
