using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Launchpad.Desktop.Services;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The tests that touch what belongs to the whole process — the hooks and the net they hand over to — run alone.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessWideCollection
{
    /// <summary>
    /// The collection's name.
    /// </summary>
    public const string Name = "process-wide";
}

/// <summary>
/// The hooks and <see cref="TaskExtensions.Forget"/>: what reaches the safety net, and when.
/// </summary>
/// <remarks>
/// The dispatcher hook is not installed here: it would mark every exception of a later headless test's dispatcher
/// job handled, and hide that test's failure.
/// </remarks>
[Collection(ProcessWideCollection.Name)]
public sealed class UnhandledExceptionHooksTests : IDisposable
{
    private readonly RecordingReporter _net = new();

    public UnhandledExceptionHooksTests()
    {
        // What an earlier test left waiting for a net is drained, so each test sees its own reports only.
        UnhandledExceptionHooks.Attach(new RecordingReporter());
        UnhandledExceptionHooks.Detach();
    }

    public void Dispose() => UnhandledExceptionHooks.Detach();

    [Fact]
    public void ReportsBeforeTheNetIsThere_AreHandedOverWhenItIs_InOrder()
    {
        InvalidOperationException first = new("first");
        InvalidOperationException second = new("second");

        UnhandledExceptionHooks.Report(first, UnhandledErrorSource.Startup);
        UnhandledExceptionHooks.Report(second, UnhandledErrorSource.UnobservedTask);
        Assert.Empty(_net.Reports);

        UnhandledExceptionHooks.Attach(_net);

        Assert.Equal([(first, UnhandledErrorSource.Startup), (second, UnhandledErrorSource.UnobservedTask)], _net.Reports);
    }

    [Fact]
    public void OnceTheNetIsThere_AReportReachesItAtOnce()
    {
        UnhandledExceptionHooks.Attach(_net);
        InvalidOperationException error = new("now");

        UnhandledExceptionHooks.Report(error, UnhandledErrorSource.Dispatcher);

        Assert.Equal([(error, UnhandledErrorSource.Dispatcher)], _net.Reports);
    }

    [Fact]
    public void AfterDetach_ReportsWaitForTheNextNet()
    {
        UnhandledExceptionHooks.Attach(_net);
        UnhandledExceptionHooks.Detach();
        InvalidOperationException late = new("late");

        UnhandledExceptionHooks.Report(late, UnhandledErrorSource.AppDomain);
        Assert.Empty(_net.Reports);

        RecordingReporter next = new();
        UnhandledExceptionHooks.Attach(next);
        Assert.Equal([(late, UnhandledErrorSource.AppDomain)], next.Reports);
    }

    [Fact]
    public void Forget_ReportsATaskThatHasFailed()
    {
        UnhandledExceptionHooks.Attach(_net);
        InvalidOperationException error = new("failed already");

        Task.FromException(error).Forget();

        Assert.Equal([(error, UnhandledErrorSource.FireAndForget)], _net.Reports);
    }

    [Fact]
    public void Forget_ReportsATaskThatFailsLater_AndEachOfItsErrors()
    {
        UnhandledExceptionHooks.Attach(_net);
        TaskCompletionSource running = new();
        InvalidOperationException one = new("one");
        ArgumentException two = new("two");

        running.Task.Forget();
        Assert.Empty(_net.Reports);

        // The continuation runs synchronously on the completing thread, so the reports are there on return.
        running.SetException([one, two]);

        Assert.Equal([(one, UnhandledErrorSource.FireAndForget), (two, UnhandledErrorSource.FireAndForget)], _net.Reports);
    }

    [Fact]
    public void Forget_SaysNothingOfASuccessOrACancellation()
    {
        UnhandledExceptionHooks.Attach(_net);
        CancellationToken cancelled = new(canceled: true);
        TaskCompletionSource succeeding = new();
        TaskCompletionSource cancelling = new();

        Task.CompletedTask.Forget();
        Task.FromCanceled(cancelled).Forget();
        succeeding.Task.Forget();
        cancelling.Task.Forget();
        succeeding.SetResult();
        cancelling.SetCanceled(cancelled);

        Assert.Empty(_net.Reports);
    }

    [Fact]
    public void Install_HandsAFaultNobodyObservedToTheNet()
    {
        UnhandledExceptionHooks.Install();
        UnhandledExceptionHooks.Attach(_net);
        string marker = Guid.NewGuid().ToString("N");

        LeaveAFaultedTask(marker);

        // A fault nobody looked at is found when its task is finalised.
        for (int attempt = 0; attempt < 10 && !_net.Reports.Any(report => Carries(report.Exception, marker)); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        (Exception _, UnhandledErrorSource source) = Assert.Single(_net.Reports, report => Carries(report.Exception, marker));
        Assert.Equal(UnhandledErrorSource.UnobservedTask, source);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LeaveAFaultedTask(string marker) => _ = Task.FromException(new InvalidOperationException(marker));

    private static bool Carries(Exception exception, string marker)
        => exception is AggregateException aggregate && aggregate.InnerExceptions.Any(inner => inner.Message == marker);

    private sealed class RecordingReporter : IUnhandledErrorReporter
    {
        private readonly ConcurrentQueue<(Exception Exception, UnhandledErrorSource Source)> _reports = new();

        public (Exception Exception, UnhandledErrorSource Source)[] Reports => [.. _reports];

        public void Report(Exception exception, UnhandledErrorSource source) => _reports.Enqueue((exception, source));

        public void RegisterHost(ContentDialog host)
        {
        }

        public void ShutdownStarted()
        {
        }
    }
}
