using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Core.Validation;

/// <summary>
/// Default <see cref="IShortcutValidator"/>, checking paths against the real file system.
/// </summary>
public sealed class ShortcutValidator : IShortcutValidator
{
    /// <summary>
    /// The longest a profile, group or shortcut name, or a workspace title, may be.
    /// </summary>
    public const int MaxNameLength = 100;

    /// <summary>
    /// The longest a shortcut's description may be: about two lines of a tile.
    /// </summary>
    public const int MaxDescriptionLength = 200;

    private readonly bool _acceptsApplicationBundles;
    private readonly ICommandResolver _commands;

    /// <summary>
    /// Initialises a new instance for the running platform, finding commands through its <c>PATH</c>.
    /// </summary>
    public ShortcutValidator()
        : this(new PathCommandResolver())
    {
    }

    /// <summary>
    /// Initialises a new instance for the running platform: application bundles are accepted on macOS.
    /// </summary>
    /// <param name="commands">Finds the programs commands name.</param>
    public ShortcutValidator(ICommandResolver commands)
        : this(OperatingSystem.IsMacOS(), commands)
    {
    }

    /// <summary>
    /// Initialises a new instance, saying whether a <c>.app</c> directory counts as an application.
    /// </summary>
    /// <param name="acceptsApplicationBundles">Whether application bundles are accepted.</param>
    /// <param name="commands">Finds the programs commands name; the platform's <c>PATH</c> when omitted.</param>
    internal ShortcutValidator(bool acceptsApplicationBundles, ICommandResolver? commands = null)
    {
        _acceptsApplicationBundles = acceptsApplicationBundles;
        _commands = commands ?? new PathCommandResolver();
    }

    /// <inheritdoc />
    public string? ValidateShortcutName(string? name) => ValidateName(name);

    /// <inheritdoc />
    public string? ValidateDescription(string? description)
        => (description?.Trim().Length ?? 0) > MaxDescriptionLength
            ? $"Keep the description to {MaxDescriptionLength} characters or fewer."
            : null;

    /// <inheritdoc />
    public string? ValidateGroupName(string? name, IEnumerable<string> otherGroupNames)
        => ValidateUniqueName(name, otherGroupNames, "A group with this name already exists.");

    /// <inheritdoc />
    public string? ValidateProfileName(string? name, IEnumerable<string> otherProfileNames)
        => ValidateUniqueName(name, otherProfileNames, "A profile with this name already exists.");

    /// <inheritdoc />
    public string? ValidateWorkspaceTitle(string? title, IEnumerable<string> otherTitles)
    {
        ArgumentNullException.ThrowIfNull(otherTitles);

        string trimmed = title?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "Enter a title.";
        }

        if (trimmed.Length > MaxNameLength)
        {
            return $"Keep the title to {MaxNameLength} characters or fewer.";
        }

        return otherTitles.Any(other => string.Equals(other.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            ? "A workspace with this title already exists."
            : null;
    }

    /// <inheritdoc />
    public string? ValidateWorkspaceDescription(string? description) => ValidateDescription(description);

    /// <inheritdoc />
    public TargetValidation ValidateTarget(ShortcutKind kind, string? target)
        => kind switch
        {
            ShortcutKind.Application => ValidateApplication(target),
            ShortcutKind.Directory => ValidateDirectory(target),
            ShortcutKind.Url => ValidateUrl(target),
            ShortcutKind.Document => ValidateDocument(target),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown shortcut kind."),
        };

    /// <summary>
    /// Whether a path names an application bundle: a directory whose name ends in <c>.app</c>.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><see langword="true"/> for an existing bundle.</returns>
    internal static bool IsApplicationBundle(string path)
        => path.TrimEnd('/', '\\').EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path);

