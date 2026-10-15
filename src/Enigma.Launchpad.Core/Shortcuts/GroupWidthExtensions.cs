using System;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// What a <see cref="GroupWidth"/> means in the workspace's lines of three columns.
/// </summary>
public static class GroupWidthExtensions
{
    /// <summary>
    /// The number of columns in a workspace's line.
    /// </summary>
    public const int LineColumns = 3;

    extension(GroupWidth width)
    {
        /// <summary>
        /// Gets how many of a line's <see cref="LineColumns"/> columns the width takes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined width.</exception>
        public int Columns => width switch
        {
            GroupWidth.Full => LineColumns,
            GroupWidth.TwoThirds => 2,
            GroupWidth.OneThird => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(width), width, "Not a group width."),
        };
    }
}
