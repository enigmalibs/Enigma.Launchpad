using System;
using System.Linq;
using System.Threading.Tasks;
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
public sealed class CategoryPageViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public CategoryPageViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void APageWithGroups_ShowsOneCardPerGroup_AndNoEmptyState()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.Data("Dev tools", "Games"));
            CategoryPageView view = await LoadedPageAsync(harness);

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
            CategoryPageView view = await LoadedPageAsync(harness);

            Assert.True(view.FindControl<StackPanel>("EmptyState")?.IsVisible);
            Assert.False(view.FindControl<Button>("NewGroupButton")?.IsVisible);
        });

    [Fact]
    public void AFoldedGroup_HidesItsBody()
        => _fixture.RunAsync(async () =>
        {
            LaunchpadData data = LaunchpadData.Empty.With(
                ShortcutKind.Application,
                [new ShortcutGroup(Guid.NewGuid(), "Folded", [], IsCollapsed: true)]);
            using WindowHarness harness = new(data);
            CategoryPageView view = await LoadedPageAsync(harness);

            TextBlock emptyHint = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("groupEmpty"));
            Assert.False(emptyHint.IsEffectivelyVisible);

            Assert.IsType<ApplicationsPageViewModel>(view.DataContext).Groups[0].IsExpanded = true;
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
            CategoryPageView view = await LoadedPageAsync(harness);

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
    public void AnApplicationTile_TradesItsGlyphForItsProgramsIcon_OnceItArrives()
        => _fixture.RunAsync(async () =>
        {
            FakeTargetIcons programIcons = new() { Hold = true };
            using WindowHarness harness = new(
                PageHarness.WithShortcuts(
                    ShortcutKind.Application,
                    ("Dev tools", [PageHarness.Shortcut("Code", @"C:\Tools\code.exe"), PageHarness.Shortcut("Git", @"C:\Tools\git.exe")])),
                services => services.AddSingleton<IProgramIcons>(programIcons));
            CategoryPageView view = await LoadedPageAsync(harness);
            (Image codeImage, Icon codeGlyph) = TileIcon(view, "Code");
            (Image gitImage, Icon gitGlyph) = TileIcon(view, "Git");
            Assert.True(codeGlyph.IsEffectivelyVisible);
            Assert.False(codeImage.IsEffectivelyVisible);

            Bitmap icon = ProgramIcons.ToBitmap(FakeProgramIconReader.Opaque(ProgramIcons.IconSize));
            programIcons.Release(@"C:\Tools\code.exe", icon);
            programIcons.Release(@"C:\Tools\git.exe", null);
            await WindowHarness.WaitUntilAsync(() => codeImage.IsEffectivelyVisible);

            Assert.Same(icon, codeImage.Source);
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

    private static async Task<CategoryPageView> LoadedPageAsync(WindowHarness harness)
    {
        await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is CategoryPageView { DataContext: CategoryPageViewModel { IsLoaded: true } });

        // One more turn of the dispatcher, so the bindings the load changed have been applied.
        await Task.Delay(20);

        return Assert.IsType<CategoryPageView>(harness.ViewModel.Navigation.CurrentPage);
    }

    /// <summary>
    /// The two things a tile's plate can show: the program's icon and the category's glyph.
    /// </summary>
    private static (Image Image, Icon Glyph) TileIcon(CategoryPageView view, string name)
    {
        Button tile = view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("tile") && button.DataContext is ShortcutViewModel shortcut && shortcut.Name == name);

        return (
            tile.GetVisualDescendants().OfType<Image>().Single(image => image.Classes.Contains("tileIcon")),
            tile.GetVisualDescendants().OfType<Icon>().Single(glyph => glyph.Classes.Contains("tileIcon")));
    }
}
