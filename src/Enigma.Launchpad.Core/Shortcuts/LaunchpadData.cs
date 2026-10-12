using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Enigma.Launchpad.Core.Shortcuts;

/// <summary>
/// Everything the user keeps in the launchpad: the profiles, in display order, and the one in use.
/// </summary>
/// <param name="Version">The format version of the document; <see cref="CurrentVersion"/> when written.</param>
/// <param name="ActiveProfileId">The identity of the profile in use.</param>
/// <param name="Profiles">The profiles, in display order; never empty once loaded.</param>
/// <remarks>
/// Every operation returns a new document and leaves this one as it was. The category accessors
/// (<see cref="GroupsOf"/>, <see cref="With"/>) act on the profile in use, which is all the category pages ever
/// see.
/// </remarks>
public sealed record LaunchpadData(int Version, Guid ActiveProfileId, IReadOnlyList<Profile> Profiles)
{
    /// <summary>
    /// The format version this build writes. Version 1 — the three categories at the root, before profiles —
    /// is still read.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Gets a new launchpad: one empty profile, in use.
    /// </summary>
    public static LaunchpadData Empty { get; } = Of(Profile.Create(Profile.DefaultName));

    /// <summary>
    /// Gets the profile in use: the one <see cref="ActiveProfileId"/> names, or the first when it names none.
    /// </summary>
    /// <exception cref="InvalidOperationException">The document has no profile.</exception>
    [JsonIgnore]
    public Profile ActiveProfile
        => FindProfile(ActiveProfileId)
            ?? (Profiles.Count > 0 ? Profiles[0] : throw new InvalidOperationException("The launchpad has no profile."));

    /// <summary>
    /// Creates a document holding one profile, in use.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The document.</returns>
    public static LaunchpadData Of(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new LaunchpadData(CurrentVersion, profile.Id, [profile]);
    }

    /// <summary>
    /// Gets the groups of one category of the profile in use.
    /// </summary>
    /// <param name="kind">The category.</param>
    /// <returns>Its groups, in display order.</returns>
    public IReadOnlyList<ShortcutGroup> GroupsOf(ShortcutKind kind) => ActiveProfile.GroupsOf(kind);

    /// <summary>
    /// Returns a copy of this document with one category of the profile in use replaced.
    /// </summary>
    /// <param name="kind">The category to replace.</param>
    /// <param name="groups">Its new groups, in display order.</param>
    /// <returns>The new document.</returns>
    public LaunchpadData With(ShortcutKind kind, IReadOnlyList<ShortcutGroup> groups)
        => Replace(ActiveProfile.With(kind, groups));

    /// <summary>
    /// Finds a profile by identity.
    /// </summary>
    /// <param name="id">The profile's identity.</param>
    /// <returns>The profile, or <see langword="null"/>.</returns>
    public Profile? FindProfile(Guid id) => Profiles.FirstOrDefault(profile => profile.Id == id);

    /// <summary>
    /// Returns a copy of this document with another profile in use.
    /// </summary>
    /// <param name="id">The profile to use.</param>
    /// <returns>The new document.</returns>
    /// <exception cref="ArgumentException">No profile has this identity.</exception>
    public LaunchpadData WithActiveProfile(Guid id)
        => this with { ActiveProfileId = Require(id).Id };

