namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// The kinds of shortcut. A group holds shortcuts of one kind, and never changes kind: the kind says what its
/// shortcuts' targets are and how they are opened.
/// </summary>
public enum ShortcutKind
{
    /// <summary>An executable (or, on macOS, an application bundle), started with optional arguments.</summary>
    Application,

    /// <summary>A directory, opened in the platform's file manager.</summary>
    Directory,

    /// <summary>An http or https address, opened in the default browser.</summary>
    Url,

    /// <summary>A file, opened with the application the system associates with it.</summary>
    Document,
}
