using System;
using System.Threading.Tasks;
using Avalonia.Media;
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
    private readonly IWebsiteIcons _websiteIcons;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    /// <param name="websiteIcons">The icons of the websites, for the tiles.</param>
    public UrlsPageViewModel(
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutActions actions,
        IWebsiteIcons websiteIcons)
        : base(
            ShortcutKind.Url,
            "URLs",
            "Web pages and web apps, opened in your browser.",
            PhosphorIcon.Globe,
            session,
            validator,
            dialogs,
            actions)
    {
        ArgumentNullException.ThrowIfNull(websiteIcons);

        _websiteIcons = websiteIcons;
    }

    /// <inheritdoc />
    internal override Task<IImage?> GetTargetIconAsync(string target) => _websiteIcons.GetAsync(target);
}
