using System.IO;
using Enigma.Launchpad.Core.Diagnostics;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The Windows installer's identity is written in <c>packaging/windows/buildmsi.ps1</c>, text the compiler never sees,
/// and nothing in the build notices it change: these pin the three lines the MSI is built from.
/// </summary>
public sealed class WindowsInstallerScriptTests
{
    /// <summary>
    /// The upgrade code of the MSI that installed 1.4.0, read from its package. An MSI that carries the installed one's
    /// code replaces it, and one that does not installs beside it, so every later MSI must keep it.
    /// </summary>
    private const string UpgradeCode = "b4c2d5a2-9182-4c17-b570-c6019eb5ec92";

    [Fact]
    public void TheInstaller_KeepsTheUpgradeCodeOfTheInstalledReleases()
    {
        Assert.Contains($"$UpgradeCode = '{UpgradeCode}'", Script());
    }

    [Fact]
    public void TheInstaller_IsNamedAfterTheProduct()
    {
        // The MSI's product name, its install folder and its two shortcuts all take this one line.
        Assert.Contains($"$ProductName = '{ProductInformation.DisplayName}'", Script());
    }

    [Fact]
    public void TheInstaller_PackagesAndStartsTheExecutableOfTheAssembly()
    {
        // The publish folder the MSI packages and the executable its shortcuts start both take this line.
        Assert.Contains($"$Assembly = '{typeof(App).Assembly.GetName().Name}'", Script());
    }

    private static string[] Script()
        => File.ReadAllLines(Path.Combine(RepositoryRoot.Find(), "packaging", "windows", "buildmsi.ps1"));
}
