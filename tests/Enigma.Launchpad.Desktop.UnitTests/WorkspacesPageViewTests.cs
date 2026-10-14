using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Enigma.Icons.Avalonia;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Dialogs;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class WorkspacesPageViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public WorkspacesPageViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void ThePage_ShowsACardPerWorkspace_WithItsTitleDescriptionAndSummary()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data());
            WorkspacesPageView view = await LoadedAsync(harness, workspaces: 2);

            Button[] cards = Cards(view);
            Assert.Equal(2, cards.Length);
            Assert.Equal(["Dev", "Coding", "1 group · 1 shortcut"], VisibleTexts(cards[0]));
            Assert.Equal(["Reading", "Empty"], VisibleTexts(cards[1]));

            Assert.True(view.FindControl<TextBox>("SearchBox")?.IsVisible);
            Assert.False(view.FindControl<StackPanel>("EmptyState")?.IsVisible);
            Assert.False(view.FindControl<TextBlock>("NoMatch")?.IsVisible);
            Assert.True(view.FindControl<Button>("NewWorkspaceButton")?.IsVisible);
        });

    [Fact]
    public void Typing_InTheSearch_LeavesOnlyTheMatchingCards()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data());
            WorkspacesPageView view = await LoadedAsync(harness, workspaces: 2);
            TextBox search = view.FindControl<TextBox>("SearchBox") ?? throw new InvalidOperationException("No search box.");

            search.Text = "read";
            harness.Window.UpdateLayout();

            Assert.Equal(["Reading"], Cards(view).Select(card => VisibleTexts(card)[0]));

            search.Text = "nothing like it";
            harness.Window.UpdateLayout();

            Assert.Empty(Cards(view));
            TextBlock noMatch = view.FindControl<TextBlock>("NoMatch") ?? throw new InvalidOperationException("No message.");
            Assert.True(noMatch.IsVisible);
            Assert.Equal("No workspace matches “nothing like it”.", noMatch.Text);
        });

    [Fact]
    public void AProfileWithoutWorkspaces_ShowsTheInvitation_AndNoSearch()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            WorkspacesPageView view = await LoadedAsync(harness, workspaces: 0);

            Assert.True(view.FindControl<StackPanel>("EmptyState")?.IsVisible);
            Assert.False(view.FindControl<TextBox>("SearchBox")?.IsVisible);
            Assert.False(view.FindControl<Button>("NewWorkspaceButton")?.IsVisible);
        });

    [Fact]
    public void ACardsMenu_OffersOpenEditCopyMovesAndDelete()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data());
            WorkspacesPageView view = await LoadedAsync(harness, workspaces: 2);

            Button actions = view.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetName(button) == "Workspace actions");
            MenuFlyout menu = Assert.IsType<MenuFlyout>(actions.Flyout);

            string[] entries = menu.Items.Select(item => item is MenuItem entry ? entry.Header?.ToString() ?? string.Empty : "—").ToArray();
            Assert.Equal(["Open", "Edit…", "Duplicate…", "—", "Move up", "Move down", "—", "Delete…"], entries);
        });

    [Fact]
    public void TheWorkspaceForm_OffersTheGlyphs_AndReturnsWhatWasEntered()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data());
            await LoadedAsync(harness, workspaces: 2);
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();

            Task<WorkspaceDetails?> answer = dialogs.EditWorkspaceAsync(null, ["Dev", "Reading"]);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            WorkspaceEditorView view = Assert.IsType<WorkspaceEditorView>(harness.Window.HostDialog.Content);
            WorkspaceEditorViewModel form = Assert.IsType<WorkspaceEditorViewModel>(view.DataContext);
            ListBox glyphs = view.FindControl<ListBox>("GlyphList") ?? throw new InvalidOperationException("No glyph list.");
            Assert.Equal("New workspace", harness.Window.HostDialog.Title);
            Assert.Equal(form.Choices.Count, glyphs.ItemCount);
            Assert.Same(form.SelectedGlyph, glyphs.SelectedItem);
            Assert.False(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            view.FindControl<TextBox>("TitleBox")!.Text = "dev";
            Assert.False(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            view.FindControl<TextBox>("TitleBox")!.Text = "Lab";
            glyphs.SelectedIndex = 12;
            Assert.True(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            Button confirm = harness.Window.HostDialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PrimaryButton");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            WorkspaceDetails details = await answer ?? throw new InvalidOperationException("The dialog was cancelled.");
            Assert.Equal(new WorkspaceDetails("Lab", null, form.Choices[12].Glyph.ToString()), details);
        });

    [Fact]
    public void ClickingACard_OpensItsWorkspace_InPlaceOfTheList_AndBackReturns()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data());
            WorkspacesPageView view = await LoadedAsync(harness, workspaces: 2);

            Cards(view)[0].Command!.Execute(null);
            await WindowHarness.WaitUntilAsync(() => harness.OpenPage is not null);
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            WorkspacePageView open = harness.OpenPage!;
            Assert.False(view.FindControl<DockPanel>("WorkspaceListPage")?.IsVisible);
            Assert.Equal("Dev", open.FindControl<TextBlock>("WorkspaceTitle")?.Text);
            Assert.Same(harness.ViewModel.Navigation.Items[0], harness.ViewModel.Navigation.SelectedItem);

            // The group shows its kind's glyph beside its name.
            Icon kind = open.GetVisualDescendants().OfType<Icon>().Single(icon => icon.Classes.Contains("groupKind"));
            Assert.Equal(Enigma.Icons.Phosphor.PhosphorIcon.Globe, kind.Kind);

            // New group asks for the kind first.
            Button newGroup = open.FindControl<Button>("NewGroupButton") ?? throw new InvalidOperationException("No New group button.");
            MenuFlyout kinds = Assert.IsType<MenuFlyout>(newGroup.Flyout);
            Assert.Equal(["Applications", "Folders", "Links", "Documents"], kinds.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()));
            Assert.Equal(
                [ShortcutKind.Application, ShortcutKind.Directory, ShortcutKind.Url, ShortcutKind.Document],
                kinds.Items.OfType<MenuItem>().Select(item => (ShortcutKind)item.CommandParameter!));

            open.FindControl<Button>("BackButton")!.Command!.Execute(null);
            await WindowHarness.WaitUntilAsync(() => harness.OpenPage is null);
            harness.Window.UpdateLayout();

            Assert.True(view.FindControl<DockPanel>("WorkspaceListPage")?.IsVisible);
            Assert.Equal(2, Cards(view).Length);
        });

    [Fact]
    public void AnEmptyWorkspace_OffersAGroupOfEachKind()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(Data());
            WorkspacesPageView view = await LoadedAsync(harness, workspaces: 2);

            Cards(view)[1].Command!.Execute(null);
            await WindowHarness.WaitUntilAsync(() => harness.OpenPage is not null);
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            WorkspacePageView open = harness.OpenPage!;
            Assert.True(open.FindControl<StackPanel>("EmptyState")?.IsVisible);
            Assert.False(open.FindControl<Button>("NewGroupButton")?.IsVisible);
            Assert.Equal(
                ["Create a group of applications", "Create a group of folders", "Create a group of links", "Create a group of documents"],
                open.FindControl<StackPanel>("EmptyKinds")!.Children.OfType<Button>().Select(button => AutomationProperties.GetName(button)));
        });

    private static async Task<WorkspacesPageView> LoadedAsync(WindowHarness harness, int workspaces)
    {
        await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is WorkspacesPageView { DataContext: WorkspacesPageViewModel { IsLoaded: true } page }
            && (page.Workspaces.Count == workspaces));
        await Task.Delay(20);
        harness.Window.UpdateLayout();

        return (WorkspacesPageView)(harness.ViewModel.Navigation.CurrentPage ?? throw new InvalidOperationException("No page."));
    }

    private static Button[] Cards(WorkspacesPageView view)
        => [.. view.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("workspaceCard") && button.IsEffectivelyVisible)];

    private static string[] VisibleTexts(Button card)
        => [.. card.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text!)];

    private static LaunchpadData Data()
    {
        Profile work = new(
            Guid.NewGuid(),
            "Work",
            [
                new Workspace(
                    Guid.NewGuid(),
                    "Dev",
                    [new ShortcutGroup(Guid.NewGuid(), "Tools", ShortcutKind.Url, [PageHarness.Shortcut("Docs", "https://example.com")])],
                    "Coding",
                    "Code"),
                new Workspace(Guid.NewGuid(), "Reading", []),
            ]);

        return LaunchpadData.Of(work);
    }
}
