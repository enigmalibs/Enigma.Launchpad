using System;
using System.Collections.Generic;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// A user-made group of shortcuts of one kind inside a workspace, in the order the user arranged them.
/// </summary>
/// <param name="Id">Stable identity, used to find the group again across renames and moves.</param>
/// <param name="Name">What the group is called; unique among the workspace's groups of the same kind, ignoring
/// case.</param>
/// <param name="Kind">The kind of every shortcut in the group: a group never mixes kinds, and never changes kind.</param>
/// <param name="Shortcuts">The group's shortcuts, in display order.</param>
/// <param name="IsCollapsed">Whether the group is shown folded.</param>
/// <param name="Width">How much of a line of the workspace the group takes; the whole line unless the user chose
/// otherwise.</param>
public sealed record ShortcutGroup(
    Guid Id,
    string Name,
    ShortcutKind Kind,
    IReadOnlyList<Shortcut> Shortcuts,
    bool IsCollapsed = false,
    GroupWidth Width = GroupWidth.Full);
