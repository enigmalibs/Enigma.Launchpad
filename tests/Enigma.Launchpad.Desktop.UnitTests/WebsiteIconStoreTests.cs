using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WebsiteIconStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Uri Site = new("https://example.com/");

    private readonly TempDirectory _temp = new();
    private readonly WebsiteIconStore _store;

    public WebsiteIconStoreTests() => _store = new WebsiteIconStore(new AppPaths(_temp.Path), new FixedTimeProvider(Now));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task ASiteNeverStored_HasNothing()
        => Assert.Null(await _store.ReadAsync(Site, TestContext.Current.CancellationToken));

    [Fact]
    public async Task AStoredIcon_ReadsBack()
    {
        await _store.WriteAsync(Site, [1, 2, 3], TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(_store.PathOf(Site), Now.UtcDateTime);

        StoredWebsiteIcon? stored = await _store.ReadAsync(Site, TestContext.Current.CancellationToken);

        Assert.NotNull(stored);
        Assert.Equal([1, 2, 3], stored.Bytes);
        Assert.True(stored.IsFresh);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public async Task AnIcon_StaysFreshForAWeek(int minutesPastAWeek, bool fresh)
    {
        await _store.WriteAsync(Site, [1], TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(_store.PathOf(Site), (Now - WebsiteIconStore.FreshFor - TimeSpan.FromMinutes(minutesPastAWeek)).UtcDateTime);

        StoredWebsiteIcon? stored = await _store.ReadAsync(Site, TestContext.Current.CancellationToken);

        Assert.Equal(fresh, stored?.IsFresh);
        Assert.Equal([1], stored?.Bytes);
    }

    [Fact]
    public async Task StoringAgain_ReplacesTheIcon()
    {
        await _store.WriteAsync(Site, [1, 2, 3], TestContext.Current.CancellationToken);
        await _store.WriteAsync(Site, [4], TestContext.Current.CancellationToken);

        Assert.Equal([4], (await _store.ReadAsync(Site, TestContext.Current.CancellationToken))?.Bytes);
    }

    [Fact]
    public async Task EachSite_HasItsOwnFile_InTheIconsFolder()
    {
        Uri plain = new("http://example.com/");
        Uri other = new("https://example.org:8443/");

        await _store.WriteAsync(Site, [1], TestContext.Current.CancellationToken);
        await _store.WriteAsync(plain, [2], TestContext.Current.CancellationToken);
        await _store.WriteAsync(other, [3], TestContext.Current.CancellationToken);

        string folder = Path.Combine(_temp.Path, WebsiteIconStore.FolderName);
        Assert.Equal(3, Directory.GetFiles(folder).Length);
        Assert.All([Site, plain, other], site => Assert.Equal(folder, Path.GetDirectoryName(_store.PathOf(site))));
        Assert.Equal([2], (await _store.ReadAsync(plain, TestContext.Current.CancellationToken))?.Bytes);
    }
}
