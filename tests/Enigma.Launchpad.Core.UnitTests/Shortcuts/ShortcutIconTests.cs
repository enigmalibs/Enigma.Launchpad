using System;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Shortcuts;

public sealed class ShortcutIconTests
{
    private static readonly byte[] Png = SamplePng.Bytes;

    [Fact]
    public void FromBytes_ThenTryReadBytes_GivesTheSameImageBack()
    {
        ShortcutIcon icon = ShortcutIcon.FromBytes(Png, isCustom: true);

        Assert.True(icon.IsCustom);
        Assert.Equal(Convert.ToBase64String(Png), icon.Png);
        Assert.True(icon.TryReadBytes(out byte[]? png));
        Assert.Equal(Png, png);
    }

    [Fact]
    public void TwoIconsOfTheSameImage_AreEqual()
        => Assert.Equal(ShortcutIcon.FromBytes(Png, isCustom: false), ShortcutIcon.FromBytes([.. Png], isCustom: false));

    [Theory]
    [InlineData("")]
    [InlineData("not base64 at all!")]
    [InlineData("R0lGODlhAQABAAAAACw=")]
    public void TryReadBytes_RefusesWhatIsNotAPngImage(string text)
        => Assert.False(new ShortcutIcon(text).TryReadBytes(out _));

    [Fact]
    public void TryReadBytes_RefusesAnImageOverTheLimit()
    {
        byte[] large = new byte[ShortcutIcon.MaxBytes + 1];
        Png.CopyTo(large, 0);

        Assert.False(ShortcutIcon.FromBytes(large, isCustom: true).TryReadBytes(out _));
    }

    [Fact]
    public void TryReadBytes_AcceptsAnImageAtTheLimit()
    {
        byte[] largest = new byte[ShortcutIcon.MaxBytes];
        Png.CopyTo(largest, 0);

        Assert.True(ShortcutIcon.FromBytes(largest, isCustom: true).TryReadBytes(out byte[]? png));
        Assert.Equal(ShortcutIcon.MaxBytes, png.Length);
    }
}
