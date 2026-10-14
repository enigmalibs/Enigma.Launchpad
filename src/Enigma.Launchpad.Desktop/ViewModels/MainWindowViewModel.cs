using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.Navigation;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Desktop.Navigation;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Enigma.Launchpad.Desktop.Views.Pages;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.ViewModels;

/// <summary>
/// The main window: the navigation rail over the workspaces, the profiles and the settings, the page it shows, and the
/// profile list above it.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly NavigationItem _profilesItem;

    /// <summary>
    /// Initialises a new instance and builds the rail, selecting the workspaces.
    /// </summary>
    /// <param name="navigation">The navigation service the rail is bound to.</param>
    /// <param name="pageFactory">Builds pages out of the container.</param>
    /// <param name="profileSwitcher">The profile list at the top of the window.</param>
    /// <param name="logger">Where navigation failures are reported.</param>
    public MainWindowViewModel(
        INavigationService navigation,
        ContainerPageFactory pageFactory,
        ProfileSwitcherViewModel profileSwitcher,
        ILogger<MainWindowViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(pageFactory);
        ArgumentNullException.ThrowIfNull(profileSwitcher);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        ProfileSwitcher = profileSwitcher;
        Navigation = navigation;
        Navigation.PageFactory = pageFactory.Create;

        // Nothing on the navigation service throws: a page that cannot be built is reported here, and
        // not subscribing would make such a failure invisible.
        Navigation.NavigationFailed += OnNavigationFailed;

        Navigation.Items.Add(Item("Workspaces", PhosphorIcon.SquaresFour, typeof(WorkspacesPageView), typeof(WorkspacesPageViewModel)));

        _profilesItem = Item("Profiles", PhosphorIcon.UsersThree, typeof(ProfilesPageView), typeof(ProfilesPageViewModel));
        Navigation.FooterItems.Add(_profilesItem);
        Navigation.FooterItems.Add(Item("Settings", PhosphorIcon.Gear, typeof(SettingsPageView), typeof(SettingsPageViewModel)));

        ManageProfilesCommand = new RelayCommand(OnManageProfiles);

        Navigation.SelectedItem = Navigation.Items[0];
    }

    /// <summary>
    /// Gets the navigation service the rail and the page area are bound to.
    /// </summary>
    public INavigationService Navigation { get; }

    /// <summary>
    /// Gets the profile list at the top of the window.
    /// </summary>
    public ProfileSwitcherViewModel ProfileSwitcher { get; }

    /// <summary>
    /// Gets the command opening the Profiles page, from beside the profile list.
    /// </summary>
    public RelayCommand ManageProfilesCommand { get; }

    private static NavigationItem Item(string header, PhosphorIcon icon, Type pageType, Type viewModelType)
        => new()
        {
            Header = header,
            IconData = PhosphorIconSet.Instance.GetGlyph(icon, PhosphorWeight.Regular).ToGeometry(),
            PageType = pageType,
            PageViewModelType = viewModelType,
        };

    private void OnManageProfiles() => Navigation.SelectedItem = _profilesItem;

    private void OnNavigationFailed(object? sender, NavigationFailedEventArgs e)
        => _logger.LogError(e.Exception, "Navigation failed in phase {Phase}", e.Phase);
}
