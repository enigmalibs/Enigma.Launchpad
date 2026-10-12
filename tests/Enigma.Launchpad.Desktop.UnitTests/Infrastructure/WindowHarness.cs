using System;
using System.Threading.Tasks;
using Avalonia;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// The real main window, built from the real container over a temporary data directory, shown headlessly.
/// Must be created and disposed on the headless UI thread.
/// </summary>
/// <remarks>
/// Website icons come from an offline fetcher unless a test replaces it: no test may depend on the network.
/// </remarks>
internal sealed class WindowHarness : IDisposable
{
    private readonly TempDirectory _data = new();

    public WindowHarness(LaunchpadData? data = null, Action<IServiceCollection>? replace = null)
    {
        if (data is not null)
        {
            // The store awaits with ConfigureAwait(false) throughout, so blocking on it here cannot deadlock.
            new JsonLaunchpadStore(new AppPaths(_data.Path), TimeProvider.System, NullLogger<JsonLaunchpadStore>.Instance)
                .SaveAsync(data)
                .GetAwaiter()
                .GetResult();
        }

        Provider = CompositionRootTests.BuildProvider(_data.Path, services =>
        {
            services.AddSingleton<IWebsiteIconFetcher>(new FakeWebsiteIconFetcher());
            replace?.Invoke(services);
        });
        App.LoadSettings(Provider, Application.Current ?? throw new InvalidOperationException("No application."));
        Window = Provider.GetRequiredService<MainWindow>();
        App.RegisterHosts(Provider, Window);
        App.LoadLaunchpad(Provider);
        ViewModel = Provider.GetRequiredService<MainWindowViewModel>();
        Window.DataContext = ViewModel;
        Window.Show();
    }

    public ServiceProvider Provider { get; }

    public MainWindow Window { get; }

    public MainWindowViewModel ViewModel { get; }

    public string DataDirectory => _data.Path;

    /// <summary>
    /// Waits, letting the dispatcher run, until a condition holds.
    /// </summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold in time.");
            }

            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Waits for the current page to have loaded its category, and for its bindings to have been applied.
    /// </summary>
    public async Task<CategoryPageView> LoadedPageAsync()
    {
        await WaitUntilAsync(() => ViewModel.Navigation.CurrentPage is CategoryPageView { DataContext: CategoryPageViewModel { IsLoaded: true } });

        // One more turn of the dispatcher, so the bindings the load changed have been applied.
        await Task.Delay(20);
        Window.UpdateLayout();

        return (CategoryPageView)(ViewModel.Navigation.CurrentPage ?? throw new InvalidOperationException("No page."));
    }

    public void Dispose()
    {
        Window.Close();

        // Every pending save lands before the data directory goes.
        Provider.GetRequiredService<ILaunchpadSession>().FlushAsync().GetAwaiter().GetResult();
        Provider.GetRequiredService<ISettingsService>().FlushAsync().GetAwaiter().GetResult();
        Provider.Dispose();
        _data.Dispose();
    }
}
