using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Persistence;

public sealed class AtomicFileTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task WriteAllBytes_CreatesTheFile()
    {
        string path = _temp.Combine("icon.bin");

        await AtomicFile.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAllBytes_ReplacesAnExistingFile_AndLeavesNothingBeside()
    {
        string path = _temp.CreateFile("icon.bin", "old and longer");

        await AtomicFile.WriteAllBytesAsync(path, [4, 5], TestContext.Current.CancellationToken);

        Assert.Equal([4, 5], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }

    [Fact]
    public async Task WriteAllText_WritesUtf8WithoutAByteOrderMark()
    {
        string path = _temp.Combine("doc.json");

        await AtomicFile.WriteAllTextAsync(path, "é", TestContext.Current.CancellationToken);

        Assert.Equal([0xC3, 0xA9], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(_temp.Path));
    }
}
