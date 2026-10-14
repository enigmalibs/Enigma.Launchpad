using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Enigma.Icons.Avalonia;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class WorkspacePageViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public WorkspacePageViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void APageWithGroups_ShowsOneCardPerGroup_AndNoEmptyState()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.Data("Dev tools", "Games"));
            WorkspacePageView view = await LoadedPageAsync(harness);

            ItemsControl list = view.FindControl<ItemsControl>("GroupList") ?? throw new InvalidOperationException("No group list.");
            Assert.Equal(2, list.ItemCount);
            Assert.NotNull(list.ContainerFromIndex(1));
            Assert.False(view.FindControl<StackPanel>("EmptyState")?.IsVisible);
            Assert.True(view.FindControl<Button>("NewGroupButton")?.IsVisible);

            string[] names = view.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.Classes.Contains("groupName"))
                .Select(text => text.Text ?? string.Empty)
                .ToArray();
            Assert.Equal(["Dev tools", "Games"], names);
        });

    [Fact]
    public void AnEmptyCategory_ShowsTheInvitation_InsteadOfTheHeaderButton()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            WorkspacePageView view = await LoadedPageAsync(harness);

            Assert.True(view.FindControl<StackPanel>("EmptyState")?.IsVisible);
            Assert.False(view.FindControl<Button>("NewGroupButton")?.IsVisible);
        });

    [Fact]
    public void AFoldedGroup_HidesItsBody()
        => _fixture.RunAsync(async () =>
        {
            LaunchpadData data = LaunchpadData.Empty.With(
                ShortcutKind.Application,
                [new ShortcutGroup(Guid.NewGuid(), "Folded", ShortcutKind.Application, [], IsCollapsed: true)]);
            using WindowHarness harness = new(data);
            WorkspacePageView view = await LoadedPageAsync(harness);

            TextBlock emptyHint = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("groupEmpty"));
            Assert.False(emptyHint.IsEffectivelyVisible);

            Assert.IsType<WorkspacePageViewModel>(view.DataContext).Groups[0].IsExpanded = true;
            await WindowHarness.WaitUntilAsync(() => emptyHint.IsEffectivelyVisible);
        });

    [Fact]
    public void TheNameDialog_EnablesItsConfirmButton_OnlyForAnAcceptedName()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            await LoadedPageAsync(harness);
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();

            Task<string?> answer = dialogs.PromptNameAsync(
                "New group",
                "Group name",
                "Create",
                null,
                name => string.IsNullOrWhiteSpace(name) ? "Enter a name." : null);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);

            NameDialogViewModel model = Assert.IsType<NameDialogViewModel>(Assert.IsAssignableFrom<Control>(harness.Window.HostDialog.Content).DataContext);
            Assert.False(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            model.Name = "  Games  ";
            Assert.True(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            // The dialog's template is applied on the next layout pass; the buttons exist only after it.
            harness.Window.UpdateLayout();
            Button confirm = harness.Window.HostDialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PrimaryButton");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("Games", await answer);
        });

    [Fact]
    public void TheDeleteQuestion_IsRed_OnlyWhileItIsAsked()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            await LoadedPageAsync(harness);
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();

            Task<bool> answer = dialogs.ConfirmDeleteAsync("Delete the group?", "This cannot be undone.", "Delete");
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);

            Assert.Contains(LaunchpadDialogs.DangerClass, harness.Window.HostDialog.Classes);

            await harness.Window.HostDialog.HideAsync();

            Assert.False(await answer);
            Assert.DoesNotContain(LaunchpadDialogs.DangerClass, harness.Window.HostDialog.Classes);
        });

    [Fact]
    public void EveryShortcut_IsDrawnAsATile()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.WithShortcuts(
                ShortcutKind.Application,
                ("Dev tools", [PageHarness.Shortcut("Code", @"C:\Tools\code.exe"), PageHarness.Shortcut("Git", @"C:\Tools\git.exe")])));
            WorkspacePageView view = await LoadedPageAsync(harness);

            Button[] tiles = view.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tile")).ToArray();
            Assert.Equal(2, tiles.Length);
            Assert.All(tiles, tile => Assert.NotNull(tile.ContextMenu));

            string[] subtitles = view.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.Classes.Contains("tileSubtitle"))
                .Select(text => text.Text ?? string.Empty)
                .ToArray();
            Assert.Equal([@"C:\Tools\code.exe", @"C:\Tools\git.exe"], subtitles);
        });

    [Fact]
    public void ADescription_IsShownUnderTheTilesIconRow_OnlyWhenThereIsOne()
        => _fixture.RunAsync(async () =>
        {
            Shortcut described = PageHarness.Shortcut("Code", @"C:\Tools\code.exe") with { Description = "The editor" };
            using WindowHarness harness = new(PageHarness.WithShortcuts(
                ShortcutKind.Application,
                ("Dev tools", [described, PageHarness.Shortcut("Git", @"C:\Tools\git.exe")]),
                ("Plain", [PageHarness.Shortcut("Shell", @"C:\Tools\sh.exe")])));
            WorkspacePageView view = await LoadedPageAsync(harness);

            TextBlock[] descriptions = view.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.Classes.Contains("tileDescription"))
                .ToArray();
            Assert.Equal(3, descriptions.Length);
            Assert.Equal(["The editor"], descriptions.Where(text => text.IsEffectivelyVisible).Select(text => text.Text));

            Button[] tiles = view.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tile")).ToArray();
            Button code = tiles.Single(tile => tile.DataContext is ShortcutViewModel { Name: "Code" });
            Button git = tiles.Single(tile => tile.DataContext is ShortcutViewModel { Name: "Git" });
            Button shell = tiles.Single(tile => tile.DataContext is ShortcutViewModel { Name: "Shell" });

            // A tile without a description keeps its size; one in the same row grows with its neighbour.
            Assert.Equal(62, shell.Bounds.Height);
            Assert.True(code.Bounds.Height > 62);
            Assert.Equal(code.Bounds.Height, git.Bounds.Height);
        });

    [Fact]
    public void AnApplicationTile_TradesItsGlyphForItsProgramsIcon_OnceItArrives()
        => _fixture.RunAsync(async () =>
        {
            FakeTargetIcons programIcons = new() { Hold = true };
            using WindowHarness harness = new(
                PageHarness.WithShortcuts(
                    ShortcutKind.Application,
                    ("Dev tools", [PageHarness.Shortcut("Code", @"C:\Tools\code.exe"), PageHarness.Shortcut("Git", @"C:\Tools\git.exe")])),
                services => services.AddSingleton<IProgramIcons>(programIcons));
            WorkspacePageView view = await LoadedPageAsync(harness);
            (Image codeImage, Icon codeGlyph) = TileIcon(view, "Code");
            (Image gitImage, Icon gitGlyph) = TileIcon(view, "Git");
            Assert.True(codeGlyph.IsEffectivelyVisible);
            Assert.False(codeImage.IsEffectivelyVisible);

            programIcons.Release(@"C:\Tools\code.exe", TestImages.Png(ProgramIcons.IconSize, ProgramIcons.IconSize));
            programIcons.Release(@"C:\Tools\git.exe", null);
            await WindowHarness.WaitUntilAsync(() => codeImage.IsEffectivelyVisible);

            Assert.Equal(new PixelSize(ProgramIcons.IconSize, ProgramIcons.IconSize), Assert.IsType<Bitmap>(codeImage.Source).PixelSize);
            Assert.False(codeGlyph.IsEffectivelyVisible);
            Assert.True(gitGlyph.IsEffectivelyVisible);
            Assert.False(gitImage.IsEffectivelyVisible);
        });

    [Fact]
    public void TheShortcutForm_ForALink_HasNoBrowse_AndConfirmsOnlyWhenValid()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            await LoadedPageAsync(harness);
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();

            Task<Shortcut?> answer = dialogs.EditShortcutAsync(ShortcutKind.Url, null);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);
            harness.Window.UpdateLayout();

            Assert.Equal("Add link", harness.Window.HostDialog.Title);
            ShortcutEditorViewModel form = Assert.IsType<ShortcutEditorViewModel>(Assert.IsAssignableFrom<Control>(harness.Window.HostDialog.Content).DataContext);
            Assert.False(harness.Window.HostDialog.IsPrimaryButtonEnabled);
            Assert.DoesNotContain(
                harness.Window.HostDialog.GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible && (button.Content is StackPanel) && button.Command == form.BrowseCommand);

            form.Name = "Avalonia";
            form.Target = "docs.avaloniaui.net";
            Assert.True(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            Button confirm = harness.Window.HostDialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PrimaryButton");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Shortcut? shortcut = await answer;
            Assert.NotNull(shortcut);
            Assert.Equal("https://docs.avaloniaui.net", shortcut.Target);
        });

    [Fact]
    public void TheShortcutForm_HasAnIconRow_ShowingTheKindsGlyph_UntilAnImageIsChosen()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            await LoadedPageAsync(harness);
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();

            Task<Shortcut?> answer = dialogs.EditShortcutAsync(ShortcutKind.Directory, null);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);
            harness.Window.UpdateLayout();

            Control content = Assert.IsAssignableFrom<Control>(harness.Window.HostDialog.Content);
            ShortcutEditorViewModel form = Assert.IsType<ShortcutEditorViewModel>(content.DataContext);
            Icon glyph = content.GetVisualDescendants().OfType<Icon>().Single(icon => icon.Name == "IconGlyph");
            Image image = content.GetVisualDescendants().OfType<Image>().Single(candidate => candidate.Name == "IconImage");
            Button choose = content.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ChooseIconButton");

            Assert.True(glyph.IsEffectivelyVisible);
            Assert.False(image.IsEffectivelyVisible);
            Assert.True(choose.IsEffectivelyVisible);
            Assert.Same(form.ChooseIconCommand, choose.Command);

            await harness.Window.HostDialog.HideAsync();
            Assert.Null(await answer);
        });

    // The window opens on the workspaces; the harness opens the applications page and waits for it.
    private static Task<WorkspacePageView> LoadedPageAsync(WindowHarness harness) => harness.LoadedPageAsync();

    /// <summary>
    /// The two things a tile's plate can show: the program's icon and the category's glyph.
    /// </summary>
    private static (Image Image, Icon Glyph) TileIcon(WorkspacePageView view, string name)
    {
        Button tile = view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("tile") && button.DataContext is ShortcutViewModel shortcut && shortcut.Name == name);

        return (
            tile.GetVisualDescendants().OfType<Image>().Single(image => image.Classes.Contains("tileIcon")),
            tile.GetVisualDescendants().OfType<Icon>().Single(glyph => glyph.Classes.Contains("tileIcon")));
    }
}
