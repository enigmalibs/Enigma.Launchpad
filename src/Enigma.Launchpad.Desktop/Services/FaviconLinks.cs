using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Finds the icons a web page declares — in its <c>&lt;link&gt;</c> tags, and in the web app manifest it links —
/// best first.
/// </summary>
/// <remarks>
/// A small scanner rather than an HTML parser: icon links are plain tags in a page's head, and one it misses only
/// costs a fallback to <c>/favicon.ico</c>. SVG icons are left out, since Avalonia cannot draw them without a
/// renderer of its own.
/// </remarks>
internal static partial class FaviconLinks
{
    /// <summary>
    /// The size an icon is wanted at, in pixels: twice the tile's 24, so it stays sharp up to 200 % scaling.
    /// </summary>
    internal const int PreferredSize = 48;

    // What an icon declaring no size usually is: a classic favicon, and an iOS home-screen icon.
    private const int UnsizedIcon = 32;
    private const int UnsizedTouchIcon = 180;

    // A manifest icon declaring no size it can read is taken for an app icon, which is large.
    private const int UnsizedManifestIcon = 192;

    /// <summary>
    /// Finds a page's icons.
    /// </summary>
    /// <param name="html">The page, or its beginning.</param>
    /// <param name="page">The page's address, which relative links resolve against.</param>
    /// <returns>The icons' addresses, best first: the smallest at least <see cref="PreferredSize"/> pixels, then
    /// the larger of those below it; page order breaks ties.</returns>
    internal static IReadOnlyList<Uri> Find(string html, Uri page) => Rank(FindLinked(html, page));

    /// <summary>
    /// Finds the icons a page links, in page order.
    /// </summary>
    /// <param name="html">The page, or its beginning.</param>
    /// <param name="page">The page's address, which relative links resolve against.</param>
    /// <returns>Each icon's address and size: the largest it declares, or a typical size for its kind.</returns>
    internal static IReadOnlyList<(Uri Address, int Size)> FindLinked(string html, Uri page)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(page);

        List<(Uri Address, int Size)> icons = [];

        foreach (Match tag in LinkTag().Matches(html))
        {
            Dictionary<string, string> attributes = ReadAttributes(tag.Value);

            if (!attributes.TryGetValue("rel", out string? rel) || !attributes.TryGetValue("href", out string? href))
            {
                continue;
            }

            string[] relations = rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            bool isIcon = relations.Contains("icon", StringComparer.OrdinalIgnoreCase);
            bool isTouchIcon = relations.Any(relation => relation.StartsWith("apple-touch-icon", StringComparison.OrdinalIgnoreCase));

            attributes.TryGetValue("type", out string? type);

            if ((!isIcon && !isTouchIcon) || (TryReadImageAddress(page, WebUtility.HtmlDecode(href), type) is not { } address))
            {
                continue;
            }

            attributes.TryGetValue("sizes", out string? sizes);
            icons.Add((address, LargestSize(sizes) ?? (isIcon ? UnsizedIcon : UnsizedTouchIcon)));
        }

