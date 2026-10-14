using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One workspace on the Workspaces page: its glyph, title, description, what it holds, and what can be done with it.
/// </summary>
/// <remarks>
/// A snapshot, like a profile card: the page builds the cards again whenever the workspaces change, so nothing here
/// changes after it is made. Its commands act on the page, which asks the user and hands the change to the session.
/// </remarks>
public sealed class WorkspaceItemViewModel : ObservableObject
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="page">The page the card is on.</param>
    /// <param name="workspace">The workspace as stored.</param>
    /// <param name="canMoveUp">Whether it can move one place up.</param>
    /// <param name="canMoveDown">Whether it can move one place down.</param>
    public WorkspaceItemViewModel(WorkspacesPageViewModel page, Workspace workspace, bool canMoveUp, bool canMoveDown)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(workspace);

        Id = workspace.Id;
        Title = workspace.Title;
        Description = workspace.Description ?? string.Empty;
        Glyph = Glyphs.OfWorkspace(workspace.Icon);
        GroupCount = workspace.Groups.Count;
        ShortcutCount = workspace.Groups.Sum(group => group.Shortcuts.Count);
        Summary = Summarise(GroupCount, ShortcutCount);

        OpenCommand = new RelayCommand(() => page.Open(this));
        EditCommand = new AsyncRelayCommand(() => page.EditAsync(this));
        DuplicateCommand = new AsyncRelayCommand(() => page.DuplicateAsync(this));
        DeleteCommand = new AsyncRelayCommand(() => page.DeleteAsync(this));
        MoveUpCommand = new RelayCommand(() => page.Move(this, -1), () => canMoveUp);
        MoveDownCommand = new RelayCommand(() => page.Move(this, 1), () => canMoveDown);
    }

    /// <summary>
    /// Gets the workspace's identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the workspace's title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets what the workspace is for; empty when it does not say.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets a value indicating whether the card shows a description.
    /// </summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>
    /// Gets the workspace's glyph.
    /// </summary>
    public PhosphorIcon Glyph { get; }

    /// <summary>
    /// Gets what the workspace holds, in a few words.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the command opening the workspace in place of the list.
    /// </summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>
    /// Gets the command editing the workspace's title, description and glyph.
    /// </summary>
    public AsyncRelayCommand EditCommand { get; }

    /// <summary>
    /// Gets the command copying the workspace under a new title.
    /// </summary>
    public AsyncRelayCommand DuplicateCommand { get; }

    /// <summary>
    /// Gets the command deleting the workspace, after confirmation.
    /// </summary>
    public AsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// Gets the command moving the workspace one place up.
    /// </summary>
    public RelayCommand MoveUpCommand { get; }

    /// <summary>
    /// Gets the command moving the workspace one place down.
    /// </summary>
    public RelayCommand MoveDownCommand { get; }

    /// <summary>
    /// Gets how many groups the workspace holds.
    /// </summary>
    internal int GroupCount { get; }

    /// <summary>
    /// Gets how many shortcuts its groups hold.
    /// </summary>
    internal int ShortcutCount { get; }

    /// <summary>
    /// Words what a workspace holds.
    /// </summary>
    /// <param name="groups">How many groups it has.</param>
    /// <param name="shortcuts">How many shortcuts they hold.</param>
    /// <returns>"Empty", or the two counts.</returns>
    internal static string Summarise(int groups, int shortcuts)
        => groups == 0
            ? "Empty"
            : $"{groups} {(groups == 1 ? "group" : "groups")} · {shortcuts} {(shortcuts == 1 ? "shortcut" : "shortcuts")}";

    /// <summary>
    /// Whether the workspace answers a search: its title or its description holds the text, ignoring case.
    /// </summary>
    /// <param name="search">The search, already trimmed; empty matches everything.</param>
    /// <returns><see langword="true"/> when the card is shown.</returns>
    internal bool Matches(string search)
        => (search.Length == 0)
            || Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || Description.Contains(search, StringComparison.CurrentCultureIgnoreCase);
}
