using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class ShortcutSelectionViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public ShortcutSelectionViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheDialog_ShowsACheckboxPerWorkspaceGroupAndShortcut_AndReturnsWhatIsLeftChecked()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            await harness.LoadedPageAsync();
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();
            Profile content = new(
                Guid.NewGuid(),
                "Work",
                [
                    new Workspace(Guid.NewGuid(), "Dev", [new ShortcutGroup(Guid.NewGuid(), "Dev tools", ShortcutKind.Application, [PageHarness.Shortcut("Code"), PageHarness.Shortcut("Shell")])]),
                    new Workspace(Guid.NewGuid(), "Reading", [new ShortcutGroup(Guid.NewGuid(), "Docs", ShortcutKind.Url, [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net")])]),
                ]);

            Task<Profile?> answer = dialogs.SelectShortcutsAsync("Export “Work”", "Choose what to export.", "Export", content);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            ShortcutSelectionView view = Assert.IsType<ShortcutSelectionView>(harness.Window.HostDialog.Content);
            ShortcutSelectionViewModel model = Assert.IsType<ShortcutSelectionViewModel>(view.DataContext);
            TreeView tree = view.FindControl<TreeView>("SelectionTree") ?? throw new InvalidOperationException("No tree.");
            Assert.Equal(2, tree.ItemCount);
            Assert.Equal("Export “Work”", harness.Window.HostDialog.Title);
            Assert.True(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            // Everything is open, so every workspace, group and shortcut is on screen without unfolding anything.
            CheckBox[] boxes = VisibleBoxes(tree);
            Assert.Equal(["Dev", "Dev tools", "Code", "Shell", "Reading", "Docs", "Avalonia"], boxes.Select(Title));
            Assert.All(boxes, box => Assert.True(box.IsChecked));

            // As a click would: the box tells the line, which passes it on.
            boxes[4].IsChecked = false;
            Assert.False(model.Workspaces[1].Children[0].IsChecked);
            Assert.Equal("1 workspace · 2 shortcuts selected", view.FindControl<TextBlock>("SelectionSummary")?.Text);

            boxes[0].IsChecked = false;
            Assert.False(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            boxes[1].IsChecked = true;
            Assert.True(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            // Open is where it starts, not a lock: a group still folds.
            model.Workspaces[0].Children[0].IsExpanded = false;
            harness.Window.UpdateLayout();
            Assert.Equal(["Dev", "Dev tools", "Reading", "Docs", "Avalonia"], VisibleBoxes(tree).Select(Title));

            Button confirm = harness.Window.HostDialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PrimaryButton");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Profile chosen = await answer ?? throw new InvalidOperationException("The dialog was cancelled.");
            Workspace dev = Assert.Single(chosen.Workspaces);
            Assert.Equal("Dev", dev.Title);
            Assert.Equal(["Code", "Shell"], Assert.Single(dev.Groups).Shortcuts.Select(shortcut => shortcut.Name));
        });

    private static CheckBox[] VisibleBoxes(TreeView tree)
        => tree.GetVisualDescendants().OfType<CheckBox>().Where(box => box.IsEffectivelyVisible).ToArray();

    private static string Title(CheckBox box) => ((SelectionNodeViewModel)box.DataContext!).Title;
}
