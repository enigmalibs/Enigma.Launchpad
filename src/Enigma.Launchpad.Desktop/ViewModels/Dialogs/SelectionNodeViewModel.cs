using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// One line of the selection tree — a category, a group or a shortcut — with its checkbox.
/// </summary>
/// <remarks>
/// <para>
/// Checking or unchecking a line does the same to everything under it. A line with lines under it shows the state
/// they share, or indeterminate when they differ; only a line with nothing under it — a shortcut, or an empty group
/// — keeps a state of its own.
/// </para>
/// <para>
/// The user chooses checked or unchecked only: indeterminate is shown, never chosen, and clicking it unchecks.
/// </para>
/// </remarks>
public sealed class SelectionNodeViewModel : ObservableObject
{
    private readonly SelectionNodeViewModel? _parent;
    private readonly Action _selectionChanged;
    private readonly List<SelectionNodeViewModel> _children = [];

    private SelectionNodeViewModel(
        string title,
        string? detail,
        PhosphorIcon icon,
        ShortcutKind kind,
        SelectionNodeViewModel? parent,
        Action selectionChanged)
    {
        Title = title;
        Detail = detail;
        Icon = icon;
        Kind = kind;
        _parent = parent;
        _selectionChanged = selectionChanged;
    }

    /// <summary>
    /// Gets what the line is called.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets what is shown beside the name — a shortcut's target, how many shortcuts a group holds — if anything.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets the line's icon.
    /// </summary>
    public PhosphorIcon Icon { get; }

    /// <summary>
    /// Gets the lines under this one, in display order.
    /// </summary>
    public IReadOnlyList<SelectionNodeViewModel> Children => _children;

    /// <summary>
    /// Gets or sets a value indicating whether the lines under this one are shown. Categories start open and groups
    /// folded, so the dialog opens on a short list; the user unfolds a group to see its shortcuts.
    /// </summary>
    public bool IsExpanded { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets whether the line is chosen: <see langword="true"/>, <see langword="false"/>, or
    /// <see langword="null"/> when only part of what is under it is. Setting <see langword="null"/> does nothing.
    /// </summary>
    public bool? IsChecked
    {
        get => State;
        set
        {
            if (value is not { } chosen)
            {
                return;
            }

            Apply(chosen);
            _parent?.FollowChildren();
            _selectionChanged();
        }
    }

    /// <summary>
    /// Gets the category the line belongs to.
    /// </summary>
    internal ShortcutKind Kind { get; }

    /// <summary>
    /// Gets the group the line stands for; <see langword="null"/> for a category or a shortcut.
    /// </summary>
    internal ShortcutGroup? Group { get; private init; }

    /// <summary>
    /// Gets the shortcut the line stands for; <see langword="null"/> for a category or a group.
    /// </summary>
    internal Shortcut? Shortcut { get; private init; }

    /// <summary>
    /// Gets a value indicating whether anything of the line is chosen: itself, or part of what is under it.
    /// </summary>
    internal bool IsIncluded => State != false;

    /// <summary>
    /// The state behind <see cref="IsChecked"/>, set without passing it on: what the user chose is passed on by
    /// <see cref="IsChecked"/> alone.
    /// </summary>
    private bool? State { get; set => SetProperty(ref field, value, nameof(IsChecked)); } = true;

    /// <summary>
    /// Builds a category's line, open, with a line for each of its groups and their shortcuts, everything chosen.
    /// </summary>
    /// <param name="kind">The category.</param>
    /// <param name="title">What the category is called.</param>
    /// <param name="icon">The category's icon, which its shortcuts share.</param>
    /// <param name="groups">The category's groups.</param>
    /// <param name="selectionChanged">Called once after every choice the user makes.</param>
    /// <returns>The category's line.</returns>
    internal static SelectionNodeViewModel ForCategory(
        ShortcutKind kind,
        string title,
        PhosphorIcon icon,
        IReadOnlyList<ShortcutGroup> groups,
        Action selectionChanged)
    {
        SelectionNodeViewModel category = new(title, null, icon, kind, null, selectionChanged)
        {
            IsExpanded = true,
        };

        foreach (ShortcutGroup group in groups)
        {
            SelectionNodeViewModel groupNode = new(group.Name, Count(group.Shortcuts.Count), PhosphorIcon.SquaresFour, kind, category, selectionChanged)
            {
                Group = group,
            };

            foreach (Shortcut shortcut in group.Shortcuts)
            {
                groupNode._children.Add(new SelectionNodeViewModel(shortcut.Name, shortcut.Target, icon, kind, groupNode, selectionChanged)
                {
                    Shortcut = shortcut,
                });
            }

            category._children.Add(groupNode);
        }

        return category;
    }

    private static string Count(int shortcuts)
        => shortcuts switch
        {
            0 => "empty",
            1 => "1 shortcut",
            _ => $"{shortcuts} shortcuts",
        };

    /// <summary>
    /// Takes a state, and gives it to everything under the line.
    /// </summary>
    private void Apply(bool chosen)
    {
        State = chosen;

        foreach (SelectionNodeViewModel child in _children)
        {
            child.Apply(chosen);
        }
    }

    /// <summary>
    /// Takes the state the lines under this one share, or indeterminate, and has the line above do the same.
    /// </summary>
    private void FollowChildren()
    {
        State = _children.All(child => child.State == true) ? true
            : _children.All(child => child.State == false) ? false
            : null;

        _parent?.FollowChildren();
    }
}
