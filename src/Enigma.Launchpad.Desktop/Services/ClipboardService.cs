using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Puts text on the system clipboard. A seam of its own so a test can assert what was copied.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes once the clipboard has it.</returns>
    Task SetTextAsync(string text);
}

/// <summary>
/// Default <see cref="IClipboardService"/>: the main window's clipboard.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc />
    public async Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        IClipboard? clipboard = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window.Clipboard
            : null;

        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
