namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// The three categories a shortcut belongs to. A shortcut never changes kind: the kind says what its
/// target is and how it is opened.
/// </summary>
public enum ShortcutKind
{
    /// <summary>An executable (or, on macOS, an application bundle), started with optional arguments.</summary>
    Application,

    /// <summary>A directory, opened in the platform's file manager.</summary>
    Directory,

    /// <summary>An http or https address, opened in the default browser.</summary>
    Url,
}
