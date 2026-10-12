using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Launchpad.Core.Settings;

/// <summary>
/// Carries the settings as they now are.
/// </summary>
public sealed class SettingsChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="settings">The new settings.</param>
    public SettingsChangedEventArgs(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
    }

    /// <summary>
    /// Gets the new settings.
    /// </summary>
    public AppSettings Settings { get; }
}

/// <summary>
/// The user's preferences, and the file they live in.
/// </summary>
/// <remarks>
/// Every consumer reads <see cref="Current"/> and listens to <see cref="Changed"/>, so a preference takes
/// effect where it is used rather than being passed along by the page that shows it.
/// </remarks>
public interface ISettingsService
{
    /// <summary>
    /// Gets the path of the settings file.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the settings as they now are; the defaults until <see cref="LoadAsync"/> has run.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Raised after <see cref="Update"/> changed the settings, on the thread that changed them. Loading does
    /// not raise it: whoever loads applies what was read.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>
    /// Reads the settings from disk. A missing file gives the defaults; an unreadable one is moved aside first.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes once <see cref="Current"/> holds what was read.</returns>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the settings and saves them in the background. Nothing happens when nothing changed.
    /// </summary>
    /// <param name="change">Produces the new settings from the current ones.</param>
    /// <returns>The settings now in effect.</returns>
    AppSettings Update(Func<AppSettings, AppSettings> change);

    /// <summary>
    /// Waits for every change made so far to be written.
    /// </summary>
    /// <returns>A task that completes once the last pending save has finished, successfully or not.</returns>
    Task FlushAsync();
}
