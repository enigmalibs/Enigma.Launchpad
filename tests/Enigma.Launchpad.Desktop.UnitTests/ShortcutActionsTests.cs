using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ShortcutActionsTests
{
    private readonly ScriptedLauncher _launcher = new();
    private readonly RecordingNotifications _notifications = new();
    private readonly RecordingClipboard _clipboard = new();
    private readonly FakeSettingsService _settings = new();
    private readonly RecordingMainWindow _window = new();
    private readonly ShortcutActions _actions;

    public ShortcutActionsTests() => _actions = new ShortcutActions(_launcher, _notifications, _clipboard, _settings, _window);

    [Fact]
    public void Open_ThatSucceeds_TellsTheUserNothing()
    {
        Assert.True(_actions.Open(ShortcutKind.Url, Shortcut()));

        Assert.Equal(["Launch Url"], _launcher.Calls);
        Assert.Empty(_notifications.Shown);
    }

    [Theory]
    [InlineData(LaunchOutcome.TargetMissing, "Shortcut target not found")]
    [InlineData(LaunchOutcome.Refused, "This shortcut cannot be opened")]
    [InlineData(LaunchOutcome.Failed, "The shortcut could not be opened")]
    public void Open_ThatFails_ShowsAnErrorWithTheLaunchersMessage(LaunchOutcome outcome, string title)
    {
        _launcher.Result = new LaunchResult(outcome, "Details.");

        Assert.False(_actions.Open(ShortcutKind.Application, Shortcut()));

        Assert.Equal(("Error", title, "Details."), Assert.Single(_notifications.Shown));
    }

    [Fact]
    public void Reveal_ThatFails_ShowsAnError()
    {
        _launcher.Result = new LaunchResult(LaunchOutcome.TargetMissing, "Gone.");

        Assert.False(_actions.Reveal(ShortcutKind.Directory, Shortcut()));

        Assert.Equal(["Reveal Directory"], _launcher.Calls);
        Assert.Single(_notifications.Shown);
    }

    [Fact]
    public void OpenInTerminal_ThatSucceeds_MinimizesTheWindow_WhenTheUserAskedForIt()
    {
        _settings.Update(current => current with { MinimizeAfterLaunch = true });

        Assert.True(_actions.OpenInTerminal(Shortcut()));

        Assert.Equal(["Terminal"], _launcher.Calls);
        Assert.Equal(1, _window.Minimized);
        Assert.Empty(_notifications.Shown);
    }

    [Fact]
    public void OpenInTerminal_ThatFails_ShowsAnError_AndNeverMinimizes()
    {
        _settings.Update(current => current with { MinimizeAfterLaunch = true });
        _launcher.Result = new LaunchResult(LaunchOutcome.Failed, "No terminal was found on this computer.");

        Assert.False(_actions.OpenInTerminal(Shortcut()));

        Assert.Equal(("Error", "The shortcut could not be opened", "No terminal was found on this computer."), Assert.Single(_notifications.Shown));
        Assert.Equal(0, _window.Minimized);
    }

    [Fact]
    public async Task CopyTarget_PutsTheTargetOnTheClipboard()
    {
        await _actions.CopyTargetAsync(Shortcut("https://example.com"));

        Assert.Equal(["https://example.com"], _clipboard.Texts);
    }

    [Fact]
    public void Open_ThatSucceeds_LeavesTheWindowAlone_ByDefault()
    {
        Assert.True(_actions.Open(ShortcutKind.Url, Shortcut()));

        Assert.Equal(0, _window.Minimized);
    }

    [Fact]
    public void Open_ThatSucceeds_MinimizesTheWindow_WhenTheUserAskedForIt()
    {
        _settings.Update(current => current with { MinimizeAfterLaunch = true });

        Assert.True(_actions.Open(ShortcutKind.Application, Shortcut()));

        Assert.Equal(1, _window.Minimized);
    }

    [Fact]
    public void Open_ThatFails_NeverMinimizes()
    {
        _settings.Update(current => current with { MinimizeAfterLaunch = true });
        _launcher.Result = new LaunchResult(LaunchOutcome.TargetMissing, "Gone.");

        Assert.False(_actions.Open(ShortcutKind.Application, Shortcut()));

        Assert.Equal(0, _window.Minimized);
    }

    [Fact]
    public void OpenFolder_OpensItAsADirectory_WithoutMinimizing()
    {
        _settings.Update(current => current with { MinimizeAfterLaunch = true });

        Assert.True(_actions.OpenFolder(@"C:\Users\me\AppData\Roaming\Enigma.Launchpad"));

        Assert.Equal(["Launch Directory"], _launcher.Calls);
        Assert.Equal(@"C:\Users\me\AppData\Roaming\Enigma.Launchpad", _launcher.LastTarget);
        Assert.Equal(0, _window.Minimized);
    }

    [Fact]
    public void OpenFolder_ThatFails_ShowsAnError()
    {
        _launcher.Result = new LaunchResult(LaunchOutcome.TargetMissing, "Gone.");

        Assert.False(_actions.OpenFolder(@"C:\Missing"));

        Assert.Equal(("Error", "Shortcut target not found", "Gone."), Assert.Single(_notifications.Shown));
    }

    private static Shortcut Shortcut(string target = @"C:\Tools") => new(Guid.NewGuid(), "Name", target);

    private sealed class ScriptedLauncher : IShortcutLauncher
    {
        public LaunchResult Result { get; set; } = LaunchResult.Launched;

        public List<string> Calls { get; } = [];

        public string? LastTarget { get; private set; }

        public LaunchResult Launch(ShortcutKind kind, Shortcut shortcut)
        {
            Calls.Add($"Launch {kind}");
            LastTarget = shortcut.Target;
            return Result;
        }

        public LaunchResult Reveal(ShortcutKind kind, Shortcut shortcut)
        {
            Calls.Add($"Reveal {kind}");
            return Result;
        }

        public LaunchResult OpenInTerminal(Shortcut shortcut)
        {
            Calls.Add("Terminal");
            return Result;
        }
    }

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
