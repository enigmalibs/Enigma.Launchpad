using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// A program's icon as read from the operating system.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Bgra">Four bytes per pixel — blue, green, red, alpha — top row first, with straight (not
/// premultiplied) alpha.</param>
public sealed record ProgramIconPixels(int Width, int Height, byte[] Bgra);

/// <summary>
/// Reads a program's icon from the operating system. A seam of its own so the policy around it — which paths are
/// read, caching, threading — can be tested without the shell.
/// </summary>
public interface IProgramIconReader
{
    /// <summary>
    /// Reads the icon the platform shows for a file. Blocks while the platform works: callers keep it off the UI
    /// thread.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="size">The icon's width and height, in pixels.</param>
    /// <returns>The icon, or <see langword="null"/> when the platform has none to give.</returns>
    ProgramIconPixels? Read(string path, int size);
}

/// <summary>
/// Default <see cref="IProgramIconReader"/>: the Windows shell, which gives an executable, a <c>.lnk</c>, a script
/// or a document the icon Explorer shows for it. No other platform gives an executable an icon of its own, so
/// there it reads nothing.
/// </summary>
public sealed class ShellProgramIconReader : IProgramIconReader
{
    /// <inheritdoc />
    public ProgramIconPixels? Read(string path, int size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        return OperatingSystem.IsWindows() ? NativeMethods.ReadShellIcon(path, size) : null;
    }

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        // Always an icon, never a thumbnail of the file's content.
        private const int ImageFlagsIconOnly = 0x4;
        private const uint DibRgbColors = 0;

        /// <summary>
        /// Asks the shell for a file's icon, and copies it out as 32 bpp pixels.
        /// </summary>
        internal static ProgramIconPixels? ReadShellIcon(string path, int size)
        {
            Guid factoryId = typeof(IShellItemImageFactory).GUID;

            if ((SHCreateItemFromParsingName(path, IntPtr.Zero, ref factoryId, out IShellItemImageFactory? factory) < 0) || (factory is null))
            {
                return null;
            }

            IntPtr bitmap;

            try
            {
                if (factory.GetImage(new NativeSize(size, size), ImageFlagsIconOnly, out bitmap) < 0)
                {
                    return null;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }

            try
            {
                return CopyPixels(bitmap);
            }
            finally
            {
                DeleteObject(bitmap);
            }
        }

        private static ProgramIconPixels? CopyPixels(IntPtr bitmap)
        {
            if (GetObject(bitmap, Marshal.SizeOf<NativeBitmap>(), out NativeBitmap description) == 0)
            {
                return null;
            }

            int width = description.Width;
            int height = Math.Abs(description.Height);

            // A negative height asks for the rows top first, whichever way the bitmap stores them.
            BitmapInfoHeader format = new()
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            };

            byte[] pixels = new byte[width * height * 4];
            IntPtr screen = GetDC(IntPtr.Zero);

            try
            {
                if (GetDIBits(screen, bitmap, 0, (uint)height, pixels, ref format, DibRgbColors) != height)
                {
                    return null;
                }
            }
            finally
            {
                _ = ReleaseDC(IntPtr.Zero, screen);
            }

            return HasAlpha(pixels) ? new ProgramIconPixels(width, height, pixels) : null;
        }

        // An image that is transparent everywhere would draw nothing; the caller's fallback is better.
        private static bool HasAlpha(byte[] pixels)
        {
            for (int index = 3; index < pixels.Length; index += 4)
            {
                if (pixels[index] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? item);

        [DllImport("gdi32.dll", EntryPoint = "GetObjectW", ExactSpelling = true)]
        private static extern int GetObject(IntPtr handle, int size, out NativeBitmap bitmap);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int GetDIBits(IntPtr deviceContext, IntPtr bitmap, uint startLine, uint lineCount, byte[] bits, ref BitmapInfoHeader format, uint usage);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(NativeSize size, int flags, out IntPtr bitmap);
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct NativeSize(int width, int height)
        {
            public readonly int Width = width;
            public readonly int Height = height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeBitmap
        {
            public int Type;
            public int Width;
            public int Height;
            public int WidthBytes;
            public ushort Planes;
            public ushort BitsPixel;
            public IntPtr Bits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public int Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public int Compression;
            public int SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public int ClrUsed;
            public int ClrImportant;
        }
    }
}
