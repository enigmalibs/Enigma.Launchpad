using System.Linq;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Pages;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class TileActionsTests
{
    [Theory]
    [InlineData(ShortcutKind.Application, @"C:\Tools\code.exe", false)]
    [InlineData(ShortcutKind.Directory, @"C:\Projects", true)]
    [InlineData(ShortcutKind.Url, "https://example.com/", true)]
    public async Task FoldersAndLinks_CopyTheirTargetFromTheTile(ShortcutKind kind, string target, bool canCopy)
    {
        ShortcutViewModel tile = await TileAsync(kind, target);

        Assert.Equal(canCopy, tile.CanCopyFromTile);
    }

    [Theory]
    [InlineData(ShortcutKind.Directory, @"C:\Projects", "Copy path")]
    [InlineData(ShortcutKind.Url, "https://example.com/", "Copy URL")]
    public async Task TheCopyButton_SaysWhatItCopies(ShortcutKind kind, string target, string label)
    {
        ShortcutViewModel tile = await TileAsync(kind, target);

        Assert.Equal(label, tile.CopyTargetLabel);
    }

    [Theory]
    [InlineData(ShortcutKind.Application, @"C:\Tools\code.exe", false)]
    [InlineData(ShortcutKind.Directory, @"C:\Projects", true)]
    [InlineData(ShortcutKind.Url, "https://example.com/", false)]
    public async Task OnlyFolders_OpenInATerminal(ShortcutKind kind, string target, bool offered)
    {
        ShortcutViewModel tile = await TileAsync(kind, target);

        Assert.Equal(offered, tile.CanOpenInTerminal);
        Assert.Equal(offered, tile.OpenInTerminalCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenInTerminal_OpensTheFoldersTerminal()
    {
        PageHarness harness = new(PageHarness.WithShortcuts(ShortcutKind.Directory, ("Work", [PageHarness.Shortcut("Projects", @"C:\Projects")])), ShortcutKind.Directory);
        ShortcutViewModel tile = (await harness.LoadAsync()).Groups[0].Shortcuts[0];

        tile.OpenInTerminalCommand.Execute(null);

        (string action, _, Shortcut shortcut) = Assert.Single(harness.Actions.Performed);
        Assert.Equal("Terminal", action);
        Assert.Equal(@"C:\Projects", shortcut.Target);
    }

    private static async Task<ShortcutViewModel> TileAsync(ShortcutKind kind, string target)
    {
        PageHarness harness = new(PageHarness.WithShortcuts(kind, ("Group", [PageHarness.Shortcut("Item", target)])), kind);
        CategoryPageViewModel page = await harness.LoadAsync();
        return page.Groups.Single().Shortcuts.Single();
    }
}
