using System;
using System.IO;
using System.Linq;
using Enigma.Launchpad.Desktop.Services;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// Reads real icons through the Windows shell; elsewhere there is nothing to read.
/// </summary>
public sealed class ShellProgramIconReaderTests
{
    private readonly ShellProgramIconReader _reader = new();

    [Fact]
    public void OnWindows_AProgram_HasTheIconExplorerShows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only the Windows shell gives a program an icon.");
        string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        ProgramIconPixels? icon = _reader.Read(explorer, 48);

        Assert.NotNull(icon);
        Assert.Equal(48, icon.Width);
        Assert.Equal(48, icon.Height);
        Assert.Equal(48 * 48 * 4, icon.Bgra.Length);
        Assert.Contains(icon.Bgra.Where((_, index) => index % 4 == 3), alpha => alpha == 0xFF);
    }

    [Fact]
    public void OnWindows_AMissingProgram_HasNoIcon()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only the Windows shell gives a program an icon.");
        string missing = Path.Combine(Path.GetTempPath(), "Enigma.Launchpad.Tests", Guid.NewGuid().ToString("N"), "missing.exe");

        Assert.Null(_reader.Read(missing, 48));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ASizeBelowOnePixel_IsRefused(int size)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _reader.Read("tool.exe", size));
}
