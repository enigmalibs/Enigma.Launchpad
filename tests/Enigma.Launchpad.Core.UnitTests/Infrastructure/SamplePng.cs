namespace Enigma.Launchpad.Core.UnitTests.Infrastructure;

/// <summary>
/// Bytes that start like a PNG file: enough for the Core's checks, which never decode an image.
/// </summary>
internal static class SamplePng
{
    public static byte[] Bytes => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];
}
