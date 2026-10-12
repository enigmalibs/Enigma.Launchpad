using System;
using System.IO;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// A fresh directory under the system's temporary folder, deleted with everything in it on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Enigma.Launchpad.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory is not worth failing a test over.
        }
        catch (UnauthorizedAccessException)
        {
            // Likewise.
        }
    }
}
