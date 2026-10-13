using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The buttons inside the tiles, on the real page through the headless platform.
/// </summary>
[Collection(HeadlessCollection.Name)]
public sealed class TileActionsViewTests(HeadlessAvaloniaFixture fixture)
{
    private const string Folder = @"C:\Projects";

    [Fact]
    public void TheActions_AreHidden_UntilTheTileIsHovered()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(FolderData());
            CategoryPageView view = await DirectoriesAsync(harness);
            Button tile = Tile(view);
            StackPanel actions = Actions(tile);
            Assert.False(actions.IsEffectivelyVisible);

            harness.Window.MouseMove(Centre(harness, tile));
            harness.Window.UpdateLayout();

            Assert.True(actions.IsEffectivelyVisible);
        });

    [Fact]
    public void TheActions_ShowForATileWithTheKeyboardFocus()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(FolderData());
            CategoryPageView view = await DirectoriesAsync(harness);
            Button tile = Tile(view);

            tile.Focus(NavigationMethod.Tab);
            harness.Window.UpdateLayout();

            Assert.True(Actions(tile).IsEffectivelyVisible);
        });

    [Fact]
    public void ClickingCopyPath_CopiesIt_WithoutOpeningTheFolder()
        => fixture.RunAsync(async () =>
        {
            RecordingShortcutActions recording = new();
            using WindowHarness harness = new(FolderData(), services => services.AddSingleton<IShortcutActions>(recording));
            CategoryPageView view = await DirectoriesAsync(harness);
            Button tile = Tile(view);
            harness.Window.MouseMove(Centre(harness, tile));
            harness.Window.UpdateLayout();
            Button copy = Assert.Single(ActionButtons(tile), button => button.IsVisible);
            Assert.True(copy.IsEffectivelyVisible);

            Point point = Centre(harness, copy);
            harness.Window.MouseDown(point, MouseButton.Left);
            harness.Window.MouseUp(point, MouseButton.Left);
            await WindowHarness.WaitUntilAsync(() => recording.Performed.Count > 0);

            (string action, _, Shortcut shortcut) = Assert.Single(recording.Performed);
            Assert.Equal("Copy", action);
            Assert.Equal(Folder, shortcut.Target);
        });

    [Fact]
    public void AnApplicationTile_OffersRunWithArguments_WhichAsksThenRuns()
        => fixture.RunAsync(async () =>
        {
            RecordingShortcutActions recording = new();
            Shortcut code = PageHarness.Shortcut("Code", @"C:\Tools\code.exe", "--new-window");
            using WindowHarness harness = new(
                PageHarness.WithShortcuts(ShortcutKind.Application, ("Tools", [code])),
                services => services.AddSingleton<IShortcutActions>(recording));
            CategoryPageView view = await harness.LoadedPageAsync();
            Button run = Assert.Single(ActionButtons(Tile(view)), button => button.IsVisible);
            Assert.Equal("Run with arguments", AutomationProperties.GetName(run));

            run.Command!.Execute(null);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);
            harness.Window.UpdateLayout();

            Assert.Equal("Run Code", harness.Window.HostDialog.Title);
            TextBox field = harness.Window.HostDialog.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "ArgumentsBox");
            Assert.Equal("--new-window", field.Text);

            field.Text = "--wait notes.txt";
            Button confirm = harness.Window.HostDialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PrimaryButton");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WindowHarness.WaitUntilAsync(() => recording.Performed.Count > 0);

            Assert.Equal("--wait notes.txt", Assert.Single(recording.Performed).Shortcut.Arguments);
        });

    [Fact]
    public void ALinkTile_OffersCopyUrl_AndAnApplicationTile_NoCopy()
        => fixture.RunAsync(async () =>
        {
            LaunchpadData data = PageHarness.WithShortcuts(ShortcutKind.Url, ("Reading", [PageHarness.Shortcut("Docs", "https://example.com/")]))
                .With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Tools", [PageHarness.Shortcut("Code", @"C:\Tools\code.exe")])]);
            using WindowHarness harness = new(data);

            CategoryPageView applications = await harness.LoadedPageAsync();
            Assert.DoesNotContain(ActionButtons(Tile(applications)), button => button.IsVisible && (AutomationProperties.GetName(button) ?? string.Empty).StartsWith("Copy", StringComparison.Ordinal));

            CategoryPageView urls = await ShowAsync<UrlsPageViewModel>(harness, 2);
            Button copy = Assert.Single(ActionButtons(Tile(urls)), button => button.IsVisible);
            Assert.Equal("Copy URL", AutomationProperties.GetName(copy));
        });

    private static LaunchpadData FolderData()
        => PageHarness.WithShortcuts(ShortcutKind.Directory, ("Work", [PageHarness.Shortcut("Projects", Folder)]));

    private static async Task<CategoryPageView> DirectoriesAsync(WindowHarness harness)
    {
        await harness.LoadedPageAsync();
        return await ShowAsync<DirectoriesPageViewModel>(harness, 1);
    }

    // Selects a category in the rail, as a click on it would, and waits for its page.
    private static async Task<CategoryPageView> ShowAsync<TPage>(WindowHarness harness, int index)
        where TPage : CategoryPageViewModel
    {
        harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.Items[index];
        await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage?.DataContext is TPage);
        return await harness.LoadedPageAsync();
    }

    private static Button Tile(CategoryPageView view)
        => view.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("tile"));

    private static StackPanel Actions(Button tile)
        => tile.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Classes.Contains("tileActions"));

    private static Button[] ActionButtons(Button tile)
        => [.. tile.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tileAction"))];

    private static Point Centre(WindowHarness harness, Visual control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), harness.Window)
            ?? throw new InvalidOperationException("Not in the window.");
}
