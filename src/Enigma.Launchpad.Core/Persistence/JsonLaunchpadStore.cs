using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// The document is written in format version 3: profiles holding workspaces, holding groups of one kind each. A
/// version 2 file — profiles holding three categories — or a version 1 file — the categories at the root, before
/// profiles — is read into that shape, each profile's groups in one <see cref="Workspace.DefaultTitle"/> workspace,
/// and written back as version 3 on the next save. Such a file is first copied, once, to
/// <c>launchpad.v&lt;version&gt;.backup.json</c> beside it, so a build that only reads the older version has a file
/// to go back to.
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
    /// How the document is written and read: camelCase, indented, a group's kind as its camelCase name, and strict
    /// about the fields it requires and the kinds it knows, so a damaged file is detected on load instead of
    /// surfacing as a null deep inside the application.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
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

            if (TryParse(json, out LaunchpadData? data, out int version, out string? reason))
            {
                if (version < LaunchpadData.CurrentVersion)
                {
                    BackUp(path, version);
                }

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
    /// Reads a document, refusing anything this build cannot fully understand.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <param name="data">The document, in the current format, when it was read.</param>
    /// <param name="reason">Why it was refused, for the log.</param>
    /// <returns><see langword="true"/> when the document was read.</returns>
    internal static bool TryParse(
        string json,
        [NotNullWhen(true)] out LaunchpadData? data,
        [NotNullWhen(false)] out string? reason)
        => TryParse(json, out data, out _, out reason);

    /// <summary>
    /// Reads a document, refusing anything this build cannot fully understand. A version 2 document — profiles
    /// holding three categories — is read with each profile's groups in one <see cref="Workspace.DefaultTitle"/>
    /// workspace, typed by their category: applications, then directories, then URLs. A version 1 document — from
    /// before profiles — is read the same way into a single <see cref="Profile.DefaultName"/> profile. A profile
    /// with no group gets no workspace.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <param name="data">The document, in the current format, when it was read.</param>
    /// <param name="version">The format version the file was in, when it was read.</param>
    /// <param name="reason">Why it was refused, for the log.</param>
    /// <returns><see langword="true"/> when the document was read.</returns>
    internal static bool TryParse(
        string json,
        [NotNullWhen(true)] out LaunchpadData? data,
        out int version,
        [NotNullWhen(false)] out string? reason)
    {
        data = null;
        version = 0;

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

            LaunchpadData? parsed = probe.Version switch
            {
                1 => JsonSerializer.Deserialize<LaunchpadDataV1>(json, SerializerOptions)?.ToCurrent(),
                2 => JsonSerializer.Deserialize<LaunchpadDataV2>(json, SerializerOptions)?.ToCurrent(),
                _ => JsonSerializer.Deserialize<LaunchpadData>(json, SerializerOptions),
            };

            if ((parsed is null) || !IsWellFormed(parsed))
            {
                reason = "a profile, a workspace, a group or a shortcut is missing";
                return false;
            }

            version = probe.Version;

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
            && profile.Workspaces.All(workspace => (workspace is not null)
                && workspace.Groups.All(group => (group is not null) && group.Shortcuts.All(shortcut => shortcut is not null))));

    /// <summary>
    /// Keeps a copy of a file in an older format, before it is first saved in the current one. A copy already there
    /// — from an earlier start — is never replaced: it is the file as the older build left it.
    /// </summary>
    private void BackUp(string path, int version)
    {
        string directory = Path.GetDirectoryName(path) ?? _paths.DataDirectory;
        string backup = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"launchpad.v{version}.backup.json"));

        if (File.Exists(backup))
        {
            return;
        }

        try
        {
            File.Copy(path, backup, overwrite: false);
            _logger.LogInformation("The launchpad file is in format version {Version}; a copy was kept as {Backup}", version, backup);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A copy is a convenience: the file itself is read, and only ever replaced by an atomic save.
            _logger.LogWarning(exception, "The launchpad file could not be copied to {Backup}", backup);
        }
    }

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
        IReadOnlyList<GroupV2> Applications,
        IReadOnlyList<GroupV2> Directories,
        IReadOnlyList<GroupV2> Urls)
    {
        /// <summary>
        /// Moves the groups into one profile, in use, of a current document.
        /// </summary>
        /// <returns>The document; <see langword="null"/> when a list holds a <c>null</c>.</returns>
        public LaunchpadData? ToCurrent()
            => new ProfileV2(Guid.NewGuid(), Profile.DefaultName, Applications, Directories, Urls).ToCurrent() is { } profile
                ? LaunchpadData.Of(profile)
                : null;
    }

    /// <summary>
    /// The version 2 document: profiles holding three categories, before there were workspaces.
    /// </summary>
    /// <param name="Version">Always 2.</param>
    /// <param name="ActiveProfileId">The identity of the profile in use.</param>
    /// <param name="Profiles">The profiles, in display order.</param>
    private sealed record LaunchpadDataV2(int Version, Guid ActiveProfileId, IReadOnlyList<ProfileV2> Profiles)
    {
        /// <summary>
        /// Moves each profile's groups into a workspace.
        /// </summary>
        /// <returns>The document; <see langword="null"/> when a list holds a <c>null</c>.</returns>
        public LaunchpadData? ToCurrent()
        {
            List<Profile> profiles = [];

            foreach (ProfileV2? profile in Profiles)
            {
                if (profile?.ToCurrent() is not { } current)
                {
                    return null;
                }

                profiles.Add(current);
            }

            return new LaunchpadData(LaunchpadData.CurrentVersion, ActiveProfileId, profiles);
        }
    }

    /// <summary>
    /// A version 2 profile, or the root of a version 1 document.
    /// </summary>
    /// <param name="Id">The profile's identity.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Applications">The groups of the applications category.</param>
    /// <param name="Directories">The groups of the directories category.</param>
    /// <param name="Urls">The groups of the URLs category.</param>
    private sealed record ProfileV2(
        Guid Id,
        string Name,
        IReadOnlyList<GroupV2> Applications,
        IReadOnlyList<GroupV2> Directories,
        IReadOnlyList<GroupV2> Urls)
    {
        /// <summary>
        /// Puts the groups, typed by their category, in one workspace — none when there is no group.
        /// </summary>
        /// <returns>The profile; <see langword="null"/> when a list holds a <c>null</c>.</returns>
        public Profile? ToCurrent()
        {
            List<ShortcutGroup> groups = [];

            foreach ((ShortcutKind kind, IReadOnlyList<GroupV2> category) in new[]
            {
                (ShortcutKind.Application, Applications),
                (ShortcutKind.Directory, Directories),
                (ShortcutKind.Url, Urls),
            })
            {
                foreach (GroupV2? group in category)
                {
                    if (group is null)
                    {
                        return null;
                    }

                    groups.Add(new ShortcutGroup(group.Id, group.Name, kind, group.Shortcuts, group.IsCollapsed));
                }
            }

            Workspace[] workspaces = groups.Count == 0
                ? []
                : [new Workspace(Guid.NewGuid(), Workspace.DefaultTitle, groups, Workspace.MigratedDescription, Workspace.MigratedIcon)];

            return new Profile(Id, Name, workspaces);
        }
    }

    /// <summary>
    /// A group of versions 1 and 2, whose kind was the category it was in.
    /// </summary>
    /// <param name="Id">The group's identity.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Shortcuts">Its shortcuts.</param>
    /// <param name="IsCollapsed">Whether it was folded.</param>
    private sealed record GroupV2(Guid Id, string Name, IReadOnlyList<Shortcut> Shortcuts, bool IsCollapsed = false);
}
