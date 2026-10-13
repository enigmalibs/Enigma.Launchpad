using System;
using Enigma.Launchpad.Core.Exchange;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Core.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Enigma.Launchpad.Core.DependencyInjection;

/// <summary>
/// Registers the services of the Launchpad core library.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers validation, persistence, settings, export / import and launching. Every service is a stateless
        /// or internally synchronised singleton.
        /// </summary>
        /// <returns>The same collection, so calls can be chained.</returns>
        public IServiceCollection AddLaunchpadCore()
        {
            ArgumentNullException.ThrowIfNull(services);

            // Tried, so a test (or the host) can put its own clock in first.
            services.TryAddSingleton(TimeProvider.System);

            // A factory, so the container never has to choose between the two constructors.
            services.AddSingleton<IAppPaths>(_ => new AppPaths());
            services.AddSingleton<ILaunchpadStore, JsonLaunchpadStore>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IShortcutValidator, ShortcutValidator>();
            services.AddSingleton<IShortcutExchange, ShortcutExchange>();
            services.AddSingleton<IProcessStarter, ProcessStarter>();
            services.AddSingleton<IShortcutLauncher, ShortcutLauncher>();

            return services;
        }
    }
}
