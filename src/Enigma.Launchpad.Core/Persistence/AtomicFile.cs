using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.Launchpad.Core.Persistence;

/// <summary>
/// Replaces a file's contents in one step, so that a reader sees either the old document or the new
/// one and never half of either.
/// </summary>
/// <remarks>
/// <para>
/// A plain <c>File.WriteAllText</c> truncates the file and then fills it: a crash, a full disk or a
/// second instance reading in between leaves a partial document, which the next start would take for
/// a corrupt one. The new contents are written to a temporary file beside the target instead — in the
/// same directory, so the final move is a rename on the same volume — and then moved over it.
/// </para>
/// <para>
/// Windows refuses that move while another handle holds the target open for reading, for the
/// microseconds another instance takes to load it. The move is retried briefly then, rather than
/// losing the write; elsewhere a rename over an open file simply succeeds.
/// </para>
/// </remarks>
public static class AtomicFile
{
    private const int ReplaceAttempts = 50;

    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    private static readonly TimeSpan ReplaceRetryDelay = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Writes a text file atomically, as UTF-8 without a byte-order mark.
    /// </summary>
    /// <param name="path">The file to replace or create.</param>
    /// <param name="contents">What it must contain.</param>
    /// <param name="cancellationToken">Cancels the write before the file is replaced.</param>
    /// <returns>A task that completes once the file has been replaced.</returns>
    public static Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        return WriteThroughTemporaryAsync(
            path,
            (temporary, token) => File.WriteAllTextAsync(temporary, contents, new UTF8Encoding(false), token),
            cancellationToken);
    }

    /// <summary>
    /// Writes a binary file atomically.
    /// </summary>
    /// <param name="path">The file to replace or create.</param>
    /// <param name="contents">What it must contain.</param>
    /// <param name="cancellationToken">Cancels the write before the file is replaced.</param>
    /// <returns>A task that completes once the file has been replaced.</returns>
    public static Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        return WriteThroughTemporaryAsync(
            path,
            (temporary, token) => File.WriteAllBytesAsync(temporary, contents, token),
            cancellationToken);
    }

    /// <summary>
    /// Writes the new contents beside the target, then moves them over it.
    /// </summary>
    private static async Task WriteThroughTemporaryAsync(
        string path,
        Func<string, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await write(temporary, cancellationToken).ConfigureAwait(false);
            await ReplaceAsync(temporary, path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    private static async Task ReplaceAsync(string temporary, string path, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception exception) when ((attempt < ReplaceAttempts) && IsHeldOpen(exception))
            {
                await Task.Delay(ReplaceRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Whether a refused move is Windows saying another handle holds the target open — the only refusal
    /// worth waiting out. Anything else is a real error.
    /// </summary>
    private static bool IsHeldOpen(Exception exception)
        => OperatingSystem.IsWindows()
            && exception is UnauthorizedAccessException or IOException { HResult: SharingViolation or LockViolation };

    /// <summary>
    /// Removes the temporary file when the move did not consume it; a leftover is not worth an error.
    /// </summary>
    private static void DeleteQuietly(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (IOException)
        {
            // Nothing a caller could do about it.
        }
        catch (UnauthorizedAccessException)
        {
            // Likewise.
        }
    }
}
