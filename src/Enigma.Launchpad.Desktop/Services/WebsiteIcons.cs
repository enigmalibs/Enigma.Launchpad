using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// The icons of websites, for URL tiles.
/// </summary>
public interface IWebsiteIcons
{
    /// <summary>
    /// Gets the icon of the website an address belongs to.
    /// </summary>
    /// <param name="url">The address.</param>
    /// <param name="cancellationToken">Stops this wait only: the icon is still fetched for whoever asks next.</param>
    /// <returns>The icon as a PNG file no larger than <see cref="IconPng.MaxSize"/>, or <see langword="null"/> when
    /// there is none to show.</returns>
    Task<byte[]?> GetAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IWebsiteIcons"/>: looks each site's icon up once, off the UI thread, and keeps it — or the
/// fact that it has none — for the rest of the session.
/// </summary>
/// <remarks>
/// <para>
/// Only a site's root is ever asked for, never the address itself. A shortcut can come from someone else's
/// export file, and fetching its own path on its own — <c>…/logout</c>, a router's <c>…/reboot</c> — could do
/// something merely because a page was shown. Opening a link is the user's choice; drawing its tile is not.
/// </para>
/// <para>
/// An icon stored less than a week ago is shown without asking the site. An older one is fetched again; when
/// that brings nothing — offline, or the site has dropped its icon — the stored one is shown still.
/// </para>
/// </remarks>
public sealed class WebsiteIcons : IWebsiteIcons
{
    private static readonly Task<byte[]?> NoIcon = Task.FromResult<byte[]?>(null);

    private readonly IWebsiteIconFetcher _fetcher;
    private readonly IWebsiteIconStore _store;
    private readonly ILogger<WebsiteIcons> _logger;
    private readonly object _sync = new();
    private readonly Dictionary<Uri, Task<byte[]?>> _icons = [];

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="fetcher">Downloads icons from the sites.</param>
    /// <param name="store">Keeps icons between sessions.</param>
    /// <param name="logger">Where unexpected failures are reported.</param>
    public WebsiteIcons(IWebsiteIconFetcher fetcher, IWebsiteIconStore store, ILogger<WebsiteIcons> logger)
    {
        ArgumentNullException.ThrowIfNull(fetcher);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _fetcher = fetcher;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (SiteOf(url) is not { } site)
        {
            return NoIcon;
        }

        lock (_sync)
        {
            if (!_icons.TryGetValue(site, out Task<byte[]?>? icon))
            {
                // The fetch is shared by every caller, so no caller's token may cancel it.
                icon = Task.Run(() => LoadAsync(site), CancellationToken.None);
                _icons.Add(site, icon);
            }

            return icon.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// The root of the site an address belongs to: its scheme, host and port, and nothing else — no path, no
    /// query, no user name or password.
    /// </summary>
    /// <param name="url">The address.</param>
    /// <returns>The site's root, or <see langword="null"/> for anything but an http or https address.</returns>
    internal static Uri? SiteOf(string url)
        => UrlNormalizer.TryParse(url, out Uri? address, out _) && (address is not null)
            ? new UriBuilder(address.Scheme, address.IdnHost, address.Port).Uri
            : null;

    // An icon is decoration: whatever goes wrong at any step, the tile keeps its glyph, so no step throws.
    private async Task<byte[]?> LoadAsync(Uri site)
    {
        StoredWebsiteIcon? stored = await ReadStoredAsync(site);
        byte[]? previous = (stored is null) ? null : TryNormalize(stored.Bytes, site, LogLevel.Debug);

        if ((stored is { IsFresh: true }) && (previous is not null))
        {
            return previous;
        }

        if ((await FetchAsync(site) is { } fetched) && (TryNormalize(fetched, site, LogLevel.Warning) is { } icon))
        {
            await StoreAsync(site, fetched);
            return icon;
        }

        // Offline, or the site offers nothing now: the last icon it had beats the glyph.
        return previous;
    }

    private async Task<StoredWebsiteIcon?> ReadStoredAsync(Uri site)
    {
        try
        {
            return await _store.ReadAsync(site);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "The stored icon of {Site} could not be read", site);
            return null;
        }
    }

    private async Task<byte[]?> FetchAsync(Uri site)
    {
        try
        {
            return await _fetcher.FetchAsync(site);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Reading the icon of {Site} failed", site);
            return null;
        }
    }

    private async Task StoreAsync(Uri site, byte[] icon)
    {
        try
        {
            await _store.WriteAsync(site, icon);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The icon of {Site} could not be stored", site);
        }
    }

    private byte[]? TryNormalize(byte[] bytes, Uri site, LogLevel failureLevel)
    {
        try
        {
            return IconPng.Normalize(bytes);
        }
        catch (Exception exception)
        {
            _logger.Log(failureLevel, exception, "The icon of {Site} could not be decoded", site);
            return null;
        }
    }
}
