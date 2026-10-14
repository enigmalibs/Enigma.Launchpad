using System;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// One shortcut: a named target. Its kind is the kind of the group it is in, not a field of its own.
/// </summary>
/// <param name="Id">Stable identity, used to find the shortcut again across edits and moves.</param>
/// <param name="Name">What the shortcut is called.</param>
/// <param name="Target">The executable path or command, the directory path, the URL, or the document's path.</param>
/// <param name="Arguments">Command-line arguments; applications only, <see langword="null"/> otherwise.</param>
/// <param name="Description">A line saying what the shortcut is for, shown on its tile; <see langword="null"/> when
/// there is none.</param>
/// <param name="Icon">The icon its tile shows — chosen by the user, or found from the target and kept;
/// <see langword="null"/> when there is none yet.</param>
public sealed record Shortcut(
    Guid Id,
    string Name,
    string Target,
    string? Arguments = null,
    string? Description = null,
    ShortcutIcon? Icon = null);
