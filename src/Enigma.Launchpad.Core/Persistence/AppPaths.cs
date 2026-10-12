using System;
using System.IO;

namespace Enigma.Launchpad.Core.Persistence;

/// <summary>
/// Where the application keeps the user's data.
/// </summary>
public interface IAppPaths
{
    /// <summary>
    /// Gets the directory holding the user's data, creating it if it does not exist.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Builds the absolute path of a file inside the data directory, creating the directory if needed.
    /// </summary>
    /// <param name="fileName">The file's name.</param>
    /// <returns>The absolute path.</returns>
    string GetDataFile(string fileName);
}

/// <summary>
/// Default <see cref="IAppPaths"/>: an <c>Enigma.Launchpad</c> directory under the operating system's
/// per-user application-data folder (<c>%APPDATA%</c> on Windows, <c>$XDG_CONFIG_HOME</c> or
/// <c>~/.config</c> on Linux, the platform equivalent on macOS).
/// </summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>
    /// The directory name used under the user's application-data folder.
    /// </summary>
    public const string FolderName = "Enigma.Launchpad";

    private readonly string _root;

    /// <summary>
    /// Initialises a new instance using the operating system's per-user location.
    /// </summary>
    public AppPaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
            FolderName))
    {
    }

    /// <summary>
    /// Initialises a new instance rooted at an explicit directory. Tests use this so they never touch
    /// the developer's own data.
    /// </summary>
    /// <param name="root">The directory to use.</param>
    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = root;
    }

    /// <inheritdoc />
    public string DataDirectory
    {
        get
        {
            Directory.CreateDirectory(_root);
            return _root;
        }
    }

    /// <inheritdoc />
    public string GetDataFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(DataDirectory, fileName);
    }
}
