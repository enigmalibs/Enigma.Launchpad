using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WebsiteIconFetcherTests : IDisposable
{
    private const string Site = "https://example.com/";

    private readonly StubHttpHandler _http = new();
    private readonly RecordingLogger<WebsiteIconFetcher> _logger = new();
    private readonly WebsiteIconFetcher _fetcher;

    public WebsiteIconFetcherTests() => _fetcher = new WebsiteIconFetcher(_http, _logger);

    public void Dispose() => _fetcher.Dispose();

    [Fact]
    public async Task TheBestLinkedIcon_IsDownloaded()
    {
        byte[] large = ImageBytes.Png(64);
        _http.Html(Site, """<link rel="icon" sizes="16x16" href="/16.png"><link rel="icon" sizes="64x64" href="/64.png">""");
        _http.Answer("https://example.com/16.png", ImageBytes.Png(16), "image/png");
        _http.Answer("https://example.com/64.png", large, "image/png");

        Assert.Equal(large, await FetchAsync());
        Assert.Equal([Site, "https://example.com/64.png"], RequestedAddresses());
    }

    [Fact]
    public async Task ALinkThatFails_GivesWayToTheNext_ThenToFaviconIco()
    {
        byte[] favicon = ImageBytes.Ico(1);
        _http.Html(Site, """<link rel="icon" href="/missing.png"><link rel="icon" href="/broken.png">""");
        _http.Answer("https://example.com/broken.png", Encoding.UTF8.GetBytes("<html>"), "image/png");
        _http.Answer("https://example.com/favicon.ico", favicon, "image/x-icon");

        Assert.Equal(favicon, await FetchAsync());
        Assert.Equal(
            [Site, "https://example.com/missing.png", "https://example.com/broken.png", "https://example.com/favicon.ico"],
            RequestedAddresses());
    }

    [Theory]
    [InlineData("<html><head><title>No icon</title></head></html>", "text/html", HttpStatusCode.OK)]
    [InlineData("{}", "application/json", HttpStatusCode.OK)]
    [InlineData("Not found", "text/html", HttpStatusCode.NotFound)]
    public async Task WithoutALink_FaviconIcoIsTried(string page, string mediaType, HttpStatusCode status)
    {
        byte[] favicon = ImageBytes.Ico(1);
        _http.Answer(Site, Encoding.UTF8.GetBytes(page), mediaType, status);
        _http.Answer("https://example.com/favicon.ico", favicon, "image/vnd.microsoft.icon");

        Assert.Equal(favicon, await FetchAsync());
    }

    [Fact]
    public async Task AnHtmlPageServedAsTheFavicon_IsNoIcon()
    {
        _http.Html(Site, "<html></html>");
        _http.Html("https://example.com/favicon.ico", "<html>Page not found</html>");

        Assert.Null(await FetchAsync());
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task OnlyTheFirstThreeLinks_AreTried_BeforeFaviconIco()
    {
        _http.Html(Site, string.Concat(Enumerable.Range(1, 5).Select(index => $"""<link rel="icon" href="/{index}.png">""")));

        Assert.Null(await FetchAsync());
        Assert.Equal(
            [Site, "https://example.com/1.png", "https://example.com/2.png", "https://example.com/3.png", "https://example.com/favicon.ico"],
            RequestedAddresses());
    }

    [Fact]
    public async Task AnAnnouncedOversizedIcon_IsRefused()
    {
        _http.Html(Site, "<html></html>");
        _http.Answer("https://example.com/favicon.ico", ImageBytes.Ico(new byte[WebsiteIconFetcher.MaxIconBytes]), "image/x-icon");

        Assert.Null(await FetchAsync());
    }

    [Fact]
    public async Task AnUnannouncedOversizedIcon_IsRefused()
    {
        _http.Html(Site, "<html></html>");
        _http.Answer(
            "https://example.com/favicon.ico",
            _ => StubHttpHandler.UnannouncedResponse(ImageBytes.Ico(new byte[WebsiteIconFetcher.MaxIconBytes]), "image/x-icon"));

        Assert.Null(await FetchAsync());
    }

    [Fact]
    public async Task AnIconLinkedFarIntoALongPage_IsBeyondWhatIsRead()
    {
        string filler = new(' ', WebsiteIconFetcher.MaxPageBytes);
        _http.Html(Site, $"""<html>{filler}<link rel="icon" href="/late.png"></html>""");
        _http.Answer("https://example.com/late.png", ImageBytes.Png(1), "image/png");

        Assert.Null(await FetchAsync());
        Assert.DoesNotContain("https://example.com/late.png", RequestedAddresses());
    }

    [Fact]
    public async Task AnUnreachableSite_IsAskedOnce()
    {
        _http.Fail(Site, new HttpRequestException("No such host is known."));

        Assert.Null(await FetchAsync());
        Assert.Equal([Site], RequestedAddresses());
        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public async Task ASiteThatTimesOut_IsAskedOnce()
    {
        _http.Fail(Site, new TaskCanceledException("The request timed out."));

        Assert.Null(await FetchAsync());
        Assert.Equal([Site], RequestedAddresses());
    }

    [Fact]
    public async Task TheCallersCancellation_IsHonoured()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _fetcher.FetchAsync(new Uri(Site), cancellation.Token));
    }

    [Fact]
    public async Task LinksAndFaviconIco_ResolveAgainstWhereTheRedirectsEnded()
    {
        byte[] favicon = ImageBytes.Ico(1);
        _http.Answer("http://example.com/", request =>
        {
            // What the redirect handler leaves behind once it has followed the site to its new address.
            request.RequestUri = new Uri("https://www.example.com/");
            return StubHttpHandler.Response(Encoding.UTF8.GetBytes("<html></html>"), "text/html");
        });
        _http.Answer("https://www.example.com/favicon.ico", favicon, "image/x-icon");

        Assert.Equal(favicon, await _fetcher.FetchAsync(new Uri("http://example.com/"), TestContext.Current.CancellationToken));
        Assert.Equal("https://www.example.com/favicon.ico", RequestedAddresses().Last());
    }

    [Fact]
    public async Task Requests_SayWhoIsAsking_AndWhatTheyWant()
    {
        _http.Html(Site, """<link rel="icon" href="/a.png">""");
        _http.Answer("https://example.com/a.png", ImageBytes.Png(1), "image/png");

        await FetchAsync();

        (Uri _, string pageAccept, string userAgent) = _http.Requests.First();
        (Uri _, string iconAccept, _) = _http.Requests.Last();
        Assert.Equal("text/html", pageAccept);
        Assert.Equal("image/*", iconAccept);
        Assert.Equal($"Enigma.Launchpad/{WebsiteIconFetcher.ProductVersion}", userAgent);
    }

    [Fact]
    public void TheConnection_SendsNoCookie_AndGivesUpQuickly()
    {
        using SocketsHttpHandler handler = WebsiteIconFetcher.CreateHandler();

        Assert.False(handler.UseCookies);
        Assert.Null(handler.Credentials);
        Assert.Equal(5, handler.MaxAutomaticRedirections);
        Assert.Equal(TimeSpan.FromSeconds(5), handler.ConnectTimeout);
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, true)]
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01 }, true)]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, true)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }, true)]
    [InlineData(new byte[] { 0x42, 0x4D, 0x00 }, true)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 }, false)]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, false)]
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C }, false)]
    [InlineData(new byte[0], false)]
    public void OnlyTheSignaturesOfDecodableImages_AreAccepted(byte[] bytes, bool expected)
        => Assert.Equal(expected, WebsiteIconFetcher.IsImage(bytes));

    private Task<byte[]?> FetchAsync() => _fetcher.FetchAsync(new Uri(Site), TestContext.Current.CancellationToken);

    private string[] RequestedAddresses() => _http.Requests.Select(request => request.Address.AbsoluteUri).ToArray();
}
