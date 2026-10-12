using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Finds the icons a web page declares in its <c>&lt;link&gt;</c> tags, best first.
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

    /// <summary>
    /// Finds a page's icons.
    /// </summary>
    /// <param name="html">The page, or its beginning.</param>
    /// <param name="page">The page's address, which relative links resolve against.</param>
    /// <returns>The icons' addresses, best first: the smallest at least <see cref="PreferredSize"/> pixels, then
    /// the larger of those below it; page order breaks ties.</returns>
    internal static IReadOnlyList<Uri> Find(string html, Uri page)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(page);

        List<(Uri Address, int Size, int Order)> icons = [];

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

            if ((!isIcon && !isTouchIcon)
                || (attributes.TryGetValue("type", out string? type) && type.Contains("svg", StringComparison.OrdinalIgnoreCase))
                || !Uri.TryCreate(page, WebUtility.HtmlDecode(href).Trim(), out Uri? address)
                || ((address.Scheme != Uri.UriSchemeHttp) && (address.Scheme != Uri.UriSchemeHttps))
                || address.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int size = DeclaredSize(attributes) ?? (isIcon ? UnsizedIcon : UnsizedTouchIcon);
            icons.Add((address, size, icons.Count));
        }

        return icons
            .OrderBy(icon => icon.Size >= PreferredSize ? 0 : 1)
            .ThenBy(icon => icon.Size >= PreferredSize ? icon.Size : -icon.Size)
            .ThenBy(icon => icon.Order)
            .Select(icon => icon.Address)
            .Distinct()
            .ToArray();
    }

    // The largest of the sizes a link declares ("16x16 32x32"), or null when it declares none it can read.
    private static int? DeclaredSize(Dictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue("sizes", out string? sizes))
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
