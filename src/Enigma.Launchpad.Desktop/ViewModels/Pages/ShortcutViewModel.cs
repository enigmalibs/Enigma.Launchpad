using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.DragAndDrop;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One shortcut's tile on a category page.
/// </summary>
/// <remarks>
/// Like a group's, a shortcut's commands act on the page — a move changes its group's order, a delete is
/// confirmed and saved — so they forward to the page rather than doing the work themselves.
/// </remarks>
public sealed class ShortcutViewModel : ObservableObject
{
    private readonly CategoryPageViewModel _page;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="page">The page the shortcut is on.</param>
    /// <param name="group">The group the shortcut is in.</param>
    /// <param name="model">The shortcut as stored.</param>
    public ShortcutViewModel(CategoryPageViewModel page, GroupViewModel group, Shortcut model)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(model);

        _page = page;
        Group = group;
        Model = model;

        OpenCommand = new RelayCommand(() => _page.OpenShortcut(this));
        EditCommand = new AsyncRelayCommand(() => _page.EditShortcutAsync(this));
        FindIconCommand = new AsyncRelayCommand(() => _page.FindShortcutIconAsync(this), () => CanFindIcon);
        CopyTargetCommand = new AsyncRelayCommand(() => _page.CopyShortcutTargetAsync(this));
        RevealCommand = new RelayCommand(() => _page.RevealShortcut(this), () => CanReveal);
        DeleteCommand = new AsyncRelayCommand(() => _page.DeleteShortcutAsync(this));
        MoveUpCommand = new RelayCommand(() => _page.MoveShortcut(this, -1), () => _page.CanMoveShortcut(this, -1));
        MoveDownCommand = new RelayCommand(() => _page.MoveShortcut(this, 1), () => _page.CanMoveShortcut(this, 1));

