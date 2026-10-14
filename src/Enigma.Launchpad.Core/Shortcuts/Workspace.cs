using System;
using System.Collections.Generic;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// A user-made workspace: a title, a line saying what it is for, an icon, and groups of shortcuts — each group of one
/// kind, the workspace mixing groups of every kind.
/// </summary>
/// <param name="Id">Stable identity, used to find the workspace again across edits and moves.</param>
/// <param name="Title">What the workspace is called; unique among the profile's workspaces, ignoring case.</param>
/// <param name="Groups">The workspace's groups, in display order.</param>
/// <param name="Description">What the workspace is for; <see langword="null"/> when there is nothing to say.</param>
/// <param name="Icon">The name of the glyph the workspace shows; <see langword="null"/> for the default one. Glyphs
/// belong to the application, so a name it does not know also shows the default.</param>
public sealed record Workspace(
    Guid Id,
    string Title,
    IReadOnlyList<ShortcutGroup> Groups,
    string? Description = null,
    string? Icon = null)
{
    /// <summary>
    /// The title of the workspace a profile's shortcuts are moved into when they were kept before there were
    /// workspaces — in an older <c>launchpad.json</c>, or an older export file.
    /// </summary>
    public const string DefaultTitle = "Shortcuts";

    /// <summary>
    /// The description of the workspace an older <c>launchpad.json</c>'s shortcuts are moved into.
    /// </summary>
    public const string MigratedDescription = "Your shortcuts from before workspaces.";

    /// <summary>
    /// The glyph of the workspace older shortcuts are moved into.
    /// </summary>
    public const string MigratedIcon = "SquaresFour";

    /// <summary>
    /// Creates an empty workspace.
    /// </summary>
    /// <param name="title">What the workspace is called.</param>
    /// <param name="description">What it is for, if anything.</param>
    /// <param name="icon">The name of its glyph, if any.</param>
    /// <returns>The workspace, with a new identity.</returns>
    public static Workspace Create(string title, string? description = null, string? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new Workspace(Guid.NewGuid(), title, [], description, icon);
    }
}
