using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// Opens a shortcut: starts the application — a program's path, or a command found on the <c>PATH</c> — opens
    /// the directory in the file manager, opens the address in the browser, or opens the document in the application
    /// the system associates with it.
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

    /// <summary>
    /// Opens a terminal in a directory shortcut's folder: Windows Terminal or the Command Prompt on Windows,
    /// Terminal on macOS, the desktop's terminal on Linux.
    /// </summary>
    /// <param name="shortcut">The directory shortcut.</param>
    /// <returns>How it ended.</returns>
    LaunchResult OpenInTerminal(Shortcut shortcut);
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
/// edited by hand cannot turn a URL shortcut into a launcher for another scheme. An application whose target is a
/// command is looked up on the <c>PATH</c> when it is opened, and the program found is started.
/// </remarks>
public sealed class ShortcutLauncher : IShortcutLauncher
{
    // What starting a program that is not installed fails with: ERROR_FILE_NOT_FOUND on Windows, ENOENT elsewhere.
    private const int ProgramNotFound = 2;

    // The terminals tried on Linux, the distribution's own choice first.
    private static readonly string[] LinuxTerminals = ["x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm"];

    private readonly IProcessStarter _starter;
    private readonly ICommandResolver _commands;
    private readonly ILogger<ShortcutLauncher> _logger;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="starter">Starts the processes.</param>
    /// <param name="commands">Finds the programs commands name.</param>
    /// <param name="logger">Where failures are reported.</param>
    public ShortcutLauncher(IProcessStarter starter, ICommandResolver commands, ILogger<ShortcutLauncher> logger)
    {
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(logger);

        _starter = starter;
        _commands = commands;
        _logger = logger;
    }

    /// <inheritdoc />
    public LaunchResult Launch(ShortcutKind kind, Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        if (IsCommand(kind, shortcut.Target))
        {
            return _commands.Resolve(shortcut.Target) is { } program
                ? Start(BuildCommandStartInfo(program, shortcut.Arguments), shortcut)
                : CommandMissing(shortcut.Target);
        }

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

        if (IsCommand(kind, shortcut.Target))
        {
            return _commands.Resolve(shortcut.Target) is { } program
                ? Start(BuildRevealStartInfo(CurrentPlatform, program), shortcut)
                : CommandMissing(shortcut.Target);
        }

        LaunchResult? refusal = CheckTarget(kind, shortcut.Target);

        return refusal ?? Start(BuildRevealStartInfo(CurrentPlatform, shortcut.Target), shortcut);
    }

    /// <inheritdoc />
    public LaunchResult OpenInTerminal(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        LaunchResult? refusal = CheckTarget(ShortcutKind.Directory, shortcut.Target);

        return refusal ?? StartFirstTerminal(BuildTerminalStartInfos(CurrentPlatform, shortcut.Target), shortcut);
    }

    /// <summary>
    /// The terminals a folder can be opened in, the preferred first.
    /// </summary>
    /// <param name="platform">The desktop the application runs on.</param>
    /// <param name="directory">The folder.</param>
    /// <returns>The start information of each; the folder is always the working directory or an argument of its own,
    /// never part of a command line a terminal or a shell would parse.</returns>
    internal static IReadOnlyList<ProcessStartInfo> BuildTerminalStartInfos(DesktopPlatform platform, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        switch (platform)
        {
            case DesktopPlatform.Windows:
                // "-d ." opens in Windows Terminal's own working directory, so the path never meets its command
                // line, where a ';' would start a new tab.
                ProcessStartInfo windowsTerminal = new("wt.exe") { UseShellExecute = false, WorkingDirectory = directory };
                windowsTerminal.ArgumentList.Add("-d");
                windowsTerminal.ArgumentList.Add(".");
                return [windowsTerminal, new ProcessStartInfo("cmd.exe") { UseShellExecute = false, WorkingDirectory = directory }];

            case DesktopPlatform.MacOS:
                ProcessStartInfo mac = new("open") { UseShellExecute = false };
                mac.ArgumentList.Add("-a");
                mac.ArgumentList.Add("Terminal");
                mac.ArgumentList.Add(directory);
                return [mac];

            default:
                return LinuxTerminals.Select(terminal => new ProcessStartInfo(terminal) { UseShellExecute = false, WorkingDirectory = directory }).ToArray();
        }
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

            case ShortcutKind.Document:
                return new ProcessStartInfo(shortcut.Target)
                {
                    UseShellExecute = true,

                    // The application opening it starts beside it, so its File › Open does too.
                    WorkingDirectory = Path.GetDirectoryName(shortcut.Target) ?? string.Empty,
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown shortcut kind.");
        }
    }

    /// <summary>
    /// How a command's program is started, once found on the <c>PATH</c>.
    /// </summary>
    /// <param name="program">The program's full path.</param>
    /// <param name="arguments">The shortcut's arguments, if any.</param>
    /// <returns>The start information: in the user's home folder, where a desktop launcher or a terminal starts a
    /// command, rather than wherever the program happens to be installed.</returns>
    internal static ProcessStartInfo BuildCommandStartInfo(string program, string? arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);

        ProcessStartInfo command = new(program)
        {
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            command.Arguments = arguments.Trim();
        }

        return command;
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
    /// Whether an application's target is a command to look up on the <c>PATH</c> rather than a program's path.
    /// </summary>
    private static bool IsCommand(ShortcutKind kind, string target)
        => (kind == ShortcutKind.Application) && !Path.IsPathFullyQualified(target.Trim()) && PathCommandResolver.IsCommandName(target);

    private static LaunchResult CommandMissing(string command)
        => new(LaunchOutcome.TargetMissing, $"No program called “{command.Trim()}” was found on the PATH.");

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
            ShortcutKind.Document when !File.Exists(target)
                => new LaunchResult(LaunchOutcome.TargetMissing, $"The document no longer exists: {target}"),
            ShortcutKind.Url when !UrlNormalizer.TryParse(target, out _, out string? error)
                => new LaunchResult(LaunchOutcome.Refused, error),
            _ => null,
        };

    // Each terminal that is not installed gives way to the next; any other failure is the answer.
    private LaunchResult StartFirstTerminal(IReadOnlyList<ProcessStartInfo> terminals, Shortcut shortcut)
    {
        foreach (ProcessStartInfo terminal in terminals)
        {
            try
            {
                _starter.Start(terminal);
                return LaunchResult.Launched;
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == ProgramNotFound)
            {
                _logger.LogDebug(exception, "The terminal {Terminal} is not installed", terminal.FileName);
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
            {
                _logger.LogWarning(exception, "Opening a terminal in {Name} failed", shortcut.Name);
                return new LaunchResult(LaunchOutcome.Failed, $"A terminal could not be opened in \u201C{shortcut.Name}\u201D: {exception.Message}");
            }
        }

        return new LaunchResult(LaunchOutcome.Failed, "No terminal was found on this computer.");
    }

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
