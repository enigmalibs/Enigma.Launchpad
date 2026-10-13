using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class ProfilesPageViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public ProfilesPageViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheRailFooter_HoldsProfiles_ThenSettings()
        => _fixture.Run(() =>
        {
            using WindowHarness harness = new();

            Assert.Equal(["Profiles", "Settings"], harness.ViewModel.Navigation.FooterItems.Select(item => item.Header));
            Assert.Equal(typeof(ProfilesPageView), harness.ViewModel.Navigation.FooterItems[0].PageType);
            Assert.Equal(typeof(ProfilesPageViewModel), harness.ViewModel.Navigation.FooterItems[0].PageViewModelType);
        });

    [Fact]
    public void TheManageButton_OpensTheProfilesPage_WithACardPerProfile()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.WorkAndHome());
            await harness.LoadedPageAsync();
            Button manage = harness.Window.FindControl<Button>("ManageProfilesButton") ?? throw new InvalidOperationException("No manage button.");

            manage.Command?.Execute(manage.CommandParameter);
            await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is ProfilesPageView { DataContext: ProfilesPageViewModel { Profiles.Count: 2 } });
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            ProfilesPageView view = (ProfilesPageView)(harness.ViewModel.Navigation.CurrentPage ?? throw new InvalidOperationException("No page."));
            Assert.Same(harness.ViewModel.Navigation.FooterItems[0], harness.ViewModel.Navigation.SelectedItem);

            Border[] cards = view.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("profileCard")).ToArray();
            Assert.Equal(2, cards.Length);
            Assert.Contains("active", cards[0].Classes);
            Assert.DoesNotContain("active", cards[1].Classes);

            Border[] badges = view.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("activeBadge")).ToArray();
            Assert.Equal([true, false], badges.Select(badge => badge.IsVisible));
        });

    [Fact]
    public void ACardsMenu_OffersExportAndImport_BetweenTheCopyAndTheMoves()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.WorkAndHome());
            await harness.LoadedPageAsync();
            harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.FooterItems[0];
            await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is ProfilesPageView { DataContext: ProfilesPageViewModel { Profiles.Count: 2 } });
            await Task.Delay(20);
            harness.Window.UpdateLayout();

            ProfilesPageView view = (ProfilesPageView)(harness.ViewModel.Navigation.CurrentPage ?? throw new InvalidOperationException("No page."));
            Button actions = view.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetName(button) == "Profile actions");
            MenuFlyout menu = Assert.IsType<MenuFlyout>(actions.Flyout);

            string[] entries = menu.Items.Select(item => item is MenuItem entry ? entry.Header?.ToString() ?? string.Empty : "—").ToArray();
            Assert.Equal(["Rename…", "Duplicate…", "—", "Export…", "Import…", "—", "Move up", "Move down", "—", "Delete…"], entries);
        });
}
