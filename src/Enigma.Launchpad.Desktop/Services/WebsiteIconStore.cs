using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// A website's icon as last stored on disk.
/// </summary>
/// <param name="Bytes">The icon file, as the site served it.</param>
/// <param name="IsFresh">Whether it is recent enough to show without asking the site again.</param>
public sealed record StoredWebsiteIcon(byte[] Bytes, bool IsFresh);

/// <summary>
/// Keeps website icons on disk between sessions, so a site is asked for its icon at most once a week and an
/// unreachable site still shows the last icon it had.
/// </summary>
public interface IWebsiteIconStore
{
    /// <summary>
    /// Reads a site's stored icon.
    /// </summary>
    /// <param name="site">The site's root address.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The icon and whether it is fresh, or <see langword="null"/> when none is stored.</returns>
    /// <exception cref="IOException">The stored file could not be read.</exception>
    Task<StoredWebsiteIcon?> ReadAsync(Uri site, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a site's icon, replacing any earlier one.
    /// </summary>
    /// <param name="site">The site's root address.</param>
    /// <param name="icon">The icon file's bytes.</param>
    /// <param name="cancellationToken">Stops the write before the stored icon is replaced.</param>
    /// <returns>A task that completes once the icon is stored.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    Task WriteAsync(Uri site, byte[] icon, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IWebsiteIconStore"/>: one file per site in an <c>icons</c> folder beside the launchpad's data.
/// </summary>
/// <remarks>
/// Files are named after a hash of the site's root, so any address — ports, international hosts — makes a valid
/// file name. An icon is fresh for a week after it was written. The folder is only a cache: deleting it costs one
/// request per site.
/// </remarks>
public sealed class WebsiteIconStore : IWebsiteIconStore
{
    /// <summary>
    /// The folder, under the data directory, holding the icons.
    /// </summary>
    internal const string FolderName = "icons";

    /// <summary>
    /// How long a stored icon is shown without asking the site again.
    /// </summary>
    internal static readonly TimeSpan FreshFor = TimeSpan.FromDays(7);

    private readonly IAppPaths _paths;
    private readonly TimeProvider _time;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="paths">Where the launchpad keeps its data.</param>
    /// <param name="time">Tells how old a stored icon is.</param>
    public WebsiteIconStore(IAppPaths paths, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);

        _paths = paths;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<StoredWebsiteIcon?> ReadAsync(Uri site, CancellationToken cancellationToken = default)
    {
        string path = PathOf(site);

        if (!File.Exists(path))
        {
            return null;
        }

        TimeSpan age = _time.GetUtcNow().UtcDateTime - File.GetLastWriteTimeUtc(path);
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);

        return new StoredWebsiteIcon(bytes, age < FreshFor);
    }

    /// <inheritdoc />
    public async Task WriteAsync(Uri site, byte[] icon, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(icon);

        string path = PathOf(site);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await AtomicFile.WriteAllBytesAsync(path, icon, cancellationToken);
    }

    /// <summary>
    /// Where a site's icon is stored.
    /// </summary>
    /// <param name="site">The site's root address.</param>
    /// <returns>The file's full path.</returns>
    internal string PathOf(Uri site)
    {
        ArgumentNullException.ThrowIfNull(site);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(site.AbsoluteUri));
        return Path.Combine(_paths.DataDirectory, FolderName, Convert.ToHexStringLower(hash.AsSpan(0, 16)) + ".icon");
    }
}
