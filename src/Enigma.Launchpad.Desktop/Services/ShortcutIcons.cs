using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Enigma.Launchpad.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// An icon ready to show, with the form it is kept in.
/// </summary>
/// <param name="Image">What the tile draws.</param>
/// <param name="Icon">What the shortcut stores.</param>
public sealed record IconImage(IImage Image, ShortcutIcon Icon);

/// <summary>
/// The icons of shortcut tiles: the ones shortcuts keep, and the ones found from their targets.
/// </summary>
public interface IShortcutIcons
{
    /// <summary>
    /// Whether a kind of shortcut can have its icon found from its target: a program's, a website's. A folder's
    /// cannot; it only ever has the icon the user chose.
    /// </summary>
    /// <param name="kind">The kind of shortcut.</param>
    /// <returns><see langword="true"/> for applications and URLs.</returns>
    bool CanFind(ShortcutKind kind);

    /// <summary>
    /// Gets the image of an icon a shortcut keeps.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The image, or <see langword="null"/> when the icon is not an image that can be shown.</returns>
    IImage? Show(ShortcutIcon icon);

    /// <summary>
    /// Finds the icon of a shortcut's target: the program's own, or its website's.
    /// </summary>
    /// <param name="kind">The kind of shortcut.</param>
    /// <param name="target">Its target.</param>
    /// <param name="cancellationToken">Stops this wait only.</param>
    /// <returns>The icon, not marked as chosen by the user; <see langword="null"/> when none was found, and always
    /// for a kind that cannot have one found.</returns>
    Task<IconImage?> FindAsync(ShortcutKind kind, string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the icon of a shortcut's target again — from the program, or from the website itself — rather than
    /// answering what was found earlier.
    /// </summary>
    /// <param name="kind">The kind of shortcut.</param>
    /// <param name="target">Its target.</param>
    /// <param name="cancellationToken">Stops this wait only.</param>
    /// <returns>The icon, not marked as chosen by the user; <see langword="null"/> when none was found, and always
    /// for a kind that cannot have one found.</returns>
    Task<IconImage?> RefreshAsync(ShortcutKind kind, string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an image file the user picked as a shortcut's icon, off the UI thread.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The icon, marked as chosen by the user.</returns>
    /// <exception cref="InvalidDataException">The file is too large, or not an image that can be shown; the message
    /// says which, worded for the user.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be read.</exception>
    Task<IconImage> ReadFileAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IShortcutIcons"/>, over the program and website icon sources.
/// </summary>
/// <remarks>
/// The sources answer PNG files; this is the one place they become images, whether they were just found or kept
/// with a shortcut since an earlier session. An icon is decoration: nothing here throws over a bad one.
/// </remarks>
public sealed class ShortcutIcons : IShortcutIcons
{
    /// <summary>
    /// The largest image file accepted as an icon: far beyond any icon, small enough never to stall the decoder.
    /// </summary>
    internal const long MaxFileBytes = 10 * 1024 * 1024;

    private readonly IProgramIcons _programIcons;
    private readonly IWebsiteIcons _websiteIcons;
    private readonly ILogger<ShortcutIcons> _logger;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="programIcons">Finds programs' icons.</param>
    /// <param name="websiteIcons">Finds websites' icons.</param>
    /// <param name="logger">Where an icon that cannot be shown is reported.</param>
    public ShortcutIcons(IProgramIcons programIcons, IWebsiteIcons websiteIcons, ILogger<ShortcutIcons> logger)
    {
        ArgumentNullException.ThrowIfNull(programIcons);
        ArgumentNullException.ThrowIfNull(websiteIcons);
        ArgumentNullException.ThrowIfNull(logger);

        _programIcons = programIcons;
        _websiteIcons = websiteIcons;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanFind(ShortcutKind kind) => kind is ShortcutKind.Application or ShortcutKind.Url;

    /// <inheritdoc />
    public IImage? Show(ShortcutIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (!icon.TryReadBytes(out byte[]? png))
        {
            _logger.LogDebug("A shortcut's icon is not a PNG image it can show");
            return null;
        }

        try
        {
            return IconPng.Decode(png);
        }
        catch (Exception exception)
        {
            // Whatever the decoder makes of a damaged file, the tile keeps its glyph.
            _logger.LogDebug(exception, "A shortcut's icon could not be decoded");
            return null;
        }
    }

    /// <inheritdoc />
    public Task<IconImage?> FindAsync(ShortcutKind kind, string target, CancellationToken cancellationToken = default)
        => FindAsync(kind, target, refresh: false, cancellationToken);

    /// <inheritdoc />
    public Task<IconImage?> RefreshAsync(ShortcutKind kind, string target, CancellationToken cancellationToken = default)
        => FindAsync(kind, target, refresh: true, cancellationToken);

    /// <inheritdoc />
    public Task<IconImage> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(() => ReadFileCoreAsync(path, cancellationToken), cancellationToken);
    }

    private async Task<IconImage?> FindAsync(ShortcutKind kind, string target, bool refresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        Task<byte[]?> lookup = kind switch
        {
            ShortcutKind.Application => refresh ? _programIcons.RefreshAsync(target, cancellationToken) : _programIcons.GetAsync(target, cancellationToken),
            ShortcutKind.Url => refresh ? _websiteIcons.RefreshAsync(target, cancellationToken) : _websiteIcons.GetAsync(target, cancellationToken),
            _ => Task.FromResult<byte[]?>(null),
        };

        if (await lookup is not { } png)
        {
            return null;
        }

        ShortcutIcon icon = ShortcutIcon.FromBytes(png, isCustom: false);

        return Show(icon) is { } image ? new IconImage(image, icon) : null;
    }

    private static async Task<IconImage> ReadFileCoreAsync(string path, CancellationToken cancellationToken)
    {
        if (new FileInfo(path) is { Exists: true, Length: > MaxFileBytes })
        {
            throw new InvalidDataException("This file is larger than 10 MB, too large for an icon.");
        }

        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        byte[] png;

        try
        {
            png = IconPng.Normalize(bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The decoder's own exceptions are not a documented set; to the user they all mean the same.
            throw new InvalidDataException("This file is not an image the launchpad can show.", exception);
        }

        return new IconImage(IconPng.Decode(png), ShortcutIcon.FromBytes(png, isCustom: true));
    }
}
