using System;
using Enigma.Launchpad.Core.DependencyInjection;
using Enigma.Launchpad.Core.Exchange;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLaunchpadCore_ReturnsTheSameCollection()
    {
        ServiceCollection services = new();

        Assert.Same(services, services.AddLaunchpadCore());
    }

    [Theory]
    [InlineData(typeof(TimeProvider))]
    [InlineData(typeof(IAppPaths))]
    [InlineData(typeof(ILaunchpadStore))]
    [InlineData(typeof(ISettingsService))]
    [InlineData(typeof(IShortcutValidator))]
    [InlineData(typeof(IShortcutExchange))]
    [InlineData(typeof(IProcessStarter))]
    [InlineData(typeof(IShortcutLauncher))]
    public void AddLaunchpadCore_RegistersEveryService(Type serviceType)
    {
        using ServiceProvider provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    [Fact]
    public void AddLaunchpadCore_KeepsAClockRegisteredBeforeIt()
    {
        TimeProvider clock = new Infrastructure.FixedTimeProvider(DateTimeOffset.UnixEpoch);
        ServiceCollection services = new();
        services.AddSingleton(clock);
        services.AddLaunchpadCore();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddLaunchpadCore();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
