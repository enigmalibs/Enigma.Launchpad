using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// What the application does to its own window. A seam of its own so a test can assert it without one.
/// </summary>
public interface IMainWindowService
{
    /// <summary>
    /// Minimizes the main window, so whatever was just opened comes to the front.
    /// </summary>
    void Minimize();
}

/// <summary>
/// Default <see cref="IMainWindowService"/>: the desktop lifetime's main window.
/// </summary>
public sealed class MainWindowService : IMainWindowService
{
    /// <inheritdoc />
    public void Minimize()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            window.WindowState = WindowState.Minimized;
        }
    }
}
