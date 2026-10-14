using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private readonly FakeCommandResolver _commands = new();
    private readonly ShortcutLauncher _launcher;

    public ShortcutLauncherTests()
        => _launcher = new ShortcutLauncher(_starter, _commands, NullLogger<ShortcutLauncher>.Instance);

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
    public void Launch_StartsTheProgramACommandLeadsTo_InTheHomeFolder_WithItsArguments()
    {
        string program = _temp.CreateFile(Path.Combine("bin", "code.cmd"));
        _commands.Programs["code"] = program;

        LaunchResult result = _launcher.Launch(ShortcutKind.Application, Shortcut("Code", "code", " --new-window "));

        Assert.True(result.Succeeded);
        ProcessStartInfo started = Assert.Single(_starter.Started);
        Assert.Equal(program, started.FileName);
        Assert.True(started.UseShellExecute);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), started.WorkingDirectory);
        Assert.Equal("--new-window", started.Arguments);
        Assert.Equal(["code"], _commands.Lookups);
    }

    [Fact]
    public void Launch_OpensADocumentThroughTheShell_InItsOwnFolder()
    {
        string report = _temp.CreateFile(Path.Combine("docs", "report.pdf"));

        LaunchResult result = _launcher.Launch(ShortcutKind.Document, Shortcut("Report", report, "--ignored"));

        Assert.True(result.Succeeded);
        ProcessStartInfo started = Assert.Single(_starter.Started);
        Assert.Equal(report, started.FileName);
        Assert.True(started.UseShellExecute);
        Assert.Equal(_temp.Combine("docs"), started.WorkingDirectory);
        Assert.Empty(started.Arguments);
    }

    [Fact]
    public void Launch_OfAMissingDocument_StartsNothing_AndSaysSo()
    {
        LaunchResult result = _launcher.Launch(ShortcutKind.Document, Shortcut("Gone", _temp.Combine("gone.pdf")));

        Assert.Equal(LaunchOutcome.TargetMissing, result.Outcome);
        Assert.StartsWith("The document no longer exists", result.Message, StringComparison.Ordinal);
        Assert.Empty(_starter.Attempted);
    }

    [Fact]
    public void Reveal_ShowsADocumentInItsFolder()
    {
        string report = _temp.CreateFile("report.pdf");

        Assert.True(_launcher.Reveal(ShortcutKind.Document, Shortcut("Report", report)).Succeeded);
        Assert.Single(_starter.Started);
    }

    [Fact]
    public void Launch_OfACommandNotOnThePath_StartsNothing_AndSaysSo()
    {
        LaunchResult result = _launcher.Launch(ShortcutKind.Application, Shortcut("Gone", "gone"));

        Assert.Equal(LaunchOutcome.TargetMissing, result.Outcome);
        Assert.Equal("No program called “gone” was found on the PATH.", result.Message);
        Assert.Empty(_starter.Attempted);
    }

    [Fact]
    public void Launch_OfAFullPath_NeverLooksOnThePath()
    {
        string program = _temp.CreateFile("tool.exe");

        Assert.True(_launcher.Launch(ShortcutKind.Application, Shortcut("Tool", program)).Succeeded);

        Assert.Empty(_commands.Lookups);
    }

    [Fact]
    public void Reveal_ShowsTheProgramACommandLeadsTo()
    {
        string program = _temp.CreateFile(Path.Combine("bin", "code.exe"));
        _commands.Programs["code"] = program;

        Assert.True(_launcher.Reveal(ShortcutKind.Application, Shortcut("Code", "code")).Succeeded);

        ProcessStartInfo started = Assert.Single(_starter.Started);
        string shown = started.ArgumentList.Count > 0 ? started.ArgumentList[^1] : started.FileName;
        Assert.Contains(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? program : _temp.Combine("bin"), shown, StringComparison.Ordinal);
    }

    [Fact]
    public void Reveal_OfACommandNotOnThePath_SaysSo()
    {
        LaunchResult result = _launcher.Reveal(ShortcutKind.Application, Shortcut("Gone", "gone"));

        Assert.Equal(LaunchOutcome.TargetMissing, result.Outcome);
        Assert.Empty(_starter.Attempted);
    }

    [Fact]
    public void ATerminal_OnWindows_IsWindowsTerminal_ThenTheCommandPrompt_InTheFolder()
    {
        string folder = @"C:\Work;Projects";

        ProcessStartInfo[] terminals = [.. ShortcutLauncher.BuildTerminalStartInfos(DesktopPlatform.Windows, folder)];

        Assert.Equal(["wt.exe", "cmd.exe"], terminals.Select(terminal => terminal.FileName));
        Assert.All(terminals, terminal => Assert.Equal(folder, terminal.WorkingDirectory));
        Assert.All(terminals, terminal => Assert.False(terminal.UseShellExecute));
        Assert.Equal(["-d", "."], terminals[0].ArgumentList);
        Assert.Empty(terminals[1].ArgumentList);
        Assert.Empty(terminals[1].Arguments);
    }

    [Fact]
    public void ATerminal_OnMacOS_IsTerminal_GivenTheFolderAsAnArgumentOfItsOwn()
    {
        ProcessStartInfo terminal = Assert.Single(ShortcutLauncher.BuildTerminalStartInfos(DesktopPlatform.MacOS, "/Users/me/My Projects"));

        Assert.Equal("open", terminal.FileName);
        Assert.Equal(["-a", "Terminal", "/Users/me/My Projects"], terminal.ArgumentList);
        Assert.False(terminal.UseShellExecute);
    }

    [Fact]
    public void ATerminal_OnLinux_IsTheDesktopsOwn_ThenTheCommonOnes_InTheFolder()
    {
        ProcessStartInfo[] terminals = [.. ShortcutLauncher.BuildTerminalStartInfos(DesktopPlatform.Linux, "/home/me/work")];

        Assert.Equal(["x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm"], terminals.Select(terminal => terminal.FileName));
        Assert.All(terminals, terminal => Assert.Equal("/home/me/work", terminal.WorkingDirectory));
        Assert.All(terminals, terminal => Assert.Empty(terminal.ArgumentList));
    }

    [Fact]
    public void OpenInTerminal_StartsThePreferredTerminal()
    {
        LaunchResult result = _launcher.OpenInTerminal(Shortcut("Temp", _temp.Path));

        Assert.True(result.Succeeded);
        ProcessStartInfo started = Assert.Single(_starter.Started);
        Assert.Equal(_temp.Path, started.WorkingDirectory);
    }

    [Fact]
    public void OpenInTerminal_TriesTheNextTerminal_WhenOneIsNotInstalled()
    {
        string preferred = TerminalsHere()[0];
        _starter.Missing.Add(preferred);

        LaunchResult result = _launcher.OpenInTerminal(Shortcut("Temp", _temp.Path));

        Assert.True(result.Succeeded);
        Assert.Equal(TerminalsHere()[1], Assert.Single(_starter.Started).FileName);
    }

    [Fact]
    public void OpenInTerminal_WithNoTerminalInstalled_SaysSo()
    {
        foreach (string terminal in TerminalsHere())
        {
            _starter.Missing.Add(terminal);
        }

        LaunchResult result = _launcher.OpenInTerminal(Shortcut("Temp", _temp.Path));

        Assert.Equal(new LaunchResult(LaunchOutcome.Failed, "No terminal was found on this computer."), result);
        Assert.Empty(_starter.Started);
    }

    [Fact]
    public void OpenInTerminal_ThatFailsOtherwise_StopsThere_AndSaysWhy()
    {
        _starter.Failure = new Win32Exception(5, "Access is denied.");

        LaunchResult result = _launcher.OpenInTerminal(Shortcut("Temp", _temp.Path));

        Assert.Equal(LaunchOutcome.Failed, result.Outcome);
        Assert.Equal("A terminal could not be opened in \u201CTemp\u201D: Access is denied.", result.Message);
        Assert.Single(_starter.Attempted);
    }

    [Fact]
    public void OpenInTerminal_OfAMissingFolder_StartsNothing()
    {
        string missing = _temp.Combine("gone");

        LaunchResult result = _launcher.OpenInTerminal(Shortcut("Gone", missing));

        Assert.Equal(LaunchOutcome.TargetMissing, result.Outcome);
        Assert.Empty(_starter.Attempted);
    }

    private static string[] TerminalsHere()
        => [.. ShortcutLauncher.BuildTerminalStartInfos(
                OperatingSystem.IsWindows() ? DesktopPlatform.Windows : OperatingSystem.IsMacOS() ? DesktopPlatform.MacOS : DesktopPlatform.Linux,
                "/")
            .Select(terminal => terminal.FileName)];

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
