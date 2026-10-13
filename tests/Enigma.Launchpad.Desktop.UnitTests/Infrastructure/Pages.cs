using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// A category page wired to in-memory doubles, so its commands can be driven without a window.
/// </summary>
internal sealed class PageHarness
{
    public PageHarness(LaunchpadData? data = null, ShortcutKind kind = ShortcutKind.Application)
    {
        Store = new FakeLaunchpadStore(data);
        Session = new LaunchpadSession(Store, Notifications, NullLogger<LaunchpadSession>.Instance);
        ShortcutValidator validator = new();
        Page = kind switch
        {
            ShortcutKind.Application => new ApplicationsPageViewModel(Session, validator, Dialogs, Actions, Icons),
            ShortcutKind.Directory => new DirectoriesPageViewModel(Session, validator, Dialogs, Actions, Icons),
            _ => new UrlsPageViewModel(Session, validator, Dialogs, Actions, Icons),
        };
    }

    public FakeLaunchpadStore Store { get; }

    public RecordingNotifications Notifications { get; } = new();

    public ScriptedDialogs Dialogs { get; } = new();

    public RecordingShortcutActions Actions { get; } = new();

    public FakeShortcutIcons Icons { get; } = new();

    public LaunchpadSession Session { get; }

    public CategoryPageViewModel Page { get; }

    public async Task<CategoryPageViewModel> LoadAsync()
    {
        await Page.OnAppearingAsync();
        return Page;
    }

    /// <summary>
    /// The groups of the page's category as last written, once every pending save has finished.
    /// </summary>
    public async Task<ShortcutGroup[]> SavedGroupsAsync()
    {
        await Session.FlushAsync();
        return Store.LastSaved?.GroupsOf(Page.Kind).ToArray() ?? [];
    }

    /// <summary>
    /// The applications as last written, once every pending save has finished.
    /// </summary>
    public Task<ShortcutGroup[]> SavedApplicationsAsync() => SavedGroupsAsync();

    public static LaunchpadData Data(params string[] applicationGroups)
        => LaunchpadData.Empty.With(
            ShortcutKind.Application,
            applicationGroups.Select(name => new ShortcutGroup(Guid.NewGuid(), name, [])).ToArray());

    public static Shortcut Shortcut(string name, string target = @"C:\Tools\tool.exe", string? arguments = null)
        => new(Guid.NewGuid(), name, target, arguments);

    /// <summary>
    /// A document with two profiles, "Work" in use and "Home", each with one application group of its own:
    /// "Dev tools" and "Games".
    /// </summary>
    public static LaunchpadData WorkAndHome()
    {
        Profile work = Profile.Create("Work").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Dev tools", [])]);
        Profile home = Profile.Create("Home").With(ShortcutKind.Application, [new ShortcutGroup(Guid.NewGuid(), "Games", [])]);

        return new LaunchpadData(LaunchpadData.CurrentVersion, work.Id, [work, home]);
    }

    /// <summary>
    /// A document with one group per entry in the category, each holding the named shortcuts.
    /// </summary>
    public static LaunchpadData WithShortcuts(ShortcutKind kind, params (string Group, Shortcut[] Shortcuts)[] groups)
        => LaunchpadData.Empty.With(
            kind,
            groups.Select(group => new ShortcutGroup(Guid.NewGuid(), group.Group, group.Shortcuts)).ToArray());
}
