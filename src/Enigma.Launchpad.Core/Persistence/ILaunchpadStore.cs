using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Core.Persistence;

/// <summary>
/// Reads and writes the user's launchpad document.
/// </summary>
public interface ILaunchpadStore
{
    /// <summary>
    /// Gets the path of the document on disk.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Reads the document.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The document; an empty one when there is no file yet, or when the file could not be read — in
    /// which case it has been moved aside first, and <see cref="LaunchpadLoadResult.UnreadableFileMovedTo"/>
    /// says where.
    /// </returns>
    /// <exception cref="System.IO.IOException">The file exists but could not be read or moved aside.</exception>
    Task<LaunchpadLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the document on disk, atomically. Concurrent saves are written one after the other.
    /// </summary>
    /// <param name="data">The document to write.</param>
    /// <param name="cancellationToken">Cancels the write before the file is replaced.</param>
    /// <returns>A task that completes once the file has been replaced.</returns>
    Task SaveAsync(LaunchpadData data, CancellationToken cancellationToken = default);
}

/// <summary>
/// What <see cref="ILaunchpadStore.LoadAsync"/> found.
/// </summary>
/// <param name="Data">The document.</param>
/// <param name="UnreadableFileMovedTo">
/// Where an unreadable file was moved before an empty document was returned in its place;
/// <see langword="null"/> when the file was read, or did not exist.
/// </param>
public sealed record LaunchpadLoadResult(LaunchpadData Data, string? UnreadableFileMovedTo = null);
