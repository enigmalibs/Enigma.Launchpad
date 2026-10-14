using System.Collections.Generic;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Core.Validation;

/// <summary>
/// The rules a profile, a workspace, a group or a shortcut must meet before it is stored. Every message is worded for
/// the user.
/// </summary>
public interface IShortcutValidator
{
    /// <summary>
    /// Checks a shortcut's name.
    /// </summary>
    /// <param name="name">The name as typed.</param>
    /// <returns>Why the name is refused, or <see langword="null"/> when it is accepted.</returns>
    string? ValidateShortcutName(string? name);

    /// <summary>
    /// Checks a shortcut's description, which may be left empty.
    /// </summary>
    /// <param name="description">The description as typed.</param>
    /// <returns>Why the description is refused, or <see langword="null"/> when it is accepted.</returns>
    string? ValidateDescription(string? description);

    /// <summary>
    /// Checks a group's name against the other groups of its kind in its workspace.
    /// </summary>
    /// <param name="name">The name as typed.</param>
    /// <param name="otherGroupNames">The names of the workspace's other groups of the same kind — not the one being
    /// renamed.</param>
    /// <returns>Why the name is refused, or <see langword="null"/> when it is accepted.</returns>
    string? ValidateGroupName(string? name, IEnumerable<string> otherGroupNames);

    /// <summary>
    /// Checks a workspace's title against the other workspaces of its profile.
    /// </summary>
    /// <param name="title">The title as typed.</param>
    /// <param name="otherTitles">The titles of the profile's other workspaces — not the one being edited.</param>
    /// <returns>Why the title is refused, or <see langword="null"/> when it is accepted.</returns>
    string? ValidateWorkspaceTitle(string? title, IEnumerable<string> otherTitles);

    /// <summary>
    /// Checks a workspace's description, which may be left empty.
    /// </summary>
    /// <param name="description">The description as typed.</param>
    /// <returns>Why the description is refused, or <see langword="null"/> when it is accepted.</returns>
    string? ValidateWorkspaceDescription(string? description);

    /// <summary>
    /// Checks a profile's name against the other profiles.
    /// </summary>
    /// <param name="name">The name as typed.</param>
    /// <param name="otherProfileNames">The names of the other profiles — not the one being renamed.</param>
    /// <returns>Why the name is refused, or <see langword="null"/> when it is accepted.</returns>
    string? ValidateProfileName(string? name, IEnumerable<string> otherProfileNames);

    /// <summary>
    /// Checks a shortcut's target for its kind, and puts it in the form to store.
    /// </summary>
    /// <param name="kind">The shortcut's kind.</param>
    /// <param name="target">The target as typed.</param>
    /// <returns>The outcome.</returns>
    TargetValidation ValidateTarget(ShortcutKind kind, string? target);
}
