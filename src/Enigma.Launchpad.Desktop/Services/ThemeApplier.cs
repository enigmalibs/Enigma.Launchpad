using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Enigma.Launchpad.Core.Settings;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Puts the theme the user chose on the application, and keeps it there as the preference — or, for
/// "System", the operating system — changes.
/// </summary>
/// <remarks>
/// "System" is resolved here to Light or Dark rather than handed to Avalonia as <see cref="ThemeVariant.Default"/>:
/// Enigma.Avalonia.Desktop defines Dark and Light theme dictionaries only, and on a host whose platform theme
/// cannot be read the default variant would leave every <c>Enigma*</c> brush unresolved.
/// </remarks>
public sealed class ThemeApplier : IDisposable
{
    private readonly ISettingsService _settings;

    private Application? _application;
    private IPlatformSettings? _platform;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="settings">Where the preference is read, and announced when it changes.</param>
    public ThemeApplier(ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <summary>
    /// Applies the current preference to an application and follows it from then on. Must run on the UI thread.
    /// </summary>
    /// <param name="application">The application to theme.</param>
    public void Attach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null)
        {
            throw new InvalidOperationException("The theme is already applied to an application.");
        }

        _application = application;
        _platform = application.PlatformSettings;

        _settings.Changed += OnSettingsChanged;

        if (_platform is not null)
        {
            _platform.ColorValuesChanged += OnPlatformColorsChanged;
        }

        Apply();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;

        if (_platform is not null)
        {
            _platform.ColorValuesChanged -= OnPlatformColorsChanged;
        }

        _platform = null;
        _application = null;
    }

    /// <summary>
    /// Says which variant a preference stands for.
    /// </summary>
    /// <param name="preference">What the user chose.</param>
    /// <param name="platform">The operating system's variant; <see langword="null"/> when it cannot be read.</param>
    /// <returns>Light or Dark — never the default variant.</returns>
    internal static ThemeVariant Resolve(ThemePreference preference, PlatformThemeVariant? platform)
        => preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,

            // Dark when the system cannot say: it is the variant the application has always started in.
            _ => platform == PlatformThemeVariant.Light ? ThemeVariant.Light : ThemeVariant.Dark,
        };

    private void Apply()
    {
        if (_application is not { } application)
        {
            return;
        }

        PlatformThemeVariant? platform = _platform?.GetColorValues().ThemeVariant;
        application.RequestedThemeVariant = Resolve(_settings.Current.Theme, platform);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) => Apply();

    private void OnPlatformColorsChanged(object? sender, PlatformColorValues e)
    {
        if (_settings.Current.Theme == ThemePreference.System)
        {
            Apply();
        }
    }
}
