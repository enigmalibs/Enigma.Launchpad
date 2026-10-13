using System;
using System.Collections.Generic;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// A user-made group of shortcuts inside one category, in the order the user arranged them.
/// </summary>
/// <param name="Id">Stable identity, used to find the group again across renames and moves.</param>
/// <param name="Name">What the group is called; unique within its category, ignoring case.</param>
/// <param name="Shortcuts">The group's shortcuts, in display order.</param>
/// <param name="IsCollapsed">Whether the group is shown folded.</param>
public sealed record ShortcutGroup(Guid Id, string Name, IReadOnlyList<Shortcut> Shortcuts, bool IsCollapsed = false);
