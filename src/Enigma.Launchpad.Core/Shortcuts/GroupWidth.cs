namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// How much of a line of the workspace a group takes. The workspace lays its groups out in lines of three columns, in
/// order: groups whose widths fit together share a line.
/// </summary>
public enum GroupWidth
{
    /// <summary>The whole line: the group is alone on it.</summary>
    Full,

    /// <summary>Two of the line's three columns.</summary>
    TwoThirds,

    /// <summary>One of the line's three columns.</summary>
    OneThird,
}
