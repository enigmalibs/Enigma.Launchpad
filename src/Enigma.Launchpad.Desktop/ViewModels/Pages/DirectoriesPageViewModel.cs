using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Pages;

/// <summary>
/// The directories category.
/// </summary>
public sealed class DirectoriesPageViewModel : CategoryPageViewModel
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="session">The launchpad document.</param>
    /// <param name="validator">The rules names and targets must meet.</param>
    /// <param name="dialogs">The questions asked of the user.</param>
    /// <param name="actions">Opens shortcuts, shows them in their folder, copies their targets.</param>
    public DirectoriesPageViewModel(ILaunchpadSession session, IShortcutValidator validator, ILaunchpadDialogs dialogs, IShortcutActions actions)
        : base(
            ShortcutKind.Directory,
            "Directories",
            "Folders you open often, one click away.",
            PhosphorIcon.Folder,
            session,
            validator,
            dialogs,
            actions)
    {
    }
}
