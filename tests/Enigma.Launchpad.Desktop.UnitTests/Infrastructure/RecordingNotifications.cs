using System.Collections.Concurrent;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Records what the user would have been told.
/// </summary>
internal sealed class RecordingNotifications : IUserNotifications
{
    public ConcurrentQueue<(string Severity, string Title, string Message)> Shown { get; } = new();

    public void ShowSuccess(string title, string message) => Shown.Enqueue(("Success", title, message));

    public void ShowWarning(string title, string message) => Shown.Enqueue(("Warning", title, message));

    public void ShowError(string title, string message) => Shown.Enqueue(("Error", title, message));
}
