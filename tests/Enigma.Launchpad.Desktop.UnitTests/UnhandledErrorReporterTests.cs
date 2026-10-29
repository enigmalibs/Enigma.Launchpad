using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The safety net on the real main window: what it logs, what it shows, and when.
/// </summary>
[Collection(HeadlessCollection.Name)]
public sealed class UnhandledErrorReporterTests(HeadlessAvaloniaFixture fixture)
{
    private readonly RecordingLogger<UnhandledErrorReporter> _logger = new();
    private readonly RecordingClipboard _clipboard = new();

    [Fact]
    public void AReportedError_IsLogged_AndShownInTheErrorDialog()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = Harness();
            ContentDialog host = harness.Window.HostDialog;
            InvalidOperationException error = new("The shortcut list could not be drawn");

            Reporter(harness).Report(error, UnhandledErrorSource.Dispatcher);
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);

            Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, error));

            Assert.Equal(UnhandledErrorReporter.Title, host.Title);
            Assert.NotNull(host.IconData);
            Assert.Equal("Close", host.CloseButtonText);
            Assert.Equal(DefaultButton.Close, host.DefaultButton);
            Assert.Null(host.PrimaryButtonText);

            UnhandledErrorView view = Assert.IsType<UnhandledErrorView>(host.Content);
            Assert.Equal("The shortcut list could not be drawn", view.FindControl<TextBlock>("MessageText")?.Text);
            Assert.False(view.FindControl<TextBlock>("MoreText")?.IsVisible);

            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });

    [Fact]
    public void AnotherErrorWhileTheDialogIsOpen_IsCountedIntoIt()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = Harness();
            ContentDialog host = harness.Window.HostDialog;
            IUnhandledErrorReporter reporter = Reporter(harness);

            reporter.Report(new InvalidOperationException("first"), UnhandledErrorSource.Dispatcher);
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);
            UnhandledErrorView view = Assert.IsType<UnhandledErrorView>(host.Content);

            reporter.Report(new InvalidOperationException("second"), UnhandledErrorSource.UnobservedTask);
            await WindowHarness.WaitUntilAsync(() => view.FindControl<TextBlock>("MoreText")?.IsVisible == true);

            Assert.Same(view, host.Content);
            Assert.Equal("1 more error occurred.", view.FindControl<TextBlock>("MoreText")?.Text);
            Assert.Equal("first", view.FindControl<TextBlock>("MessageText")?.Text);

            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });

    [Fact]
    public void ACancellationSomeoneAskedFor_IsLoggedQuietly_AndShowsNothing()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = Harness();
            using CancellationTokenSource source = new();
            source.Cancel();

            Reporter(harness).Report(new OperationCanceledException(source.Token), UnhandledErrorSource.FireAndForget);
            await Task.Delay(50);

            Assert.False(harness.Window.HostDialog.IsOpen);
            Assert.All(_logger.Entries, entry => Assert.Equal(LogLevel.Debug, entry.Level));
            Assert.NotEmpty(_logger.Entries);
        });

    [Fact]
    public void WhileAQuestionIsOpen_TheErrorWaitsForItsAnswer()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = Harness();
            ContentDialog host = harness.Window.HostDialog;

            Task<bool> question = harness.Provider.GetRequiredService<ILaunchpadDialogs>().ConfirmAsync("Replace?", "Your shortcuts would change.", "Replace");
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);
            object? asked = host.Content;

            Reporter(harness).Report(new InvalidOperationException("while asking"), UnhandledErrorSource.Dispatcher);
            await Task.Delay(50);
            Assert.Same(asked, host.Content);

            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(await question);

            await WindowHarness.WaitUntilAsync(() => host.IsOpen && host.Content is UnhandledErrorView);
            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });

    [Fact]
    public void AnErrorBeforeTheHost_IsShownOnceTheHostIsThere()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = Harness();
            ContentDialog host = harness.Window.HostDialog;
            UnhandledErrorReporter early = new(
                harness.Provider.GetRequiredService<IContentDialogService>(),
                _clipboard,
                TimeProvider.System,
                _logger);

            early.Report(new InvalidOperationException("at startup"), UnhandledErrorSource.Startup);
            await Task.Delay(50);
            Assert.False(host.IsOpen);

            early.RegisterHost(host);
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);

            Assert.IsType<UnhandledErrorView>(host.Content);
            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });

    [Fact]
    public void CopyDetails_PutsEveryErrorsDetailsOnTheClipboard()
        => fixture.RunAsync(async () =>
        {
            using WindowHarness harness = Harness();
            ContentDialog host = harness.Window.HostDialog;

            Reporter(harness).Report(new InvalidOperationException("copy me"), UnhandledErrorSource.Dispatcher);
            await WindowHarness.WaitUntilAsync(() => host.IsOpen);
            UnhandledErrorViewModel model = Assert.IsType<UnhandledErrorViewModel>(Assert.IsType<UnhandledErrorView>(host.Content).DataContext);

            await model.CopyDetailsCommand.ExecuteAsync(null);

            string copied = Assert.Single(_clipboard.Texts);
            Assert.Equal(model.Details, copied);
            Assert.StartsWith("System.InvalidOperationException: copy me", copied, StringComparison.Ordinal);

            await host.HideAsync().WaitAsync(TimeSpan.FromSeconds(10));
        });

    private WindowHarness Harness()
        => new(replace: services =>
        {
            services.AddSingleton<ILogger<UnhandledErrorReporter>>(_logger);
            services.AddSingleton<IClipboardService>(_clipboard);
        });

    private static IUnhandledErrorReporter Reporter(WindowHarness harness) => harness.Provider.GetRequiredService<IUnhandledErrorReporter>();

    private sealed class RecordingClipboard : IClipboardService
    {
        public List<string> Texts { get; } = [];

        public Task SetTextAsync(string text)
        {
            Texts.Add(text);
            return Task.CompletedTask;
        }
    }
}
