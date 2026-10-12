using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// The applications category.
/// </summary>
public sealed class ApplicationsPageViewModel : CategoryPageViewModel
{
    private readonly IProgramIcons _programIcons;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    /// <param name="programIcons">The icons of the programs, for the tiles.</param>
    public ApplicationsPageViewModel(
        ILaunchpadSession session,
        IShortcutValidator validator,
        ILaunchpadDialogs dialogs,
        IShortcutActions actions,
        IProgramIcons programIcons)
        : base(
            ShortcutKind.Application,
            "Applications",
            "Programs you launch, grouped your way.",
            PhosphorIcon.AppWindow,
            session,
            validator,
            dialogs,
            actions)
    {
        ArgumentNullException.ThrowIfNull(programIcons);

        _programIcons = programIcons;
    }

    /// <inheritdoc />
    internal override Task<IImage?> GetTargetIconAsync(string target) => _programIcons.GetAsync(target);
}
