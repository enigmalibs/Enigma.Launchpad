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
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;

namespace Enigma.Launchpad.Core.Exchange;

/// <summary>
/// Default <see cref="IShortcutExchange"/>: an indented JSON document marked as a launchpad shortcuts export.
/// </summary>
/// <remarks>
/// <para>
/// The document holds names, targets, arguments and descriptions only — no identities and no fold state: it is a
/// way to move shortcuts between profiles and machines, not a copy of <c>launchpad.json</c>, whose format it never
/// follows.
/// </para>
/// <para>
/// A file is untrusted input. One that is not a version 1 export is refused as a whole; inside one that is, an
/// entry the shortcut form would not accept is left out and counted — except that a path need not exist, since the
/// file may come from another machine and the launcher checks every target when it opens it.
/// </para>
/// </remarks>
public sealed class ShortcutExchange : IShortcutExchange
{
    /// <summary>
    /// The value of the document's <c>format</c> field, which says what the file is.
    /// </summary>
    public const string FormatName = "enigma-launchpad-shortcuts";

    /// <summary>
    /// The format version this build writes, and the only one it reads.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// What the content of a file that does not name its profile is called.
    /// </summary>
    public const string DefaultContentName = "Imported";

    /// <summary>
    /// How the document is written and read: camelCase and indented, without the fields a shortcut does not use,
    /// tolerant of the comments and trailing commas of a file edited by hand, and strict about everything else.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// The same tolerance as <see cref="SerializerOptions"/>, for reading the marker and the version.
    /// </summary>
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IShortcutValidator _validator;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="validator">The rules an imported name must meet.</param>
    public ShortcutExchange(IShortcutValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    /// <inheritdoc />
    public Task ExportAsync(string path, Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(profile);

        return AtomicFile.WriteAllTextAsync(path, Serialize(profile), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShortcutImport> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        return TryParse(json, out ShortcutImport? import, out string? reason)
            ? import
            : throw new InvalidDataException(reason);
    }

    /// <summary>
    /// Writes a profile's groups and shortcuts as an export document.
    /// </summary>
    /// <param name="profile">What to export.</param>
    /// <returns>The document's JSON.</returns>
    internal static string Serialize(Profile profile)
        => JsonSerializer.Serialize(
            new ExportDocument(
                FormatName,
                CurrentVersion,
                profile.Name,
                Write(profile.Applications),
                Write(profile.Directories),
                Write(profile.Urls)),
            SerializerOptions);

    /// <summary>
    /// Reads an export document, refusing anything that is not a version 1 export and leaving out the entries that
    /// are not valid.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <param name="import">What can be imported, when the document was read.</param>
    /// <param name="reason">Why it was refused, worded for the user.</param>
    /// <returns><see langword="true"/> when the document was read.</returns>
    internal bool TryParse(
        string json,
        [NotNullWhen(true)] out ShortcutImport? import,
        [NotNullWhen(false)] out string? reason)
    {
        import = null;

        // The marker and the version say whether the rest can be understood at all, so they are read on their own
        // first: a file of another kind gets "not an export" rather than a complaint about a field it never had.
        if (!TryReadHeader(json, out reason))
        {
            return false;
        }

        ExportDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<ExportDocument>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            reason = WithLine("The file is damaged: a group or a shortcut is incomplete.", exception);
            return false;
        }

        if ((document is null) || !IsWellFormed(document))
        {
            reason = "The file is damaged: a group or a shortcut is missing.";
            return false;
        }

        int skipped = 0;
        string name = (document.Profile?.Trim() is { Length: > 0 } named) && (_validator.ValidateProfileName(named, []) is null)
            ? named
            : DefaultContentName;

        Profile content = new(
            Guid.NewGuid(),
            name,
            Read(ShortcutKind.Application, document.Applications, ref skipped),
            Read(ShortcutKind.Directory, document.Directories, ref skipped),
            Read(ShortcutKind.Url, document.Urls, ref skipped));

        import = new ShortcutImport(content, skipped);
        reason = null;
        return true;
    }

    /// <summary>
    /// Checks that a text is JSON holding this format's marker and version.
    /// </summary>
    private static bool TryReadHeader(string json, [NotNullWhen(false)] out string? reason)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);
            JsonElement root = document.RootElement;

            if ((root.ValueKind != JsonValueKind.Object)
                || !root.TryGetProperty("format", out JsonElement format)
                || (format.ValueKind != JsonValueKind.String)
                || (format.GetString() != FormatName))
            {
                reason = "The file is not a launchpad shortcuts export.";
                return false;
            }

            if (!root.TryGetProperty("version", out JsonElement version)
                || (version.ValueKind != JsonValueKind.Number)
                || !version.TryGetInt32(out int number))
            {
                reason = "The file does not say which format version it is in.";
                return false;
            }

            if (number != CurrentVersion)
            {
                reason = string.Create(
                    CultureInfo.InvariantCulture,
                    $"The file is in format version {number}, which this version of the launchpad cannot read.");
                return false;
            }

