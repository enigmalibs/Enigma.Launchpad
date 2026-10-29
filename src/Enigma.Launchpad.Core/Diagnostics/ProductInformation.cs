using System;
using System.Reflection;

namespace Enigma.Launchpad.Core.Diagnostics;

/// <summary>
/// Identity of the Enigma.Launchpad product, for the About dialog and the Settings page.
/// </summary>
public static class ProductInformation
{
    /// <summary>
    /// The product's name in words, as the surfaces that present the product by name show it: the window's title,
    /// the About dialog and the Linux launcher entry.
    /// </summary>
    public const string DisplayName = "Enigma Launchpad";

    /// <summary>
    /// The version and the revision this build was cut from, read once from this assembly.
    /// </summary>
    /// <remarks>
    /// From <b>this</b> assembly rather than the entry assembly: Core and Desktop carry one version between them, and
    /// under a test host the entry assembly is the runner.
    /// </remarks>
    private static readonly ProductVersion Product = ProductVersion.From(
        typeof(ProductInformation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        FallbackVersion());

    /// <summary>
    /// Gets the displayable version, without the source revision the SDK appends.
    /// </summary>
    public static string Version => Product.Version;

    /// <summary>
    /// Gets the short commit this build was cut from, or <see langword="null"/> when it recorded none — a build from a
    /// source drop rather than from the repository.
    /// </summary>
    public static string? BuildSha => Product.BuildSha;

    /// <summary>
    /// Gets the copyright notice <c>Directory.Build.props</c> stamps on the assembly, or an empty string when it
    /// carries none.
    /// </summary>
    public static string Copyright { get; } =
        typeof(ProductInformation).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright?.Trim() ?? string.Empty;

    /// <summary>
    /// The plain assembly version, for when the informational one says nothing usable, trimmed to three components:
    /// the fourth is a zero nobody wrote.
    /// </summary>
    private static string? FallbackVersion()
    {
        Version? version = typeof(ProductInformation).Assembly.GetName().Version;

        return version is null ? null : version.ToString(version.Build >= 0 ? 3 : 2);
    }
}
