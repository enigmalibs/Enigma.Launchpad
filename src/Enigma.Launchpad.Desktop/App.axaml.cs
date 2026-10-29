using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Core.DependencyInjection;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Desktop.DependencyInjection;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop;

/// <summary>
/// The Avalonia <see cref="Application"/> for Enigma.Launchpad, and the composition root.
/// </summary>
/// <remarks>
/// <para>
/// The host is <em>started</em>, never run: Avalonia's classic desktop lifetime runs the
/// application, and the host only supplies configuration, logging and services.
/// </para>
/// <para>
/// Every <c>using</c> directive in this project sits at file scope, above the namespace declaration:
/// inside a namespace starting with <c>Enigma.</c>, an inline <c>Avalonia.Xxx</c> reference would bind
/// to the <c>Enigma.Avalonia</c> namespace shipped by Enigma.Avalonia.Desktop.
/// </para>
/// </remarks>
public partial class App : Application
{
    private IHost? _host;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // First: a command or a posted callback that throws from here on is caught and shown, not the end of the
            // process. The dispatcher is safe to touch now that the platform is set up; the headless tests never come
            // this way.
            UnhandledExceptionHooks.InstallDispatcher();

            IHost host = BuildHost();
            host.Start();
            _host = host;

            IServiceProvider services = host.Services;

            // The safety net, as soon as it can be built: anything caught before it was is handed over. Its dialog
            // waits for the window's host (RegisterHosts).
            IUnhandledErrorReporter errors = services.GetRequiredService<IUnhandledErrorReporter>();
            UnhandledExceptionHooks.Attach(errors);
            desktop.ShutdownRequested += (_, _) => errors.ShutdownStarted();

            // Before the window: a window that paints dark and then flips to light is a worse first
            // impression than one that starts in the theme the user chose.
            LoadSettings(services, this);

            MainWindow window = services.GetRequiredService<MainWindow>();

            // Hosts first: reading the launchpad may already have something to tell the user. Read before the
            // main ViewModel is built, so the profile list and the first page start on the profile in use.
            RegisterHosts(services, window);
            LoadLaunchpad(services);
            window.DataContext = services.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = window;
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Builds the application host.
    /// </summary>
    /// <returns>The built, unstarted host.</returns>
    internal static IHost BuildHost()
    {
        // A desktop application must not depend on its working directory, so configuration is read
        // from beside the executable.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.SetMinimumLevel(LogLevel.Information);

        ConfigureServices(builder.Services);

        return builder.Build();
    }

    /// <summary>
    /// Registers everything the application needs. Kept apart from <see cref="BuildHost"/> so a test can
    /// validate the whole container without an Avalonia platform behind it.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    internal static void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLaunchpadCore();
        services.AddEnigmaDesktopServices();
        services.AddLaunchpadApp();
    }

    /// <summary>
    /// Reads the user's preferences and puts their theme on the application, which then follows the preference.
    /// Blocks: it runs once, before any window exists, and the settings service never needs the UI thread to
    /// finish a read.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="application">The application to theme.</param>
    internal static void LoadSettings(IServiceProvider services, Application application)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(application);

        services.GetRequiredService<ISettingsService>().LoadAsync().GetAwaiter().GetResult();
        services.GetRequiredService<ThemeApplier>().Attach(application);
    }

    /// <summary>
    /// Reads the launchpad. Blocks, like <see cref="LoadSettings"/>: it runs once, before the main ViewModel is
    /// built, and the session never needs the UI thread to finish a read — what it has to tell the user is posted.
    /// </summary>
    /// <param name="services">The application's services.</param>
    internal static void LoadLaunchpad(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.GetRequiredService<ILaunchpadSession>().LoadAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Hands the window's host controls and storage provider to the Enigma services. Must run before the
    /// window is shown: a service whose host is still unregistered throws the moment a page uses it.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="window">The main window.</param>
    internal static void RegisterHosts(IServiceProvider services, MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(window);

        services.GetRequiredService<IContentDialogService>().RegisterHost(window.HostDialog);
        services.GetRequiredService<IOverlayService>().RegisterHost(window.HostOverlay);
        services.GetRequiredService<IInfoBarService>().RegisterHost(window.HostInfoBar);
        services.GetRequiredService<IFileDialogService>().SetStorageProvider(window.StorageProvider);
        services.GetRequiredService<IFolderDialogService>().SetStorageProvider(window.StorageProvider);

        // The safety net shows its dialog on the same host as every question.
        services.GetRequiredService<IUnhandledErrorReporter>().RegisterHost(window.HostDialog);
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        IHost? host = _host;

        if (host is null)
        {
            return;
        }

        _host = null;

        // Closing: no cancellation is worth a word now, and a late report must not log through the container disposed
        // below.
        host.Services.GetRequiredService<IUnhandledErrorReporter>().ShutdownStarted();
        UnhandledExceptionHooks.Detach();

        // The process is tearing down and no message pump remains, so blocking here is correct. The session and
        // the settings save on the thread pool, never on the UI thread, so waiting for their last saves cannot
        // deadlock.
        host.Services.GetRequiredService<ILaunchpadSession>().FlushAsync().GetAwaiter().GetResult();
        host.Services.GetRequiredService<ISettingsService>().FlushAsync().GetAwaiter().GetResult();
        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();
    }
}