            reason = null;
            return true;
        }
        catch (JsonException exception)
        {
            reason = WithLine("The file is not valid JSON.", exception);
            return false;
        }
    }

    private static ExportGroup[] Write(IReadOnlyList<ShortcutGroup> groups)
        => groups
            .Select(group => new ExportGroup(
                group.Name,
                group.Shortcuts
                    .Select(shortcut => new ExportShortcut(
                        shortcut.Name,
                        shortcut.Target,
                        string.IsNullOrWhiteSpace(shortcut.Arguments) ? null : shortcut.Arguments,
                        string.IsNullOrWhiteSpace(shortcut.Description) ? null : shortcut.Description))
                    .ToArray()))
            .ToArray();

    /// <summary>
    /// Whether every list holds real entries. The serializer's nullability checks cover properties, not the elements
    /// of a list, so a <c>null</c> inside an array is caught here.
    /// </summary>
    private static bool IsWellFormed(ExportDocument document)
        => new[] { document.Applications, document.Directories, document.Urls }
            .All(groups => (groups ?? []).All(group => (group is not null) && (group.Shortcuts ?? []).All(shortcut => shortcut is not null)));

    private static string WithLine(string message, JsonException exception)
        => exception.LineNumber is { } line
            ? string.Create(CultureInfo.InvariantCulture, $"{message} (line {line + 1})")
            : message;

    /// <summary>
    /// The target to store, or <see langword="null"/> when the shortcut form would refuse it: a URL as the form
    /// normalises it, a path when it is absolute — wherever it points.
    /// </summary>
    private static string? ReadTarget(ShortcutKind kind, string target)
    {
        if (kind == ShortcutKind.Url)
        {
            return UrlNormalizer.TryNormalize(target, out string normalized, out _) ? normalized : null;
        }

        string path = target.Trim();

        return (path.Length > 0) && !path.Contains('\0', StringComparison.Ordinal) && Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : null;
    }

    private ShortcutGroup[] Read(ShortcutKind kind, IReadOnlyList<ExportGroup>? groups, ref int skipped)
    {
        List<ShortcutGroup> read = [];

        foreach (ExportGroup group in groups ?? [])
        {
            IReadOnlyList<ExportShortcut> shortcuts = group.Shortcuts ?? [];

            if (_validator.ValidateGroupName(group.Name, []) is not null)
            {
                skipped += 1 + shortcuts.Count;
                continue;
            }

            List<Shortcut> kept = [];

            foreach (ExportShortcut shortcut in shortcuts)
            {
                if (Read(kind, shortcut) is { } valid)
                {
                    kept.Add(valid);
                }
                else
                {
                    skipped++;
                }
            }

            read.Add(new ShortcutGroup(Guid.NewGuid(), group.Name.Trim(), kept));
        }

        return [.. read];
    }

    private Shortcut? Read(ShortcutKind kind, ExportShortcut shortcut)
    {
        if ((_validator.ValidateShortcutName(shortcut.Name) is not null)
            || (_validator.ValidateDescription(shortcut.Description) is not null)
            || (ReadTarget(kind, shortcut.Target) is not { } target))
        {
            return null;
        }

        // Only an application takes arguments; the form has no field for them anywhere else.
        string? arguments = (kind == ShortcutKind.Application) && !string.IsNullOrWhiteSpace(shortcut.Arguments)
            ? shortcut.Arguments.Trim()
            : null;

        string? description = string.IsNullOrWhiteSpace(shortcut.Description) ? null : shortcut.Description.Trim();

        return new Shortcut(Guid.NewGuid(), shortcut.Name.Trim(), target, arguments, description);
    }

    /// <summary>
    /// The export document, version 1.
    /// </summary>
    /// <param name="Format">Always <see cref="FormatName"/>.</param>
    /// <param name="Version">Always <see cref="CurrentVersion"/>.</param>
    /// <param name="Profile">The name of the profile it was exported from, for information.</param>
    /// <param name="Applications">The groups of the applications category; none when absent.</param>
    /// <param name="Directories">The groups of the directories category; none when absent.</param>
    /// <param name="Urls">The groups of the URLs category; none when absent.</param>
    private sealed record ExportDocument(
        string Format,
        int Version,
        string? Profile = null,
        IReadOnlyList<ExportGroup>? Applications = null,
        IReadOnlyList<ExportGroup>? Directories = null,
        IReadOnlyList<ExportGroup>? Urls = null);

    /// <summary>
    /// One exported group.
    /// </summary>
    /// <param name="Name">The group's name.</param>
    /// <param name="Shortcuts">Its shortcuts, in display order; none when absent.</param>
    private sealed record ExportGroup(string Name, IReadOnlyList<ExportShortcut>? Shortcuts = null);

    /// <summary>
    /// One exported shortcut.
    /// </summary>
    /// <param name="Name">The shortcut's name.</param>
    /// <param name="Target">The executable path, the directory path, or the URL.</param>
    /// <param name="Arguments">Command-line arguments, for an application that has some.</param>
    /// <param name="Description">What the shortcut is for, when it says.</param>
    private sealed record ExportShortcut(
        string Name,
        string Target,
        string? Arguments = null,
        string? Description = null);
}
