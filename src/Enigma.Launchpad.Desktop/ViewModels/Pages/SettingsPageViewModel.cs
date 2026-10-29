using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Launchpad.Core.Diagnostics;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One choice in the theme list: the preference, and the words the list shows for it.
/// </summary>
/// <param name="Value">The preference.</param>
/// <param name="Label">What the list says: "Follow the system" is what the setting does, "System" what the code calls it.</param>
public sealed record ThemeOption(ThemePreference Value, string Label);

/// <summary>
/// The settings page: the user's preferences, where their data lives, and which version is running.
/// </summary>
/// <remarks>
/// The page holds no copy of the preferences: it reads them from the settings service and writes every change
/// straight back, and it listens to the service so a change made elsewhere shows here too.
/// </remarks>
public sealed class SettingsPageViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IShortcutActions _actions;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="settings">The preferences.</param>
    /// <param name="paths">Where the user's data lives.</param>
    /// <param name="actions">Opens the data folder.</param>
    /// <param name="about">Shows the About dialog from the About card.</param>
    public SettingsPageViewModel(ISettingsService settings, IAppPaths paths, IShortcutActions actions, IAboutDialogService about)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(about);

        _settings = settings;
        _actions = actions;
        DataDirectory = paths.DataDirectory;

        _settings.Changed += OnSettingsChanged;
        OpenDataFolderCommand = new RelayCommand(OnOpenDataFolder);
        OpenAboutCommand = new AsyncRelayCommand(about.ShowAsync);
    }

    /// <summary>
    /// Gets the themes the user can choose from, in the order the list shows them.
    /// </summary>
    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(ThemePreference.System, "Follow the system"),
        new(ThemePreference.Light, "Light"),
        new(ThemePreference.Dark, "Dark"),
    ];

    /// <summary>
    /// Gets or sets the theme in effect; setting it applies and saves it.
    /// </summary>
    public ThemeOption SelectedTheme
    {
        get => Themes.FirstOrDefault(option => option.Value == _settings.Current.Theme) ?? Themes[0];
        set
        {
            // A list being torn down hands its binding a null; that is not a choice.
            if (value is not null)
            {
                _settings.Update(current => current with { Theme = value.Value });
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the window is minimized once a shortcut has been opened.
    /// </summary>
    public bool MinimizeAfterLaunch
    {
        get => _settings.Current.MinimizeAfterLaunch;
        set => _settings.Update(current => current with { MinimizeAfterLaunch = value });
    }

    /// <summary>
    /// Gets the folder holding the user's shortcuts and settings.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Gets the version of the running application, without the source revision the SDK appends.
    /// </summary>
    public string Version => ProductInformation.Version;

    /// <summary>
    /// Gets the command opening the data folder in the file manager.
    /// </summary>
    public RelayCommand OpenDataFolderCommand { get; }

    /// <summary>
    /// Gets the command opening the About dialog.
    /// </summary>
    public AsyncRelayCommand OpenAboutCommand { get; }

    private void OnOpenDataFolder() => _actions.OpenFolder(DataDirectory);

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(MinimizeAfterLaunch));
    }
}