        return icons;
    }

    /// <summary>
    /// Finds the web app manifest a page links.
    /// </summary>
    /// <param name="html">The page, or its beginning.</param>
    /// <param name="page">The page's address, which a relative link resolves against.</param>
    /// <returns>The manifest's address, or <see langword="null"/> when the page links none over http or https.</returns>
    internal static Uri? FindManifest(string html, Uri page)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(page);

        foreach (Match tag in LinkTag().Matches(html))
        {
            Dictionary<string, string> attributes = ReadAttributes(tag.Value);

            if (attributes.TryGetValue("rel", out string? rel)
                && rel.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains("manifest", StringComparer.OrdinalIgnoreCase)
                && attributes.TryGetValue("href", out string? href)
                && Uri.TryCreate(page, WebUtility.HtmlDecode(href).Trim(), out Uri? address)
                && IsWebAddress(address))
            {
                return address;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the icons a web app manifest declares, in its order.
    /// </summary>
    /// <param name="json">The manifest.</param>
    /// <param name="manifest">Its address, which relative sources resolve against.</param>
    /// <returns>Each icon's address and size; none when the manifest cannot be read.</returns>
    /// <remarks>An icon meant only as a monochrome silhouette is left out: it is drawn in one colour by the system
    /// using it, and would show as a block here.</remarks>
    internal static IReadOnlyList<(Uri Address, int Size)> ReadManifest(string json, Uri manifest)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(manifest);

        List<(Uri Address, int Size)> icons = [];

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });

            if ((document.RootElement.ValueKind != JsonValueKind.Object)
                || !document.RootElement.TryGetProperty("icons", out JsonElement list)
                || (list.ValueKind != JsonValueKind.Array))
            {
                return icons;
            }

            foreach (JsonElement icon in list.EnumerateArray())
            {
                if ((icon.ValueKind == JsonValueKind.Object)
                    && (Text(icon, "src") is { } source)
                    && !IsMonochromeOnly(Text(icon, "purpose"))
                    && (TryReadImageAddress(manifest, source, Text(icon, "type")) is { } address))
                {
                    icons.Add((address, LargestSize(Text(icon, "sizes")) ?? UnsizedManifestIcon));
                }
            }
        }
        catch (JsonException)
        {
            // A manifest that is not JSON has no icons to offer; the page's links and /favicon.ico remain.
        }

        return icons;
    }

    /// <summary>
    /// Ranks icons, best first: the smallest at least <see cref="PreferredSize"/> pixels, then the larger of those
    /// below it; the order given breaks ties, and an address given twice counts once.
    /// </summary>
    /// <param name="icons">The icons and their sizes.</param>
    /// <returns>Their addresses, ranked.</returns>
    internal static IReadOnlyList<Uri> Rank(IEnumerable<(Uri Address, int Size)> icons)
        => icons
            .Select((icon, order) => (icon.Address, icon.Size, Order: order))
            .OrderBy(icon => icon.Size >= PreferredSize ? 0 : 1)
            .ThenBy(icon => icon.Size >= PreferredSize ? icon.Size : -icon.Size)
            .ThenBy(icon => icon.Order)
            .Select(icon => icon.Address)
            .Distinct()
            .ToArray();

    // An http or https address that is not an SVG image, by its declared type or its extension.
    private static Uri? TryReadImageAddress(Uri baseAddress, string reference, string? type)
        => ((type is null) || !type.Contains("svg", StringComparison.OrdinalIgnoreCase))
            && Uri.TryCreate(baseAddress, reference.Trim(), out Uri? address)
            && IsWebAddress(address)
            && !address.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? address
                : null;

    private static bool IsWebAddress(Uri address) => (address.Scheme == Uri.UriSchemeHttp) || (address.Scheme == Uri.UriSchemeHttps);

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && (value.ValueKind == JsonValueKind.String) ? value.GetString() : null;

    private static bool IsMonochromeOnly(string? purpose)
    {
        string[] purposes = (purpose ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return (purposes.Length > 0) && purposes.All(value => value.Equals("monochrome", StringComparison.OrdinalIgnoreCase));
    }

    // The largest of the sizes declared ("16x16 32x32"), or null when none can be read — "any" included.
    private static int? LargestSize(string? sizes)
    {
        if (sizes is null)
        {
            return null;
        }

        int? largest = null;

        foreach (string token in sizes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] sides = token.Split('x', 'X');

            if ((sides.Length == 2)
                && int.TryParse(sides[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
                && int.TryParse(sides[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height))
            {
                largest = Math.Max(largest ?? 0, Math.Max(width, height));
            }
        }

        return largest;
    }

    // HTML ignores every repeat of an attribute, so the first one wins.
    private static Dictionary<string, string> ReadAttributes(string tag)
    {
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match attribute in Attribute().Matches(tag))
        {
            string value = attribute.Groups[2].Success
                ? attribute.Groups[2].Value
                : attribute.Groups[3].Success ? attribute.Groups[3].Value : attribute.Groups[4].Value;

            attributes.TryAdd(attribute.Groups[1].Value, value);
        }

        return attributes;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"([^\s""'<>/=]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'=<>`]+))", RegexOptions.CultureInvariant)]
    private static partial Regex Attribute();
}
