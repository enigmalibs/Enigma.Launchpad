using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

[Collection(HeadlessCollection.Name)]
public sealed class IconPngTests(HeadlessAvaloniaFixture fixture)
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void ASmallImage_KeepsItsSize()
        => fixture.Run(() =>
        {
            using Bitmap icon = IconPng.Decode(TestImages.Png(16, 16));

            Assert.Equal(new PixelSize(16, 16), icon.PixelSize);
        });

    [Fact]
    public void ALargerImage_IsScaledDownToFit_KeepingItsShape()
        => fixture.Run(() =>
        {
            using Bitmap icon = IconPng.Decode(TestImages.Png(256, 128));

            Assert.Equal(new PixelSize(IconPng.MaxSize, IconPng.MaxSize / 2), icon.PixelSize);
        });

    [Fact]
    public void Normalize_GivesAPngNoLargerThanTheMaximum()
        => fixture.Run(() =>
        {
            byte[] png = IconPng.Normalize(TestImages.Png(100, 100));

            Assert.True(png.AsSpan().StartsWith(PngSignature));
            Assert.Equal(new PixelSize(IconPng.MaxSize, IconPng.MaxSize), TestImages.SizeOf(png));
        });

    [Fact]
    public void BytesThatAreNoImage_AreRefused()
        => fixture.Run(() => Assert.ThrowsAny<Exception>(() => IconPng.Decode(ImageBytes.Png(1, 2, 3))));
}
