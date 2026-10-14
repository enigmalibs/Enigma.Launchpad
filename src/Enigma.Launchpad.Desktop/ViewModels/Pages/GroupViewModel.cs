using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.DragAndDrop;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One group of an open workspace: its name, its kind, whether it is folded, and its shortcuts.
/// </summary>
/// <remarks>
/// A group's commands act on the page it belongs to — a rename is checked against its siblings, a move
/// changes the page's order — so they forward to the page rather than doing the work themselves.
/// </remarks>
public sealed class GroupViewModel : ObservableObject
{
    private readonly WorkspacePageViewModel _page;
    private readonly bool _isInitialised;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="page">The page the group is on.</param>
    /// <param name="group">The group as stored.</param>
    public GroupViewModel(WorkspacePageViewModel page, ShortcutGroup group)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(group);

        _page = page;
        Id = group.Id;
        Kind = group.Kind;
        Name = group.Name;
        IsExpanded = !group.IsCollapsed;
        Shortcuts = new ObservableCollection<ShortcutViewModel>(group.Shortcuts.Select(shortcut => new ShortcutViewModel(page, this, shortcut)));
        Shortcuts.CollectionChanged += OnShortcutsChanged;

        AddShortcutCommand = new AsyncRelayCommand(() => _page.AddShortcutAsync(this));

        RenameCommand = new AsyncRelayCommand(() => _page.RenameGroupAsync(this));
        MoveToWorkspaceCommand = new AsyncRelayCommand(() => _page.MoveGroupToWorkspaceAsync(this));
        DeleteCommand = new AsyncRelayCommand(() => _page.DeleteGroupAsync(this));
        MoveUpCommand = new RelayCommand(() => _page.MoveGroup(this, -1), () => _page.CanMoveGroup(this, -1));
        MoveDownCommand = new RelayCommand(() => _page.MoveGroup(this, 1), () => _page.CanMoveGroup(this, 1));

        _isInitialised = true;
    }

    /// <summary>
    /// Gets the group's identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the kind of every shortcut in the group.
    /// </summary>
    public ShortcutKind Kind { get; }

    /// <summary>
    /// Gets the glyph of the group's kind, beside its name.
    /// </summary>
    public PhosphorIcon KindGlyph => Glyphs.OfKind(Kind);

    /// <summary>
    /// Gets what the group's kind is called, for its glyph's tooltip.
    /// </summary>
    public string KindTitle => new GroupKindOption(Kind).Title;

    /// <summary>
    /// Gets what an empty group says about how it fills up.
    /// </summary>
    public string EmptyText => $"No {GroupKindOption.Noun(Kind)} in this group yet. Add one with +.";

    /// <summary>
    /// Gets or sets the group's name.
    /// </summary>
    public string Name { get; set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the group shows its shortcuts. A change is saved.
    /// </summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && _isInitialised)
            {
                _page.Persist();
            }
        }
    }

    /// <summary>
    /// Gets the group's shortcuts, in display order.
    /// </summary>
    public ObservableCollection<ShortcutViewModel> Shortcuts { get; }

    /// <summary>
    /// Gets a value indicating whether the group holds any shortcut.
    /// </summary>
    public bool HasShortcuts => Shortcuts.Count > 0;

    /// <summary>
    /// Gets the number of shortcuts, for the badge beside the name.
    /// </summary>
    public int Count => Shortcuts.Count;

    /// <summary>
    /// Gets or sets what the group shows while something is dragged over it.
    /// </summary>
    public DropHint DropHint
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsDropBefore));
                OnPropertyChanged(nameof(IsDropAfter));
                OnPropertyChanged(nameof(IsDropInside));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a drop would land in front of the group.
    /// </summary>
    public bool IsDropBefore => DropHint == DropHint.Before;

    /// <summary>
    /// Gets a value indicating whether a drop would land behind the group.
    /// </summary>
    public bool IsDropAfter => DropHint == DropHint.After;

    /// <summary>
    /// Gets a value indicating whether a drop would land inside the group, at its end.
    /// </summary>
    public bool IsDropInside => DropHint == DropHint.Inside;

    /// <summary>
    /// Gets or sets a value indicating whether the group is the one being dragged.
    /// </summary>
    public bool IsDragging { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the command adding a shortcut at the end of the group.
    /// </summary>
    public AsyncRelayCommand AddShortcutCommand { get; }

    /// <summary>
    /// Gets the command renaming the group.
    /// </summary>
    public AsyncRelayCommand RenameCommand { get; }

    /// <summary>
    /// Gets the command moving the group, with its shortcuts, to another workspace.
    /// </summary>
    public AsyncRelayCommand MoveToWorkspaceCommand { get; }

    /// <summary>
    /// Gets the command deleting the group, after confirmation.
    /// </summary>
    public AsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// Gets the command moving the group one place up.
    /// </summary>
    public RelayCommand MoveUpCommand { get; }

    /// <summary>
    /// Gets the command moving the group one place down.
    /// </summary>
    public RelayCommand MoveDownCommand { get; }

    /// <summary>
    /// Gets the group as it should be stored.
    /// </summary>
    /// <returns>The group.</returns>
    public ShortcutGroup ToModel() => new(Id, Name, Kind, Shortcuts.Select(shortcut => shortcut.Model).ToArray(), !IsExpanded);

    /// <summary>
    /// Re-evaluates the commands that depend on the group's position on the page.
    /// </summary>
    internal void RefreshPosition()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private void OnShortcutsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasShortcuts));
        OnPropertyChanged(nameof(Count));

        foreach (ShortcutViewModel shortcut in Shortcuts)
        {
            shortcut.RefreshPosition();
        }
    }
}
