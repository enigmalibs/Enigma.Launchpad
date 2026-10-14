using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Core.Exchange;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// The Profiles page: every profile as a card, and the ways to create, use, rename, copy, export, import into,
/// reorder and delete them.
/// </summary>
/// <remarks>
/// The page keeps no state of its own beyond the cards: every change goes to the session, and the cards are built
/// again from the session whenever the profiles change — here, or from the profile list at the top of the window —
/// and whenever the page appears, so the counts follow edits made in the workspaces.
/// </remarks>
public sealed class ProfilesPageViewModel : ObservableObject, INavigationViewModel
{
    private readonly ILaunchpadSession _session;
    private readonly IShortcutValidator _validator;
    private readonly ILaunchpadDialogs _dialogs;
    private readonly IShortcutExchange _exchange;
    private readonly IPathPicker _picker;
    private readonly IUserNotifications _notifications;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules a profile's name must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="exchange">Writes and reads export files.</param>
    /// <param name="picker">Lets the user choose the file to export to or import from.</param>
    /// <param name="notifications">Where the user is told how an export or an import went.</param>
    public ProfilesPageViewModel(
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutExchange exchange,
        IPathPicker picker,
        IUserNotifications notifications)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(notifications);

        _session = session;
        _validator = validator;
        _dialogs = dialogs;
        _exchange = exchange;
        _picker = picker;
        _notifications = notifications;

