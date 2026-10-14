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
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Persistence;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;

namespace Enigma.Launchpad.Core.Exchange;

/// <summary>
/// Default <see cref="IShortcutExchange"/>: an indented JSON document marked as a launchpad shortcuts export.
/// </summary>
/// <remarks>
/// <para>
/// The document holds workspaces — titles, descriptions and icons — with their groups — names and kinds — and the
/// groups' shortcuts — names, targets, arguments, descriptions and icons. It holds no identities and no fold state: it
/// is a way to move shortcuts between profiles and machines, not a copy of <c>launchpad.json</c>, whose format it never
/// follows. Version 2 is written; version 1 — the three categories, from before workspaces — is still read, into one
/// <see cref="Workspace.DefaultTitle"/> workspace.
/// </para>
/// <para>
/// A file is untrusted input. One that is not a version 1 or 2 export is refused as a whole; inside one that is, an
/// entry the forms would not accept — or a group of a kind this build does not know — is left out and counted, with
/// everything in it — except that a path need not exist, since the file may come from another machine and the
/// launcher checks every target when it opens it. An icon that is not a PNG image of an acceptable size, or a
/// workspace glyph whose name is not a plain identifier, is left out on its own: what carried it is kept.
/// </para>
/// </remarks>
public sealed class ShortcutExchange : IShortcutExchange
{
    /// <summary>
    /// The value of the document's <c>format</c> field, which says what the file is.
    /// </summary>
    public const string FormatName = "enigma-launchpad-shortcuts";

    /// <summary>
    /// The format version this build writes.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// What the content of a file that does not name its profile is called.
    /// </summary>
    public const string DefaultContentName = "Imported";

    /// <summary>
    /// The longest name of a workspace glyph read from a file.
    /// </summary>
    private const int MaxIconNameLength = 64;

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
    /// Writes a profile's workspaces, groups and shortcuts as an export document.
    /// </summary>
    /// <param name="profile">What to export.</param>
    /// <returns>The document's JSON.</returns>
    internal static string Serialize(Profile profile)
        => JsonSerializer.Serialize(
            new ExportDocument(
                FormatName,
                CurrentVersion,
                profile.Name,
                Workspaces: profile.Workspaces
                    .Select(workspace => new ExportWorkspace(
                        workspace.Title,
                        string.IsNullOrWhiteSpace(workspace.Description) ? null : workspace.Description,
                        string.IsNullOrWhiteSpace(workspace.Icon) ? null : workspace.Icon,
                        Write(workspace.Groups)))
                    .ToArray()),
            SerializerOptions);