    /// <summary>
    /// Returns a copy of this document with a profile added. The profile in use does not change.
    /// </summary>
    /// <param name="profile">The profile to add.</param>
    /// <param name="index">Where it goes in the list; at the end when omitted.</param>
    /// <returns>The new document.</returns>
    /// <exception cref="ArgumentException">A profile with the same identity is already there.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the list.</exception>
    public LaunchpadData AddProfile(Profile profile, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (FindProfile(profile.Id) is not null)
        {
            throw new ArgumentException("A profile with this identity is already in the launchpad.", nameof(profile));
        }

        int at = index ?? Profiles.Count;
        ArgumentOutOfRangeException.ThrowIfNegative(at, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(at, Profiles.Count, nameof(index));

        List<Profile> profiles = [.. Profiles];
        profiles.Insert(at, profile);

        return this with { Profiles = profiles };
    }

    /// <summary>
    /// Returns a copy of this document with a profile renamed.
    /// </summary>
    /// <param name="id">The profile to rename.</param>
    /// <param name="name">Its new name.</param>
    /// <returns>The new document.</returns>
    /// <exception cref="ArgumentException">No profile has this identity.</exception>
    public LaunchpadData RenameProfile(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Replace(Require(id) with { Name = name });
    }

    /// <summary>
    /// Returns a copy of this document with groups and shortcuts imported into one of its profiles, merged as
    /// <see cref="Profile.Import"/> does. The profile in use does not change.
    /// </summary>
    /// <param name="id">The profile receiving them.</param>
    /// <param name="source">What to import.</param>
    /// <returns>The new document.</returns>
    /// <exception cref="ArgumentException">No profile has this identity.</exception>
    public LaunchpadData ImportInto(Guid id, Profile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Replace(Require(id).Import(source));
    }

    /// <summary>
    /// Returns a copy of this document without a profile. When it was the profile in use, the one that took its
    /// place in the list — or, for the last of the list, the one before it — is used instead.
    /// </summary>
    /// <param name="id">The profile to remove.</param>
    /// <returns>The new document.</returns>
    /// <exception cref="ArgumentException">No profile has this identity.</exception>
    /// <exception cref="InvalidOperationException">It is the only profile: a launchpad always has one.</exception>
    public LaunchpadData RemoveProfile(Guid id)
    {
        int index = IndexOf(Require(id).Id);

        if (Profiles.Count == 1)
        {
            throw new InvalidOperationException("The last profile cannot be removed.");
        }

        List<Profile> profiles = [.. Profiles];
        profiles.RemoveAt(index);

        Guid active = ActiveProfile.Id == id
            ? profiles[Math.Min(index, profiles.Count - 1)].Id
            : ActiveProfileId;

        return this with { Profiles = profiles, ActiveProfileId = active };
    }

    /// <summary>
    /// Whether a profile can move by an offset without leaving the list.
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="offset">How many places, negative for up.</param>
    /// <returns><see langword="true"/> when the move stays in the list.</returns>
    public bool CanMoveProfile(Guid id, int offset)
    {
        int index = IndexOf(id);
        int target = index + offset;

        return (index >= 0) && (offset != 0) && (target >= 0) && (target < Profiles.Count);
    }

    /// <summary>
    /// Returns a copy of this document with a profile moved by an offset.
    /// </summary>
    /// <param name="id">The profile.</param>
    /// <param name="offset">How many places, negative for up.</param>
    /// <returns>The new document.</returns>
    /// <exception cref="ArgumentException">The profile is unknown, or the move would leave the list.</exception>
    public LaunchpadData MoveProfile(Guid id, int offset)
    {
        Profile profile = Require(id);

        if (!CanMoveProfile(id, offset))
        {
            throw new ArgumentException("The profile cannot move this far.", nameof(offset));
        }

        int index = IndexOf(id);
        List<Profile> profiles = [.. Profiles];
        profiles.RemoveAt(index);
        profiles.Insert(index + offset, profile);

        return this with { Profiles = profiles };
    }

    /// <summary>
    /// Returns this document made usable as it is: with at least one profile, and one in use.
    /// </summary>
    /// <returns>The document, or a repaired copy.</returns>
    /// <remarks>
    /// Only a document edited by hand needs this: an empty profile list gets a <see cref="Profile.DefaultName"/>
    /// profile, and an identity in use that names no profile falls back to the first one. Nothing is lost either
    /// way, so it is repaired rather than refused.
    /// </remarks>
    public LaunchpadData Normalised()
    {
        if (Profiles.Count == 0)
        {
            Profile profile = Profile.Create(Profile.DefaultName);
            return this with { ActiveProfileId = profile.Id, Profiles = [profile] };
        }

        return FindProfile(ActiveProfileId) is null
            ? this with { ActiveProfileId = Profiles[0].Id }
            : this;
    }

    private Profile Require(Guid id)
        => FindProfile(id) ?? throw new ArgumentException("No profile has this identity.", nameof(id));

    private int IndexOf(Guid id)
    {
        for (int index = 0; index < Profiles.Count; index++)
        {
            if (Profiles[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private LaunchpadData Replace(Profile profile)
        => this with { Profiles = Profiles.Select(existing => existing.Id == profile.Id ? profile : existing).ToArray() };
}
