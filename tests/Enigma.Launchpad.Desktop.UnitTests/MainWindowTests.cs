using System.Linq;
using Avalonia.VisualTree;
using Enigma.Avalonia.Desktop.Controls.Navigation;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class MainWindowTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public MainWindowTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void MainWindow_ShowsOnlyTheWorkspaces_AndOpensThem()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();

            NavigationItem item = Assert.Single(harness.ViewModel.Navigation.Items);
            Assert.Equal(("Workspaces", typeof(WorkspacesPageView), typeof(WorkspacesPageViewModel)), (item.Header, item.PageType, item.PageViewModelType));

            NavigationView rail = harness.Window.GetVisualDescendants().OfType<NavigationView>().Single();
            Assert.Equal(1, rail.Items?.Count);
            Assert.Equal(["Profiles", "Settings"], harness.ViewModel.Navigation.FooterItems.Select(footer => footer.Header));

            await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is not null);
            WorkspacesPageView page = Assert.IsType<WorkspacesPageView>(harness.ViewModel.Navigation.CurrentPage);
            Assert.IsType<WorkspacesPageViewModel>(page.DataContext);
        });

    [Fact]
    public void AnOpenWorkspace_StaysOpen_WhileAnotherPageIsShown()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.Data("Dev tools"));
            await harness.LoadedPageAsync();

            harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.FooterItems[1];
            await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is SettingsPageView);
            harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.Items[0];
            await WindowHarness.WaitUntilAsync(() => harness.OpenPage is not null);

            Assert.Equal("Dev tools", Assert.Single(Assert.IsType<WorkspacePageViewModel>(harness.OpenPage!.DataContext).Groups).Name);
        });
}
