using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// Reads real icons through the Windows shell; elsewhere there is nothing to read.
/// </summary>
public sealed class ShellProgramIconReaderTests
{
    private static readonly string Explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    private readonly ShellProgramIconReader _reader = new();

    [Fact]
    public void OnWindows_AProgram_HasTheIconExplorerShows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only the Windows shell gives a program an icon.");

        ProgramIconPixels? icon = _reader.Read(Explorer, 48);

        Assert.NotNull(icon);
        Assert.Equal(48, icon.Width);
        Assert.Equal(48, icon.Height);
        Assert.Equal(48 * 48 * 4, icon.Bgra.Length);
        Assert.Contains(icon.Bgra.Where((_, index) => index % 4 == 3), alpha => alpha == 0xFF);
    }

    [Fact]
    public async Task OnWindows_AProgramReadFromTheThreadPool_StillHasItsIcon()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only the Windows shell gives a program an icon.");

        ProgramIconPixels?[] icons = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => _reader.Read(Explorer, 48), TestContext.Current.CancellationToken)));

        Assert.All(icons, Assert.NotNull);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void OnWindows_TheClassicIcon_AloneGivesAProgramsIcon_WithTransparency()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only the Windows shell gives a program an icon.");

        ProgramIconPixels? icon = ShellProgramIconReader.ReadClassicIcon(Explorer);

        Assert.NotNull(icon);
        Assert.Equal(icon.Width, icon.Height);
        Assert.Equal(icon.Width * icon.Height * 4, icon.Bgra.Length);
        Assert.True(ShellProgramIconReader.HasAlpha(icon.Bgra));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void OnWindows_AMissingProgram_HasNoIcon()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only the Windows shell gives a program an icon.");
        string missing = Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tests", Guid.NewGuid().ToString("N"), "missing.exe");

        Assert.Null(_reader.Read(missing, 48));
        Assert.Null(ShellProgramIconReader.ReadClassicIcon(missing));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void OnWindows_ShellWork_RunsOnASingleThreadedApartment()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Apartments are a Windows notion.");

        (ApartmentState apartment, int thread) = ShellProgramIconReader.OnStaThread(
            () => (Thread.CurrentThread.GetApartmentState(), Environment.CurrentManagedThreadId));

        Assert.Equal(ApartmentState.STA, apartment);
        Assert.NotEqual(Environment.CurrentManagedThreadId, thread);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void OnWindows_AFailureOnTheShellThread_IsThrownToTheCaller()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Apartments are a Windows notion.");
        InvalidOperationException failure = new("The shell refused.");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => ShellProgramIconReader.OnStaThread<int>(() => throw failure)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ASizeBelowOnePixel_IsRefused(int size)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _reader.Read("tool.exe", size));

    [Fact]
    public void PixelsTransparentEverywhere_HaveNoAlpha()
    {
        Assert.False(ShellProgramIconReader.HasAlpha(new byte[16]));
        Assert.True(ShellProgramIconReader.HasAlpha([0, 0, 0, 0, 9, 9, 9, 1]));
    }

    [Fact]
    public void AMask_MakesItsBlackOpaque_AndItsWhiteClear()
    {
        byte[] icon = [10, 20, 30, 0, 40, 50, 60, 0];
        byte[] mask = [0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0];

        ShellProgramIconReader.ApplyMask(icon, mask);

        Assert.Equal([10, 20, 30, 0xFF, 40, 50, 60, 0], icon);
    }

    [Fact]
    public void AMaskOfAnotherSize_IsRefused()
        => Assert.Throws<ArgumentException>(() => ShellProgramIconReader.ApplyMask(new byte[8], new byte[4]));
}
