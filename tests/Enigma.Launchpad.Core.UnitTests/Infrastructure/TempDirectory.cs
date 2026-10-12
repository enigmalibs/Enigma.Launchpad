using System;
using System.IO;

namespace Enigma.Launchpad.Core.UnitTests.Infrastructure;

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

    public string CreateFile(string name, string contents = "")
    {
        string path = Combine(name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? Path);
        File.WriteAllText(path, contents);
        return path;
    }

    public string CreateDirectory(string name)
    {
        string path = Combine(name);
        Directory.CreateDirectory(path);
        return path;
    }

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
