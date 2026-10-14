using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Enigma.Launchpad.Core.Launching;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Launching;

public sealed class PathCommandResolverTests : IDisposable
{
    private static readonly string[] WindowsExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ACommand_IsFoundInTheFirstDirectoryOfThePathThatHasIt()
    {
        string first = _temp.CreateDirectory("first");
        _temp.CreateFile(Path.Combine("second", "tool.exe"));
        _temp.CreateFile(Path.Combine("third", "tool.exe"));

        PathCommandResolver resolver = Windows(first, _temp.Combine("second"), _temp.Combine("third"));

        Assert.Equal(_temp.Combine("second", "tool.exe"), resolver.Resolve("tool"));
    }

    [Fact]
    public void OnWindows_TheExtensionsAreTriedInOrder()
    {
        _temp.CreateFile(Path.Combine("bin", "code.cmd"));
        _temp.CreateFile(Path.Combine("bin", "code.exe"));

        Assert.Equal(_temp.Combine("bin", "code.exe"), Windows(_temp.Combine("bin")).Resolve("code"));
    }

    [Fact]
    public void OnWindows_ANameWithOneOfTheExtensions_IsTriedAsItIs()
    {
        _temp.CreateFile(Path.Combine("bin", "code.cmd"));
        _temp.CreateFile(Path.Combine("bin", "code.exe"));

        // The name ends in ".CMD", whatever its case, so it is used as typed, without the extensions added.
        Assert.Equal(_temp.Combine("bin", "code.cmd"), Windows(_temp.Combine("bin")).Resolve("  code.cmd  "));
    }

    [Fact]
    public void OnWindows_ANameWithAnotherExtension_HasTheExtensionsAdded()
    {
        _temp.CreateFile(Path.Combine("bin", "python3.12.exe"));

        Assert.Equal(_temp.Combine("bin", "python3.12.exe"), Windows(_temp.Combine("bin")).Resolve("python3.12"));
    }

    [Fact]
    public void Elsewhere_TheNameIsTriedAsItIs()
    {
        // Both may be executed: "code" is then not found because its name is not completed, not because it cannot run.
        AllowExecution(_temp.CreateFile(Path.Combine("bin", "firefox")), _temp.CreateFile(Path.Combine("bin", "code.exe")));

        PathCommandResolver resolver = new(() => [_temp.Combine("bin")], () => WindowsExtensions, isWindows: false);

        Assert.Equal(_temp.Combine("bin", "firefox"), resolver.Resolve("firefox"));
        Assert.Null(resolver.Resolve("code"));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void Elsewhere_AFileThatMayNotBeExecuted_IsNoProgram()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Only Unix files carry execute permissions.");

        string plain = _temp.CreateFile(Path.Combine("bin", "notes"));
        string program = _temp.CreateFile(Path.Combine("bin", "tool"));
        File.SetUnixFileMode(plain, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.SetUnixFileMode(program, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        PathCommandResolver resolver = new(() => [_temp.Combine("bin")], () => [], isWindows: false);

        Assert.Null(resolver.Resolve("notes"));
        Assert.Equal(program, resolver.Resolve("tool"));
    }

    [Fact]
    public void AFolderNamedLikeTheCommand_IsNoProgram()
    {
        _temp.CreateDirectory(Path.Combine("bin", "tool.exe"));

        Assert.Null(Windows(_temp.Combine("bin")).Resolve("tool"));
    }

    [Fact]
    public void EmptyAndRelativeEntries_AreNeverSearched()
    {
        string current = Directory.GetCurrentDirectory();
        string name = "launchpad-test-" + Guid.NewGuid().ToString("N");
        string planted = Path.Combine(current, name + ".exe");
        File.WriteAllText(planted, string.Empty);

        try
        {
            PathCommandResolver resolver = Windows(string.Empty, ".", "relative", "  ");

            Assert.Null(resolver.Resolve(name));
        }
        finally
        {
            File.Delete(planted);
        }
    }

    [Fact]
    public void QuotedEntries_AreRead()
    {
        _temp.CreateFile(Path.Combine("My Tools", "tool.exe"));

        Assert.Equal(_temp.Combine("My Tools", "tool.exe"), Windows($"\"{_temp.Combine("My Tools")}\"").Resolve("tool"));
    }

    [Fact]
    public void ACommandNowhereOnThePath_IsNotFound()
    {
        _temp.CreateFile(Path.Combine("bin", "tool.exe"));

        Assert.Null(Windows(_temp.Combine("bin")).Resolve("other"));
    }

    [Fact]
    public void ThePathIsReadOnEveryLookup()
    {
        List<string> directories = [];
        PathCommandResolver resolver = new(() => directories, () => WindowsExtensions, isWindows: true);
        _temp.CreateFile(Path.Combine("bin", "tool.exe"));

        Assert.Null(resolver.Resolve("tool"));

        directories.Add(_temp.Combine("bin"));

        Assert.NotNull(resolver.Resolve("tool"));
    }

    [Theory]
    [InlineData("notepad", true)]
    [InlineData("  code.cmd  ", true)]
    [InlineData("my tool", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("bin/tool", false)]
    public void IsCommandName_AcceptsANameAndNoPath(string? text, bool expected)
        => Assert.Equal(expected, PathCommandResolver.IsCommandName(text));

    [Fact]
    public void IsCommandName_RefusesAFullPath()
        => Assert.False(PathCommandResolver.IsCommandName(_temp.Combine("tool.exe")));

    [Fact]
    public void ATextThatIsNoCommandName_IsNeverLookedFor()
    {
        _temp.CreateFile(Path.Combine("bin", "tool.exe"));

        Assert.Null(Windows(_temp.Path).Resolve(Path.Combine("bin", "tool")));
    }

    private static PathCommandResolver Windows(params string[] directories)
        => new(() => directories, () => WindowsExtensions, isWindows: true);

    // Outside Windows a file is a program only once its owner may execute it; Windows has no such permission.
    private static void AllowExecution(params string[] paths)
    {
        if (!OperatingSystem.IsWindows())
        {
            foreach (string path in paths)
            {
                File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute);
            }
        }
    }
}
