using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Downloads a website's icon. A seam of its own so the policy around it — which sites are asked, caching,
/// decoding — can be tested without the network.
/// </summary>
public interface IWebsiteIconFetcher
{
    /// <summary>
    /// Downloads the best icon a website offers.
    /// </summary>
    /// <param name="site">The site's root address (<c>https://host/</c>); nothing else of a shortcut is ever sent.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>The icon file's bytes — PNG, ICO, GIF, JPEG, WebP or BMP — or <see langword="null"/> when the site
    /// cannot be reached or offers none.</returns>
    Task<byte[]?> FetchAsync(Uri site, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IWebsiteIconFetcher"/>: asks the site itself, the way a browser finds a favicon.
/// </summary>
/// <remarks>
/// <para>
/// The site's home page is read for the icons it links; failing those, <c>/favicon.ico</c> is tried. The first
/// candidate that is really an image wins: servers often answer a missing icon with an HTML page and a success
/// status, so the bytes' signature is checked rather than the declared content type.
/// </para>
/// <para>
/// Every request is bounded — in time, in size, in redirects — and carries no cookie and no credential. A site
/// that cannot be reached at all is not asked again for <c>/favicon.ico</c>.
/// </para>
/// </remarks>
public sealed class WebsiteIconFetcher : IWebsiteIconFetcher, IDisposable
{
    /// <summary>
    /// How much of the home page is read; icon links sit in its head.
    /// </summary>
    internal const int MaxPageBytes = 256 * 1024;

    /// <summary>
    /// The largest icon file accepted.
    /// </summary>
    internal const int MaxIconBytes = 1024 * 1024;

    /// <summary>
    /// How many of the page's icon links are tried before <c>/favicon.ico</c>.
    /// </summary>
    internal const int MaxLinkedIcons = 3;

    private readonly HttpClient _http;
    private readonly ILogger<WebsiteIconFetcher> _logger;

    /// <summary>
    /// Initialises a new instance with its own connection pool.
    /// </summary>
    /// <param name="logger">Where failures are reported.</param>
    public WebsiteIconFetcher(ILogger<WebsiteIconFetcher> logger)
        : this(CreateHandler(), logger)
    {
    }

    /// <summary>
    /// Initialises a new instance over a given handler, which it then owns.
    /// </summary>
    /// <param name="handler">Sends the requests.</param>
    /// <param name="logger">Where failures are reported.</param>
    internal WebsiteIconFetcher(HttpMessageHandler handler, ILogger<WebsiteIconFetcher> logger)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(logger);

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Enigma.Launchpad", ProductVersion));
        _logger = logger;
    }

    /// <summary>
    /// Gets the application's version, as the User-Agent names it.
    /// </summary>
    internal static string ProductVersion
        => typeof(WebsiteIconFetcher).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <inheritdoc />
    public async Task<byte[]?> FetchAsync(Uri site, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(site);

        List<Uri> candidates = [];
        Uri page = site;

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, site);
            request.Headers.Accept.ParseAdd("text/html");
            using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Links and /favicon.ico resolve against where the redirects ended, not where they started.
            page = response.RequestMessage?.RequestUri ?? site;

            if (response.IsSuccessStatusCode && IsHtml(response.Content.Headers.ContentType))
            {
                byte[] html = await ReadUpToAsync(response.Content, MaxPageBytes, cancellationToken);
                string text = Encoding.UTF8.GetString(html, 0, Math.Min(html.Length, MaxPageBytes));
                candidates.AddRange(FaviconLinks.Find(text, page).Take(MaxLinkedIcons));
            }
        }
        catch (Exception exception) when (IsNetworkFailure(exception, cancellationToken))
        {
            _logger.LogDebug(exception, "The site {Site} could not be reached for its icon", site);
            return null;
        }

        Uri favicon = new(page, "/favicon.ico");

        if (!candidates.Contains(favicon))
        {
            candidates.Add(favicon);
        }

        foreach (Uri candidate in candidates)
        {
            if (await TryDownloadIconAsync(candidate, cancellationToken) is { } icon)
            {
                return icon;
            }
        }

        _logger.LogDebug("The site {Site} offers no icon that can be shown", site);
        return null;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// The connection settings icons are fetched with.
    /// </summary>
    /// <returns>A handler sending no cookie, following few redirects, giving up quickly on a dead host.</returns>
    internal static SocketsHttpHandler CreateHandler()
        => new()
        {
            UseCookies = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),

            // A long-lived client must still notice DNS changes.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

    /// <summary>
    /// Whether bytes start like an image file Avalonia can decode.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns><see langword="true"/> for PNG, ICO, GIF, JPEG, WebP and BMP.</returns>
    internal static bool IsImage(ReadOnlySpan<byte> bytes)
        => bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
            || bytes.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0x01, 0x00])
            || bytes.StartsWith("GIF8"u8)
            || bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])
            || (bytes.StartsWith("RIFF"u8) && (bytes.Length >= 12) && bytes[8..12].SequenceEqual("WEBP"u8))
            || bytes.StartsWith("BM"u8);

    private static bool IsHtml(MediaTypeHeaderValue? contentType)
        => contentType?.MediaType is { } mediaType
            && (mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)
                || mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase));

    // A timeout surfaces as a cancellation the caller did not ask for.
    private static bool IsNetworkFailure(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException or IOException
            || ((exception is OperationCanceledException) && !cancellationToken.IsCancellationRequested);

    // Reads until the end or just past a limit, so an oversized body is never held whole.
    private static async Task<byte[]> ReadUpToAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream copy = new();
        byte[] chunk = new byte[16 * 1024];
        int read;

        while ((copy.Length <= limit) && ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0))
        {
            copy.Write(chunk, 0, read);
        }

        return copy.ToArray();
    }

    private async Task<byte[]?> TryDownloadIconAsync(Uri address, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, address);
            request.Headers.Accept.ParseAdd("image/*");
            using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode || (response.Content.Headers.ContentLength > MaxIconBytes))
            {
                return null;
            }

            byte[] icon = await ReadUpToAsync(response.Content, MaxIconBytes, cancellationToken);

            return (icon.Length <= MaxIconBytes) && IsImage(icon) ? icon : null;
        }
        catch (Exception exception) when (IsNetworkFailure(exception, cancellationToken))
        {
            _logger.LogDebug(exception, "The icon {Address} could not be downloaded", address);
            return null;
        }
    }
}
