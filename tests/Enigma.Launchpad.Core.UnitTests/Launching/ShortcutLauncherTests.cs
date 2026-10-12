using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Launching;

public sealed class ShortcutLauncherTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RecordingProcessStarter _starter = new();
    private readonly ShortcutLauncher _launcher;

    public ShortcutLauncherTests()
        => _launcher = new ShortcutLauncher(_starter, NullLogger<ShortcutLauncher>.Instance);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Launch_StartsAnApplicationThroughTheShellInItsOwnFolder()
    {
        string program = _temp.CreateFile(Path.Combine("bin", "tool.exe"));

        LaunchResult result = _launcher.Launch(ShortcutKind.Application, Shortcut("Tool", program, "  --flag value  "));

        Assert.True(result.Succeeded);
        ProcessStartInfo started = Assert.Single(_starter.Started);
        Assert.Equal(program, started.FileName);
        Assert.True(started.UseShellExecute);
        Assert.Equal(_temp.Combine("bin"), started.WorkingDirectory);
        Assert.Equal("--flag value", started.Arguments);
    }

    [Fact]
    public void Launch_PassesNoArgumentsWhenThereAreNone()
    {
        string program = _temp.CreateFile("tool.exe");

        _launcher.Launch(ShortcutKind.Application, Shortcut("Tool", program, "   "));

        Assert.Equal(string.Empty, Assert.Single(_starter.Started).Arguments);
    }

    [Fact]
    public void Launch_OpensADirectoryThroughTheShell()
    {
        LaunchResult result = _launcher.Launch(ShortcutKind.Directory, Shortcut("Temp", _temp.Path));

        Assert.True(result.Succeeded);
        ProcessStartInfo started = Assert.Single(_starter.Started);
        Assert.Equal(_temp.Path, started.FileName);
        Assert.True(started.UseShellExecute);
    }

    [Fact]
    public void Launch_OpensAnAddressEscaped()
    {
        LaunchResult result = _launcher.Launch(ShortcutKind.Url, Shortcut("Docs", "https://example.com/a b"));

        Assert.True(result.Succeeded);
        ProcessStartInfo started = Assert.Single(_starter.Started);
        Assert.Equal("https://example.com/a%20b", started.FileName);
        Assert.True(started.UseShellExecute);
    }

    [Theory]
    [InlineData(ShortcutKind.Application, "missing.exe", "The program no longer exists")]
    [InlineData(ShortcutKind.Directory, "missing", "The folder no longer exists")]
    public void Launch_ReportsAMissingTargetWithoutStartingAnything(ShortcutKind kind, string name, string message)
    {
        LaunchResult result = _launcher.Launch(kind, Shortcut("Gone", _temp.Combine(name)));

        Assert.Equal(LaunchOutcome.TargetMissing, result.Outcome);
        Assert.StartsWith(message, result.Message, StringComparison.Ordinal);
        Assert.Empty(_starter.Started);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not an address")]
    public void Launch_RefusesAnAddressThatIsNotHttp(string target)
    {
        LaunchResult result = _launcher.Launch(ShortcutKind.Url, Shortcut("Odd", target));

        Assert.Equal(LaunchOutcome.Refused, result.Outcome);
        Assert.NotNull(result.Message);
        Assert.Empty(_starter.Started);
    }

    [Theory]
    [MemberData(nameof(StartFailures))]
    public void Launch_ReportsAStartFailure(Exception failure)
    {
        _starter.Failure = failure;

        LaunchResult result = _launcher.Launch(ShortcutKind.Directory, Shortcut("Temp", _temp.Path));

        Assert.Equal(LaunchOutcome.Failed, result.Outcome);
        Assert.Contains("\"Temp\" could not be opened", result.Message, StringComparison.Ordinal);
    }

    public static TheoryData<Exception> StartFailures()
        => new()
        {
            new Win32Exception(2, "The system cannot find the file specified."),
            new InvalidOperationException("No file name."),
            new PlatformNotSupportedException(),
        };

    [Fact]
    public void Reveal_RefusesAnAddress()
    {
        LaunchResult result = _launcher.Reveal(ShortcutKind.Url, Shortcut("Docs", "https://example.com"));

        Assert.Equal(LaunchOutcome.Refused, result.Outcome);
        Assert.Empty(_starter.Started);
    }

    [Fact]
    public void Reveal_ReportsAMissingTarget()
    {
        LaunchResult result = _launcher.Reveal(ShortcutKind.Directory, Shortcut("Gone", _temp.Combine("missing")));

        Assert.Equal(LaunchOutcome.TargetMissing, result.Outcome);
        Assert.Empty(_starter.Started);
    }

    [Fact]
    public void Reveal_StartsTheFileManager()
    {
        string program = _temp.CreateFile("tool.exe");

        Assert.True(_launcher.Reveal(ShortcutKind.Application, Shortcut("Tool", program)).Succeeded);
        Assert.Single(_starter.Started);
    }

    [Fact]
    public void BuildRevealStartInfo_OnWindows_SelectsTheItemInExplorer()
    {
        ProcessStartInfo info = ShortcutLauncher.BuildRevealStartInfo(DesktopPlatform.Windows, @"C:\Program Files\Tool\tool.exe");

        Assert.Equal("explorer.exe", info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal([@"/select,C:\Program Files\Tool\tool.exe"], info.ArgumentList);
    }

    [Fact]
    public void BuildRevealStartInfo_OnMacOS_RevealsTheItemInFinder()
    {
        ProcessStartInfo info = ShortcutLauncher.BuildRevealStartInfo(DesktopPlatform.MacOS, "/Applications/Editor.app");

        Assert.Equal("open", info.FileName);
        Assert.Equal(["-R", "/Applications/Editor.app"], info.ArgumentList);
    }

    [Fact]
    public void BuildRevealStartInfo_OnLinux_OpensTheContainingDirectory()
    {
        ProcessStartInfo info = ShortcutLauncher.BuildRevealStartInfo(DesktopPlatform.Linux, "/opt/tool/bin/tool");

        Assert.Equal(Path.GetDirectoryName("/opt/tool/bin/tool"), info.FileName);
        Assert.True(info.UseShellExecute);
    }

    private static Shortcut Shortcut(string name, string target, string? arguments = null)
        => new(Guid.NewGuid(), name, target, arguments);
}
