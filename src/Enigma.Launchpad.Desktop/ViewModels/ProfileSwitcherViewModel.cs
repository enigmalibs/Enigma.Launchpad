using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels;

/// <summary>
/// One entry of the profile list: what the list needs of a profile, and nothing that changes as it is edited.
/// </summary>
/// <param name="Id">The profile's identity.</param>
/// <param name="Name">Its name.</param>
public sealed record ProfileOption(Guid Id, string Name);

/// <summary>
/// The profile list at the top of the window: every profile, with the one in use selected. Choosing another one
/// puts it in use.
/// </summary>
/// <remarks>
/// The list follows the session: a profile added, renamed, moved or deleted elsewhere, or put in use from the
/// Profiles page, shows here too. While it follows, a selection the list itself makes is not a choice.
/// </remarks>
public sealed class ProfileSwitcherViewModel : ObservableObject
{
    private readonly ILaunchpadSession _session;

    private bool _isFollowing;

    /// <summary>
    /// Initialises a new instance from the session as it is now.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    public ProfileSwitcherViewModel(ILaunchpadSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _session.ProfilesChanged += OnProfilesChanged;
        Follow();
    }

    /// <summary>
    /// Gets the profiles, in display order.
    /// </summary>
    public ObservableCollection<ProfileOption> Profiles { get; } = [];

    /// <summary>
    /// Gets or sets the profile in use; setting it puts that profile in use.
    /// </summary>
    public ProfileOption? SelectedProfile
    {
        get;
        set
        {
            if (!SetProperty(ref field, value) || _isFollowing || (value is null))
            {
                return;
            }

            _session.SwitchProfile(value.Id);
        }
    }

    /// <summary>
    /// Reads the profiles back from the session. The list is rebuilt only when a profile's name or place changed,
    /// so a plain switch leaves the open list alone.
    /// </summary>
    private void Follow()
    {
        _isFollowing = true;

        try
        {
            ProfileOption[] options = _session.Profiles.Select(profile => new ProfileOption(profile.Id, profile.Name)).ToArray();

            if (!Profiles.SequenceEqual(options))
            {
                Profiles.Clear();

                foreach (ProfileOption option in options)
                {
                    Profiles.Add(option);
                }
            }

            Guid active = _session.ActiveProfile.Id;
            SelectedProfile = Profiles.FirstOrDefault(option => option.Id == active);
        }
        finally
        {
            _isFollowing = false;
        }
    }

    private void OnProfilesChanged(object? sender, EventArgs e) => Follow();
}
