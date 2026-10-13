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

    private static async Task<ShortcutViewModel> TileAsync(ShortcutKind kind, string target)
    {
        PageHarness harness = new(PageHarness.WithShortcuts(kind, ("Group", [PageHarness.Shortcut("Item", target)])), kind);
        CategoryPageViewModel page = await harness.LoadAsync();
        return page.Groups.Single().Shortcuts.Single();
    }
}
