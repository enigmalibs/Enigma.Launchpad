using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Settings;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// What can be done with a shortcut outside the launchpad: open it, show it in its folder, copy its target.
/// Failures are reported to the user here, so callers do not each word them.
/// </summary>
public interface IShortcutActions
{
    /// <summary>
    /// Opens a shortcut; a failure is shown to the user.
    /// </summary>
    /// <param name="kind">The shortcut's kind.</param>
    /// <param name="shortcut">The shortcut.</param>
    /// <returns><see langword="true"/> when it was opened.</returns>
    bool Open(ShortcutKind kind, Shortcut shortcut);

    /// <summary>
    /// Shows an application or a directory in the file manager; a failure is shown to the user.
    /// </summary>
    /// <param name="kind">The shortcut's kind.</param>
    /// <param name="shortcut">The shortcut.</param>
    /// <returns><see langword="true"/> when the file manager was opened.</returns>
    bool Reveal(ShortcutKind kind, Shortcut shortcut);

    /// <summary>
    /// Opens a terminal in a directory shortcut's folder; a failure is shown to the user.
    /// </summary>
    /// <param name="shortcut">The directory shortcut.</param>
    /// <returns><see langword="true"/> when a terminal was opened.</returns>
    bool OpenInTerminal(Shortcut shortcut);

    /// <summary>
    /// Copies a shortcut's target to the clipboard.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <returns>A task that completes once the clipboard has it.</returns>
    Task CopyTargetAsync(Shortcut shortcut);

    /// <summary>
    /// Opens a folder of the application's own — not a shortcut — in the file manager; a failure is shown to
    /// the user. The window stays where it is.
    /// </summary>
    /// <param name="path">The folder.</param>
    /// <returns><see langword="true"/> when the file manager was opened.</returns>
    bool OpenFolder(string path);
}

/// <summary>
/// Default <see cref="IShortcutActions"/>.
/// </summary>
public sealed class ShortcutActions : IShortcutActions
{
    private readonly IShortcutLauncher _launcher;
    private readonly IUserNotifications _notifications;
    private readonly IClipboardService _clipboard;
    private readonly ISettingsService _settings;
    private readonly IMainWindowService _window;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="launcher">Opens shortcuts.</param>
    /// <param name="notifications">Where failures are shown.</param>
    /// <param name="clipboard">The system clipboard.</param>
    /// <param name="settings">Says whether the window steps aside once a shortcut is open.</param>
    /// <param name="window">The window that steps aside.</param>
    public ShortcutActions(
        IShortcutLauncher launcher,
        IUserNotifications notifications,
        IClipboardService clipboard,
        ISettingsService settings,
        IMainWindowService window)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(window);

        _launcher = launcher;
        _notifications = notifications;
        _clipboard = clipboard;
        _settings = settings;
        _window = window;
    }

    /// <inheritdoc />
    public bool Open(ShortcutKind kind, Shortcut shortcut) => StepAsideAfter(_launcher.Launch(kind, shortcut));

    /// <inheritdoc />
    public bool Reveal(ShortcutKind kind, Shortcut shortcut) => Report(_launcher.Reveal(kind, shortcut));

    /// <inheritdoc />
    public bool OpenInTerminal(Shortcut shortcut) => StepAsideAfter(_launcher.OpenInTerminal(shortcut));

    /// <inheritdoc />
    public Task CopyTargetAsync(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        return _clipboard.SetTextAsync(shortcut.Target);
    }

    /// <inheritdoc />
    public bool OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Opened as a directory shortcut would be, so the launcher's checks and its failure messages apply.
        return Report(_launcher.Launch(ShortcutKind.Directory, new Shortcut(Guid.Empty, Path.GetFileName(path), path)));
    }

    // Something was opened outside the launchpad, as a click on a tile does: the window steps aside if asked to.
    private bool StepAsideAfter(LaunchResult result)
    {
        if (!Report(result))
        {
            return false;
        }

        if (_settings.Current.MinimizeAfterLaunch)
        {
            _window.Minimize();
        }

        return true;
    }

    private bool Report(LaunchResult result)
    {
        if (result.Succeeded)
        {
            return true;
        }

        string title = result.Outcome switch
        {
            LaunchOutcome.TargetMissing => "Shortcut target not found",
            LaunchOutcome.Refused => "This shortcut cannot be opened",
            _ => "The shortcut could not be opened",
        };

        _notifications.ShowError(title, result.Message ?? string.Empty);
        return false;
    }
}
