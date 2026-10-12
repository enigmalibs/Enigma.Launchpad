using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.UnitTests.Infrastructure;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class ShortcutEditorViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakePathPicker _picker = new();
    private readonly ShortcutValidator _validator = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ANewForm_IsInvalid_ButShowsNoErrorsYet()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Application);

        Assert.True(form.IsNew);
        Assert.False(form.IsValid);
        Assert.NotNull(form.NameError);
        Assert.NotNull(form.TargetError);
        Assert.Null(form.VisibleNameError);
        Assert.Null(form.VisibleTargetError);
    }

    [Theory]
    [InlineData(ShortcutKind.Application, "Program", true, true)]
    [InlineData(ShortcutKind.Directory, "Folder", true, false)]
    [InlineData(ShortcutKind.Url, "Address", false, false)]
    public void TheFieldsFollowTheKind(ShortcutKind kind, string label, bool canBrowse, bool hasArguments)
    {
        ShortcutEditorViewModel form = Form(kind);

        Assert.Equal(label, form.TargetLabel);
        Assert.Equal(canBrowse, form.CanBrowse);
        Assert.Equal(canBrowse, form.BrowseCommand.CanExecute(null));
        Assert.Equal(hasArguments, form.HasArguments);
        Assert.False(string.IsNullOrWhiteSpace(form.TargetPlaceholder));
    }

    [Fact]
    public void Typing_ShowsTheErrorOfTheEditedFieldOnly()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory);

        form.Target = _temp.Combine("missing");

        Assert.Equal("There is no folder at this path.", form.VisibleTargetError);
        Assert.Null(form.VisibleNameError);

        form.Target = _temp.Path;
        form.Name = "Temp";

        Assert.True(form.IsValid);
        Assert.Null(form.VisibleTargetError);
    }

    [Fact]
    public void ToShortcut_TrimsAndNormalises_AndKeepsArgumentsForApplications()
    {
        string program = Path.Combine(_temp.Path, "tool.exe");
        File.WriteAllText(program, string.Empty);
        ShortcutEditorViewModel form = Form(ShortcutKind.Application);

        form.Name = "  Tool  ";
        form.Target = $"\"{program}\"";
        form.Arguments = "  --flag  ";
        Shortcut shortcut = form.ToShortcut();

        Assert.Equal("Tool", shortcut.Name);
        Assert.Equal(program, shortcut.Target);
        Assert.Equal("--flag", shortcut.Arguments);
        Assert.NotEqual(Guid.Empty, shortcut.Id);
    }

    [Fact]
    public void ToShortcut_DropsBlankArguments()
    {
        string program = Path.Combine(_temp.Path, "tool.exe");
        File.WriteAllText(program, string.Empty);
        ShortcutEditorViewModel form = Form(ShortcutKind.Application);
        form.Name = "Tool";
        form.Target = program;
        form.Arguments = "   ";

        Assert.Null(form.ToShortcut().Arguments);
    }

    [Fact]
    public void ToShortcut_NormalisesALink()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Url);
        form.Name = "Example";
        form.Target = " example.com ";

        Shortcut shortcut = form.ToShortcut();

        Assert.Equal("https://example.com", shortcut.Target);
        Assert.Null(shortcut.Arguments);
    }

    [Fact]
    public void ToShortcut_OnAnInvalidForm_Throws()
        => Assert.Throws<InvalidOperationException>(() => Form(ShortcutKind.Url).ToShortcut());

    [Fact]
    public void AnEditedShortcut_StartsValid_AndKeepsItsIdentity()
    {
        Shortcut existing = new(Guid.NewGuid(), "Temp", _temp.Path);
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, existing);

        Assert.False(form.IsNew);
        Assert.True(form.IsValid);

        form.Name = "Scratch";

        Assert.Equal(existing with { Name = "Scratch" }, form.ToShortcut());
    }

    [Fact]
    public async Task Browse_FillsTheTarget_AndTheNameWhenItIsEmpty()
    {
        string program = Path.Combine(_temp.Path, "Editor.exe");
        File.WriteAllText(program, string.Empty);
        _picker.NextFile = program;
        ShortcutEditorViewModel form = Form(ShortcutKind.Application);

        await form.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(program, form.Target);
        Assert.Equal("Editor", form.Name);
        Assert.True(form.IsValid);
        Assert.Equal(("File", (string?)string.Empty), Assert.Single(_picker.Requests));
    }

    [Fact]
    public async Task Browse_KeepsANameAlreadyTyped_AndStartsFromTheCurrentTarget()
    {
        _picker.NextFolder = _temp.Path;
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory);
        form.Name = "Scratch";
        form.Target = @"C:\Somewhere";

        await form.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("Scratch", form.Name);
        Assert.Equal(_temp.Path, form.Target);
        Assert.Equal(("Folder", (string?)@"C:\Somewhere"), Assert.Single(_picker.Requests));
    }

    [Fact]
    public async Task Browse_Cancelled_ChangesNothing()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory);
        form.Target = "typed";

        await form.BrowseCommand.ExecuteAsync(null);

        Assert.Equal("typed", form.Target);
        Assert.Equal(string.Empty, form.Name);
    }

    private ShortcutEditorViewModel Form(ShortcutKind kind, Shortcut? existing = null) => new(kind, existing, _validator, _picker);
}
