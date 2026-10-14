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
            WorkspacePageView view = await DirectoriesAsync(harness);
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
            WorkspacePageView view = await DirectoriesAsync(harness);
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
            WorkspacePageView view = await DirectoriesAsync(harness);
            Button tile = Tile(view);
            harness.Window.MouseMove(Centre(harness, tile));
            harness.Window.UpdateLayout();
            Button copy = ActionButtons(tile).Single(button => AutomationProperties.GetName(button) == "Copy path");
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
            WorkspacePageView view = await harness.LoadedPageAsync();
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
    public void AFolderTile_OffersOpenInTerminal_AndCopyPath()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(FolderData());
            WorkspacePageView view = await DirectoriesAsync(harness);

            Assert.Equal(
                ["Open in terminal", "Copy path"],
                ActionButtons(Tile(view)).Where(button => button.IsVisible).Select(button => AutomationProperties.GetName(button)));
        });

    [Fact]
    public void ALinkTile_OffersCopyUrl_AndAnApplicationTile_NoCopy()
        => fixture.RunAsync(async () =>
        {
            LaunchpadData data = PageHarness.WithShortcuts(ShortcutKind.Url, ("Reading", [PageHarness.Shortcut("Docs", "https://example.com/")]))
                .With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Tools", ShortcutKind.Application, [PageHarness.Shortcut("Code", @"C:\Tools\code.exe")])]);
            using WindowHarness harness = new(data);

            // Both in one workspace: what a tile offers follows its group's kind.
            WorkspacePageView view = await harness.LoadedPageAsync();
            Assert.DoesNotContain(ActionButtons(Tile(view, "Code")), button => button.IsVisible && (AutomationProperties.GetName(button) ?? string.Empty).StartsWith("Copy", StringComparison.Ordinal));

            Button copy = Assert.Single(ActionButtons(Tile(view, "Docs")), button => button.IsVisible);
            Assert.Equal("Copy URL", AutomationProperties.GetName(copy));
        });

    [Fact]
    public void ADocumentTile_OffersCopyPath_Only()
        => fixture.RunAsync(async () =>
        {
            LaunchpadData data = PageHarness.WithShortcuts(ShortcutKind.Document, ("Papers", [PageHarness.Shortcut("Report", @"C:\Docs\report.pdf")]));
            using WindowHarness harness = new(data);
            WorkspacePageView view = await harness.LoadedPageAsync();

            Button copy = Assert.Single(ActionButtons(Tile(view, "Report")), button => button.IsVisible);
            Assert.Equal("Copy path", AutomationProperties.GetName(copy));
        });

    private static LaunchpadData FolderData()
        => PageHarness.WithShortcuts(ShortcutKind.Directory, ("Work", [PageHarness.Shortcut("Projects", Folder)]));

    // The folder's group is in the workspace the harness opens.
    private static Task<WorkspacePageView> DirectoriesAsync(WindowHarness harness) => harness.LoadedPageAsync();

    private static Button Tile(WorkspacePageView view, string? name = null)
        => view.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("tile") && ((name is null) || (AutomationProperties.GetName(button) == name)));

    private static StackPanel Actions(Button tile)
        => tile.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Classes.Contains("tileActions"));

    private static Button[] ActionButtons(Button tile)
        => [.. tile.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tileAction"))];

    private static Point Centre(WindowHarness harness, Visual control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), harness.Window)
            ?? throw new InvalidOperationException("Not in the window.");
}
