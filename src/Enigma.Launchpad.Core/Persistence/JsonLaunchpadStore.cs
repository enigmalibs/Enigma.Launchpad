using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Core.Persistence;

/// <summary>
/// Default <see cref="ILaunchpadStore"/>: an indented JSON document, <c>launchpad.json</c>, in the user's
/// data directory.
/// </summary>
/// <remarks>
/// <para>
/// The document is written in format version 2, with profiles; a version 1 file, from before profiles, is read
/// into one profile and written back as version 2 on the next save.
/// </para>
/// <para>
/// A file that cannot be read — malformed JSON, a missing required field, a format version this build does
/// not know — is never overwritten: it is moved aside to <c>launchpad.corrupt-&lt;UTC stamp&gt;.json</c>
/// and an empty document is returned in its place, so the user loses nothing they could recover by hand.
/// </para>
/// </remarks>
public sealed class JsonLaunchpadStore : ILaunchpadStore
{
    /// <summary>
    /// The document's file name.
    /// </summary>
    public const string FileName = "launchpad.json";

    /// <summary>
    /// How the document is written and read: camelCase, indented, and strict about the fields it requires,
    /// so a damaged file is detected on load instead of surfacing as a null deep inside the application.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IAppPaths _paths;
    private readonly TimeProvider _time;
    private readonly ILogger<JsonLaunchpadStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="paths">Where the user's data lives.</param>
    /// <param name="time">The clock the name of a moved-aside file is stamped from.</param>
    /// <param name="logger">Where an unreadable file is reported.</param>
    public JsonLaunchpadStore(IAppPaths paths, TimeProvider time, ILogger<JsonLaunchpadStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public string FilePath => _paths.GetDataFile(FileName);

    /// <inheritdoc />
    public async Task<LaunchpadLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string path = FilePath;

            if (!File.Exists(path))
            {
                return new LaunchpadLoadResult(LaunchpadData.Empty);
            }

            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            if (TryParse(json, out LaunchpadData? data, out string? reason))
            {
                return new LaunchpadLoadResult(data);
            }

            string movedTo = MoveAside(path);
            _logger.LogWarning("The launchpad file could not be read ({Reason}); it was moved to {MovedTo}", reason, movedTo);

            return new LaunchpadLoadResult(LaunchpadData.Empty, movedTo);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(LaunchpadData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        string json = Serialize(data);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await AtomicFile.WriteAllTextAsync(FilePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Writes a document as the store would, always in the current format version.
    /// </summary>
    /// <param name="data">The document.</param>
    /// <returns>Its JSON.</returns>
    internal static string Serialize(LaunchpadData data)
        => JsonSerializer.Serialize(data with { Version = LaunchpadData.CurrentVersion }, SerializerOptions);

    /// <summary>
    /// Reads a document, refusing anything this build cannot fully understand. A version 1 document — from
    /// before profiles — is read into a single <see cref="Profile.DefaultName"/> profile holding its groups.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <param name="data">The document, in the current format, when it was read.</param>
    /// <param name="reason">Why it was refused, for the log.</param>
    /// <returns><see langword="true"/> when the document was read.</returns>
    internal static bool TryParse(
        string json,
        [NotNullWhen(true)] out LaunchpadData? data,
        [NotNullWhen(false)] out string? reason)
    {
        data = null;

        try
        {
            // The version says which shape the rest of the document has, so it is read on its own first.
            if (JsonSerializer.Deserialize<VersionProbe>(json, SerializerOptions) is not { } probe)
            {
                reason = "the document is empty";
                return false;
            }

            if ((probe.Version < 1) || (probe.Version > LaunchpadData.CurrentVersion))
            {
                reason = $"format version {probe.Version} is not supported by this build";
                return false;
            }

            LaunchpadData? parsed = probe.Version == 1
                ? JsonSerializer.Deserialize<LaunchpadDataV1>(json, SerializerOptions)?.ToCurrent()
                : JsonSerializer.Deserialize<LaunchpadData>(json, SerializerOptions);

            if ((parsed is null) || !IsWellFormed(parsed))
            {
                reason = "a profile, a group or a shortcut is missing";
                return false;
            }

            data = parsed.Normalised();
            reason = null;
            return true;
        }
        catch (JsonException exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Whether every list holds real entries. The serializer's nullability checks cover properties, not
    /// the elements of a list, so a <c>null</c> inside an array is caught here.
    /// </summary>
    private static bool IsWellFormed(LaunchpadData data)
        => data.Profiles.All(profile => (profile is not null)
            && Enum.GetValues<ShortcutKind>()
                .Select(profile.GroupsOf)
                .All(groups => groups.All(group => (group is not null) && group.Shortcuts.All(shortcut => shortcut is not null))));

    private string MoveAside(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? _paths.DataDirectory;
        string stamp = _time.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string target = Path.Combine(directory, $"launchpad.corrupt-{stamp}.json");

        for (int suffix = 2; File.Exists(target); suffix++)
        {
            target = Path.Combine(directory, $"launchpad.corrupt-{stamp}-{suffix}.json");
        }

        File.Move(path, target);

        return target;
    }

    /// <summary>
    /// The one field every format version shares.
    /// </summary>
    /// <param name="Version">The document's format version.</param>
    private sealed record VersionProbe(int Version);

    /// <summary>
    /// The version 1 document: the three categories at the root, before there were profiles.
    /// </summary>
    /// <param name="Version">Always 1.</param>
    /// <param name="Applications">The groups of the applications category.</param>
    /// <param name="Directories">The groups of the directories category.</param>
    /// <param name="Urls">The groups of the URLs category.</param>
    private sealed record LaunchpadDataV1(
        int Version,
        IReadOnlyList<ShortcutGroup> Applications,
        IReadOnlyList<ShortcutGroup> Directories,
        IReadOnlyList<ShortcutGroup> Urls)
    {
        /// <summary>
        /// Moves the groups into one profile, in use, of a current document. Lists holding a <c>null</c> are
        /// carried over as they are, for the well-formedness check to refuse.
        /// </summary>
        public LaunchpadData ToCurrent()
            => LaunchpadData.Of(new Profile(Guid.NewGuid(), Profile.DefaultName, Applications, Directories, Urls));
    }
}