    private static string? ValidateUniqueName(string? name, IEnumerable<string> otherNames, string duplicateMessage)
    {
        ArgumentNullException.ThrowIfNull(otherNames);

        if (ValidateName(name) is { } error)
        {
            return error;
        }

        string trimmed = name?.Trim() ?? string.Empty;

        return otherNames.Any(other => string.Equals(other.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            ? duplicateMessage
            : null;
    }

    private static string? ValidateName(string? name)
    {
        string trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "Enter a name.";
        }

        return trimmed.Length > MaxNameLength
            ? $"Keep the name to {MaxNameLength} characters or fewer."
            : null;
    }

    /// <summary>
    /// An application is a program's full path, or a command the <c>PATH</c> leads to — kept as typed, so it follows
    /// the <c>PATH</c> wherever the shortcut is opened.
    /// </summary>
    private TargetValidation ValidateApplication(string? target)
    {
        string text = Unquote(target);

        if (text.Length == 0)
        {
            return TargetValidation.Invalid("Enter a path or a command.");
        }

        if (!Path.IsPathFullyQualified(text))
        {
            return ValidateCommand(text);
        }

        if (!TryReadPath(text, out string path, out string? error))
        {
            return TargetValidation.Invalid(error);
        }

        if (File.Exists(path) || (_acceptsApplicationBundles && IsApplicationBundle(path)))
        {
            return TargetValidation.Valid(path);
        }

        return TargetValidation.Invalid(Directory.Exists(path)
            ? "This is a folder, not a program. Add it to a folders group instead."
            : "There is no file at this path.");
    }

    private TargetValidation ValidateCommand(string command)
    {
        if (!PathCommandResolver.IsCommandName(command))
        {
            return TargetValidation.Invalid("Enter a full path, or a command found on the PATH.");
        }

        if (_commands.Resolve(command) is not null)
        {
            return TargetValidation.Valid(command);
        }

        string message = $"No program called “{command}” was found on the PATH.";

        return TargetValidation.Invalid(command.Any(char.IsWhiteSpace) ? $"{message} Arguments go in Default arguments." : message);
    }

    private static TargetValidation ValidateDirectory(string? target)
    {
        if (!TryReadPath(target, out string path, out string? error))
        {
            return TargetValidation.Invalid(error);
        }

        if (Directory.Exists(path))
        {
            return TargetValidation.Valid(path);
        }

        return TargetValidation.Invalid(File.Exists(path)
            ? "This is a file, not a folder. Add it to an applications or a documents group instead."
            : "There is no folder at this path.");
    }

    /// <summary>
    /// A document is any existing file; the system's association decides what opens it.
    /// </summary>
    private static TargetValidation ValidateDocument(string? target)
    {
        if (!TryReadPath(target, out string path, out string? error))
        {
            return TargetValidation.Invalid(error);
        }

        if (File.Exists(path))
        {
            return TargetValidation.Valid(path);
        }

        return TargetValidation.Invalid(Directory.Exists(path)
            ? "This is a folder, not a file. Add it to a folders group instead."
            : "There is no file at this path.");
    }

    private static TargetValidation ValidateUrl(string? target)
        => UrlNormalizer.TryNormalize(target, out string normalized, out string? error)
            ? TargetValidation.Valid(normalized)
            : TargetValidation.Invalid(error ?? "This is not a valid web address.");

    /// <summary>
    /// Reads a path as typed: trimmed, without the quotes a "copy as path" puts around it, and absolute.
    /// </summary>
    private static bool TryReadPath(string? target, out string path, [NotNullWhen(false)] out string? error)
    {
        path = Unquote(target);

        if (path.Length == 0)
        {
            error = "Enter a path.";
            return false;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            error = "Enter a full path, starting from the drive or the root.";
            return false;
        }

        // Resolves "." and ".." segments, so the stored path is the one the file manager shows.
        path = Path.GetFullPath(path);
        error = null;
        return true;
    }

    /// <summary>
    /// Reads a target as typed: trimmed, and without the quotes a "copy as path" puts around it.
    /// </summary>
    private static string Unquote(string? target)
    {
        string text = target?.Trim() ?? string.Empty;

        return (text.Length >= 2) && text.StartsWith('"') && text.EndsWith('"') ? text[1..^1].Trim() : text;
    }
}
