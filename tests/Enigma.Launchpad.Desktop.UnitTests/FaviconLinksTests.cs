using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Launchpad.Desktop.Services;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class FaviconLinksTests
{
    private static readonly Uri Page = new("https://example.com/");

    [Theory]
    [InlineData("""<link rel="icon" href="/a.png">""")]
    [InlineData("""<link rel="shortcut icon" href="/a.png">""")]
    [InlineData("""<link rel='alternate icon' href='/a.png'>""")]
    [InlineData("""<link rel=icon href=/a.png>""")]
    [InlineData("""<LINK REL="ICON" HREF="/a.png" />""")]
    [InlineData("""<link href="/a.png" rel="apple-touch-icon">""")]
    [InlineData("""<link href="/a.png" rel="apple-touch-icon-precomposed">""")]
    [InlineData("<link\n  rel=\"icon\"\n  href=\"/a.png\"\n>")]
    public void AnIconLink_IsFound(string html)
        => Assert.Equal([new Uri("https://example.com/a.png")], FaviconLinks.Find(html, Page));

    [Theory]
    [InlineData("""<link rel="mask-icon" href="/a.png">""")]
    [InlineData("""<link rel="manifest" href="/site.webmanifest">""")]
    [InlineData("""<link rel="stylesheet" href="/a.css">""")]
    [InlineData("""<link rel="icon" type="image/svg+xml" href="/a">""")]
    [InlineData("""<link rel="icon" href="/favicon.svg">""")]
    [InlineData("""<link rel="icon" href="data:image/png;base64,iVBORw0KGgo=">""")]
    [InlineData("""<link rel="icon" href="javascript:alert(1)">""")]
    [InlineData("""<link rel="icon">""")]
    [InlineData("""<meta name="icon" content="/a.png">""")]
    public void AnythingElse_IsLeftOut(string html)
        => Assert.Empty(FaviconLinks.Find(html, Page));

    [Theory]
    [InlineData("a.png", "https://example.com/docs/a.png")]
    [InlineData("/a.png", "https://example.com/a.png")]
    [InlineData("//cdn.example.net/a.png", "https://cdn.example.net/a.png")]
    [InlineData("http://static.example.org/a.png", "http://static.example.org/a.png")]
    [InlineData("/a.png?v=2&amp;size=64", "https://example.com/a.png?v=2&size=64")]
    [InlineData("  /a.png  ", "https://example.com/a.png")]
    public void ALink_ResolvesAgainstThePage(string href, string expected)
        => Assert.Equal(
            [new Uri(expected)],
            FaviconLinks.Find($"""<link rel="icon" href="{href}">""", new Uri("https://example.com/docs/index.html")));

    [Fact]
    public void Icons_AreRanked_FromTheSmallestAtLeast48Pixels_ThenTheLargestBelow()
    {
        const string Html = """
            <link rel="icon" sizes="16x16" href="/16.png">
            <link rel="icon" sizes="512x512" href="/512.png">
            <link rel="icon" sizes="32x32" href="/32.png">
            <link rel="icon" sizes="64x64" href="/64.png">
            <link rel="apple-touch-icon" sizes="180x180" href="/180.png">
            """;

        Assert.Equal(["/64.png", "/180.png", "/512.png", "/32.png", "/16.png"], Paths(FaviconLinks.Find(Html, Page)));
    }

    [Fact]
    public void AnUnsizedTouchIcon_CountsAsLarge_AndAnUnsizedIcon_AsAClassicFavicon()
    {
        const string Html = """
            <link rel="icon" href="/favicon.png">
            <link rel="apple-touch-icon" href="/touch.png">
            <link rel="icon" sizes="16x16" href="/16.png">
            """;

        Assert.Equal(["/touch.png", "/favicon.png", "/16.png"], Paths(FaviconLinks.Find(Html, Page)));
    }

    [Fact]
    public void ALinkDeclaringSeveralSizes_CountsAsItsLargest()
    {
        const string Html = """
            <link rel="icon" sizes="16x16 32x32 48x48" href="/multi.ico">
            <link rel="icon" sizes="40x40" href="/40.png">
            """;

        Assert.Equal(["/multi.ico", "/40.png"], Paths(FaviconLinks.Find(Html, Page)));
    }

    [Fact]
    public void EqualIcons_KeepThePagesOrder_AndARepeatedAddressCountsOnce()
    {
        const string Html = """
            <link rel="icon" href="/b.png">
            <link rel="icon" href="/a.png">
            <link rel="shortcut icon" href="/b.png">
            """;

        Assert.Equal(["/b.png", "/a.png"], Paths(FaviconLinks.Find(Html, Page)));
    }

    [Theory]
    [InlineData("""<link rel="manifest" href="/site.webmanifest">""", "https://example.com/site.webmanifest")]
    [InlineData("""<link href="manifest.json" rel="MANIFEST">""", "https://example.com/docs/manifest.json")]
    [InlineData("""<link rel="manifest" href="https://cdn.example.net/m.json"><link rel="manifest" href="/second.json">""", "https://cdn.example.net/m.json")]
    public void AManifestLink_IsFound(string html, string expected)
        => Assert.Equal(new Uri(expected), FaviconLinks.FindManifest(html, new Uri("https://example.com/docs/index.html")));

    [Theory]
    [InlineData("""<link rel="icon" href="/a.png">""")]
    [InlineData("""<link rel="manifest">""")]
    [InlineData("""<link rel="manifest" href="data:application/json,{}">""")]
    public void NoUsableManifestLink_IsNone(string html)
        => Assert.Null(FaviconLinks.FindManifest(html, Page));

    [Fact]
    public void AManifestsIcons_AreRead_InOrder_WithTheirSizes()
    {
        const string Json = """
            {
              "name": "App",
              "icons": [
                { "src": "icons/192.png", "sizes": "192x192", "type": "image/png" },
                { "src": "/any.png", "sizes": "any" },
                { "src": "https://cdn.example.net/512.webp", "sizes": "256x256 512x512", "purpose": "maskable any" },
              ]
            }
            """;

        Assert.Equal(
            [
                (new Uri("https://example.com/assets/icons/192.png"), 192),
                (new Uri("https://example.com/any.png"), 192),
                (new Uri("https://cdn.example.net/512.webp"), 512),
            ],
            FaviconLinks.ReadManifest(Json, new Uri("https://example.com/assets/site.webmanifest")));
    }

    [Theory]
    [InlineData("""{ "icons": [ { "src": "/a.svg", "sizes": "any" } ] }""")]
    [InlineData("""{ "icons": [ { "src": "/a", "type": "image/svg+xml" } ] }""")]
    [InlineData("""{ "icons": [ { "src": "/a.png", "purpose": "monochrome" } ] }""")]
    [InlineData("""{ "icons": [ { "src": "javascript:alert(1)" } ] }""")]
    [InlineData("""{ "icons": [ { "sizes": "48x48" }, "a.png", 7 ] }""")]
    [InlineData("""{ "icons": {} }""")]
    [InlineData("""{ "name": "No icons" }""")]
    [InlineData("""[]""")]
    [InlineData("""<html>Not a manifest</html>""")]
    public void AManifestWithNoUsableIcon_GivesNone(string json)
        => Assert.Empty(FaviconLinks.ReadManifest(json, Page));

    [Fact]
    public void LinkedAndManifestIcons_AreRankedTogether()
    {
        IReadOnlyList<Uri> ranked = FaviconLinks.Rank(
        [
            (new Uri("https://example.com/32.png"), 32),
            (new Uri("https://example.com/192.png"), 192),
            (new Uri("https://example.com/64.png"), 64),
        ]);

        Assert.Equal(["/64.png", "/192.png", "/32.png"], Paths(ranked));
    }

    private static IEnumerable<string> Paths(IReadOnlyList<Uri> icons) => icons.Select(icon => icon.AbsolutePath).ToArray();
}
