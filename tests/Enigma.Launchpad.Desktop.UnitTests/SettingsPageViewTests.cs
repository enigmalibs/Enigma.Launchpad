using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Enigma.Avalonia.Desktop.Controls;
using Enigma.Avalonia.Desktop.Controls.Navigation;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class SettingsPageViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public SettingsPageViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheRail_EndsWithSettings()
        => _fixture.Run(() =>
        {
            using WindowHarness harness = new();

            NavigationItem settings = harness.ViewModel.Navigation.FooterItems[^1];
            Assert.Equal("Settings", settings.Header);
            Assert.Equal(typeof(SettingsPageView), settings.PageType);
            Assert.Equal(typeof(SettingsPageViewModel), settings.PageViewModelType);
        });

    [Fact]
    public void SelectingSettings_ShowsItsCards()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            SettingsPageView view = await OpenSettingsAsync(harness);

            string?[] headers = view.GetVisualDescendants().OfType<SettingsCard>().Select(card => card.Header).ToArray();
            Assert.Equal(["Theme", "Minimize after opening a shortcut", "Data folder", "Enigma Launchpad"], headers);
            Assert.StartsWith("Version ", view.FindControl<TextBlock>("VersionText")?.Text, StringComparison.Ordinal);
        });

    [Fact]
    public void ChoosingATheme_InTheList_ReThemesTheApplication_AndIsSaved()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            SettingsPageView view = await OpenSettingsAsync(harness);
            ComboBox themes = view.FindControl<ComboBox>("ThemeChoice") ?? throw new InvalidOperationException("No theme list.");

            themes.SelectedIndex = 1;
            Assert.Equal(ThemeVariant.Light, Application.Current?.RequestedThemeVariant);

            themes.SelectedIndex = 2;
            Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);

            ISettingsService settings = harness.Provider.GetRequiredService<ISettingsService>();
            Assert.Equal(ThemePreference.Dark, settings.Current.Theme);
        });

    [Fact]
    public void TheMinimizeSwitch_FollowsTheSetting()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new();
            SettingsPageView view = await OpenSettingsAsync(harness);
            ToggleSwitch toggle = view.FindControl<ToggleSwitch>("MinimizeSwitch") ?? throw new InvalidOperationException("No switch.");

            Assert.False(toggle.IsChecked);

            toggle.IsChecked = true;

            Assert.True(harness.Provider.GetRequiredService<ISettingsService>().Current.MinimizeAfterLaunch);
        });

    private static async Task<SettingsPageView> OpenSettingsAsync(WindowHarness harness)
    {
        harness.ViewModel.Navigation.SelectedItem = harness.ViewModel.Navigation.FooterItems[^1];
        await WindowHarness.WaitUntilAsync(() => harness.ViewModel.Navigation.CurrentPage is SettingsPageView);

        await Task.Delay(20);
        harness.Window.UpdateLayout();

        return (SettingsPageView)(harness.ViewModel.Navigation.CurrentPage ?? throw new InvalidOperationException("No page."));
    }
}
