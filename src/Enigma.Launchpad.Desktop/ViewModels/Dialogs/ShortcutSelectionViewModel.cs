using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.ViewModels.Pages;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// A dialog choosing part of a profile's shortcuts — what to export, or what to import — as a tree of checkboxes:
/// the workspaces, their groups, and the groups' shortcuts.
/// </summary>
/// <remarks>
/// Everything starts chosen, since taking it all is the common case, and open, so every shortcut is in view without
/// unfolding anything; a long tree scrolls.
/// </remarks>
public sealed class ShortcutSelectionViewModel : ObservableObject
{
    private readonly Profile _content;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="message">What the dialog says above the tree.</param>
    /// <param name="content">The shortcuts to choose from.</param>
    public ShortcutSelectionViewModel(string message, Profile content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(content);

        Message = message;
        _content = content;

        Workspaces = content.Workspaces
            .Select(workspace => SelectionNodeViewModel.ForWorkspace(workspace, Recount))
            .ToArray();

        Recount();
    }

    /// <summary>
    /// Gets what the dialog says above the tree.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the workspaces, in the content's order.
    /// </summary>
    public IReadOnlyList<SelectionNodeViewModel> Workspaces { get; }

    /// <summary>
    /// Gets a value indicating whether anything is chosen.
    /// </summary>
    public bool HasSelection { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets what is chosen, in a few words.
    /// </summary>
    public string Summary { get; private set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>
    /// Returns the content reduced to what is chosen, in its order: a workspace or a group with content when any of
    /// it is chosen, with that only; an empty one when it is chosen itself.
    /// </summary>
    /// <returns>The chosen part, under the content's identity and name.</returns>
    public Profile ToProfile()
        => _content with
        {
            Workspaces = Workspaces
                .Where(workspace => workspace.IsIncluded)
                .Select(workspace => (workspace.Workspace ?? throw new InvalidOperationException("A workspace line has no workspace.")) with
                {
                    Groups = workspace.Children
                        .Where(group => group.IsIncluded)
                        .Select(group => (group.Group ?? throw new InvalidOperationException("A group line has no group.")) with
                        {
                            Shortcuts = group.Children
                                .Where(shortcut => shortcut.IsIncluded)
                                .Select(shortcut => shortcut.Shortcut ?? throw new InvalidOperationException("A shortcut line has no shortcut."))
                                .ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };

    private void Recount()
    {
        SelectionNodeViewModel[] workspaces = Workspaces.Where(workspace => workspace.IsIncluded).ToArray();
        int shortcuts = workspaces
            .SelectMany(workspace => workspace.Children)
            .Where(group => group.IsIncluded)
            .Sum(group => group.Children.Count(shortcut => shortcut.IsIncluded));

        HasSelection = workspaces.Length > 0;
        Summary = HasSelection
            ? $"{ProfileItemViewModel.Summarise(workspaces.Length, shortcuts)} selected"
            : "Nothing selected";
    }
}
