using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// The URLs category.
/// </summary>
public sealed class UrlsPageViewModel : CategoryPageViewModel
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    /// <param name="icons">The icons of the tiles: kept with the shortcuts, or the websites' own.</param>
    /// <param name="notifications">Where a failed icon lookup is reported.</param>
    public UrlsPageViewModel(
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutActions actions,
        IShortcutIcons icons,
        IUserNotifications notifications)
        : base(
            ShortcutKind.Url,
            "URLs",
            "Web pages and web apps, opened in your browser.",
            PhosphorIcon.Globe,
            session,
            validator,
            dialogs,
            actions,
            icons,
            notifications)
    {
    }
}
