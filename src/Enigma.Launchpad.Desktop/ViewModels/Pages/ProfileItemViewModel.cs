using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// One profile on the Profiles page: its name, whether it is in use, what it holds, and what can be done with it.
/// </summary>
/// <remarks>
/// A snapshot: the page builds the cards again whenever the profiles change, so nothing here changes after it is
/// made. Its commands act on the page, which asks the user and hands the change to the session.
/// </remarks>
public sealed class ProfileItemViewModel : ObservableObject
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="page">The page the card is on.</param>
    /// <param name="profile">The profile as stored.</param>
    /// <param name="isActive">Whether it is the profile in use.</param>
    /// <param name="canDelete">Whether it may be deleted — not when it is the only one.</param>
    /// <param name="canMoveUp">Whether it can move one place up.</param>
    /// <param name="canMoveDown">Whether it can move one place down.</param>
    public ProfileItemViewModel(ProfilesPageViewModel page, Profile profile, bool isActive, bool canDelete, bool canMoveUp, bool canMoveDown)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(profile);

        Id = profile.Id;
        Name = profile.Name;
        IsActive = isActive;
        Summary = Summarise(profile.CountWorkspaces(), profile.CountShortcuts());

        UseCommand = new RelayCommand(() => page.Use(this), () => !IsActive);
        RenameCommand = new AsyncRelayCommand(() => page.RenameAsync(this));
        DuplicateCommand = new AsyncRelayCommand(() => page.DuplicateAsync(this));
        ExportCommand = new AsyncRelayCommand(() => page.ExportAsync(this), () => profile.CountWorkspaces() > 0);
        ImportCommand = new AsyncRelayCommand(() => page.ImportAsync(this));
        DeleteCommand = new AsyncRelayCommand(() => page.DeleteAsync(this), () => canDelete);
        MoveUpCommand = new RelayCommand(() => page.Move(this, -1), () => canMoveUp);
        MoveDownCommand = new RelayCommand(() => page.Move(this, 1), () => canMoveDown);
    }

    /// <summary>
    /// Gets the profile's identity.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the profile's name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether it is the profile in use.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets what the profile holds, in a few words.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the command putting the profile in use.
    /// </summary>
    public RelayCommand UseCommand { get; }

    /// <summary>
    /// Gets the command renaming the profile.
    /// </summary>
    public AsyncRelayCommand RenameCommand { get; }

    /// <summary>
    /// Gets the command copying the profile under a new name.
    /// </summary>
    public AsyncRelayCommand DuplicateCommand { get; }

    /// <summary>
    /// Gets the command writing the workspaces, groups and shortcuts the user picks to a file; not for a profile with
    /// no workspace.
    /// </summary>
    public AsyncRelayCommand ExportCommand { get; }

    /// <summary>
    /// Gets the command adding the workspaces, groups and shortcuts the user picks from a file to the profile.
    /// </summary>
    public AsyncRelayCommand ImportCommand { get; }

    /// <summary>
    /// Gets the command deleting the profile, after confirmation.
    /// </summary>
    public AsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// Gets the command moving the profile one place up.
    /// </summary>
    public RelayCommand MoveUpCommand { get; }

    /// <summary>
    /// Gets the command moving the profile one place down.
    /// </summary>
    public RelayCommand MoveDownCommand { get; }

    /// <summary>
    /// Words what a profile holds.
    /// </summary>
    /// <param name="workspaces">How many workspaces it has.</param>
    /// <param name="shortcuts">How many shortcuts they hold.</param>
    /// <returns>"No workspaces yet", or the two counts.</returns>
    internal static string Summarise(int workspaces, int shortcuts)
        => workspaces == 0
            ? "No workspaces yet"
            : $"{workspaces} {(workspaces == 1 ? "workspace" : "workspaces")} · {shortcuts} {(shortcuts == 1 ? "shortcut" : "shortcuts")}";
}
