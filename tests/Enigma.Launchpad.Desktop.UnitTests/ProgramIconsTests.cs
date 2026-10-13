using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class ProgramIconsTests : IDisposable
{
    private readonly HeadlessAvaloniaFixture _fixture;
    private readonly TempDirectory _temp = new();
    private readonly FakeProgramIconReader _reader = new();
    private readonly RecordingLogger<ProgramIcons> _logger = new();
    private readonly ProgramIcons _icons;

    public ProgramIconsTests(HeadlessAvaloniaFixture fixture)
    {
        _fixture = fixture;
        _icons = new ProgramIcons(_reader, _logger);
    }

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(@"\\server\share\tool.exe")]
    [InlineData("//server/share/tool.exe")]
    [InlineData(@"\\?\UNC\server\share\tool.exe")]
    public async Task ANetworkPath_IsNeverRead(string path)
    {
        Assert.Null(await _icons.GetAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(_reader.Reads);
    }

    [Fact]
    public async Task AMissingProgram_IsNeverRead()
    {
        Assert.Null(await _icons.GetAsync(_temp.Combine("missing.exe"), TestContext.Current.CancellationToken));
        Assert.Empty(_reader.Reads);
    }

    [Fact]
    public async Task AProgram_IsReadAtTheIconSize()
    {
        string program = Program("tool.exe");

        await _icons.GetAsync(program, TestContext.Current.CancellationToken);

        (string path, int size, _) = Assert.Single(_reader.Reads);
        Assert.Equal(program, path);
        Assert.Equal(ProgramIcons.IconSize, size);
    }

    [Fact]
    public async Task AProgram_IsReadOffTheCallingThread()
    {
        string program = Program("tool.exe");
        using ManualResetEventSlim gate = new();
        using ManualResetEventSlim started = new();
        _reader.Gate = gate;
        _reader.Started = started;
        int caller = Environment.CurrentManagedThreadId;

        Task<byte[]?> icon = _icons.GetAsync(program, TestContext.Current.CancellationToken);

        // This thread holds on until the read is underway, so the read cannot be running on it.
        Assert.True(started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.NotEqual(caller, Assert.Single(_reader.Reads).ThreadId);

        gate.Set();
        await icon;
    }

    [Fact]
    public async Task AProgramWithoutAnIcon_IsReadOnce()
    {
        string program = Program("tool.exe");

        Assert.Null(await _icons.GetAsync(program, TestContext.Current.CancellationToken));
        Assert.Null(await _icons.GetAsync(program, TestContext.Current.CancellationToken));

        Assert.Single(_reader.Reads);
    }

    [Fact]
    public async Task RequestsWhileAReadIsUnderway_ShareIt_AndNeverWaitForIt()
    {
        string program = Program("tool.exe");
        using ManualResetEventSlim gate = new();
        _reader.Gate = gate;

        Task<byte[]?> first = _icons.GetAsync(program, TestContext.Current.CancellationToken);
        Task<byte[]?> second = _icons.GetAsync(program, TestContext.Current.CancellationToken);

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        gate.Set();
        await Task.WhenAll(first, second);

        Assert.Single(_reader.Reads);
    }

    [Fact]
    public async Task ACancelledWait_LeavesTheReadToTheNextCaller()
    {
        string program = Program("tool.exe");
        using ManualResetEventSlim gate = new();
        using CancellationTokenSource cancellation = new();
        _reader.Gate = gate;
        _reader.Answer = _ => FakeProgramIconReader.Opaque(4);

        Task<byte[]?> abandoned = _icons.GetAsync(program, cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        gate.Set();
        Assert.NotNull(await _icons.GetAsync(program, TestContext.Current.CancellationToken));
        Assert.Single(_reader.Reads);
    }

    [Fact]
    public async Task ARefresh_ReadsTheProgramAgain()
    {
        string program = Program("tool.exe");
        await _icons.GetAsync(program, TestContext.Current.CancellationToken);

        await _icons.RefreshAsync(program, TestContext.Current.CancellationToken);
        await _icons.GetAsync(program, TestContext.Current.CancellationToken);

        Assert.Equal(2, _reader.Reads.Count);
    }

    [Fact]
    public async Task ARefreshWhileAReadIsUnderway_SharesIt()
    {
        string program = Program("tool.exe");
        using ManualResetEventSlim gate = new();
        _reader.Gate = gate;

        Task<byte[]?> first = _icons.GetAsync(program, TestContext.Current.CancellationToken);
        Task<byte[]?> refreshed = _icons.RefreshAsync(program, TestContext.Current.CancellationToken);

        gate.Set();
        await Task.WhenAll(first, refreshed);

        Assert.Single(_reader.Reads);
    }

    [Fact]
    public async Task ARefresh_NeverReadsANetworkPath()
    {
        Assert.Null(await _icons.RefreshAsync(@"\\server\share\tool.exe", TestContext.Current.CancellationToken));
        Assert.Empty(_reader.Reads);
    }

    [Fact]
    public async Task OnWindows_OneProgramUnderTwoSpellings_IsReadOnce()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows paths ignore case.");
        string program = Program("tool.exe");

        await _icons.GetAsync(program, TestContext.Current.CancellationToken);
        await _icons.GetAsync(program.ToUpperInvariant(), TestContext.Current.CancellationToken);

        Assert.Single(_reader.Reads);
    }

    [Fact]
    public async Task AFailingRead_GivesNoIcon_AndIsLoggedAsAWarning()
    {
        string program = Program("tool.exe");
        COMException failure = new("The shell refused.");
        _reader.Answer = _ => throw failure;

        Assert.Null(await _icons.GetAsync(program, TestContext.Current.CancellationToken));

        (LogLevel level, string message, Exception? exception) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains(program, message, StringComparison.Ordinal);
        Assert.Same(failure, exception);
    }

    [Fact]
    public void AnIcon_BecomesAPngOfItsSize()
        => _fixture.RunAsync(async () =>
        {
            string program = Program("tool.exe");
            _reader.Answer = _ => FakeProgramIconReader.Opaque(ProgramIcons.IconSize);

            byte[]? icon = await _icons.GetAsync(program);

            Assert.Equal(new PixelSize(ProgramIcons.IconSize, ProgramIcons.IconSize), TestImages.SizeOf(Assert.IsType<byte[]>(icon)));
            Assert.Empty(_logger.Entries);
        });

    [Fact]
    public void PixelsThatDoNotFillTheIcon_AreRefused()
        => Assert.Throws<ArgumentException>(() => ProgramIcons.ToBitmap(new ProgramIconPixels(2, 2, new byte[15])));

    [Fact]
    public void ToBitmap_KeepsTheColours()
        => _fixture.Run(() =>
        {
            ProgramIconPixels pixels = FakeProgramIconReader.Opaque(2);

            using Bitmap bitmap = ProgramIcons.ToBitmap(pixels);

            byte[] copy = new byte[pixels.Bgra.Length];
            GCHandle pinned = GCHandle.Alloc(copy, GCHandleType.Pinned);

            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, 2, 2), pinned.AddrOfPinnedObject(), copy.Length, 2 * 4);
            }
            finally
            {
                pinned.Free();
            }

            Assert.Equal(pixels.Bgra.Take(4), copy.Take(4));
        });

    private string Program(string name)
    {
        string path = _temp.Combine(name);
        File.WriteAllBytes(path, []);
        return path;
    }
}
