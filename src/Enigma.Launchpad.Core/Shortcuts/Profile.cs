using System;
using System.Collections.Generic;
using System.Linq;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// One set of shortcuts the user switches between — say "Work" and "Home": its own groups in each category.
/// </summary>
/// <param name="Id">Stable identity, used to find the profile again across renames and moves.</param>
/// <param name="Name">What the profile is called; unique among the profiles, ignoring case.</param>
/// <param name="Applications">The groups of the applications category.</param>
/// <param name="Directories">The groups of the directories category.</param>
/// <param name="Urls">The groups of the URLs category.</param>
public sealed record Profile(
    Guid Id,
    string Name,
    IReadOnlyList<ShortcutGroup> Applications,
    IReadOnlyList<ShortcutGroup> Directories,
    IReadOnlyList<ShortcutGroup> Urls)
{
    /// <summary>
    /// The name of the profile a new launchpad starts with, and that an older file's shortcuts are moved into.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// Creates a profile with no group in any category.
    /// </summary>
    /// <param name="name">What the profile is called.</param>
    /// <returns>The profile, with a new identity.</returns>
    public static Profile Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Profile(Guid.NewGuid(), name, [], [], []);
    }

    /// <summary>
    /// Gets the groups of one category.
    /// </summary>
    /// <param name="kind">The category.</param>
    /// <returns>Its groups, in display order.</returns>
    public IReadOnlyList<ShortcutGroup> GroupsOf(ShortcutKind kind)
        => kind switch
        {
            ShortcutKind.Application => Applications,
            ShortcutKind.Directory => Directories,
            ShortcutKind.Url => Urls,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown shortcut kind."),
        };

    /// <summary>
    /// Returns a copy of this profile with one category's groups replaced.
    /// </summary>
    /// <param name="kind">The category to replace.</param>
    /// <param name="groups">Its new groups, in display order.</param>
    /// <returns>The new profile.</returns>
    public Profile With(ShortcutKind kind, IReadOnlyList<ShortcutGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return kind switch
        {
            ShortcutKind.Application => this with { Applications = groups },
            ShortcutKind.Directory => this with { Directories = groups },
            ShortcutKind.Url => this with { Urls = groups },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown shortcut kind."),
        };
    }

    /// <summary>
    /// Copies this profile under another name. The copy, its groups and its shortcuts all get new identities,
    /// so no identity is ever shared between two profiles.
    /// </summary>
    /// <param name="name">What the copy is called.</param>
    /// <returns>The copy.</returns>
    public Profile Duplicate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Profile(Guid.NewGuid(), name, Copy(Applications), Copy(Directories), Copy(Urls));
    }

    /// <summary>
    /// Returns a copy of this profile with another profile's groups and shortcuts added to it. A group whose name
    /// matches one of the same category, ignoring case, adds its shortcuts at that group's end; any other group is
    /// added, unfolded, at the end of its category. A shortcut identical to one already in the group it would join —
    /// same name and target, ignoring case, and same arguments — is left out, so importing the same content twice
    /// adds nothing the second time.
    /// </summary>
    /// <param name="source">What to add.</param>
    /// <returns>The new profile; its identity and name are this one's.</returns>
    /// <remarks>Everything added gets a new identity, so no identity is ever shared with the source.</remarks>
    public Profile Import(Profile source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Profile result = this;

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            result = result.With(kind, Merge(GroupsOf(kind), source.GroupsOf(kind)));
        }

        return result;
    }

    /// <summary>
    /// Counts the profile's groups, in every category.
    /// </summary>
    /// <returns>The number of groups.</returns>
    public int CountGroups() => Applications.Count + Directories.Count + Urls.Count;

    /// <summary>
    /// Counts the profile's shortcuts, in every group of every category.
    /// </summary>
    /// <returns>The number of shortcuts.</returns>
    public int CountShortcuts() => Applications.Concat(Directories).Concat(Urls).Sum(group => group.Shortcuts.Count);

    private static ShortcutGroup[] Merge(IReadOnlyList<ShortcutGroup> groups, IReadOnlyList<ShortcutGroup> incoming)
    {
        List<ShortcutGroup> merged = [.. groups];

        foreach (ShortcutGroup group in incoming)
        {
            int index = merged.FindIndex(existing => SameText(existing.Name, group.Name));

            if (index < 0)
            {
                merged.Add(new ShortcutGroup(Guid.NewGuid(), group.Name, []));
                index = merged.Count - 1;
            }

            List<Shortcut> shortcuts = [.. merged[index].Shortcuts];

            foreach (Shortcut shortcut in group.Shortcuts)
            {
                if (!shortcuts.Any(existing => IsSame(existing, shortcut)))
                {
                    shortcuts.Add(shortcut with { Id = Guid.NewGuid() });
                }
            }

            merged[index] = merged[index] with { Shortcuts = shortcuts };
        }

        return [.. merged];
    }

    private static bool IsSame(Shortcut first, Shortcut second)
        => SameText(first.Name, second.Name)
            && SameText(first.Target, second.Target)
            && string.Equals(ArgumentsOf(first), ArgumentsOf(second), StringComparison.Ordinal);

    private static bool SameText(string first, string second)
        => string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? ArgumentsOf(Shortcut shortcut)
        => string.IsNullOrWhiteSpace(shortcut.Arguments) ? null : shortcut.Arguments.Trim();

    private static ShortcutGroup[] Copy(IReadOnlyList<ShortcutGroup> groups)
        => groups
            .Select(group => group with
            {
                Id = Guid.NewGuid(),
                Shortcuts = group.Shortcuts.Select(shortcut => shortcut with { Id = Guid.NewGuid() }).ToArray(),
            })
            .ToArray();
}
