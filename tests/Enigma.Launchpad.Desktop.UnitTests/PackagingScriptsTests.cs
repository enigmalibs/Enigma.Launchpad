using System;
using System.IO;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

/// <summary>
/// The publish scripts name the application's project and the command in text the compiler never sees, so nothing
/// but this test ties them to the project that really exists and to the publish they promise.
/// </summary>
public sealed class PackagingScriptsTests
{
    private const string Project = "Enigma.Launchpad.Desktop";

    [Theory]
    [InlineData("windows", "publish.ps1", "win-x64")]
    [InlineData("windows", "publish.bat", "win-x64")]
    [InlineData("linux", "publish.sh", "linux-x64")]
    public void ThePublishScripts_PublishTheDesktopProjectInReleaseReadyToRun(string platform, string script, string runtime)
    {
        string root = RepositoryRoot.Find();
        string text = File.ReadAllText(Path.Combine(root, "packaging", platform, script));

        // Each script resolves the project from where it sits, with its own platform's separator.
        Assert.True(
            text.Contains($"src/{Project}", StringComparison.Ordinal) || text.Contains($"src\\{Project}", StringComparison.Ordinal),
            $"{script} does not name src/{Project}");
        Assert.True(File.Exists(Path.Combine(root, "src", Project, $"{Project}.csproj")), $"src/{Project} holds no project");
        Assert.Contains($"-c Release -r {runtime} -p:PublishReadyToRun=true", text, StringComparison.Ordinal);
    }
}
