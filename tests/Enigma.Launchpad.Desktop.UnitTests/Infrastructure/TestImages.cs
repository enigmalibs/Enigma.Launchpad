using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Real image files, made and read with Avalonia: call them on the headless UI thread.
/// </summary>
internal static class TestImages
{
    /// <summary>
    /// A PNG file of one colour, as a site would serve it.
    /// </summary>
    public static byte[] Png(int width, int height) => Save(width, height, PngBitmapEncoderOptions.Default);

    /// <summary>
    /// The size of an image file.
    /// </summary>
    public static PixelSize SizeOf(byte[] image)
    {
        using MemoryStream file = new(image, writable: false);
        using Bitmap bitmap = new(file);
        return bitmap.PixelSize;
    }

    private static byte[] Save(int width, int height, BitmapEncoderOptions options)
    {
        byte[] bgra = Enumerable.Repeat<byte>(0xC0, width * height * 4).ToArray();
        using Bitmap bitmap = ProgramIcons.ToBitmap(new ProgramIconPixels(width, height, bgra));
        using MemoryStream file = new();
        bitmap.Save(file, options);
        return file.ToArray();
    }
}
