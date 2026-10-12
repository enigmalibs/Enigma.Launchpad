using System;
using Avalonia;

namespace Enigma.Launchpad.Desktop;

/// <summary>
/// Entry point of the Enigma.Launchpad desktop application.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the Avalonia classic desktop lifetime.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Builds the Avalonia application. Also used by the XAML previewer.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
