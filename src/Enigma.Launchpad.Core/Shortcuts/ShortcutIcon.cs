using System;
using System.Diagnostics.CodeAnalysis;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// The icon a shortcut's tile shows, kept with the shortcut so it is there at the next start without asking the
/// operating system or the network again.
/// </summary>
/// <param name="Png">The image: a PNG file, in base64.</param>
/// <param name="IsCustom">Whether the user chose it, rather than it being found from the shortcut's target.</param>
/// <remarks>
/// The image is held as text so a document stays plain JSON and two shortcuts with the same icon compare equal.
/// Nothing here decodes it: a stored icon that turns out not to be an image only costs its tile the glyph.
/// </remarks>
public sealed record ShortcutIcon(string Png, bool IsCustom = false)
{
    /// <summary>
    /// The largest image accepted from a file someone else wrote, such as an import: far above the few kilobytes
    /// of the icons the launchpad stores itself.
    /// </summary>
    public const int MaxBytes = 256 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Creates an icon from a PNG file's bytes.
    /// </summary>
    /// <param name="png">The PNG file.</param>
    /// <param name="isCustom">Whether the user chose it.</param>
    /// <returns>The icon.</returns>
    public static ShortcutIcon FromBytes(byte[] png, bool isCustom)
    {
        ArgumentNullException.ThrowIfNull(png);
        return new ShortcutIcon(Convert.ToBase64String(png), isCustom);
    }

    /// <summary>
    /// Reads the image back, if it is a PNG file of an acceptable size.
    /// </summary>
    /// <param name="png">The PNG file's bytes, when this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> for text that is not base64, an image over <see cref="MaxBytes"/>, or bytes
    /// that do not start like a PNG file.</returns>
    public bool TryReadBytes([NotNullWhen(true)] out byte[]? png)
    {
        png = null;

        // Base64 never decodes to more than three bytes per four characters, so a long text is refused unread.
        if ((Png is null) || (Png.Length > (MaxBytes + 2) / 3 * 4))
        {
            return false;
        }

        byte[] buffer = new byte[Png.Length / 4 * 3];

        if (!Convert.TryFromBase64String(Png, buffer, out int written)
            || (written > MaxBytes)
            || !buffer.AsSpan(0, written).StartsWith(PngSignature))
        {
            return false;
        }

        png = buffer.AsSpan(0, written).ToArray();
        return true;
    }
}
