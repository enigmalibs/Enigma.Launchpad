using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.DragAndDrop;

/// <summary>
/// What travels with a dragged group or shortcut: its category and its identity, and nothing else. The
/// category comes along so a drop can refuse an item that does not belong on the page it lands on.
/// </summary>
/// <param name="Kind">The category the dragged item belongs to.</param>
/// <param name="Id">The dragged item's identity.</param>
public sealed record DragPayload(ShortcutKind Kind, Guid Id)
{
    /// <summary>
    /// Writes the payload as the text the drag carries.
    /// </summary>
    /// <returns>The text, <c>kind:id</c>.</returns>
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{(int)Kind}:{Id:N}");

    /// <summary>
    /// Reads a payload back from the text a drag carried.
    /// </summary>
    /// <param name="text">The text; anything not written by <see cref="Format"/> is refused.</param>
    /// <param name="payload">The payload.</param>
    /// <returns><see langword="true"/> when the text was a payload.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out DragPayload? payload)
    {
        payload = null;

        if (text is null)
        {
            return false;
        }

        int colon = text.IndexOf(':', StringComparison.Ordinal);

        if ((colon <= 0)
            || !int.TryParse(text.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out int kind)
            || !Enum.IsDefined((ShortcutKind)kind)
            || !Guid.TryParseExact(text.AsSpan(colon + 1), "N", out Guid id))
        {
            return false;
        }

        payload = new DragPayload((ShortcutKind)kind, id);
        return true;
    }
}
