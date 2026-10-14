using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Core.Exchange;

/// <summary>
/// Writes a profile's workspaces, groups and shortcuts to a file another launchpad can import, and reads such a file
/// back.
/// </summary>
public interface IShortcutExchange
{
    /// <summary>
    /// Writes every workspace, group and shortcut of a profile to a file, atomically, replacing what it held. To
    /// export part of a profile, pass a copy holding only that part.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="profile">What to export; its name is written too, for the reader's information.</param>
    /// <param name="cancellationToken">Cancels the write before the file is replaced.</param>
    /// <returns>A task that completes once the file has been written.</returns>
    /// <exception cref="System.IO.IOException">The file could not be written.</exception>
    /// <exception cref="System.UnauthorizedAccessException">The file may not be written.</exception>
    Task ExportAsync(string path, Profile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an exported file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What the file holds that can be imported, and how many of its entries were left out.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    /// The file is not an export this build can read; the message says why, worded for the user.
    /// </exception>
    /// <exception cref="System.IO.IOException">The file could not be read.</exception>
    /// <exception cref="System.UnauthorizedAccessException">The file may not be read.</exception>
    Task<ShortcutImport> ReadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// What <see cref="IShortcutExchange.ReadAsync"/> found in a file.
/// </summary>
/// <param name="Content">
/// The workspaces, groups and shortcuts that can be imported, as a profile named after the one they were exported
/// from. Every workspace, group and shortcut has a new identity.
/// </param>
/// <param name="SkippedEntries">
/// How many workspaces, groups and shortcuts of the file were left out because they are not valid: a workspace or a
/// group left out counts with everything in it.
/// </param>
public sealed record ShortcutImport(Profile Content, int SkippedEntries);
