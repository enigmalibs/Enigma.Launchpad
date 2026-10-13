using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class RunWithArgumentsTests
{
    private const string CodePath = @"C:\Tools\code.exe";

    private readonly Shortcut _code = PageHarness.Shortcut("Code", CodePath, "--new-window");

    [Fact]
    public void TheDialog_StartsFromTheDefaultArguments()
    {
        ArgumentsDialogViewModel dialog = new(_code with { Arguments = "  --new-window  " });

        Assert.Equal(CodePath, dialog.Program);
        Assert.Equal("--new-window", dialog.DefaultArguments);
        Assert.Equal("--new-window", dialog.Arguments);
        Assert.Equal("Filled in with the shortcut's default arguments. Changes apply to this run only.", dialog.Hint);
    }

    [Fact]
    public void TheDialog_ForAProgramWithoutDefaults_StartsEmpty_AndSaysSo()
    {
        ArgumentsDialogViewModel dialog = new(_code with { Arguments = null });

        Assert.Equal(string.Empty, dialog.Arguments);
        Assert.Equal("The shortcut has no default arguments. What you type applies to this run only.", dialog.Hint);
    }

    [Fact]
    public void TheDialog_GivesTheArgumentsTrimmed()
    {
        ArgumentsDialogViewModel dialog = new(_code) { Arguments = "  --wait file.txt  " };

        Assert.Equal("--wait file.txt", dialog.TrimmedArguments);
    }

    [Fact]
    public async Task TheProgram_RunsWithTheArgumentsTyped_AndTheShortcutIsUnchanged()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code])));
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        harness.Dialogs.ArgumentAnswers.Enqueue("--wait notes.txt");

        await tile.RunWithArgumentsCommand.ExecuteAsync(null);
        await harness.Session.FlushAsync();

        Assert.Equal([_code], harness.Dialogs.ArgumentPrompts);
        (string action, ShortcutKind kind, Shortcut run) = Assert.Single(harness.Actions.Performed);
        Assert.Equal(("Open", ShortcutKind.Application), (action, kind));
        Assert.Equal(_code with { Arguments = "--wait notes.txt" }, run);
        Assert.Equal(_code, tile.Model);
        Assert.Empty(harness.Store.Saved);
    }

    [Fact]
    public async Task NoArgumentsTyped_RunsTheProgramWithout()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code])));
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];
        harness.Dialogs.ArgumentAnswers.Enqueue(string.Empty);

        await tile.RunWithArgumentsCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(harness.Actions.Performed).Shortcut.Arguments);
    }

    [Fact]
    public async Task Cancelling_RunsNothing()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code])));
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];

        await tile.RunWithArgumentsCommand.ExecuteAsync(null);

        Assert.Single(harness.Dialogs.ArgumentPrompts);
        Assert.Empty(harness.Actions.Performed);
    }

    [Fact]
    public async Task AClick_StillRunsWithTheDefaultArguments()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Application, ("Dev tools", [_code])));
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];

        tile.OpenCommand.Execute(null);

        Assert.Equal("--new-window", Assert.Single(harness.Actions.Performed).Shortcut.Arguments);
        Assert.Empty(harness.Dialogs.ArgumentPrompts);
    }

    [Theory]
    [InlineData(ShortcutKind.Application, CodePath, true)]
    [InlineData(ShortcutKind.Directory, @"C:\Projects", false)]
    [InlineData(ShortcutKind.Url, "https://example.com/", false)]
    public async Task OnlyApplications_RunWithArguments(ShortcutKind kind, string target, bool offered)
    {
        PageHarness harness = new(PageHarness.WithShortcuts(kind, ("Group", [PageHarness.Shortcut("Item", target)])), kind);
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups.Single().Shortcuts.Single();

        Assert.Equal(offered, tile.CanRunWithArguments);
        Assert.Equal(offered, tile.RunWithArgumentsCommand.CanExecute(null));
    }
}
