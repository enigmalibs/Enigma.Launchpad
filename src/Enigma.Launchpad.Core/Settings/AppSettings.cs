using System;

namespace Enigma.Launchpad.Core.Settings;

/// <summary>
/// The user's preferences. Every property has a default, so a file written by an older build — or by hand,
/// missing a field — still reads.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// The format version this build reads and writes.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Gets the preferences of a user who has not chosen anything yet.
    /// </summary>
    public static AppSettings Defaults { get; } = new();

    /// <summary>
    /// Gets the format version of the settings; <see cref="CurrentVersion"/> when written.
    /// </summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// Gets the colours the application is shown in.
    /// </summary>
    public ThemePreference Theme { get; init; } = ThemePreference.System;

    /// <summary>
    /// Gets a value indicating whether the window is minimized once a shortcut has been opened, so the
    /// launched application comes to the front.
    /// </summary>
    public bool MinimizeAfterLaunch { get; init; }

    /// <summary>
    /// Returns these settings with every value this build cannot act on put back to its default.
    /// </summary>
    /// <returns>The settings, usable as they are.</returns>
    /// <remarks>
    /// The serializer accepts a number for an enum, so a hand-edited <c>"theme": 7</c> reads without an error;
    /// it is turned back into the default here rather than reaching the theme code as an unknown value.
    /// </remarks>
    public AppSettings Normalised()
        => Enum.IsDefined(Theme) ? this : this with { Theme = Defaults.Theme };
}
