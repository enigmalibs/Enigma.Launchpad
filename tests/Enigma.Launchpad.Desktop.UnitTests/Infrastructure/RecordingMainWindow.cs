using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Counts how often the window would have been minimized.
/// </summary>
internal sealed class RecordingMainWindow : IMainWindowService
{
    public int Minimized { get; private set; }

    public void Minimize() => Minimized++;
}
