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
    public void TheDialog_ShowsACheckboxPerCategory_AndReturnsWhatIsLeftChecked()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            await harness.LoadedPageAsync();
            ILaunchpadDialogs dialogs = harness.Provider.GetRequiredService<ILaunchpadDialogs>();
            Profile content = Profile.Create("Work")
                .With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Dev tools", [PageHarness.Shortcut("Code"), PageHarness.Shortcut("Shell")])])
                .With(ShortcutKind.Url, [new ShortcutGroup(Guid.NewGuid(), "Docs", [PageHarness.Shortcut("Avalonia", "https://docs.avaloniaui.net")])]);

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

            // The categories are open and their groups folded.
            CheckBox[] boxes = VisibleBoxes(tree);
            Assert.Equal(["Applications", "Dev tools", "URLs", "Docs"], boxes.Select(Title));
            Assert.All(boxes, box => Assert.True(box.IsChecked));

            // As a click would: the box tells the line, which passes it on.
            boxes[2].IsChecked = false;
            Assert.False(model.Categories[1].Children[0].IsChecked);
            Assert.Equal("1 group · 2 shortcuts selected", view.FindControl<TextBlock>("SelectionSummary")?.Text);

            boxes[0].IsChecked = false;
            Assert.False(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            boxes[1].IsChecked = true;
            Assert.True(harness.Window.HostDialog.IsPrimaryButtonEnabled);

            // A group unfolds to show its shortcuts.
            model.Categories[0].Children[0].IsExpanded = true;
            harness.Window.UpdateLayout();
            Assert.Equal(["Applications", "Dev tools", "Code", "Shell", "URLs", "Docs"], VisibleBoxes(tree).Select(Title));

            Button confirm = harness.Window.HostDialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PrimaryButton");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Profile chosen = await answer ?? throw new InvalidOperationException("The dialog was cancelled.");
            Assert.Equal(["Code", "Shell"], Assert.Single(chosen.Applications).Shortcuts.Select(shortcut => shortcut.Name));
            Assert.Empty(chosen.Urls);
        });

    private static CheckBox[] VisibleBoxes(TreeView tree)
        => tree.GetVisualDescendants().OfType<CheckBox>().Where(box => box.IsEffectivelyVisible).ToArray();

    private static string Title(CheckBox box) => ((SelectionNodeViewModel)box.DataContext!).Title;
}
