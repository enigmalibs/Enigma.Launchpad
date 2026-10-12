using System;
using Enigma.Launchpad.Core.Validation;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Validation;

public sealed class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://example.com/path?q=1", "http://example.com/path?q=1")]
    [InlineData("  https://example.com  ", "https://example.com")]
    [InlineData("example.com", "https://example.com")]
    [InlineData("www.example.com/docs", "https://www.example.com/docs")]
    [InlineData("localhost:8080", "https://localhost:8080")]
    [InlineData("HTTPS://Example.com", "HTTPS://Example.com")]
    public void TryNormalize_AcceptsHttpAddresses(string input, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(input, out string normalized, out string? error));
        Assert.Equal(expected, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///C:/Windows")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("C:\\Windows")]
    [InlineData("vscode://file/x")]
    public void TryNormalize_RefusesOtherSchemes(string input)
    {
        Assert.False(UrlNormalizer.TryNormalize(input, out string normalized, out string? error));
        Assert.Equal(string.Empty, normalized);
        Assert.Equal(UrlNormalizer.UnsupportedSchemeMessage, error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_RefusesNothing(string? input)
    {
        Assert.False(UrlNormalizer.TryNormalize(input, out _, out string? error));
        Assert.Equal("Enter a URL.", error);
    }

    [Theory]
    [InlineData("https://")]
    [InlineData("http://exa mple.com")]
    public void TryNormalize_RefusesMalformedAddresses(string input)
    {
        Assert.False(UrlNormalizer.TryNormalize(input, out _, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_ReturnsTheAddress()
    {
        Assert.True(UrlNormalizer.TryParse("https://example.com/a b", out Uri? address, out _));
        Assert.Equal("https://example.com/a%20b", address?.AbsoluteUri);
    }
}
