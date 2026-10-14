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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("The editor")]
    public void ValidateDescription_AcceptsAnEmptyOrShortDescription(string? description)
        => Assert.Null(_validator.ValidateDescription(description));

    [Fact]
    public void ValidateDescription_RefusesALongDescription_IgnoringSurroundingSpaces()
    {
        Assert.Null(_validator.ValidateDescription($"  {new string('a', ShortcutValidator.MaxDescriptionLength)}  "));
        Assert.Equal(
            "Keep the description to 200 characters or fewer.",
            _validator.ValidateDescription(new string('a', ShortcutValidator.MaxDescriptionLength + 1)));
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

        Assert.Equal("This is a folder, not a program. Add it to a folders group instead.", result.Error);
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

        Assert.Equal("This is a file, not a folder. Add it to an applications or a documents group instead.", result.Error);
    }

    [Fact]
    public void ValidateTarget_RefusesAMissingDirectory()
    {
        using TempDirectory temp = new();

        Assert.Equal(
            "There is no folder at this path.",
            _validator.ValidateTarget(ShortcutKind.Directory, temp.Combine("missing")).Error);
    }

    [Fact]
    public void ValidateTarget_RefusesARelativeDirectory()
        => Assert.Equal(
            "Enter a full path, starting from the drive or the root.",
            _validator.ValidateTarget(ShortcutKind.Directory, Path.Combine("relative", "path")).Error);

    [Fact]
    public void ValidateTarget_RefusesARelativeApplicationPath_ThatIsNoCommand()
        => Assert.Equal(
            "Enter a full path, or a command found on the PATH.",
            _validator.ValidateTarget(ShortcutKind.Application, Path.Combine("relative", "tool.exe")).Error);

    [Fact]
    public void ValidateTarget_RefusesAnEmptyDirectory()
        => Assert.Equal("Enter a path.", _validator.ValidateTarget(ShortcutKind.Directory, "  ").Error);

    [Fact]
    public void ValidateTarget_RefusesAnEmptyApplication()
        => Assert.Equal("Enter a path or a command.", _validator.ValidateTarget(ShortcutKind.Application, "  ").Error);

    [Fact]
    public void ValidateTarget_AcceptsACommandOnThePath_AndKeepsItAsTyped()
    {
        FakeCommandResolver commands = new();
        commands.Programs["code"] = Path.Combine(Path.GetTempPath(), "code.cmd");

        TargetValidation result = new ShortcutValidator(acceptsApplicationBundles: false, commands)
            .ValidateTarget(ShortcutKind.Application, "  \"code\"  ");

        Assert.True(result.IsValid);
        Assert.Equal("code", result.NormalizedTarget);
        Assert.Equal(["code"], commands.Lookups);
    }

    [Fact]
    public void ValidateTarget_RefusesACommandNotOnThePath()
        => Assert.Equal(
            "No program called “gone” was found on the PATH.",
            new ShortcutValidator(acceptsApplicationBundles: false, new FakeCommandResolver())
                .ValidateTarget(ShortcutKind.Application, "gone").Error);

    [Fact]
    public void ValidateTarget_RefusingACommandWithASpace_PointsAtTheArguments()
        => Assert.Equal(
            "No program called “code --wait” was found on the PATH. Arguments go in Default arguments.",
            new ShortcutValidator(acceptsApplicationBundles: false, new FakeCommandResolver())
                .ValidateTarget(ShortcutKind.Application, "code --wait").Error);

    [Fact]
    public void ValidateTarget_NeverLooksADirectoryUpOnThePath()
    {
        FakeCommandResolver commands = new();
        commands.Programs["Projects"] = Path.GetTempPath();

        Assert.False(new ShortcutValidator(acceptsApplicationBundles: false, commands).ValidateTarget(ShortcutKind.Directory, "Projects").IsValid);
        Assert.Empty(commands.Lookups);
    }

    [Theory]
    [InlineData("Dev")]
    [InlineData("  Dev  ")]
    public void ValidateWorkspaceTitle_AcceptsANewTitle(string title)
        => Assert.Null(_validator.ValidateWorkspaceTitle(title, ["Reading", "Games"]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateWorkspaceTitle_RefusesAnEmptyTitle(string? title)
        => Assert.Equal("Enter a title.", _validator.ValidateWorkspaceTitle(title, []));

    [Fact]
    public void ValidateWorkspaceTitle_RefusesALongTitle()
    {
        Assert.Null(_validator.ValidateWorkspaceTitle(new string('a', ShortcutValidator.MaxNameLength), []));
        Assert.Equal(
            "Keep the title to 100 characters or fewer.",
            _validator.ValidateWorkspaceTitle(new string('a', ShortcutValidator.MaxNameLength + 1), []));
    }

    [Fact]
    public void ValidateWorkspaceTitle_RefusesATitleAnotherWorkspaceHas_IgnoringCase()
        => Assert.Equal("A workspace with this title already exists.", _validator.ValidateWorkspaceTitle(" dev ", ["Reading", "Dev"]));

    [Fact]
    public void ValidateWorkspaceDescription_AcceptsNoneOrAShortOne_AndRefusesALongOne()
    {
        Assert.Null(_validator.ValidateWorkspaceDescription(null));
        Assert.Null(_validator.ValidateWorkspaceDescription(new string('x', ShortcutValidator.MaxDescriptionLength)));
        Assert.NotNull(_validator.ValidateWorkspaceDescription(new string('x', ShortcutValidator.MaxDescriptionLength + 1)));
    }

    [Fact]
    public void ValidateTarget_AcceptsAnExistingFileAsADocument()
    {
        using TempDirectory temp = new();
        string report = temp.CreateFile("report.pdf");

        TargetValidation result = _validator.ValidateTarget(ShortcutKind.Document, $"  \"{report}\"  ");

        Assert.True(result.IsValid);
        Assert.Equal(report, result.NormalizedTarget);
    }

    [Fact]
    public void ValidateTarget_RefusesAFolder_AMissingFile_OrARelativePath_AsADocument()
    {
        using TempDirectory temp = new();

        Assert.Equal("This is a folder, not a file. Add it to a folders group instead.", _validator.ValidateTarget(ShortcutKind.Document, temp.Path).Error);
        Assert.Equal("There is no file at this path.", _validator.ValidateTarget(ShortcutKind.Document, temp.Combine("missing.pdf")).Error);
        Assert.Equal("Enter a full path, starting from the drive or the root.", _validator.ValidateTarget(ShortcutKind.Document, "report.pdf").Error);
        Assert.Equal("Enter a path.", _validator.ValidateTarget(ShortcutKind.Document, " ").Error);
    }

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
