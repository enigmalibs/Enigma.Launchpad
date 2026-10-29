using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Launchpad.Core.Diagnostics;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.Views.Dialogs;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The About dialog: what it says, how it is shown, and where it is opened from.
/// </summary>
[Collection(HeadlessCollection.Name)]
public sealed class AboutDialogTests(HeadlessAvaloniaFixture fixture)
{
    [Fact]
    public void TheViewModel_DescribesTheRunningBuild()
    {
        AboutViewModel about = new();

        Assert.Equal("Enigma Launchpad", about.DisplayName);
        Assert.Equal(ProductInformation.Version, about.Version);
        Assert.Equal(ProductInformation.BuildSha, about.BuildSha);
        Assert.Equal(ProductInformation.Copyright, about.Copyright);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("9a1b2c3", true)]
    public void TheBuildLine_IsThereOnlyWithARevision(string? sha, bool shown)
        => Assert.Equal(shown, new AboutViewModel("1.0.0", sha, "Copyright").HasBuildSha);

    [Theory]
    [InlineData("", false)]
    [InlineData("Copyright © 2026 Josué Clément", true)]
    public void TheCopyrightLine_IsThereOnlyWithANotice(string copyright, bool shown)
        => Assert.Equal(shown, new AboutViewModel("1.0.0", null, copyright).HasCopyright);

    [Fact]
    public void TheService_ShowsTheAboutViewUntitled_WithOneButtonThatOnlyCloses()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            ContentDialog host = harness.Window.HostDialog;

            Task showing = harness.Provider.GetRequiredService<IAboutDialogService>().ShowAsync();
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);

            // No title and no icon: the card would draw them as a column beside the view.
            Assert.Null(host.Title);
            Assert.Null(host.IconData);
            Assert.Equal("Close", host.CloseButtonText);
            Assert.Equal(DefaultButton.Close, host.DefaultButton);
            Assert.Null(host.PrimaryButtonText);
            Assert.Null(host.SecondaryButtonText);

            AboutView view = Assert.IsType<AboutView>(host.Content);
            Assert.IsType<AboutViewModel>(view.DataContext);

            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await showing.WaitAsync(TimeSpan.FromSeconds(10));
        });

    [Fact]
    public void TheSettingsPagesAboutButton_OpensIt()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.FooterItems[^1];
            await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is SettingsPageView);
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            SettingsPageView page = (SettingsPageView)harness.ViewModel.Navigation.CurrentPage!;
            Button about = page.FindControl<Button>("OpenAboutButton") ?? throw new InvalidOperationException("No About button.");
            Assert.Equal("About Enigma Launchpad", about.Content);

            // A real click, so what is tested is the binding and not only the command.
            Point middle = about.TranslatePoint(new Point(about.Bounds.Width / 2, about.Bounds.Height / 2), harness.Window)
                ?? throw new InvalidOperationException("The About button is not in the window.");
            harness.Window.MouseDown(middle, MouseButton.Left);
            harness.Window.MouseUp(middle, MouseButton.Left);
            await WindowHarness.WaitUntilAsync(() => harness.Window.HostDialog.IsOpen);

            Assert.IsType<AboutView>(harness.Window.HostDialog.Content);

            await harness.Window.HostDialog.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });

    [Fact]
    public void TheView_ShowsTheProductAndItsBuild_AndNoCredits()
        => fixture.Run(() =>
        {
            AboutView view = new() { DataContext = new AboutViewModel("1.0.0", "9a1b2c3", "Copyright © 2026 Josué Clément") };
            Window window = new() { Content = view, Width = 480, Height = 640 };
            window.Show();

            try
            {
                string[] texts = [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

                Assert.Equal(380, view.Width);
                Assert.Contains("Enigma Launchpad", texts);
                Assert.Contains("1.0.0", texts);
                Assert.Contains("9a1b2c3", texts);
                Assert.Contains("Copyright © 2026 Josué Clément", texts);
                Assert.Empty(view.GetLogicalDescendants().OfType<ItemsControl>());

                Image icon = Assert.Single(view.GetLogicalDescendants().OfType<Image>());
                Assert.NotNull(icon.Source);
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        });

    [Fact]
    public void TheView_HidesTheBuildLineOfABuildWithNoRevision_AndAnEmptyCopyright()
        => fixture.Run(() =>
        {
            AboutView view = new() { DataContext = new AboutViewModel("1.0.0", null, string.Empty) };
            Window window = new() { Content = view };
            window.Show();

            try
            {
                StackPanel buildLine = view.GetLogicalDescendants().OfType<StackPanel>().Single(panel => panel.Name == "BuildLine");
                TextBlock copyright = view.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == "Copyright");

                Assert.False(buildLine.IsVisible);
                Assert.False(copyright.IsVisible);
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        });

    [Fact]
    public void InTheMainWindow_TheDialogShowsTheViewWholeWithoutScrolling_AndCloseUnderIt()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            ContentDialog host = harness.Window.HostDialog;

            Task showing = harness.Provider.GetRequiredService<IAboutDialogService>().ShowAsync();
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            AboutView view = Assert.IsType<AboutView>(host.Content);
            Visual[] shown = [.. host.GetVisualDescendants()];
            Assert.Contains(view, shown);
            Assert.Contains(shown.OfType<TextBlock>(), text => text.Text == "Enigma Launchpad");

            // The content row of the library's card scrolls once the card reaches its maximum height; an About box
            // that needs scrolling is one nobody reads to the end.
            ScrollViewer content = shown.OfType<ScrollViewer>().First();
            Assert.True(
                content.Extent.Height <= content.Viewport.Height + 0.5,
                $"the About dialog scrolls: {content.Extent.Height} of content in {content.Viewport.Height}");

            // The one button, under the view, on its right.
            Button close = shown.OfType<Button>().Single(button => button.IsEffectivelyVisible && Equals(button.Content, "Close"));
            Rect viewBounds = new(view.TranslatePoint(default, harness.Window) ?? default, view.Bounds.Size);
            Rect closeBounds = new(close.TranslatePoint(default, harness.Window) ?? default, close.Bounds.Size);

            Assert.True(closeBounds.Top >= viewBounds.Bottom, "the Close button is not under the view");
            Assert.True(closeBounds.Right >= viewBounds.Center.X, "the Close button is not on the right");

            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await showing.WaitAsync(TimeSpan.FromSeconds(10));
        });
}
