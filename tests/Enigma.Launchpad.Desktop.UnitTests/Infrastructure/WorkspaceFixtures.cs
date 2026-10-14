using System.Collections.Generic;
using System.Linq;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Test shorthand for documents built around one workspace — what most page tests need: the groups of one kind in a
/// profile's first workspace, read or replaced. The application itself always names the workspace it works on.
/// </summary>
internal static class WorkspaceFixtures
{
    extension(Profile profile)
    {
        /// <summary>
        /// Gets the groups of one kind in the profile's first workspace; none when it has no workspace.
        /// </summary>
        public IReadOnlyList<ShortcutGroup> GroupsOf(ShortcutKind kind)
            => profile.Workspaces.Count == 0 ? [] : profile.Workspaces[0].Groups.Where(group => group.Kind == kind).ToArray();

        /// <summary>
        /// Returns the profile with the groups of one kind in its first workspace replaced — the other kinds' groups
        /// keep their places — creating a <see cref="Workspace.DefaultTitle"/> workspace when it has none. Each group
        /// is given the kind.
        /// </summary>
        public Profile With(ShortcutKind kind, IReadOnlyList<ShortcutGroup> groups)
        {
            Workspace workspace = profile.Workspaces.Count > 0 ? profile.Workspaces[0] : Workspace.Create(Workspace.DefaultTitle);

            List<ShortcutGroup> merged = [.. workspace.Groups];
            int at = merged.FindIndex(group => group.Kind == kind);
            merged.RemoveAll(group => group.Kind == kind);
            merged.InsertRange(at < 0 ? merged.Count : at, groups.Select(group => group.Kind == kind ? group : group with { Kind = kind }));

            workspace = workspace with { Groups = merged };

            return profile.Workspaces.Count > 0 ? profile.ReplaceWorkspace(workspace) : profile.AddWorkspace(workspace);
        }
    }

    extension(LaunchpadData data)
    {
        /// <summary>
        /// Gets the groups of one kind in the first workspace of the profile in use.
        /// </summary>
        public IReadOnlyList<ShortcutGroup> GroupsOf(ShortcutKind kind) => data.ActiveProfile.GroupsOf(kind);

        /// <summary>
        /// Returns the document with the groups of one kind replaced in the first workspace of the profile in use.
        /// </summary>
        public LaunchpadData With(ShortcutKind kind, IReadOnlyList<ShortcutGroup> groups)
            => data.WithProfile(data.ActiveProfile.With(kind, groups));
    }

    extension(ILaunchpadSession session)
    {
        /// <summary>
        /// Gets the groups of one kind in the first workspace of the profile in use.
        /// </summary>
        public IReadOnlyList<ShortcutGroup> GroupsOf(ShortcutKind kind) => session.ActiveProfile.GroupsOf(kind);

        /// <summary>
        /// Replaces the groups of one kind in the first workspace of the profile in use, as an open workspace saving
        /// itself would — adding a <see cref="Workspace.DefaultTitle"/> workspace first when there is none.
        /// </summary>
        public void Update(ShortcutKind kind, IReadOnlyList<ShortcutGroup> groups)
        {
            if (session.Workspaces.Count == 0)
            {
                session.AddWorkspace(Workspace.DefaultTitle, null, Workspace.MigratedIcon);
            }

            session.UpdateGroups(session.Workspaces[0].Id, session.ActiveProfile.With(kind, groups).Workspaces[0].Groups);
        }
    }
}
