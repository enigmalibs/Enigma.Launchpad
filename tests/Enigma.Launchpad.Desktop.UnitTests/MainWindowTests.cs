using System.Linq;
using Avalonia.Controls;
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
    public void MainWindow_ShowsTheThreeCategoriesAndOpensTheFirst()
        => _fixture.Run(() =>
        {
            using WindowHarness harness = new();

            Assert.Collection(
                harness.ViewModel.Navigation.Items,
                item => Assert.Equal(typeof(ApplicationsPageViewModel), item.PageViewModelType),
                item => Assert.Equal(typeof(DirectoriesPageViewModel), item.PageViewModelType),
                item => Assert.Equal(typeof(UrlsPageViewModel), item.PageViewModelType));

            NavigationView rail = harness.Window.GetVisualDescendants().OfType<NavigationView>().Single();
            Assert.Equal(3, rail.Items?.Count);

            CategoryPageView page = Assert.IsType<CategoryPageView>(harness.ViewModel.Navigation.CurrentPage);
            Assert.IsType<ApplicationsPageViewModel>(page.DataContext);
        });

    [Fact]
    public void SelectingACategory_OpensItsPage()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();

            harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.Items[2];
            await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage?.DataContext is UrlsPageViewModel);

            Control? page = harness.ViewModel.Navigation.CurrentPage;
            Assert.IsType<UrlsPageViewModel>(Assert.IsType<CategoryPageView>(page).DataContext);
        });
}
