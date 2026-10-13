using System;
using System.Buffers.Binary;
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
    /// A JPEG file of one colour.
    /// </summary>
    public static byte[] Jpeg(int width, int height) => Save(width, height, JpegBitmapEncoderOptions.Default);

    /// <summary>
    /// An ICO file holding one square PNG image, the way modern icons are made.
    /// </summary>
    public static byte[] Ico(int size)
    {
        byte[] png = Png(size, size);
        byte[] ico = new byte[6 + 16 + png.Length];

        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(2), 1);                   // an icon, not a cursor
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(4), 1);                   // one image
        ico[6] = (byte)(size % 256);                                                   // 0 stands for 256
        ico[7] = (byte)(size % 256);
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(10), 1);                  // planes
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(12), 32);                 // bits per pixel
        BinaryPrimitives.WriteUInt32LittleEndian(ico.AsSpan(14), (uint)png.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(ico.AsSpan(18), 22);                 // where the image starts
        png.CopyTo(ico, 22);

        return ico;
    }

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
