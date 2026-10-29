using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class UnhandledErrorPolicyTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly UnhandledErrorPolicy _policy;

    public UnhandledErrorPolicyTests() => _policy = new UnhandledErrorPolicy(_time);

    [Fact]
    public void TheFirstError_IsQueuedForADialog()
    {
        Exception error = Thrown("The disk is full");

        Assert.Equal(UnhandledErrorDecision.Queue, _policy.Offer(error));
        Assert.Same(error, _policy.Pending?.First);
        Assert.Null(_policy.Showing);
    }

    [Fact]
    public void AnErrorWhileOneWaits_IsCountedIntoIt()
    {
        _policy.Offer(Thrown("first"));

        Assert.Equal(UnhandledErrorDecision.Merge, _policy.Offer(Thrown("second")));
        Assert.Equal(1, _policy.Pending?.More);
    }

    [Fact]
    public void AnErrorWhileOneIsOnScreen_IsCountedIntoIt_AndTheDialogIsTold()
    {
        _policy.Offer(Thrown("first"));
        UnhandledErrorBatch batch = _policy.BeginShow() ?? throw new InvalidOperationException("Nothing to show.");
        int changes = 0;
        batch.Changed += (_, _) => changes++;

        Assert.Equal(UnhandledErrorDecision.Merge, _policy.Offer(Thrown("second")));
        Assert.Equal(1, batch.More);
        Assert.Equal(1, changes);
        Assert.Null(_policy.Pending);
    }

    [Fact]
    public void TheSameExceptionTwice_IsOfferedOnce()
    {
        Exception error = Thrown("once");
        _policy.Offer(error);

        Assert.Equal(UnhandledErrorDecision.Ignore, _policy.Offer(error));
        Assert.Equal(0, _policy.Pending?.More);
    }

    [Fact]
    public void ACancellationSomeoneAskedFor_IsNoError()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.Equal(UnhandledErrorDecision.Ignore, _policy.Offer(new OperationCanceledException(source.Token)));
        Assert.Equal(UnhandledErrorDecision.Ignore, _policy.Offer(new AggregateException(new OperationCanceledException(source.Token))));
        Assert.Null(_policy.Pending);
    }

    [Fact]
    public void ACancellationNobodyAskedFor_IsAnError()
        => Assert.Equal(UnhandledErrorDecision.Queue, _policy.Offer(new TimeoutException("too slow", new OperationCanceledException())));

    [Fact]
    public void WhileClosing_EveryCancellationIsNoError()
    {
        _policy.IsShuttingDown = true;

        Assert.Equal(UnhandledErrorDecision.Ignore, _policy.Offer(new OperationCanceledException()));
        Assert.Equal(UnhandledErrorDecision.Ignore, _policy.Offer(new TaskCanceledException()));
    }

    [Fact]
    public void AnErrorJustClosed_IsNotShownAgainWithinTheWindow_ButIsAfterIt()
    {
        _policy.Offer(Thrown("again"));
        _policy.BeginShow();
        _policy.EndShow();

        _time.Advance(UnhandledErrorPolicy.RepeatWindow - TimeSpan.FromSeconds(1));
        Assert.Equal(UnhandledErrorDecision.Suppress, _policy.Offer(Thrown("again")));

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(UnhandledErrorDecision.Queue, _policy.Offer(Thrown("again")));
    }

    [Fact]
    public void ADifferentError_IsShownRightAfterAnotherIsClosed()
    {
        _policy.Offer(Thrown("one"));
        _policy.BeginShow();
        _policy.EndShow();

        Assert.Equal(UnhandledErrorDecision.Queue, _policy.Offer(Thrown("another")));
    }

    [Fact]
    public void Summarise_SaysWhatTheWrappedExceptionSays()
    {
        Assert.Equal("inner", UnhandledErrorPolicy.Summarise(new AggregateException(new InvalidOperationException("inner"))));
        Assert.Equal("inner", UnhandledErrorPolicy.Summarise(new TargetInvocationException(new InvalidOperationException("inner"))));
        Assert.Equal("outer", UnhandledErrorPolicy.Summarise(new InvalidOperationException("outer", new ArgumentException("inner"))));
    }

    [Fact]
    public void Summarise_NamesTheTypeOfAnExceptionWithNoMessage()
        => Assert.Equal(nameof(SilentException), UnhandledErrorPolicy.Summarise(new SilentException()));

    [Fact]
    public void TheDetails_CarryEveryInnerException_TheirStacks_AndNeverTheData()
    {
        Exception inner = Thrown("the inner one");
        InvalidOperationException outer = new("the outer one", inner);
        outer.Data["token"] = "secret-value";

        string details = UnhandledErrorPolicy.BuildDetails(outer);

        Assert.StartsWith("System.InvalidOperationException: the outer one", details, StringComparison.Ordinal);
        Assert.Contains("--- Inner exception ---", details, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: the inner one", details, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), details, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", details, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDetails_NumberTheInnerExceptionsOfAnAggregate()
    {
        string details = UnhandledErrorPolicy.BuildDetails(new AggregateException(new InvalidOperationException("a"), new ArgumentException("b")));

        Assert.Contains("--- Inner exception 1 of 2 ---", details, StringComparison.Ordinal);
        Assert.Contains("--- Inner exception 2 of 2 ---", details, StringComparison.Ordinal);
    }

    [Fact]
    public void ABatchOfSeveral_NumbersEachErrorInItsDetails()
    {
        _policy.Offer(Thrown("first"));
        _policy.Offer(Thrown("second"));

        string details = _policy.Pending?.Details ?? string.Empty;

        Assert.Contains("Error 1 of 2", details, StringComparison.Ordinal);
        Assert.Contains("Error 2 of 2", details, StringComparison.Ordinal);
        Assert.Equal("first", _policy.Pending?.Message);
    }

    /// <summary>
    /// An exception that has really been thrown, so it has a stack trace, always from the same place, so two of them
    /// with the same message are the same error.
    /// </summary>
    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private sealed class SilentException : Exception
    {
        public override string Message => string.Empty;
    }
}
