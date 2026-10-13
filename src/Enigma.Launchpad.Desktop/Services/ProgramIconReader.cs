using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

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
/// <remarks>
/// <para>
/// Every read runs on a thread of its own in a single-threaded apartment. The shell and the icon handlers it loads
/// expect one; called from the thread pool's multithreaded apartment, some of them fail now and then, which showed
/// as an icon present one day and gone the next.
/// </para>
/// <para>
/// The shell's image factory gives the best icon at the size asked for. Some older icons come out of it without
/// any transparency information, or not at all; for those, the file's classic icon is read instead, with its
/// transparency taken from the icon's mask.
/// </para>
/// </remarks>
public sealed class ShellProgramIconReader : IProgramIconReader
{
    /// <inheritdoc />
    public ProgramIconPixels? Read(string path, int size)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        return OperatingSystem.IsWindows() ? ReadOnWindows(path, size) : null;
    }

    /// <summary>
    /// Reads only the file's classic icon — the fallback of <see cref="Read"/> — on a thread of its own.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The icon, at the shell's large icon size, or <see langword="null"/>.</returns>
    [SupportedOSPlatform("windows")]
    internal static ProgramIconPixels? ReadClassicIcon(string path) => OnStaThread(() => NativeMethods.ReadClassicIcon(path));

    /// <summary>
    /// Runs work on a new thread in a single-threaded apartment, and waits for it.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="work">The work.</param>
    /// <returns>Its result; an exception it threw is thrown again here, with its original stack.</returns>
    [SupportedOSPlatform("windows")]
    internal static TResult OnStaThread<TResult>(Func<TResult> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        TResult result = default!;
        ExceptionDispatchInfo? failure = null;

        Thread thread = new(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception exception)
            {
                // Carried back to the caller, which decides what a failed read means.
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Shell icon reader",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
        return result;
    }

    /// <summary>
    /// Whether any pixel is not fully transparent: an image transparent everywhere would draw nothing.
    /// </summary>
    /// <param name="bgra">Four bytes per pixel, alpha last.</param>
    /// <returns><see langword="true"/> when some alpha is not zero.</returns>
    internal static bool HasAlpha(ReadOnlySpan<byte> bgra)
    {
        for (int index = 3; index < bgra.Length; index += 4)
        {
            if (bgra[index] != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gives an icon without transparency information the transparency of its mask: where the mask is black the
    /// icon is opaque, where it is white the icon is clear.
    /// </summary>
    /// <param name="bgra">The icon's pixels, four bytes each; their alpha is overwritten.</param>
    /// <param name="mask">The mask's pixels, read at the same size and four bytes each.</param>
    /// <exception cref="ArgumentException">The two do not have the same size.</exception>
    internal static void ApplyMask(Span<byte> bgra, ReadOnlySpan<byte> mask)
    {
        if (bgra.Length != mask.Length)
        {
            throw new ArgumentException("The mask does not have the icon's size.", nameof(mask));
        }

        for (int index = 0; index < bgra.Length; index += 4)
        {
            bgra[index + 3] = mask[index] == 0 ? (byte)0xFF : (byte)0x00;
        }
    }

    [SupportedOSPlatform("windows")]
    private static ProgramIconPixels? ReadOnWindows(string path, int size)
        => OnStaThread(() => NativeMethods.ReadShellIcon(path, size) ?? NativeMethods.ReadClassicIcon(path));

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        // Always an icon, never a thumbnail of the file's content.
        private const int ImageFlagsIconOnly = 0x4;
        private const uint DibRgbColors = 0;
        private const uint FileInfoIcon = 0x100;
        private const uint FileInfoLargeIcon = 0x0;

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
                // An image with no transparency information leaves the answer to the classic icon.
                return CopyPixels(bitmap) is { } pixels && HasAlpha(pixels.Bgra) ? pixels : null;
            }
            finally
            {
                DeleteObject(bitmap);
            }
        }

        /// <summary>
        /// Asks the shell for a file's classic icon, at its large icon size, and copies it out as 32 bpp pixels —
        /// with the transparency of its mask when its colours carry none.
        /// </summary>
        internal static ProgramIconPixels? ReadClassicIcon(string path)
        {
            FileInfoData info = default;

            if ((SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<FileInfoData>(), FileInfoIcon | FileInfoLargeIcon) == IntPtr.Zero)
                || (info.Icon == IntPtr.Zero))
            {
                return null;
            }

            try
            {
                return CopyIcon(info.Icon);
            }
            finally
            {
                DestroyIcon(info.Icon);
            }
        }

        private static ProgramIconPixels? CopyIcon(IntPtr icon)
        {
            if (!GetIconInfo(icon, out IconParts parts))
            {
                return null;
            }

            try
            {
                // A monochrome icon has no colour bitmap; there is nothing worth showing in it.
                if ((parts.Color == IntPtr.Zero) || (CopyPixels(parts.Color) is not { } colour))
                {
                    return null;
                }

                if (HasAlpha(colour.Bgra))
                {
                    return colour;
                }

                if ((parts.Mask == IntPtr.Zero)
                    || (CopyPixels(parts.Mask) is not { } mask)
                    || (mask.Width != colour.Width)
                    || (mask.Height != colour.Height))
                {
                    return null;
                }

                ApplyMask(colour.Bgra, mask.Bgra);
                return HasAlpha(colour.Bgra) ? colour : null;
            }
            finally
            {
                if (parts.Color != IntPtr.Zero)
                {
                    DeleteObject(parts.Color);
                }

                if (parts.Mask != IntPtr.Zero)
                {
                    DeleteObject(parts.Mask);
                }
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

            return new ProgramIconPixels(width, height, pixels);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? item);

        [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref FileInfoData info, uint size, uint flags);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIconInfo(IntPtr icon, out IconParts parts);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileInfoData
        {
            public IntPtr Icon;
            public int IconIndex;
            public uint Attributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string TypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IconParts
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool IsIcon;
            public int HotspotX;
            public int HotspotY;
            public IntPtr Mask;
            public IntPtr Color;
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
