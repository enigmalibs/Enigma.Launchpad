using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class ShortcutIconsTests
{
    private const string Program = @"C:\Tools\code.exe";
    private const string Link = "https://example.com/docs";

    private readonly HeadlessAvaloniaFixture _fixture;
    private readonly FakeTargetIcons _programIcons = new();
    private readonly FakeTargetIcons _websiteIcons = new();
    private readonly RecordingLogger<ShortcutIcons> _logger = new();
    private readonly ShortcutIcons _icons;

    public ShortcutIconsTests(HeadlessAvaloniaFixture fixture)
    {
        _fixture = fixture;
        _icons = new ShortcutIcons(_programIcons, _websiteIcons, _logger);
    }

    [Theory]
    [InlineData(ShortcutKind.Application, true)]
    [InlineData(ShortcutKind.Directory, false)]
    [InlineData(ShortcutKind.Url, true)]
    public void OnlyProgramsAndWebsites_HaveIconsToFind(ShortcutKind kind, bool canFind)
        => Assert.Equal(canFind, _icons.CanFind(kind));

    [Fact]
    public void AKeptIcon_IsShownAsItsImage()
        => _fixture.Run(() =>
        {
            IImage? image = _icons.Show(ShortcutIcon.FromBytes(TestImages.Png(32, 32), isCustom: true));

            Assert.Equal(new PixelSize(32, 32), Assert.IsType<Bitmap>(image).PixelSize);
        });

    [Theory]
    [InlineData("not base64")]
    [InlineData("R0lGODlhAQABAAAAACw=")]
    public void AKeptIconThatIsNoPng_ShowsNothing(string text)
        => Assert.Null(_icons.Show(new ShortcutIcon(text)));

    [Fact]
    public void AKeptPngThatDoesNotDecode_ShowsNothing_AndIsOnlyADebugNote()
        => _fixture.Run(() =>
        {
            Assert.Null(_icons.Show(ShortcutIcon.FromBytes(ImageBytes.Png(1, 2, 3), isCustom: false)));

            Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
        });

    [Fact]
    public void AProgramsIcon_IsFoundFromItsPath_AsAnAutomaticIcon()
        => _fixture.RunAsync(async () =>
        {
            byte[] png = TestImages.Png(48, 48);
            _programIcons.Answer = path => path == Program ? png : null;

            IconImage? found = await _icons.FindAsync(ShortcutKind.Application, Program);

            Assert.NotNull(found);
            Assert.Equal(ShortcutIcon.FromBytes(png, isCustom: false), found.Icon);
            Assert.Equal(new PixelSize(48, 48), Assert.IsType<Bitmap>(found.Image).PixelSize);
            Assert.Equal([Program], _programIcons.Requests);
            Assert.Empty(_websiteIcons.Requests);
        });

    [Fact]
    public void AWebsitesIcon_IsFoundFromTheAddress()
        => _fixture.RunAsync(async () =>
        {
            _websiteIcons.Answer = _ => TestImages.Png(16, 16);

            IconImage? found = await _icons.FindAsync(ShortcutKind.Url, Link);

            Assert.NotNull(found);
            Assert.False(found.Icon.IsCustom);
            Assert.Equal([Link], _websiteIcons.Requests);
            Assert.Empty(_programIcons.Requests);
        });

    [Fact]
    public async Task AFolder_HasNoIconToFind_AndNoSourceIsAsked()
    {
        Assert.Null(await _icons.FindAsync(ShortcutKind.Directory, @"C:\Docs", TestContext.Current.CancellationToken));

        Assert.Empty(_programIcons.Requests);
        Assert.Empty(_websiteIcons.Requests);
    }

    [Fact]
    public async Task NothingFound_GivesNoIcon()
        => Assert.Null(await _icons.FindAsync(ShortcutKind.Application, Program, TestContext.Current.CancellationToken));

    [Fact]
    public void AFoundIconThatDoesNotDecode_GivesNoIcon()
        => _fixture.RunAsync(async () =>
        {
            _programIcons.Answer = _ => ImageBytes.Png(9, 9, 9);

            Assert.Null(await _icons.FindAsync(ShortcutKind.Application, Program));
        });
}
