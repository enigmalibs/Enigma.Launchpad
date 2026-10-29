using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Counts how often the About dialog would have been shown.
/// </summary>
internal sealed class RecordingAboutDialog : IAboutDialogService
{
    public int Shown { get; private set; }

    public Task ShowAsync()
    {
        Shown++;
        return Task.CompletedTask;
    }
}
