using System;
using System.Collections.Generic;
using System.Linq;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// One set of shortcuts the user switches between — say "Work" and "Home": its own workspaces.
/// </summary>
/// <param name="Id">Stable identity, used to find the profile again across renames and moves.</param>
/// <param name="Name">What the profile is called; unique among the profiles, ignoring case.</param>
/// <param name="Workspaces">The profile's workspaces, in display order.</param>
/// <remarks>Every operation returns a new profile and leaves this one as it was.</remarks>
public sealed record Profile(Guid Id, string Name, IReadOnlyList<Workspace> Workspaces)
{
    /// <summary>
    /// The name of the profile a new launchpad starts with, and that an older file's shortcuts are moved into.
    /// </summary>
    public const string DefaultName = "Default";

    /// <summary>
    /// Creates a profile with no workspace.
    /// </summary>
    /// <param name="name">What the profile is called.</param>
    /// <returns>The profile, with a new identity.</returns>
    public static Profile Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Profile(Guid.NewGuid(), name, []);
    }

    /// <summary>
    /// Finds a workspace by identity.
    /// </summary>
    /// <param name="id">The workspace's identity.</param>
    /// <returns>The workspace, or <see langword="null"/>.</returns>
    public Workspace? FindWorkspace(Guid id) => Workspaces.FirstOrDefault(workspace => workspace.Id == id);

    /// <summary>
    /// Returns a copy of this profile with a workspace added.
    /// </summary>
    /// <param name="workspace">The workspace to add.</param>
    /// <param name="index">Where it goes in the list; at the end when omitted.</param>
    /// <returns>The new profile.</returns>
    /// <exception cref="ArgumentException">A workspace with the same identity is already there.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    public Profile AddWorkspace(Workspace workspace, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (FindWorkspace(workspace.Id) is not null)
        {
            throw new ArgumentException("A workspace with this identity is already in the profile.", nameof(workspace));
        }

        int at = index ?? Workspaces.Count;
        ArgumentOutOfRangeException.ThrowIfNegative(at, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(at, Workspaces.Count, nameof(index));

        List<Workspace> workspaces = [.. Workspaces];
        workspaces.Insert(at, workspace);

        return this with { Workspaces = workspaces };
    }

    /// <summary>
    /// Returns a copy of this profile with a workspace replaced by another version of itself.
    /// </summary>
    /// <param name="workspace">The workspace, with the identity of the one it replaces.</param>
    /// <returns>The new profile.</returns>
    /// <exception cref="ArgumentException">No workspace has this identity.</exception>
    public Profile ReplaceWorkspace(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        RequireWorkspace(workspace.Id);

        return this with { Workspaces = Workspaces.Select(existing => existing.Id == workspace.Id ? workspace : existing).ToArray() };
    }

    /// <summary>
    /// Returns a copy of this profile without a workspace.
    /// </summary>
    /// <param name="id">The workspace to remove.</param>
    /// <returns>The new profile.</returns>
    /// <exception cref="ArgumentException">No workspace has this identity.</exception>
    public Profile RemoveWorkspace(Guid id)
    {
        RequireWorkspace(id);
        return this with { Workspaces = Workspaces.Where(workspace => workspace.Id != id).ToArray() };
    }

    /// <summary>
    /// Whether a workspace can move by an offset without leaving the list.
    /// </summary>
    /// <param name="id">The workspace.</param>
    /// <param name="offset">How many places, negative for up.</param>
    /// <returns><see langword="true"/> when the move stays in the list.</returns>
    public bool CanMoveWorkspace(Guid id, int offset)
    {
        int index = IndexOfWorkspace(id);
        int target = index + offset;

        return (index >= 0) && (offset != 0) && (target >= 0) && (target < Workspaces.Count);
    }

    /// <summary>
    /// Returns a copy of this profile with a workspace moved by an offset.
    /// </summary>
    /// <param name="id">The workspace.</param>
    /// <param name="offset">How many places, negative for up.</param>
    /// <returns>The new profile.</returns>
    /// <exception cref="ArgumentException">The workspace is unknown, or the move would leave the list.</exception>
    public Profile MoveWorkspace(Guid id, int offset)
    {
        Workspace workspace = RequireWorkspace(id);

        if (!CanMoveWorkspace(id, offset))
        {
            throw new ArgumentException("The workspace cannot move this far.", nameof(offset));
        }

        int index = IndexOfWorkspace(id);
        List<Workspace> workspaces = [.. Workspaces];
        workspaces.RemoveAt(index);
        workspaces.Insert(index + offset, workspace);

        return this with { Workspaces = workspaces };
    }

    /// <summary>
    /// Copies this profile under another name. The copy, its workspaces, groups and shortcuts all get new identities,
    /// so no identity is ever shared between two profiles.
    /// </summary>
    /// <param name="name">What the copy is called.</param>
    /// <returns>The copy.</returns>
    public Profile Duplicate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Profile(Guid.NewGuid(), name, Workspaces.Select(Copy).ToArray());
    }

    /// <summary>
    /// Copies a workspace under new identities — its own, its groups' and its shortcuts'.
    /// </summary>
    /// <param name="workspace">The workspace to copy.</param>
    /// <returns>The copy, with the same title, description, icon and content.</returns>
    public static Workspace Copy(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return workspace with
        {
            Id = Guid.NewGuid(),
            Groups = workspace.Groups
                .Select(group => group with
                {
                    Id = Guid.NewGuid(),
                    Shortcuts = group.Shortcuts.Select(shortcut => shortcut with { Id = Guid.NewGuid() }).ToArray(),
                })
                .ToArray(),
        };
    }

    /// <summary>
    /// Returns a copy of this profile with another profile's workspaces, groups and shortcuts added to it.
    /// </summary>
    /// <param name="source">What to add.</param>
    /// <returns>The new profile; its identity and name are this one's.</returns>
    /// <remarks>
    /// <para>
    /// A workspace whose title matches one of this profile's, ignoring case, adds its groups to it; any other
    /// workspace is added at the end, with its description and icon. Within a workspace, a group whose name and kind
    /// match one already there adds its shortcuts at that group's end; any other group is added, unfolded, at the
    /// end. A shortcut identical to one already in the group it would join — same name and target, ignoring case,
    /// and same arguments — is left out, so importing the same content twice adds nothing the second time.
    /// </para>
    /// <para>Everything added gets a new identity, so no identity is ever shared with the source.</para>
    /// </remarks>
    public Profile Import(Profile source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<Workspace> workspaces = [.. Workspaces];

        foreach (Workspace incoming in source.Workspaces)
        {
            int index = workspaces.FindIndex(existing => SameText(existing.Title, incoming.Title));

            if (index < 0)
            {
                workspaces.Add(incoming with { Id = Guid.NewGuid(), Groups = [] });
                index = workspaces.Count - 1;
            }

            workspaces[index] = workspaces[index] with { Groups = Merge(workspaces[index].Groups, incoming.Groups) };
        }

        return this with { Workspaces = workspaces };
    }

    /// <summary>
    /// Counts the profile's workspaces.
    /// </summary>
    /// <returns>The number of workspaces.</returns>
    public int CountWorkspaces() => Workspaces.Count;

    /// <summary>
    /// Counts the profile's groups, in every workspace.
    /// </summary>
    /// <returns>The number of groups.</returns>
    public int CountGroups() => Workspaces.Sum(workspace => workspace.Groups.Count);

    /// <summary>
    /// Counts the profile's shortcuts, in every group of every workspace.
    /// </summary>
    /// <returns>The number of shortcuts.</returns>
    public int CountShortcuts() => Workspaces.Sum(workspace => workspace.Groups.Sum(group => group.Shortcuts.Count));

    private static ShortcutGroup[] Merge(IReadOnlyList<ShortcutGroup> groups, IReadOnlyList<ShortcutGroup> incoming)
    {
        List<ShortcutGroup> merged = [.. groups];

        foreach (ShortcutGroup group in incoming)
        {
            int index = merged.FindIndex(existing => (existing.Kind == group.Kind) && SameText(existing.Name, group.Name));

            if (index < 0)
            {
                merged.Add(new ShortcutGroup(Guid.NewGuid(), group.Name, group.Kind, []));
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

    private Workspace RequireWorkspace(Guid id)
        => FindWorkspace(id) ?? throw new ArgumentException("No workspace has this identity.", nameof(id));

    private int IndexOfWorkspace(Guid id)
    {
        for (int index = 0; index < Workspaces.Count; index++)
        {
            if (Workspaces[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }
}
