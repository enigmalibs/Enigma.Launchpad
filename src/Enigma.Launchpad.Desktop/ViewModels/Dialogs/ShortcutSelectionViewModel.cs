using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.ViewModels.Pages;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// A dialog choosing part of a profile's shortcuts — what to export, or what to import — as a tree of checkboxes:
/// the categories, their groups, and the groups' shortcuts.
/// </summary>
/// <remarks>
/// Everything starts chosen, since taking it all is the common case. The categories start open and their groups
/// folded; a long tree scrolls. A category with no group is not shown.
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

        Categories = Enum.GetValues<ShortcutKind>()
            .Where(kind => content.GroupsOf(kind).Count > 0)
            .Select(kind => SelectionNodeViewModel.ForCategory(kind, TitleOf(kind), IconOf(kind), content.GroupsOf(kind), Recount))
            .ToArray();

        Recount();
    }

    /// <summary>
    /// Gets what the dialog says above the tree.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the categories that have groups, in the rail's order.
    /// </summary>
    public IReadOnlyList<SelectionNodeViewModel> Categories { get; }

    /// <summary>
    /// Gets a value indicating whether anything is chosen.
    /// </summary>
    public bool HasSelection { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets what is chosen, in a few words.
    /// </summary>
    public string Summary { get; private set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>
    /// Returns the content reduced to what is chosen, in its order: a group with shortcuts when any of them is chosen,
    /// with those only; an empty group when it is chosen itself.
    /// </summary>
    /// <returns>The chosen part, under the content's identity and name.</returns>
    public Profile ToProfile()
    {
        Profile chosen = _content;

        foreach (ShortcutKind kind in Enum.GetValues<ShortcutKind>())
        {
            chosen = chosen.With(kind, ChosenGroups(kind).ToArray());
        }

        return chosen;
    }

    private static string TitleOf(ShortcutKind kind)
        => kind switch
        {
            ShortcutKind.Application => "Applications",
            ShortcutKind.Directory => "Directories",
            _ => "URLs",
        };

    private static PhosphorIcon IconOf(ShortcutKind kind)
        => kind switch
        {
            ShortcutKind.Application => PhosphorIcon.AppWindow,
            ShortcutKind.Directory => PhosphorIcon.Folder,
            _ => PhosphorIcon.Globe,
        };

    private IEnumerable<ShortcutGroup> ChosenGroups(ShortcutKind kind)
        => Categories
            .Where(category => category.Kind == kind)
            .SelectMany(category => category.Children)
            .Where(group => group.IsIncluded)
            .Select(group => (group.Group ?? throw new InvalidOperationException("A group line has no group.")) with
            {
                Shortcuts = group.Children
                    .Where(shortcut => shortcut.IsIncluded)
                    .Select(shortcut => shortcut.Shortcut ?? throw new InvalidOperationException("A shortcut line has no shortcut."))
                    .ToArray(),
            });

    private void Recount()
    {
        SelectionNodeViewModel[] groups = Categories.SelectMany(category => category.Children).Where(group => group.IsIncluded).ToArray();
        int shortcuts = groups.Sum(group => group.Children.Count(shortcut => shortcut.IsIncluded));

        HasSelection = groups.Length > 0;
        Summary = HasSelection
            ? $"{ProfileItemViewModel.Summarise(groups.Length, shortcuts)} selected"
            : "Nothing selected";
    }
}