    /// <summary>
    /// The name a kind is written under.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its camelCase name, as in <c>launchpad.json</c>.</returns>
    internal static string KindName(ShortcutKind kind) => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());

    /// <summary>
    /// Reads an export document, refusing anything that is not a version 1 or 2 export and leaving out the entries
    /// that are not valid.
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
        if (!TryReadHeader(json, out int version, out reason))
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
            reason = WithLine("The file is damaged: a workspace, a group or a shortcut is incomplete.", exception);
            return false;
        }

        if ((document is null) || !IsWellFormed(document))
        {
            reason = "The file is damaged: a workspace, a group or a shortcut is missing.";
            return false;
        }

        int skipped = 0;
        string name = (document.Profile?.Trim() is { Length: > 0 } named) && (_validator.ValidateProfileName(named, []) is null)
            ? named
            : DefaultContentName;

        Workspace[] workspaces = version == 1 ? ReadVersion1(document, ref skipped) : ReadWorkspaces(document.Workspaces, ref skipped);

        import = new ShortcutImport(new Profile(Guid.NewGuid(), name, workspaces), skipped);
        reason = null;
        return true;
    }

    /// <summary>
    /// Checks that a text is JSON holding this format's marker and a version this build reads.
    /// </summary>
    private static bool TryReadHeader(string json, out int version, [NotNullWhen(false)] out string? reason)
    {
        version = 0;

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

            if (!root.TryGetProperty("version", out JsonElement versionElement)
                || (versionElement.ValueKind != JsonValueKind.Number)
                || !versionElement.TryGetInt32(out version))
            {
                reason = "The file does not say which format version it is in.";
                return false;
            }

            if ((version < 1) || (version > CurrentVersion))
            {
                reason = string.Create(
                    CultureInfo.InvariantCulture,
                    $"The file is in format version {version}, which this version of the launchpad cannot read.");
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
                KindName(group.Kind),
                group.Shortcuts
                    .Select(shortcut => new ExportShortcut(
                        shortcut.Name,
                        shortcut.Target,
                        string.IsNullOrWhiteSpace(shortcut.Arguments) ? null : shortcut.Arguments,
                        string.IsNullOrWhiteSpace(shortcut.Description) ? null : shortcut.Description,
                        shortcut.Icon is { } icon ? new ExportIcon(icon.Png, icon.IsCustom) : null))
                    .ToArray()))
            .ToArray();

    /// <summary>
    /// Whether every list holds real entries. The serializer's nullability checks cover properties, not the elements
    /// of a list, so a <c>null</c> inside an array is caught here.
    /// </summary>
    private static bool IsWellFormed(ExportDocument document)
        => (document.Workspaces ?? []).All(workspace => (workspace is not null) && AreWellFormed(workspace.Groups))
            && new[] { document.Applications, document.Directories, document.Urls }.All(AreWellFormed);

    private static bool AreWellFormed(IReadOnlyList<ExportGroup>? groups)
        => (groups ?? []).All(group => (group is not null) && (group.Shortcuts ?? []).All(shortcut => shortcut is not null));

    private static string WithLine(string message, JsonException exception)
        => exception.LineNumber is { } line
            ? string.Create(CultureInfo.InvariantCulture, $"{message} (line {line + 1})")
            : message;

    /// <summary>
    /// The kind a group's <c>kind</c> field names, ignoring case.
    /// </summary>
    private static bool TryReadKind(string? text, out ShortcutKind kind)
    {
        foreach (ShortcutKind candidate in Enum.GetValues<ShortcutKind>())
        {
            if (string.Equals(text?.Trim(), KindName(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// The glyph name to store: a plain identifier, as glyph names are; anything else is dropped.
    /// </summary>
    private static string? ReadIcon(string? name)
    {
        string text = name?.Trim() ?? string.Empty;

        return (text.Length is > 0 and <= MaxIconNameLength) && char.IsAsciiLetter(text[0]) && text.All(char.IsAsciiLetterOrDigit)
            ? text
            : null;
    }

    /// <summary>
    /// The target to store, or <see langword="null"/> when the shortcut form would refuse it: a URL as the form
    /// normalises it, a path when it is absolute — wherever it points — and, for an application, a command name,
    /// whether or not this machine's <c>PATH</c> has it.
    /// </summary>
    private static string? ReadTarget(ShortcutKind kind, string target)
    {
        if (kind == ShortcutKind.Url)
        {
            return UrlNormalizer.TryNormalize(target, out string normalized, out _) ? normalized : null;
        }

        string path = target.Trim();

        if ((kind == ShortcutKind.Application) && !Path.IsPathFullyQualified(path) && PathCommandResolver.IsCommandName(path))
        {
            return path;
        }

        return (path.Length > 0) && !path.Contains('\0', StringComparison.Ordinal) && Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : null;
    }

    /// <summary>
    /// The shortcut's icon to store, or <see langword="null"/> when the file has none, or one that is not a PNG image
    /// of an acceptable size.
    /// </summary>
    private static ShortcutIcon? ReadIcon(ExportIcon? icon)
    {
        if (icon?.Png is not { } png)
        {
            return null;
        }

        ShortcutIcon read = new(png, icon.IsCustom);

        return read.TryReadBytes(out _) ? read : null;
    }

    private static int CountEntries(ExportGroup group) => 1 + (group.Shortcuts?.Count ?? 0);

    /// <summary>
    /// Reads a version 1 document — the three categories — into one workspace, or none when it has no group.
    /// </summary>
    private Workspace[] ReadVersion1(ExportDocument document, ref int skipped)
    {
        List<ShortcutGroup> groups = [];

        foreach ((ShortcutKind kind, IReadOnlyList<ExportGroup>? category) in new[]
        {
            (ShortcutKind.Application, document.Applications),
            (ShortcutKind.Directory, document.Directories),
            (ShortcutKind.Url, document.Urls),
        })
        {
            foreach (ExportGroup group in category ?? [])
            {
                if (ReadGroup(kind, group, ref skipped) is { } read)
                {
                    groups.Add(read);
                }
            }
        }

        return groups.Count == 0 ? [] : [new Workspace(Guid.NewGuid(), Workspace.DefaultTitle, groups)];
    }

    private Workspace[] ReadWorkspaces(IReadOnlyList<ExportWorkspace>? workspaces, ref int skipped)
    {
        List<Workspace> read = [];

        foreach (ExportWorkspace workspace in workspaces ?? [])
        {
            IReadOnlyList<ExportGroup> groups = workspace.Groups ?? [];

            if ((_validator.ValidateWorkspaceTitle(workspace.Title, []) is not null)
                || (_validator.ValidateWorkspaceDescription(workspace.Description) is not null))
            {
                skipped += 1 + groups.Sum(CountEntries);
                continue;
            }

            List<ShortcutGroup> kept = [];

            foreach (ExportGroup group in groups)
            {
                if (!TryReadKind(group.Kind, out ShortcutKind kind))
                {
                    skipped += CountEntries(group);
                    continue;
                }

                if (ReadGroup(kind, group, ref skipped) is { } readGroup)
                {
                    kept.Add(readGroup);
                }
            }

            string? description = string.IsNullOrWhiteSpace(workspace.Description) ? null : workspace.Description.Trim();

            read.Add(new Workspace(Guid.NewGuid(), workspace.Title.Trim(), kept, description, ReadIcon(workspace.Icon)));
        }

        return [.. read];
    }

    private ShortcutGroup? ReadGroup(ShortcutKind kind, ExportGroup group, ref int skipped)
    {
        if (_validator.ValidateGroupName(group.Name, []) is not null)
        {
            skipped += CountEntries(group);
            return null;
        }

        List<Shortcut> kept = [];

        foreach (ExportShortcut shortcut in group.Shortcuts ?? [])
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

        return new ShortcutGroup(Guid.NewGuid(), group.Name.Trim(), kind, kept);
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

        return new Shortcut(Guid.NewGuid(), shortcut.Name.Trim(), target, arguments, description, ReadIcon(shortcut.Icon));
    }

    /// <summary>
    /// The export document, version 2 — or version 1, whose three categories are read but never written.
    /// </summary>
    /// <param name="Format">Always <see cref="FormatName"/>.</param>
    /// <param name="Version">1 or 2.</param>
    /// <param name="Profile">The name of the profile it was exported from, for information.</param>
    /// <param name="Workspaces">Version 2: the workspaces; none when absent.</param>
    /// <param name="Applications">Version 1: the groups of the applications category; none when absent.</param>
    /// <param name="Directories">Version 1: the groups of the directories category; none when absent.</param>
    /// <param name="Urls">Version 1: the groups of the URLs category; none when absent.</param>
    private sealed record ExportDocument(
        string Format,
        int Version,
        string? Profile = null,
        IReadOnlyList<ExportWorkspace>? Workspaces = null,
        IReadOnlyList<ExportGroup>? Applications = null,
        IReadOnlyList<ExportGroup>? Directories = null,
        IReadOnlyList<ExportGroup>? Urls = null);

    /// <summary>
    /// One exported workspace.
    /// </summary>
    /// <param name="Title">The workspace's title.</param>
    /// <param name="Description">What it is for, when it says.</param>
    /// <param name="Icon">The name of its glyph, when it has one.</param>
    /// <param name="Groups">Its groups, in display order; none when absent.</param>
    private sealed record ExportWorkspace(
        string Title,
        string? Description = null,
        string? Icon = null,
        IReadOnlyList<ExportGroup>? Groups = null);

    /// <summary>
    /// One exported group.
    /// </summary>
    /// <param name="Name">The group's name.</param>
    /// <param name="Kind">Version 2: its kind's camelCase name. Version 1 groups have none: their category says.</param>
    /// <param name="Shortcuts">Its shortcuts, in display order; none when absent.</param>
    private sealed record ExportGroup(string Name, string? Kind = null, IReadOnlyList<ExportShortcut>? Shortcuts = null);

    /// <summary>
    /// One exported shortcut.
    /// </summary>
    /// <param name="Name">The shortcut's name.</param>
    /// <param name="Target">The executable path or command, the directory path, the URL, or the document's path.</param>
    /// <param name="Arguments">Command-line arguments, for an application that has some.</param>
    /// <param name="Description">What the shortcut is for, when it says.</param>
    /// <param name="Icon">The icon its tile shows, when it has one.</param>
    private sealed record ExportShortcut(
        string Name,
        string Target,
        string? Arguments = null,
        string? Description = null,
        ExportIcon? Icon = null);

    /// <summary>
    /// One exported icon. The image is optional here so that a damaged icon costs only itself, not the file.
    /// </summary>
    /// <param name="Png">The image: a PNG file, in base64.</param>
    /// <param name="IsCustom">Whether the user chose it.</param>
    private sealed record ExportIcon(string? Png = null, bool IsCustom = false);
}
