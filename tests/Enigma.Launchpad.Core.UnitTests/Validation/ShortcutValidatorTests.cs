using System.IO;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.UnitTests.Infrastructure;
using Enigma.Launchpad.Core.Validation;
using Xunit;

namespace Enigma.Launchpad.Core.UnitTests.Validation;

public sealed class ShortcutValidatorTests
{
    private readonly ShortcutValidator _validator = new(acceptsApplicationBundles: false);

    [Theory]
    [InlineData("Tools")]
    [InlineData("  Tools  ")]
    [InlineData("x")]
    public void ValidateShortcutName_AcceptsANonEmptyName(string name)
        => Assert.Null(_validator.ValidateShortcutName(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateShortcutName_RefusesAnEmptyName(string? name)
        => Assert.Equal("Enter a name.", _validator.ValidateShortcutName(name));

    [Fact]
    public void ValidateShortcutName_RefusesALongName()
    {
        Assert.Null(_validator.ValidateShortcutName(new string('a', ShortcutValidator.MaxNameLength)));
        Assert.NotNull(_validator.ValidateShortcutName(new string('a', ShortcutValidator.MaxNameLength + 1)));
    }

    [Fact]
    public void ValidateGroupName_RefusesADuplicateIgnoringCaseAndSpaces()
        => Assert.Equal(
            "A group with this name already exists.",
            _validator.ValidateGroupName(" dev TOOLS ", ["Games", "Dev tools"]));

    [Fact]
    public void ValidateGroupName_AcceptsAUniqueName()
        => Assert.Null(_validator.ValidateGroupName("Games", ["Dev tools"]));

    [Fact]
    public void ValidateGroupName_ReportsAnEmptyNameBeforeUniqueness()
        => Assert.Equal("Enter a name.", _validator.ValidateGroupName(" ", [string.Empty]));

    [Fact]
    public void ValidateProfileName_RefusesADuplicateIgnoringCaseAndSpaces()
        => Assert.Equal(
            "A profile with this name already exists.",
            _validator.ValidateProfileName(" WORK ", ["Home", "Work"]));

    [Fact]
    public void ValidateProfileName_AcceptsAUniqueName()
        => Assert.Null(_validator.ValidateProfileName("Home", ["Work"]));

    [Theory]
    [InlineData(null, "Enter a name.")]
    [InlineData("   ", "Enter a name.")]
    public void ValidateProfileName_RequiresAName(string? name, string expected)
        => Assert.Equal(expected, _validator.ValidateProfileName(name, []));

    [Fact]
    public void ValidateProfileName_RefusesALongName()
        => Assert.Equal(
            "Keep the name to 100 characters or fewer.",
            _validator.ValidateProfileName(new string('p', ShortcutValidator.MaxNameLength + 1), []));

    [Fact]
    public void ValidateTarget_AcceptsAnExistingFileAsAnApplication()
    {
        using TempDirectory temp = new();
        string file = temp.CreateFile("tool.exe");

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Application, file);

        Assert.True(result.IsValid);
        Assert.Equal(file, result.NormalizedTarget);
    }

    [Fact]
    public void ValidateTarget_StripsQuotesAndResolvesDots()
    {
        using TempDirectory temp = new();
        string file = temp.CreateFile("tool.exe");
        string typed = $"  \"{Path.Combine(temp.Path, "sub", "..", "tool.exe")}\"  ";

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Application, typed);

        Assert.Equal(file, result.NormalizedTarget);
    }

    [Fact]
    public void ValidateTarget_RefusesAMissingApplication()
    {
        using TempDirectory temp = new();

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Application, temp.Combine("missing.exe"));

        Assert.False(result.IsValid);
        Assert.Equal("There is no file at this path.", result.Error);
    }

    [Fact]
    public void ValidateTarget_RefusesAFolderAsAnApplication()
    {
        using TempDirectory temp = new();

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Application, temp.Path);

        Assert.Contains("Directories", result.Error);
    }

    [Fact]
    public void ValidateTarget_AcceptsAnApplicationBundleWhereTheyExist()
    {
        using TempDirectory temp = new();
        string bundle = temp.CreateDirectory("Editor.app");

        Assert.True(new ShortcutValidator(acceptsApplicationBundles: true).ValidateTarget(ShortcutKind.Application, bundle).IsValid);
        Assert.False(_validator.ValidateTarget(ShortcutKind.Application, bundle).IsValid);
    }

    [Fact]
    public void ValidateTarget_AcceptsAnExistingDirectory()
    {
        using TempDirectory temp = new();

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Directory, temp.Path);

        Assert.True(result.IsValid);
        Assert.Equal(temp.Path, result.NormalizedTarget);
    }

    [Fact]
    public void ValidateTarget_RefusesAFileAsADirectory()
    {
        using TempDirectory temp = new();
        string file = temp.CreateFile("notes.txt");

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Directory, file);

        Assert.Contains("Applications", result.Error);
    }

    [Fact]
    public void ValidateTarget_RefusesAMissingDirectory()
    {
        using TempDirectory temp = new();

        Assert.Equal(
            "There is no folder at this path.",
            _validator.ValidateTarget(ShortcutKind.Directory, temp.Combine("missing")).Error);
    }

    [Theory]
    [InlineData(ShortcutKind.Application)]
    [InlineData(ShortcutKind.Directory)]
    public void ValidateTarget_RefusesARelativePath(ShortcutKind kind)
        => Assert.Equal(
            "Enter a full path, starting from the drive or the root.",
            _validator.ValidateTarget(kind, Path.Combine("relative", "path")).Error);

    [Theory]
    [InlineData(ShortcutKind.Application)]
    [InlineData(ShortcutKind.Directory)]
    public void ValidateTarget_RefusesAnEmptyPath(ShortcutKind kind)
        => Assert.Equal("Enter a path.", _validator.ValidateTarget(kind, "  ").Error);

    [Fact]
    public void ValidateTarget_NormalisesAUrl()
    {
        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Url, " example.com ");

        Assert.Equal("https://example.com", result.NormalizedTarget);
    }

    [Fact]
    public void ValidateTarget_RefusesAnUnsupportedUrl()
        => Assert.Equal(
            UrlNormalizer.UnsupportedSchemeMessage,
            _validator.ValidateTarget(ShortcutKind.Url, "ftp://example.com").Error);
}
