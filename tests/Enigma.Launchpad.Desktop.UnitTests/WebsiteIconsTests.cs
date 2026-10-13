using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class WebsiteIconsTests
{
    private readonly HeadlessAvaloniaFixture _fixture;
    private static readonly Uri Site = new("https://example.com/");

    private readonly FakeWebsiteIconFetcher _fetcher = new();
    private readonly FakeWebsiteIconStore _store = new();
    private readonly RecordingLogger<WebsiteIcons> _logger = new();
    private readonly WebsiteIcons _icons;

    public WebsiteIconsTests(HeadlessAvaloniaFixture fixture)
    {
        _fixture = fixture;
        _icons = new WebsiteIcons(_fetcher, _store, _logger);
    }

    [Theory]
    [InlineData("https://example.com/docs/page?q=1#top", "https://example.com/")]
    [InlineData("https://user:secret@example.com/private", "https://example.com/")]
    [InlineData("http://example.com:8080/app", "http://example.com:8080/")]
    [InlineData("https://example.com:443/", "https://example.com/")]
    [InlineData("HTTPS://Example.COM/Path", "https://example.com/")]
    [InlineData("https://bücher.example/", "https://xn--bcher-kva.example/")]
    [InlineData("http://192.168.1.1/admin/reboot", "http://192.168.1.1/")]
    public void ASite_IsTheAddressRootOnly(string url, string expected)
        => Assert.Equal(expected, WebsiteIcons.SiteOf(url)?.AbsoluteUri);

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/Windows")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("not an address")]
    [InlineData("")]
    public async Task AnAddressThatIsNoWebsite_IsNeverFetched(string url)
    {
        Assert.Null(WebsiteIcons.SiteOf(url));
        Assert.Null(await _icons.GetAsync(url, TestContext.Current.CancellationToken));
        Assert.Empty(_fetcher.Requests);
    }

    [Fact]
    public async Task ManyAddressesOnOneSite_AreFetchedOnce_ThroughTheSiteRoot()
    {
        await _icons.GetAsync("https://example.com/a", TestContext.Current.CancellationToken);
        await _icons.GetAsync("https://example.com/b?page=2", TestContext.Current.CancellationToken);
        await _icons.GetAsync("https://EXAMPLE.com/", TestContext.Current.CancellationToken);

        Assert.Equal(["https://example.com/"], _fetcher.Requests.Select(site => site.AbsoluteUri));
    }

    [Fact]
    public async Task DifferentSites_AreFetchedApart()
    {
        await _icons.GetAsync("https://example.com/", TestContext.Current.CancellationToken);
        await _icons.GetAsync("http://example.com/", TestContext.Current.CancellationToken);
        await _icons.GetAsync("https://example.org/", TestContext.Current.CancellationToken);

        Assert.Equal(3, _fetcher.Requests.Count);
    }

    [Fact]
    public async Task ASiteWithoutAnIcon_IsAskedOnce()
    {
        Assert.Null(await _icons.GetAsync("https://example.com/", TestContext.Current.CancellationToken));
        Assert.Null(await _icons.GetAsync("https://example.com/", TestContext.Current.CancellationToken));

        Assert.Single(_fetcher.Requests);
    }

    [Fact]
    public void AnIcon_IsGivenAsAPng()
        => _fixture.RunAsync(async () =>
        {
            byte[] png = Png(32, 32);
            _fetcher.Answer = _ => png;

            byte[]? icon = await _icons.GetAsync("https://example.com/");

            Assert.Equal(new PixelSize(32, 32), SizeOf(icon));
        });

    [Fact]
    public void ALargerIcon_IsScaledDownToFit()
        => _fixture.RunAsync(async () =>
        {
            byte[] png = Png(96, 64);
            _fetcher.Answer = _ => png;

            byte[]? icon = await _icons.GetAsync("https://example.com/");

            Assert.Equal(new PixelSize(IconPng.MaxSize, 32), SizeOf(icon));
        });

    [Fact]
    public void BytesThatDoNotDecode_GiveNoIcon_AndAWarning()
        => _fixture.RunAsync(async () =>
        {
            _fetcher.Answer = _ => ImageBytes.Png(1, 2, 3, 4);

            Assert.Null(await _icons.GetAsync("https://example.com/"));
            Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Entries).Level);
        });

    [Fact]
    public async Task AFailingFetch_GivesNoIcon_AndAWarning()
    {
        InvalidOperationException failure = new("Broken.");
        _fetcher.Answer = _ => throw failure;

        Assert.Null(await _icons.GetAsync("https://example.com/", TestContext.Current.CancellationToken));

        (LogLevel level, string message, Exception? exception) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("https://example.com/", message, StringComparison.Ordinal);
        Assert.Same(failure, exception);
    }

    [Fact]
    public void AFreshStoredIcon_IsShown_WithoutAskingTheSite()
        => _fixture.RunAsync(async () =>
        {
            _store.Entries[Site] = new StoredWebsiteIcon(Png(32, 32), IsFresh: true);

            byte[]? icon = await _icons.GetAsync("https://example.com/");

            Assert.Equal(new PixelSize(32, 32), SizeOf(icon));
            Assert.Empty(_fetcher.Requests);
        });

    [Fact]
    public void AFetchedIcon_IsStored()
        => _fixture.RunAsync(async () =>
        {
            byte[] png = Png(32, 32);
            _fetcher.Answer = _ => png;

            Assert.NotNull(await _icons.GetAsync("https://example.com/"));

            Assert.Same(png, _store.Entries[Site].Bytes);
        });

    [Fact]
    public void AStaleStoredIcon_IsFetchedAgain_AndReplaced()
        => _fixture.RunAsync(async () =>
        {
            byte[] newer = Png(32, 32);
            _store.Entries[Site] = new StoredWebsiteIcon(Png(16, 16), IsFresh: false);
            _fetcher.Answer = _ => newer;

            byte[]? icon = await _icons.GetAsync("https://example.com/");

            Assert.Equal(new PixelSize(32, 32), SizeOf(icon));
            Assert.Same(newer, _store.Entries[Site].Bytes);
        });

    [Fact]
    public void AStaleStoredIcon_IsShownStill_WhenTheSiteHasNothing()
        => _fixture.RunAsync(async () =>
        {
            _store.Entries[Site] = new StoredWebsiteIcon(Png(16, 16), IsFresh: false);

            byte[]? icon = await _icons.GetAsync("https://example.com/");

            Assert.Equal(new PixelSize(16, 16), SizeOf(icon));
            Assert.Single(_fetcher.Requests);
        });

    [Fact]
    public void AStoredIconThatDoesNotDecode_IsFetchedAgain()
        => _fixture.RunAsync(async () =>
        {
            _store.Entries[Site] = new StoredWebsiteIcon(ImageBytes.Png(1, 2, 3), IsFresh: true);
            _fetcher.Answer = _ => Png(32, 32);

            Assert.NotNull(await _icons.GetAsync("https://example.com/"));

            Assert.Single(_fetcher.Requests);
            Assert.DoesNotContain(_logger.Entries, entry => entry.Level >= LogLevel.Warning);
        });

    [Fact]
    public void AStoreThatCannotRead_LeavesTheSiteToAnswer()
        => _fixture.RunAsync(async () =>
        {
            _store.ReadFailure = new IOException("Locked.");
            _fetcher.Answer = _ => Png(32, 32);

            Assert.NotNull(await _icons.GetAsync("https://example.com/"));
        });

    [Fact]
    public void AStoreThatCannotWrite_StillShowsTheIcon_AndWarns()
        => _fixture.RunAsync(async () =>
        {
            IOException failure = new("Disk full.");
            _store.WriteFailure = failure;
            _fetcher.Answer = _ => Png(32, 32);

            Assert.NotNull(await _icons.GetAsync("https://example.com/"));

            Assert.Same(failure, Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning).Exception);
        });

    [Fact]
    public void ARefresh_AsksTheSite_EvenWithAFreshStoredIcon_AndStoresTheAnswer()
        => _fixture.RunAsync(async () =>
        {
            byte[] newer = Png(32, 32);
            _store.Entries[Site] = new StoredWebsiteIcon(Png(16, 16), IsFresh: true);
            Assert.Equal(new PixelSize(16, 16), SizeOf(await _icons.GetAsync("https://example.com/")));
            _fetcher.Answer = _ => newer;

            byte[]? icon = await _icons.RefreshAsync("https://example.com/page");

            Assert.Equal(new PixelSize(32, 32), SizeOf(icon));
            Assert.Single(_fetcher.Requests);
            Assert.Same(newer, _store.Entries[Site].Bytes);
            Assert.Equal(new PixelSize(32, 32), SizeOf(await _icons.GetAsync("https://example.com/")));
        });

    [Fact]
    public void ARefreshThatBringsNothing_StillAnswersTheStoredIcon()
        => _fixture.RunAsync(async () =>
        {
            _store.Entries[Site] = new StoredWebsiteIcon(Png(16, 16), IsFresh: true);

            byte[]? icon = await _icons.RefreshAsync("https://example.com/");

            Assert.Equal(new PixelSize(16, 16), SizeOf(icon));
            Assert.Single(_fetcher.Requests);
        });

    [Fact]
    public async Task ARefreshOfAnAddressThatIsNoWebsite_FetchesNothing()
    {
        Assert.Null(await _icons.RefreshAsync("ftp://example.com/file", TestContext.Current.CancellationToken));
        Assert.Empty(_fetcher.Requests);
    }

    [Fact]
    public void AnIconFetchedOnce_IsShownAfterARestart_WithoutAskingTheSite()
        => _fixture.RunAsync(async () =>
        {
            using TempDirectory data = new();
            WebsiteIconStore disk = new(new AppPaths(data.Path), TimeProvider.System);
            _fetcher.Answer = _ => Png(32, 32);
            Assert.NotNull(await new WebsiteIcons(_fetcher, disk, _logger).GetAsync("https://example.com/page"));

            FakeWebsiteIconFetcher offline = new();
            byte[]? icon = await new WebsiteIcons(offline, disk, _logger).GetAsync("https://example.com/other");

            Assert.Equal(new PixelSize(32, 32), SizeOf(icon));
            Assert.Empty(offline.Requests);
        });

    private static byte[] Png(int width, int height) => TestImages.Png(width, height);

    private static PixelSize SizeOf(byte[]? icon) => TestImages.SizeOf(Assert.IsType<byte[]>(icon));
}
