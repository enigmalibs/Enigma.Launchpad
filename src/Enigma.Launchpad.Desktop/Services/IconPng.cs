using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Turns icon images into the small PNG files the launchpad keeps with its shortcuts, and back.
/// </summary>
/// <remarks>
/// Every icon — a program's, a website's, a file the user picked — goes through here, so a stored icon is always a
/// PNG no larger than <see cref="MaxSize"/>: sharp on a 24 px tile up to 200 % scaling, and a few kilobytes of JSON.
/// </remarks>
internal static class IconPng
{
    /// <summary>
    /// The largest width or height an icon is kept at, in pixels: twice the tile's 24.
    /// </summary>
    internal const int MaxSize = 48;

    /// <summary>
    /// Decodes an image file — PNG, ICO, GIF, JPEG, WebP or BMP — scaled down to fit <see cref="MaxSize"/> when it
    /// is larger.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns>The image.</returns>
    /// <exception cref="Exception">The bytes are not an image Avalonia can decode; which exception depends on the
    /// decoder.</exception>
    internal static Bitmap Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using MemoryStream stream = new(bytes, writable: false);
        Bitmap decoded = new(stream);
        PixelSize size = decoded.PixelSize;

        if ((size.Width <= MaxSize) && (size.Height <= MaxSize))
        {
            return decoded;
        }

        using (decoded)
        {
            double scale = (double)MaxSize / Math.Max(size.Width, size.Height);
            PixelSize scaled = new(
                Math.Max(1, (int)Math.Round(size.Width * scale)),
                Math.Max(1, (int)Math.Round(size.Height * scale)));

            return decoded.CreateScaledBitmap(scaled, BitmapInterpolationMode.HighQuality);
        }
    }

    /// <summary>
    /// Writes an image as a PNG file.
    /// </summary>
    /// <param name="bitmap">The image.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Encode(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using MemoryStream file = new();
        bitmap.Save(file, PngBitmapEncoderOptions.Default);
        return file.ToArray();
    }

    /// <summary>
    /// Re-encodes an image file as a PNG no larger than <see cref="MaxSize"/>.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns>The PNG file's bytes.</returns>
    /// <exception cref="Exception">The bytes are not an image Avalonia can decode.</exception>
    internal static byte[] Normalize(byte[] bytes)
    {
        using Bitmap bitmap = Decode(bytes);
        return Encode(bitmap);
    }
}