        // The tile shows the icon its shortcut keeps straight away; one still to be found replaces the glyph once it is.
        _ = LoadTargetIconAsync();
    }

    /// <summary>
    /// Gets the group the shortcut is in.
    /// </summary>
    public GroupViewModel Group { get; internal set; }

    /// <summary>
    /// Gets the shortcut as stored.
    /// </summary>
    public Shortcut Model
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Target));
                OnPropertyChanged(nameof(Subtitle));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(HasDescription));
                OnPropertyChanged(nameof(ToolTip));
            }
        }
    }

    /// <summary>
    /// Gets the shortcut's identity.
    /// </summary>
    public Guid Id => Model.Id;

    /// <summary>
    /// Gets the shortcut's name.
    /// </summary>
    public string Name => Model.Name;

    /// <summary>
    /// Gets the shortcut's target.
    /// </summary>
    public string Target => Model.Target;

    /// <summary>
    /// Gets the tile's icon, which says what kind of shortcut it is.
    /// </summary>
    public PhosphorIcon Icon => _page.Kind switch
    {
        ShortcutKind.Application => PhosphorIcon.AppWindow,
        ShortcutKind.Directory => PhosphorIcon.Folder,
        _ => PhosphorIcon.Globe,
    };

    /// <summary>
    /// Gets the shortcut's own icon — the one it keeps, or a program's or a website's once found — which the tile
    /// shows instead of <see cref="Icon"/>; <see langword="null"/> until it has been found, and when there is none.
    /// </summary>
    public IImage? TargetIcon
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasTargetIcon));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the tile shows the target's own icon rather than the category's glyph.
    /// </summary>
    public bool HasTargetIcon => TargetIcon is not null;

    /// <summary>
    /// Gets the line under the name: a link's host, or the path.
    /// </summary>
    public string Subtitle
        => (_page.Kind == ShortcutKind.Url) && UrlNormalizer.TryParse(Target, out Uri? address, out _) && (address is not null)
            ? address.Host
            : Target;

    /// <summary>
    /// Gets the shortcut's description; empty when it has none.
    /// </summary>
    public string Description => Model.Description ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the tile shows a description under its name.
    /// </summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Model.Description);

    /// <summary>
    /// Gets the tile's tooltip: the full target, the arguments when there are any, and the whole description on a
    /// line of its own, since the tile may cut it short.
    /// </summary>
    public string ToolTip
    {
        get
        {
            string target = string.IsNullOrWhiteSpace(Model.Arguments) ? Target : $"{Target} {Model.Arguments}";

            return HasDescription ? $"{target}{Environment.NewLine}{Description}" : target;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the tile can look its icon up again: an application's or a link's can, a
    /// folder's has none to find.
    /// </summary>
    public bool CanFindIcon => _page.CanFindIcons;

    /// <summary>
    /// Gets a value indicating whether the shortcut can be shown in its folder; a link cannot.
    /// </summary>
    public bool CanReveal => _page.Kind != ShortcutKind.Url;

    /// <summary>
    /// Gets or sets what the tile shows while something is dragged over it.
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
    /// Gets a value indicating whether a drop would land in front of the tile.
    /// </summary>
    public bool IsDropBefore => DropHint == DropHint.Before;

    /// <summary>
    /// Gets a value indicating whether a drop would land behind the tile.
    /// </summary>
    public bool IsDropAfter => DropHint == DropHint.After;

    /// <summary>
    /// Gets a value indicating whether a drop would land inside the tile, at its end.
    /// </summary>
    public bool IsDropInside => DropHint == DropHint.Inside;

    /// <summary>
    /// Gets or sets a value indicating whether the tile is the one being dragged.
    /// </summary>
    public bool IsDragging { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the command opening the shortcut.
    /// </summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>
    /// Gets the command editing the shortcut.
    /// </summary>
    public AsyncRelayCommand EditCommand { get; }

    /// <summary>
    /// Gets the command looking the shortcut's icon up again, after asking when it is an image the user chose.
    /// </summary>
    public AsyncRelayCommand FindIconCommand { get; }

    /// <summary>
    /// Gets the command copying the shortcut's target to the clipboard.
    /// </summary>
    public AsyncRelayCommand CopyTargetCommand { get; }

    /// <summary>
    /// Gets the command showing the shortcut in its folder.
    /// </summary>
    public RelayCommand RevealCommand { get; }

    /// <summary>
    /// Gets the command deleting the shortcut, after confirmation.
    /// </summary>
    public AsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// Gets the command moving the shortcut one place earlier in its group.
    /// </summary>
    public RelayCommand MoveUpCommand { get; }

    /// <summary>
    /// Gets the command moving the shortcut one place later in its group.
    /// </summary>
    public RelayCommand MoveDownCommand { get; }

    /// <summary>
    /// Replaces the shortcut after an edit.
    /// </summary>
    /// <param name="model">The edited shortcut, with the same identity.</param>
    internal void Update(Shortcut model)
    {
        ArgumentNullException.ThrowIfNull(model);

        bool iconChanged = (model.Target != Model.Target) || (model.Icon != Model.Icon);
        Model = model;

        if (iconChanged)
        {
            // The old icon would be wrong while the new one is shown or found.
            TargetIcon = null;
            _ = LoadTargetIconAsync();
        }
    }

    /// <summary>
    /// Shows an icon just found for the shortcut, and takes it into the shortcut.
    /// </summary>
    /// <param name="found">The icon.</param>
    internal void SetFoundIcon(IconImage found)
    {
        ArgumentNullException.ThrowIfNull(found);

        TargetIcon = found.Image;
        Model = Model with { Icon = found.Icon };
    }

    /// <summary>
    /// Re-evaluates the commands that depend on the shortcut's position in its group.
    /// </summary>
    internal void RefreshPosition()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    // Never faults: the page answers null for any icon that cannot be shown or found.
    private async Task LoadTargetIconAsync()
    {
        ShortcutIcon? kept = Model.Icon;

        if ((kept is not null) && (_page.ShowIcon(kept) is { } shown))
        {
            TargetIcon = shown;
            return;
        }

        // An icon the user chose is never replaced behind their back, even one that no longer shows.
        if ((kept is { IsCustom: true }) || !_page.CanFindIcons)
        {
            return;
        }

        string target = Target;
        IconImage? found = await _page.FindIconAsync(target);

        // An edit may have pointed the shortcut elsewhere, or given it an icon, while this one was looked up.
        if ((found is null) || (target != Target) || (Model.Icon != kept))
        {
            return;
        }

        SetFoundIcon(found);
        _page.KeepFoundIcon(this);
    }
}
