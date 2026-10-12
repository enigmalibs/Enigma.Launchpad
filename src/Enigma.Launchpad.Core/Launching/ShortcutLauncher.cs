using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Core.Launching;

/// <summary>
/// Opens shortcuts.
/// </summary>
public interface IShortcutLauncher
{
    /// <summary>
    /// Opens a shortcut: starts the application, opens the directory in the file manager, or opens the
    /// address in the browser.
    /// </summary>
    /// <param name="kind">The shortcut's kind.</param>
    /// <param name="shortcut">The shortcut.</param>
    /// <returns>How it ended.</returns>
    LaunchResult Launch(ShortcutKind kind, Shortcut shortcut);

    /// <summary>
    /// Shows an application or a directory in the platform's file manager, selecting it where the
    /// platform can.
    /// </summary>
    /// <param name="kind">The shortcut's kind; a URL has nothing to show.</param>
    /// <param name="shortcut">The shortcut.</param>
    /// <returns>How it ended.</returns>
    LaunchResult Reveal(ShortcutKind kind, Shortcut shortcut);
}

/// <summary>
/// The desktops "show in folder" is done differently on.
/// </summary>
internal enum DesktopPlatform
{
    /// <summary>Windows: Explorer, with the item selected.</summary>
    Windows,

    /// <summary>macOS: Finder, with the item selected.</summary>
    MacOS,

    /// <summary>Linux and the other Unix desktops: the containing directory, opened.</summary>
    Linux,
}

/// <summary>
/// Default <see cref="IShortcutLauncher"/>.
/// </summary>
/// <remarks>
/// Everything is started through the shell (<see cref="ProcessStartInfo.UseShellExecute"/>), which is what
/// makes one call work on all three platforms — ShellExecute on Windows, <c>open</c> on macOS, the
/// freedesktop handler on Linux — and what lets an application shortcut point at a document or a
/// <c>.lnk</c> as well as at an executable. An address is re-checked before it is opened, so a document
/// edited by hand cannot turn a URL shortcut into a launcher for another scheme.
/// </remarks>
public sealed class ShortcutLauncher : IShortcutLauncher
{
    private readonly IProcessStarter _starter;
    private readonly ILogger<ShortcutLauncher> _logger;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="starter">Starts the processes.</param>
    /// <param name="logger">Where failures are reported.</param>
    public ShortcutLauncher(IProcessStarter starter, ILogger<ShortcutLauncher> logger)
    {
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentNullException.ThrowIfNull(logger);

        _starter = starter;
        _logger = logger;
    }

    /// <inheritdoc />
    public LaunchResult Launch(ShortcutKind kind, Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        LaunchResult? refusal = CheckTarget(kind, shortcut.Target);

        return refusal ?? Start(BuildLaunchStartInfo(kind, shortcut), shortcut);
    }

    /// <inheritdoc />
    public LaunchResult Reveal(ShortcutKind kind, Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        if (kind == ShortcutKind.Url)
        {
            return new LaunchResult(LaunchOutcome.Refused, "A web address has no folder to show.");
        }

        LaunchResult? refusal = CheckTarget(kind, shortcut.Target);

        return refusal ?? Start(BuildRevealStartInfo(CurrentPlatform, shortcut.Target), shortcut);
    }

    /// <summary>
    /// How a shortcut is opened.
    /// </summary>
    /// <param name="kind">The shortcut's kind.</param>
    /// <param name="shortcut">The shortcut, whose target has already been checked.</param>
    /// <returns>The start information.</returns>
    internal static ProcessStartInfo BuildLaunchStartInfo(ShortcutKind kind, Shortcut shortcut)
    {
        switch (kind)
        {
            case ShortcutKind.Application:
                ProcessStartInfo application = new(shortcut.Target)
                {
                    UseShellExecute = true,

                    // Programs that load files beside themselves expect to start where they live.
                    WorkingDirectory = Path.GetDirectoryName(shortcut.Target.TrimEnd('/', '\\')) ?? string.Empty,
                };

                if (!string.IsNullOrWhiteSpace(shortcut.Arguments))
                {
                    application.Arguments = shortcut.Arguments.Trim();
                }

                return application;

            case ShortcutKind.Directory:
                return new ProcessStartInfo(shortcut.Target) { UseShellExecute = true };

            case ShortcutKind.Url:
                UrlNormalizer.TryParse(shortcut.Target, out Uri? address, out _);
                return new ProcessStartInfo(address?.AbsoluteUri ?? shortcut.Target) { UseShellExecute = true };

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown shortcut kind.");
        }
    }

    /// <summary>
    /// How a path is shown in the platform's file manager.
    /// </summary>
    /// <param name="platform">The desktop the application runs on.</param>
    /// <param name="path">The file or directory to show.</param>
    /// <returns>The start information; the path always goes in an argument of its own, never a command line.</returns>
    internal static ProcessStartInfo BuildRevealStartInfo(DesktopPlatform platform, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        switch (platform)
        {
            case DesktopPlatform.Windows:
                ProcessStartInfo windows = new("explorer.exe") { UseShellExecute = false };

                // "/select," takes the path as one argument with no space after the comma.
                windows.ArgumentList.Add("/select," + path);
                return windows;

            case DesktopPlatform.MacOS:
                ProcessStartInfo mac = new("open") { UseShellExecute = false };
                mac.ArgumentList.Add("-R");
                mac.ArgumentList.Add(path);
                return mac;

            default:
                // Linux has no portable "select this item" call, so the containing directory is opened.
                string parent = Path.GetDirectoryName(path.TrimEnd('/')) ?? path;
                return new ProcessStartInfo(parent) { UseShellExecute = true };
        }
    }

    private static DesktopPlatform CurrentPlatform
        => OperatingSystem.IsWindows()
            ? DesktopPlatform.Windows
            : OperatingSystem.IsMacOS() ? DesktopPlatform.MacOS : DesktopPlatform.Linux;

    /// <summary>
    /// Whether the target can be opened at all, before anything is started.
    /// </summary>
    private static LaunchResult? CheckTarget(ShortcutKind kind, string target)
        => kind switch
        {
            ShortcutKind.Application when !File.Exists(target) && !ShortcutValidator.IsApplicationBundle(target)
                => new LaunchResult(LaunchOutcome.TargetMissing, $"The program no longer exists: {target}"),
            ShortcutKind.Directory when !Directory.Exists(target)
                => new LaunchResult(LaunchOutcome.TargetMissing, $"The folder no longer exists: {target}"),
            ShortcutKind.Url when !UrlNormalizer.TryParse(target, out _, out string? error)
                => new LaunchResult(LaunchOutcome.Refused, error),
            _ => null,
        };

    private LaunchResult Start(ProcessStartInfo startInfo, Shortcut shortcut)
    {
        try
        {
            _starter.Start(startInfo);
            return LaunchResult.Launched;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            _logger.LogWarning(exception, "Opening the shortcut {Name} failed", shortcut.Name);
            return new LaunchResult(LaunchOutcome.Failed, $"\"{shortcut.Name}\" could not be opened: {exception.Message}");
        }
    }
}