        _session.ProfilesChanged += OnProfilesChanged;
        NewProfileCommand = new AsyncRelayCommand(NewProfileAsync);
    }

    /// <summary>
    /// Gets the profiles, in display order.
    /// </summary>
    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    /// <summary>
    /// Gets the command creating an empty profile at the end of the list.
    /// </summary>
    public AsyncRelayCommand NewProfileCommand { get; }

    /// <inheritdoc />
    public async Task OnAppearingAsync(object? parameter = null)
    {
        await _session.LoadAsync();
        ReadProfiles();
    }

    /// <inheritdoc />
    public Task<bool> OnDisappearingAsync() => Task.FromResult(true);

    /// <summary>
    /// Puts a profile in use.
    /// </summary>
    /// <param name="profile">The profile.</param>
    internal void Use(ProfileItemViewModel profile) => _session.SwitchProfile(profile.Id);

    /// <summary>
    /// Asks for a profile's new name and applies it.
    /// </summary>
    /// <param name="profile">The profile to rename.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task RenameAsync(ProfileItemViewModel profile)
    {
        string? name = await _dialogs.PromptNameAsync(
            "Rename profile",
            "Profile name",
            "Rename",
            profile.Name,
            candidate => _validator.ValidateProfileName(candidate, NamesExcept(profile)));

        if ((name is null) || (name == profile.Name))
        {
            return;
        }

        _session.RenameProfile(profile.Id, name);
    }

    /// <summary>
    /// Asks for a name and copies a profile under it, right after the original.
    /// </summary>
    /// <param name="profile">The profile to copy.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task DuplicateAsync(ProfileItemViewModel profile)
    {
        string? name = await _dialogs.PromptNameAsync(
            "Duplicate profile",
            "Name of the copy",
            "Duplicate",
            CopyName(profile.Name, Profiles.Select(other => other.Name)),
            candidate => _validator.ValidateProfileName(candidate, Profiles.Select(other => other.Name)));

        if (name is null)
        {
            return;
        }

        _session.DuplicateProfile(profile.Id, name);
    }

    /// <summary>
    /// Writes the workspaces, groups and shortcuts of a profile the user ticks to a file the user chooses.
    /// </summary>
    /// <param name="profile">The profile to export from.</param>
    /// <returns>A task that completes once the file is written, or the user has cancelled.</returns>
    internal async Task ExportAsync(ProfileItemViewModel profile)
    {
        if ((Stored(profile) is not { } stored) || (stored.CountWorkspaces() == 0))
        {
            return;
        }

        string title = $"Export “{stored.Name}”";
        Profile? chosen = await _dialogs.SelectShortcutsAsync(
            title,
            $"Tick the workspaces, groups and shortcuts of “{stored.Name}” to write to the file.",
            "Export…",
            stored);

        if (chosen is null)
        {
            return;
        }

        string? path = await _picker.PickJsonFileToSaveAsync(title, ExportFileName(stored.Name));

        if (path is null)
        {
            return;
        }

        try
        {
            await _exchange.ExportAsync(path, chosen);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _notifications.ShowError("The export could not be written", exception.Message);
            return;
        }

        _notifications.ShowSuccess(
            $"“{stored.Name}” exported",
            $"{ProfileItemViewModel.Summarise(chosen.CountWorkspaces(), chosen.CountShortcuts())} written to {path}.");
    }

    /// <summary>
    /// Reads a file the user chooses and adds the workspaces, groups and shortcuts the user ticks to a profile: a
    /// workspace titled like one of the profile's own adds its groups to it, a group named like one of that
    /// workspace's groups of the same kind adds its shortcuts to it, and a shortcut already there is skipped.
    /// </summary>
    /// <param name="profile">The profile to import into.</param>
    /// <returns>A task that completes once the import is done, refused, or cancelled.</returns>
    internal async Task ImportAsync(ProfileItemViewModel profile)
    {
        if (Stored(profile) is not { } target)
        {
            return;
        }

        string title = $"Import into “{target.Name}”";
        string? path = await _picker.PickJsonFileToOpenAsync(title);

        if (path is null)
        {
            return;
        }

        ShortcutImport import;

        try
        {
            import = await _exchange.ReadAsync(path);
        }
        catch (InvalidDataException exception)
        {
            _notifications.ShowError("This file cannot be imported", exception.Message);
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _notifications.ShowError("The file could not be read", exception.Message);
            return;
        }

        if (import.Content.CountWorkspaces() == 0)
        {
            _notifications.ShowWarning(
                "Nothing to import",
                import.SkippedEntries == 0 ? "The file holds no workspace." : $"{LeftOut(import.SkippedEntries)} Nothing else is in it.");
            return;
        }

        string message = $"Tick what to add to “{target.Name}” from “{import.Content.Name}”. A workspace titled like one of "
            + "its own adds its groups to it, a group named like one of that workspace's adds its shortcuts to it, and "
            + "shortcuts already there are skipped.";

        if (import.SkippedEntries > 0)
        {
            message += " " + LeftOut(import.SkippedEntries);
        }

        Profile? chosen = await _dialogs.SelectShortcutsAsync(title, message, "Import", import.Content);

        // Counted from the profile as it is right before the import, so the numbers say what the import added.
        if ((chosen is null) || (Stored(profile) is not { } before))
        {
            return;
        }

        _session.ImportIntoProfile(before.Id, chosen);

        Profile after = Stored(profile) ?? before;
        _notifications.ShowSuccess(
            $"Imported into “{after.Name}”",
            Added(
                after.CountWorkspaces() - before.CountWorkspaces(),
                after.CountGroups() - before.CountGroups(),
                after.CountShortcuts() - before.CountShortcuts()));
    }

    /// <summary>
    /// Deletes a profile with everything in it, once the user has confirmed.
    /// </summary>
    /// <param name="profile">The profile to delete.</param>
    /// <returns>A task that completes once the user has answered.</returns>
    internal async Task DeleteAsync(ProfileItemViewModel profile)
    {
        if (!profile.DeleteCommand.CanExecute(null))
        {
            return;
        }

        Profile? stored = _session.Profiles.FirstOrDefault(candidate => candidate.Id == profile.Id);

        if (stored is null)
        {
            return;
        }

        string message = stored.CountWorkspaces() == 0
            ? "This cannot be undone."
            : $"Its {ProfileItemViewModel.Summarise(stored.CountWorkspaces(), stored.CountShortcuts())} will be deleted with it. This cannot be undone.";

        if (profile.IsActive)
        {
            message += " Another profile will be put in use.";
        }

        if (!await _dialogs.ConfirmDeleteAsync($"Delete the profile “{profile.Name}”?", message, "Delete"))
        {
            return;
        }

        _session.DeleteProfile(profile.Id);
    }

    /// <summary>
    /// Moves a profile by an offset in the list.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="offset">How many places, negative for up.</param>
    internal void Move(ProfileItemViewModel profile, int offset)
    {
        if (_session.CanMoveProfile(profile.Id, offset))
        {
            _session.MoveProfile(profile.Id, offset);
        }
    }

    /// <summary>
    /// Proposes a name for a copy that no profile has yet: "Work (copy)", then "Work (copy 2)", and so on.
    /// </summary>
    /// <param name="name">The original's name.</param>
    /// <param name="existingNames">Every profile's name.</param>
    /// <returns>The proposal.</returns>
    internal static string CopyName(string name, IEnumerable<string> existingNames)
    {
        HashSet<string> taken = new(existingNames.Select(existing => existing.Trim()), StringComparer.OrdinalIgnoreCase);
        string candidate = $"{name} (copy)";

        for (int number = 2; taken.Contains(candidate); number++)
        {
            candidate = string.Create(CultureInfo.InvariantCulture, $"{name} (copy {number})");
        }

        return candidate;
    }

    /// <summary>
    /// Proposes the name of a profile's export file: "Work shortcuts.json", with any character a file name cannot
    /// hold on this platform replaced.
    /// </summary>
    /// <param name="profileName">The profile's name.</param>
    /// <returns>The file name.</returns>
    internal static string ExportFileName(string profileName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(profileName.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());

        return $"{safe} shortcuts.json";
    }

    /// <summary>
    /// Words what an import added.
    /// </summary>
    /// <param name="workspaces">How many workspaces were added.</param>
    /// <param name="groups">How many groups were added, to new workspaces or to existing ones.</param>
    /// <param name="shortcuts">How many shortcuts were added, to new groups or to existing ones.</param>
    /// <returns>The sentence.</returns>
    internal static string Added(int workspaces, int groups, int shortcuts)
    {
        List<string> parts = [];

        foreach ((int count, string noun) in new[] { (shortcuts, "shortcut"), (groups, "new group"), (workspaces, "new workspace") })
        {
            if (count > 0)
            {
                parts.Add(Count(count, noun));
            }
        }

        return parts.Count switch
        {
            0 => "Everything ticked was already there.",
            1 => $"{parts[0]} added.",
            _ => $"{string.Join(", ", parts[..^1])} and {parts[^1]} added.",
        };
    }

    private static string LeftOut(int entries)
        => entries == 1
            ? "1 entry of the file was left out because it is not valid."
            : $"{entries} entries of the file were left out because they are not valid.";

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";

    private Profile? Stored(ProfileItemViewModel profile) => _session.Profiles.FirstOrDefault(candidate => candidate.Id == profile.Id);

    private async Task NewProfileAsync()
    {
        string? name = await _dialogs.PromptNameAsync(
            "New profile",
            "Profile name",
            "Create",
            null,
            candidate => _validator.ValidateProfileName(candidate, Profiles.Select(profile => profile.Name)));

        if (name is null)
        {
            return;
        }

        _session.AddProfile(name);
    }

    private IEnumerable<string> NamesExcept(ProfileItemViewModel profile)
        => Profiles.Where(other => other.Id != profile.Id).Select(other => other.Name);

    private void ReadProfiles()
    {
        IReadOnlyList<Profile> profiles = _session.Profiles;
        Guid active = _session.ActiveProfile.Id;

        Profiles.Clear();

        foreach (Profile profile in profiles)
        {
            Profiles.Add(new ProfileItemViewModel(
                this,
                profile,
                isActive: profile.Id == active,
                canDelete: profiles.Count > 1,
                canMoveUp: _session.CanMoveProfile(profile.Id, -1),
                canMoveDown: _session.CanMoveProfile(profile.Id, 1)));
        }
    }

    private void OnProfilesChanged(object? sender, EventArgs e) => ReadProfiles();
}
