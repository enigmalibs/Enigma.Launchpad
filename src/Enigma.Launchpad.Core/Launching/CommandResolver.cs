using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Enigma.Launchpad.Core.Launching;

/// <summary>
/// Finds the program a command names — <c>notepad</c>, <c>code</c>, <c>git</c> — the way a shell does, through the
/// <c>PATH</c> environment variable.
/// </summary>
public interface ICommandResolver
{
    /// <summary>
    /// Finds the program a command names.
    /// </summary>
    /// <param name="command">The command: a program's name, with no directory.</param>
    /// <returns>The program's full path; <see langword="null"/> when no directory of the <c>PATH</c> holds it, or when
    /// the text is not a command name (<see cref="PathCommandResolver.IsCommandName"/>).</returns>
    string? Resolve(string command);
}

/// <summary>
/// Default <see cref="ICommandResolver"/>: the directories of the <c>PATH</c>, in order — on Windows with the
/// extensions of <c>PATHEXT</c>, elsewhere only files that may be executed.
/// </summary>
/// <remarks>
/// <para>
/// The current directory is never searched, unlike the Windows command prompt: a program dropped into whatever
/// folder the launchpad happens to run in must not be able to stand in for the one the user meant.
/// </para>
/// <para>
/// On Windows the user's and the machine's <c>PATH</c> are read again on every call, after the process's own, so a
/// tool installed while the launchpad runs is found without restarting it.
/// </para>
/// </remarks>
public sealed class PathCommandResolver : ICommandResolver
{
    private const string DefaultExtensions = ".COM;.EXE;.BAT;.CMD";
    private const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private readonly Func<IEnumerable<string>> _directories;
    private readonly Func<IReadOnlyList<string>> _extensions;
    private readonly bool _isWindows;

    /// <summary>
    /// Initialises a new instance over the environment of the running platform.
    /// </summary>
    public PathCommandResolver()
        : this(PathDirectories, PathExtensions, OperatingSystem.IsWindows())
    {
    }

    /// <summary>
    /// Initialises a new instance over given directories and extensions.
    /// </summary>
    /// <param name="directories">The directories to search, in order, read on every call.</param>
    /// <param name="extensions">The extensions a command is tried with on Windows, read on every call.</param>
    /// <param name="isWindows">Whether names are completed with <paramref name="extensions"/>.</param>
    internal PathCommandResolver(Func<IEnumerable<string>> directories, Func<IReadOnlyList<string>> extensions, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(extensions);

        _directories = directories;
        _extensions = extensions;
        _isWindows = isWindows;
    }

    /// <summary>
    /// Whether a text can name a command: not empty, not a path — fully qualified or relative — and nothing a file
    /// name cannot hold.
    /// </summary>
    /// <param name="text">The text, trimmed first.</param>
    /// <returns><see langword="true"/> for a name such as <c>notepad</c> or <c>code.cmd</c>.</returns>
    public static bool IsCommandName(string? text)
    {
        string name = text?.Trim() ?? string.Empty;

        return (name.Length > 0)
            && (name is not ("." or ".."))
            && !Path.IsPathFullyQualified(name)
            && (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0)
            && (name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    /// <inheritdoc />
    public string? Resolve(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        string name = command.Trim();

        if (!IsCommandName(name))
        {
            return null;
        }

        string[] candidates = CandidateNames(name);

        foreach (string directory in Directories())
        {
            foreach (string candidate in candidates)
            {
                string path = Path.Combine(directory, candidate);

                if (IsProgram(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> PathDirectories()
    {
        IEnumerable<string?> values = OperatingSystem.IsWindows()
            ?
            [
                Environment.GetEnvironmentVariable("PATH"),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
            ]
            : [Environment.GetEnvironmentVariable("PATH")];

        return values.SelectMany(value => (value ?? string.Empty).Split(Path.PathSeparator));
    }

    private static string[] PathExtensions()
    {
        string value = Environment.GetEnvironmentVariable("PATHEXT") is { Length: > 0 } pathExt ? pathExt : DefaultExtensions;

        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(extension => extension.StartsWith('.'))
            .ToArray();
    }

    // A name that already ends in one of the extensions is tried as it is; any other — even "python3.12" — has each
    // of them added, as the command prompt does. PATHEXT is upper case by convention and file names are not: the
    // extension is added in lower case, so the path found reads as the file is usually spelled.
    private string[] CandidateNames(string name)
    {
        if (!_isWindows)
        {
            return [name];
        }

        IReadOnlyList<string> extensions = _extensions();

        return extensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            ? [name]
            : extensions.Select(extension => name + extension.ToLowerInvariant()).ToArray();
    }

    private IEnumerable<string> Directories()
    {
        HashSet<string> seen = new(_isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (string entry in _directories())
        {
            string directory = entry.Trim().Trim('"');

            if (_isWindows)
            {
                directory = Environment.ExpandEnvironmentVariables(directory);
            }

            // An empty or relative entry means the current directory, which is never searched.
            if ((directory.Length > 0) && Path.IsPathFullyQualified(directory) && seen.Add(Path.TrimEndingDirectorySeparator(directory)))
            {
                yield return directory;
            }
        }
    }

    private bool IsProgram(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        // Windows has no execute permission: the extension is what makes a file a program there.
        if (_isWindows || OperatingSystem.IsWindows())
        {
            return true;
        }

        return (File.GetUnixFileMode(path) & ExecuteBits) != 0;
    }
}
