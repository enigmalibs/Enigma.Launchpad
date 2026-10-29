using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.Views.Dialogs;
using Microsoft.Extensions.Logging;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// The safety net: every exception nothing else caught ends up here, is logged, and is shown in a dialog that says
/// the application is still running.
/// </summary>
/// <remarks>
/// A safety net, not the error handling: a failure the application knows about is said where it happens, in that
/// place's own words, and never reaches this.
/// </remarks>
public interface IUnhandledErrorReporter
{
    /// <summary>
    /// Logs an exception and shows it, from any thread.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    void Report(Exception exception, UnhandledErrorSource source);

    /// <summary>
    /// Hands over the main window's dialog host, which the dialog is shown on. What was reported before is shown
    /// once it is there. Called on the UI thread.
    /// </summary>
    /// <param name="host">The main window's content dialog.</param>
    void RegisterHost(ContentDialog host);

    /// <summary>
    /// Says the application is closing: from here on a cancellation is never worth a word.
    /// </summary>
    void ShutdownStarted();
}

/// <summary>
/// Default <see cref="IUnhandledErrorReporter"/>.
/// </summary>
/// <remarks>
/// <para>
/// The log is written at once, on whatever thread reported, because that thread may be about to end the process.
/// What is shown is decided on the UI thread, by <see cref="UnhandledErrorPolicy"/>.
/// </para>
/// <para>
/// The dialog is shown on the main window's dialog host, which every question shares: showing it over another
/// question would replace that question's content, so it waits until the host is free, and before the window until
/// there is a host. If the dialog itself fails, that is logged and no dialog is tried again in this session: the net
/// must not loop on itself.
/// </para>
/// </remarks>
public sealed class UnhandledErrorReporter : IUnhandledErrorReporter
{
    /// <summary>
    /// The title the dialog opens with.
    /// </summary>
    public const string Title = "Something went wrong";

    private readonly IContentDialogService _dialogs;
    private readonly IClipboardService _clipboard;
    private readonly ILogger<UnhandledErrorReporter> _logger;
    private readonly UnhandledErrorPolicy _policy;

    private ContentDialog? _host;

    // The question the next dialog waits behind, while it waits.
    private ContentDialog? _waitingFor;

    private bool _presenting;
    private bool _broken;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="dialogs">Shows the dialog on the main window.</param>
    /// <param name="clipboard">Copies the details to the clipboard.</param>
    /// <param name="time">Measures how long ago an error was seen.</param>
    /// <param name="logger">Receives every exception, in full.</param>
    public UnhandledErrorReporter(IContentDialogService dialogs, IClipboardService clipboard, TimeProvider time, ILogger<UnhandledErrorReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _dialogs = dialogs;
        _clipboard = clipboard;
        _logger = logger;
        _policy = new UnhandledErrorPolicy(time);
    }

    /// <inheritdoc />
    public void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_policy.IsIgnoredCancellation(exception))
        {
            _logger.LogDebug(exception, "A cancellation reached the safety net ({Source}); not an error", source);
            return;
        }

        _logger.LogError(exception, "An unhandled exception reached the safety net ({Source})", source);

        Dispatcher.UIThread.Post(() => Offer(exception));
    }

    /// <inheritdoc />
    public void RegisterHost(ContentDialog host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;

        if (_policy.Pending is not null)
        {
            // Never throws: nothing awaits it.
            _ = PresentAsync();
        }
    }

    /// <inheritdoc />
    public void ShutdownStarted() => _policy.IsShuttingDown = true;

    private void Offer(Exception exception)
    {
        switch (_policy.Offer(exception))
        {
            case UnhandledErrorDecision.Queue:
                _ = PresentAsync();
                break;
            case UnhandledErrorDecision.Suppress:
                _logger.LogDebug("The same error was shown less than {Window} ago; it is not shown again", UnhandledErrorPolicy.RepeatWindow);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Shows the pending batch, once there is a free host. Never throws: nothing awaits it.
    /// </summary>
    private async Task PresentAsync()
    {
        if (_broken || _presenting || _policy.Pending is null || _host is not { } host)
        {
            // Without a host, RegisterHost brings one and tries again.
            return;
        }

        if (host.IsOpen)
        {
            WaitFor(host);
            return;
        }

        if (_policy.BeginShow() is not { } batch)
        {
            return;
        }

        _presenting = true;

        try
        {
            UnhandledErrorView view = new()
            {
                DataContext = new UnhandledErrorViewModel(batch, _clipboard),
            };

            await _dialogs.ShowAsync(dialog =>
            {
                dialog.Title = Title;
                dialog.Content = view;
                dialog.CloseButtonText = "Close";
                dialog.DefaultButton = DefaultButton.Close;
                dialog.IconData = PhosphorIconSet.Instance
                    .GetGlyph(PhosphorIcon.Warning, PhosphorWeight.Regular)
                    .ToGeometry();
                dialog.IconBrush = new SolidColorBrush(Color.Parse("#E8A33D"));
            });
        }
        catch (Exception exception)
        {
            // Logged, never reported: a dialog that cannot be shown would otherwise report itself again and again.
            _broken = true;
            _logger.LogError(exception, "The safety net's dialog could not be shown; later errors are only logged");
        }

        _presenting = false;
        _policy.EndShow();

        if (_policy.Pending is not null)
        {
            _ = PresentAsync();
        }
    }

    /// <summary>
    /// Waits for the question on the host to be answered, then tries again.
    /// </summary>
    private void WaitFor(ContentDialog host)
    {
        if (ReferenceEquals(_waitingFor, host))
        {
            return;
        }

        _waitingFor = host;
        host.Closed += OnWaitedClosed;
    }

    private void OnWaitedClosed(object? sender, DialogResult e)
    {
        if (sender is ContentDialog host)
        {
            host.Closed -= OnWaitedClosed;
        }

        _waitingFor = null;

        // Posted: the question's own continuation runs first, and may ask the next question at once.
        Dispatcher.UIThread.Post(() => _ = PresentAsync(), DispatcherPriority.Background);
    }
}
