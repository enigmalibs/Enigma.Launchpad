using System;
using System.IO;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// Guards the composition root. A missing registration or a captive dependency is invisible until
/// the page that needs it is opened, which is exactly the kind of failure a build should catch.
/// </summary>
public sealed class CompositionRootTests
{
    /// <summary>
    /// Builds the application's container, with the user's data redirected so a test never reads or
    /// writes the developer's own launchpad.
    /// </summary>
    /// <param name="dataDirectory">Where the launchpad file lives; a fresh temporary path when omitted.</param>
    /// <param name="replace">Registers test doubles over the application's own services, when given.</param>
    internal static ServiceProvider BuildProvider(string? dataDirectory = null, Action<IServiceCollection>? replace = null)
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        App.ConfigureServices(services);

        string root = dataDirectory ?? Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tests", Guid.NewGuid().ToString("N"));
        services.AddSingleton<IAppPaths>(new AppPaths(root));
        replace?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            // Proves every registration can have a constructor selected, and that no singleton
            // depends on something shorter-lived than itself.
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void Container_ValidatesOnBuild()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.NotNull(provider);
    }

    [Theory]
    [InlineData(typeof(INavigationService))]
    [InlineData(typeof(IContentDialogService))]
    [InlineData(typeof(IOverlayService))]
    [InlineData(typeof(IInfoBarService))]
    [InlineData(typeof(IFileDialogService))]
    [InlineData(typeof(IFolderDialogService))]
    public void Container_ResolvesEveryEnigmaDesktopService(Type serviceType)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Theory]
    [InlineData(typeof(ApplicationsPageViewModel))]
    [InlineData(typeof(DirectoriesPageViewModel))]
    [InlineData(typeof(UrlsPageViewModel))]
    public void Container_ResolvesEveryPageViewModel(Type viewModelType)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsAssignableFrom<CategoryPageViewModel>(provider.GetRequiredService(viewModelType));
    }

    [Theory]
    [InlineData(typeof(ILaunchpadSession))]
    [InlineData(typeof(ILaunchpadDialogs))]
    [InlineData(typeof(IUserNotifications))]
    [InlineData(typeof(IPathPicker))]
    [InlineData(typeof(IClipboardService))]
    [InlineData(typeof(IShortcutActions))]
    [InlineData(typeof(IProgramIcons))]
    [InlineData(typeof(IWebsiteIconFetcher))]
    [InlineData(typeof(IWebsiteIcons))]
    [InlineData(typeof(ISettingsService))]
    [InlineData(typeof(ThemeApplier))]
    [InlineData(typeof(IMainWindowService))]
    [InlineData(typeof(SettingsPageViewModel))]
    [InlineData(typeof(ProfileSwitcherViewModel))]
    [InlineData(typeof(ProfilesPageViewModel))]
    public void Container_ResolvesEveryApplicationService(Type serviceType)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Fact]
    public void Container_SharesOneProgramIconCache()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IProgramIcons>(), provider.GetRequiredService<IProgramIcons>());
    }

    [Fact]
    public void Container_RedirectsTheDataDirectoryInTests()
    {
        using TempDirectory temp = new();
        using ServiceProvider provider = BuildProvider(temp.Path);

        Assert.Equal(temp.Path, Path.GetDirectoryName(provider.GetRequiredService<ILaunchpadStore>().FilePath));
        Assert.Equal(temp.Path, Path.GetDirectoryName(provider.GetRequiredService<ISettingsService>().FilePath));
    }
}
