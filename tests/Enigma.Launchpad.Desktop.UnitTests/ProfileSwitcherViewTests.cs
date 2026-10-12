using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class ProfileSwitcherViewTests
{
    private readonly HeadlessAvaloniaFixture _fixture;

    public ProfileSwitcherViewTests(HeadlessAvaloniaFixture fixture) => _fixture = fixture;

    [Fact]
    public void TheWindow_OpensOnTheProfileInUse()
        => _fixture.RunAsync(async () =>
        {
            LaunchpadData data = PageHarness.WorkAndHome();
            using WindowHarness harness = new(data.WithActiveProfile(data.Profiles[1].Id));
            CategoryPageView page = await harness.LoadedPageAsync();

            ComboBox profiles = ProfileChoice(harness);
            Assert.Equal(2, profiles.ItemCount);
            Assert.Equal("Home", Assert.IsType<ProfileOption>(profiles.SelectedItem).Name);
            Assert.Equal(["Games"], GroupNames(page));
        });

    [Fact]
    public void ChoosingAProfileInTheList_ShowsItsShortcuts_AndIsRemembered()
        => _fixture.RunAsync(async () =>
        {
            using WindowHarness harness = new(PageHarness.WorkAndHome());
            CategoryPageView page = await harness.LoadedPageAsync();
            ComboBox profiles = ProfileChoice(harness);

            profiles.SelectedIndex = 1;
            await WindowHarness.WaitUntilAsync(() => GroupNames(page).SequenceEqual(["Games"]));

            await harness.Provider.GetRequiredService<ILaunchpadSession>().FlushAsync();
            JsonLaunchpadStore store = new(new AppPaths(harness.DataDirectory), TimeProvider.System, NullLogger<JsonLaunchpadStore>.Instance);
            LaunchpadLoadResult reread = await store.LoadAsync();
            Assert.Equal("Home", reread.Data.ActiveProfile.Name);
        });

    private static ComboBox ProfileChoice(WindowHarness harness)
        => harness.Window.FindControl<ComboBox>("ProfileChoice") ?? throw new InvalidOperationException("No profile list.");

    private static string[] GroupNames(CategoryPageView page)
    {
        page.UpdateLayout();

        return page.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Classes.Contains("groupName"))
            .Select(text => text.Text ?? string.Empty)
            .ToArray();
    }
}
