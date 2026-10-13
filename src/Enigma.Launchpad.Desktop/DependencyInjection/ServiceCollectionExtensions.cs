using System;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Desktop.Navigation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Enigma.Launchpad.Desktop.DependencyInjection;

/// <summary>
/// Composition root helpers for the desktop application.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the six Enigma.Avalonia.Desktop services. The library ships no registration
        /// extension of its own, so this is it.
        /// </summary>
        /// <returns>The same collection, so calls can be chained.</returns>
        public IServiceCollection AddEnigmaDesktopServices()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();
            services.AddSingleton<IOverlayService, OverlayService>();
            services.AddSingleton<IInfoBarService, InfoBarService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<IFolderDialogService, FolderDialogService>();

            return services;
        }

        /// <summary>
        /// Registers the window, the pages and their ViewModels.
        /// </summary>
        /// <remarks>
        /// Views are transient and ViewModels singletons on purpose: navigating back to a page builds a
        /// fresh control but re-attaches the ViewModel it had, so the page keeps its state while the
        /// visual tree does not leak.
        /// </remarks>
        /// <returns>The same collection, so calls can be chained.</returns>
        public IServiceCollection AddLaunchpadApp()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ContainerPageFactory>();
            services.AddSingleton<IUserNotifications, InfoBarNotifications>();
            services.AddSingleton<ILaunchpadDialogs, LaunchpadDialogs>();
            services.AddSingleton<IPathPicker, PathPicker>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddSingleton<IMainWindowService, MainWindowService>();
            services.AddSingleton<IShortcutActions, ShortcutActions>();
            services.AddSingleton<IProgramIconReader, ShellProgramIconReader>();
            services.AddSingleton<IProgramIcons, ProgramIcons>();
            services.AddSingleton<IWebsiteIconFetcher, WebsiteIconFetcher>();
            services.AddSingleton<IWebsiteIconStore, WebsiteIconStore>();
            services.AddSingleton<IWebsiteIcons, WebsiteIcons>();
            services.AddSingleton<IShortcutIcons, ShortcutIcons>();
            services.AddSingleton<ILaunchpadSession, LaunchpadSession>();
            services.AddSingleton<ThemeApplier>();

            services.AddTransient<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<ProfileSwitcherViewModel>();

            services.AddTransient<CategoryPageView>();
            services.AddSingleton<ApplicationsPageViewModel>();
            services.AddSingleton<DirectoriesPageViewModel>();
            services.AddSingleton<UrlsPageViewModel>();

            services.AddTransient<ProfilesPageView>();
            services.AddSingleton<ProfilesPageViewModel>();

            services.AddTransient<SettingsPageView>();
            services.AddSingleton<SettingsPageViewModel>();

            return services;
        }
    }
}
