using System;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// Tells the user how something went, without making the caller care which thread it is on.
/// </summary>
public interface IUserNotifications
{
    /// <summary>
    /// Shows a success: something the user asked for, that takes more than a click, is done.
    /// </summary>
    /// <param name="title">The headline.</param>
    /// <param name="message">What was done.</param>
    void ShowSuccess(string title, string message);

    /// <summary>
    /// Shows a warning: something unexpected happened, and the application coped with it.
    /// </summary>
    /// <param name="title">The headline.</param>
    /// <param name="message">What happened, and what it means for the user.</param>
    void ShowWarning(string title, string message);

    /// <summary>
    /// Shows an error: something the user asked for did not happen.
    /// </summary>
    /// <param name="title">The headline.</param>
    /// <param name="message">What happened, and what it means for the user.</param>
    void ShowError(string title, string message);
}

/// <summary>
/// Default <see cref="IUserNotifications"/>: the main window's info bar, reached on the UI thread.
/// </summary>
public sealed class InfoBarNotifications : IUserNotifications
{
    private readonly IInfoBarService _infoBar;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="infoBar">The info bar service, whose host is the main window's.</param>
    public InfoBarNotifications(IInfoBarService infoBar)
    {
        ArgumentNullException.ThrowIfNull(infoBar);
        _infoBar = infoBar;
    }

    /// <inheritdoc />
    public void ShowSuccess(string title, string message) => Show(title, message, InfoBarSeverity.Success);

    /// <inheritdoc />
    public void ShowWarning(string title, string message) => Show(title, message, InfoBarSeverity.Warning);

    /// <inheritdoc />
    public void ShowError(string title, string message) => Show(title, message, InfoBarSeverity.Error);

    // Posted, never invoked: a save fails on a thread-pool thread, and the info bar is a control.
    private void Show(string title, string message, InfoBarSeverity severity)
        => Dispatcher.UIThread.Post(() => _ = _infoBar.ShowAsync(bar =>
        {
            bar.Title = title;
            bar.Message = message;
            bar.Severity = severity;
        }));
}
