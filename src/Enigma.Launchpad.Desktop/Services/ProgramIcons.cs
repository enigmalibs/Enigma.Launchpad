using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// The icons programs show in the platform's file manager, for application tiles.
/// </summary>
public interface IProgramIcons
{
    /// <summary>
    /// Gets the icon of a program.
    /// </summary>
    /// <param name="path">The program's path.</param>
    /// <param name="cancellationToken">Stops this wait only: the icon is still read for whoever asks next.</param>
    /// <returns>The icon as a PNG file no larger than <see cref="IconPng.MaxSize"/>, or <see langword="null"/> when
    /// there is none to show.</returns>
    Task<byte[]?> GetAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IProgramIcons"/>: reads each program's icon once, off the UI thread, and keeps it — or the
/// fact that it has none — for the rest of the session.
/// </summary>
/// <remarks>
/// A network path is never read. Shortcuts arrive in import files, and asking the shell about
/// <c>\\host\share\app.exe</c> connects to that host — offering it the user's credentials — merely because a page
/// was shown. Opening such a shortcut is the user's choice; drawing its tile is not.
/// </remarks>
public sealed class ProgramIcons : IProgramIcons
{
    /// <summary>
    /// The size icons are read at, in pixels: the size icons are kept at.
    /// </summary>
    internal const int IconSize = IconPng.MaxSize;

    private static readonly Task<byte[]?> NoIcon = Task.FromResult<byte[]?>(null);

    private readonly IProgramIconReader _reader;
    private readonly ILogger<ProgramIcons> _logger;
    private readonly object _sync = new();

    // Windows paths ignore case, so one program reached under two spellings is read once.
    private readonly Dictionary<string, Task<byte[]?>> _icons
        = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="reader">Reads icons from the operating system.</param>
    /// <param name="logger">Where unexpected failures are reported.</param>
    public ProgramIcons(IProgramIconReader reader, ILogger<ProgramIcons> logger)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(logger);

        _reader = reader;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_sync)
        {
            if (!_icons.TryGetValue(path, out Task<byte[]?>? icon))
            {
                // The read is shared by every caller, so no caller's token may cancel it.
                icon = IsNetworkPath(path) ? NoIcon : Task.Run(() => Read(path), CancellationToken.None);
                _icons.Add(path, icon);
            }

            return icon.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Turns pixels read from the operating system into an image Avalonia can draw.
    /// </summary>
    /// <param name="pixels">The icon's pixels.</param>
    /// <returns>A bitmap with its own copy of the pixels.</returns>
    internal static Bitmap ToBitmap(ProgramIconPixels pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Bgra.Length != pixels.Width * pixels.Height * 4)
        {
            throw new ArgumentException("The pixels do not fill the icon's width and height.", nameof(pixels));
        }

        GCHandle pinned = GCHandle.Alloc(pixels.Bgra, GCHandleType.Pinned);

        try
        {
            return new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul,
                pinned.AddrOfPinnedObject(),
                new PixelSize(pixels.Width, pixels.Height),
                new Vector(96, 96),
                pixels.Width * 4);
        }
        finally
        {
            pinned.Free();
        }
    }

    // Both separators count: Windows reads "//host/share" as a network path too.
    private static bool IsNetworkPath(string path)
        => (path.Length >= 2) && (path[0] is '\\' or '/') && (path[1] is '\\' or '/');

    private byte[]? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            if (_reader.Read(path, IconSize) is not { } pixels)
            {
                return null;
            }

            using Bitmap bitmap = ToBitmap(pixels);
            return IconPng.Encode(bitmap);
        }
        catch (Exception exception)
        {
            // An icon is decoration: whatever stops one being read, the tile keeps its generic glyph.
            _logger.LogWarning(exception, "Reading the icon of {Path} failed", path);
            return null;
        }
    }
}
