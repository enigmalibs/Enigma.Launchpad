using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
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
    private readonly FakeShortcutIcons _icons = new();

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
    [InlineData(ShortcutKind.Application, "Program or command", true, true)]
    [InlineData(ShortcutKind.Directory, "Folder", true, false)]
    [InlineData(ShortcutKind.Url, "Address", false, false)]
    [InlineData(ShortcutKind.Document, "File", true, false)]
    public void TheFieldsFollowTheKind(ShortcutKind kind, string label, bool canBrowse, bool hasArguments)
    {
        ShortcutEditorViewModel form = Form(kind);

        Assert.Equal(label, form.TargetLabel);
        Assert.Equal(canBrowse, form.CanBrowse);
        Assert.Equal(canBrowse, form.BrowseCommand.CanExecute(null));
        Assert.Equal(hasArguments, form.HasArguments);
        Assert.False(string.IsNullOrWhiteSpace(form.TargetPlaceholder));
    }

    [Theory]
    [InlineData(ShortcutKind.Application, true)]
    [InlineData(ShortcutKind.Directory, false)]
    [InlineData(ShortcutKind.Url, false)]
    public void OnlyAnApplication_SaysItsTargetMayBeACommand(ShortcutKind kind, bool hasHint)
    {
        ShortcutEditorViewModel form = Form(kind);

        Assert.Equal(hasHint, form.TargetHint is not null);

        if (hasHint)
        {
            Assert.Contains("command found on the PATH", form.TargetHint, StringComparison.Ordinal);
        }
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

    [Theory]
    [InlineData(ShortcutKind.Application)]
    [InlineData(ShortcutKind.Directory)]
    [InlineData(ShortcutKind.Url)]
    public void EveryKind_TakesADescription_StoredTrimmed(ShortcutKind kind)
    {
        string target = kind switch
        {
            ShortcutKind.Application => Program("tool.exe"),
            ShortcutKind.Directory => _temp.Path,
            _ => "https://example.com",
        };
        ShortcutEditorViewModel form = Form(kind, new Shortcut(Guid.NewGuid(), "Here", target));

        form.Description = "  What it is for  ";

        Assert.Equal("What it is for", form.ToShortcut().Description);
    }

    [Fact]
    public void ABlankDescription_IsStoredAsNone()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path, Description: "Old"));

        form.Description = "   ";

        Assert.Null(form.ToShortcut().Description);
    }

    [Fact]
    public void ALongDescription_IsRefused_WithItsErrorShown()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path));

        Assert.Null(form.VisibleDescriptionError);

        form.Description = new string('x', ShortcutValidator.MaxDescriptionLength + 1);

        Assert.False(form.IsValid);
        Assert.Equal("Keep the description to 200 characters or fewer.", form.VisibleDescriptionError);

        form.Description = "Short";

        Assert.True(form.IsValid);
        Assert.Null(form.VisibleDescriptionError);
    }

    [Fact]
    public void AnEditedShortcut_StartsWithItsDescription_AndKeepsIt()
    {
        Shortcut existing = new(Guid.NewGuid(), "Temp", _temp.Path, Description: "Scratch space");
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, existing);

        Assert.Equal("Scratch space", form.Description);
        Assert.Equal(existing, form.ToShortcut());
    }

    [Fact]
    public void AnEditedShortcut_KeepsItsIcon()
    {
        Shortcut existing = new(Guid.NewGuid(), "Temp", _temp.Path, Icon: new ShortcutIcon("iVBORw0KGgo=", IsCustom: true));
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, existing);

        form.Name = "Scratch";

        Assert.Equal(existing.Icon, form.ToShortcut().Icon);
    }

    [Fact]
    public void AnIconFoundFromTheOldTarget_IsDropped_WhenTheTargetChanges()
    {
        Shortcut existing = new(Guid.NewGuid(), "Temp", _temp.Path, Icon: new ShortcutIcon("iVBORw0KGgo="));
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, existing);

        form.Target = Directory.CreateDirectory(_temp.Combine("Other")).FullName;

        Assert.Null(form.ToShortcut().Icon);
    }

    [Fact]
    public void AChosenIcon_IsKept_WhenTheTargetChanges()
    {
        ShortcutIcon chosen = new("iVBORw0KGgo=", IsCustom: true);
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path, Icon: chosen));

        form.Target = Directory.CreateDirectory(_temp.Combine("Other")).FullName;

        Assert.Equal(chosen, form.ToShortcut().Icon);
    }

    [Fact]
    public void AnIconFoundFromTheTarget_IsKept_WhenTheTargetIsRetypedUnchanged()
    {
        ShortcutIcon found = new("iVBORw0KGgo=");
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path, Icon: found));

        form.Target = $"  {_temp.Path}  ";

        Assert.Equal(found, form.ToShortcut().Icon);
    }

    [Fact]
    public void ANewForm_ShowsTheKindsGlyph_AndWhereTheIconWillComeFrom()
    {
        ShortcutEditorViewModel application = Form(ShortcutKind.Application);
        ShortcutEditorViewModel folder = Form(ShortcutKind.Directory);

        Assert.Null(application.Icon);
        Assert.False(application.HasIconPreview);
        Assert.Equal(PhosphorIcon.AppWindow, application.KindGlyph);
        Assert.Equal("The program's own icon, when it can be found, unless you choose an image.", application.IconHint);
        Assert.False(application.CanResetIcon);
        Assert.Equal("Use automatic icon", application.ResetIconLabel);

        Assert.Equal(PhosphorIcon.Folder, folder.KindGlyph);
        Assert.Equal("The folder glyph, unless you choose an image.", folder.IconHint);
        Assert.Equal("Remove", folder.ResetIconLabel);
    }

    [Fact]
    public void AnEditedShortcut_ShowsTheIconItKeeps()
    {
        ShortcutIcon found = new("found");
        StubImage image = new();
        _icons.Images["found"] = image;

        ShortcutEditorViewModel form = Form(ShortcutKind.Application, new Shortcut(Guid.NewGuid(), "Tool", Program("tool.exe"), Icon: found));

        Assert.Same(image, form.IconPreview);
        Assert.True(form.HasIconPreview);
        Assert.Equal("The program's own icon, found automatically.", form.IconHint);
        Assert.False(form.CanResetIcon);
    }

    [Fact]
    public async Task ChoosingAnImage_MakesItTheIcon()
    {
        string image = _temp.Combine("logo.png");
        _picker.NextImageFile = image;
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path));

        await form.ChooseIconCommand.ExecuteAsync(null);

        Assert.Equal([image], _icons.ReadFiles);
        Assert.Equal(FakeShortcutIcons.ChosenIcon(image), form.Icon);
        Assert.True(form.HasIconPreview);
        Assert.Equal("Your image.", form.IconHint);
        Assert.True(form.CanResetIcon);
        Assert.True(form.ResetIconCommand.CanExecute(null));
        Assert.Equal(FakeShortcutIcons.ChosenIcon(image), form.ToShortcut().Icon);
    }

    [Fact]
    public async Task ChoosingNoImage_ChangesNothing()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory);

        await form.ChooseIconCommand.ExecuteAsync(null);

        Assert.Empty(_icons.ReadFiles);
        Assert.Null(form.Icon);
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task AnImageThatCannotBeUsed_ShowsWhy_AndKeepsTheIcon(Type failure)
    {
        ShortcutIcon kept = new("kept", IsCustom: true);
        _picker.NextImageFile = _temp.Combine("broken.png");
        _icons.ReadFailure = (Exception)Activator.CreateInstance(failure, "This file is not an image the launchpad can show.")!;
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path, Icon: kept));

        await form.ChooseIconCommand.ExecuteAsync(null);

        Assert.Equal("This file is not an image the launchpad can show.", form.IconError);
        Assert.Equal(kept, form.Icon);
        Assert.True(form.IsValid);
    }

    [Fact]
    public async Task GivingUpAChosenImage_LeavesNoIcon_AndClearsTheError()
    {
        _picker.NextImageFile = _temp.Combine("logo.png");
        ShortcutEditorViewModel form = Form(ShortcutKind.Application);
        await form.ChooseIconCommand.ExecuteAsync(null);
        _icons.ReadFailure = new InvalidDataException("Not an image.");
        await form.ChooseIconCommand.ExecuteAsync(null);

        form.ResetIconCommand.Execute(null);

        Assert.Null(form.Icon);
        Assert.False(form.HasIconPreview);
        Assert.Null(form.IconError);
        Assert.False(form.CanResetIcon);
        Assert.False(form.ResetIconCommand.CanExecute(null));
    }

    [Fact]
    public void AnIconFoundFromTheTarget_IsSetAside_WhileTheTargetDiffers_AndComesBack()
    {
        ShortcutIcon found = new("found");
        ShortcutEditorViewModel form = Form(ShortcutKind.Directory, new Shortcut(Guid.NewGuid(), "Temp", _temp.Path, Icon: found));

        form.Target = _temp.Combine("elsewhere");

        Assert.Null(form.Icon);
        Assert.False(form.HasIconPreview);

        form.Target = _temp.Path;

        Assert.Equal(found, form.Icon);
        Assert.True(form.HasIconPreview);
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
    public async Task Browse_ForADocument_PicksAFile_AndNamesItWithoutItsExtension()
    {
        string report = Program("Budget 2026.xlsx");
        _picker.NextFile = report;
        ShortcutEditorViewModel form = Form(ShortcutKind.Document);

        await form.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(report, form.Target);
        Assert.Equal("Budget 2026", form.Name);
        Assert.True(form.IsValid);
        Assert.Equal("File", Assert.Single(_picker.Requests).Kind);
    }

    [Fact]
    public void ADocumentsForm_SaysItsIconIsTheFiles_AndRefusesAFolder()
    {
        ShortcutEditorViewModel form = Form(ShortcutKind.Document);

        Assert.Equal("The file's own icon, when it can be found, unless you choose an image.", form.IconHint);
        Assert.Equal(Enigma.Icons.Phosphor.PhosphorIcon.FileText, form.KindGlyph);
        Assert.False(form.HasArguments);

        form.Target = _temp.Path;

        Assert.Equal("This is a folder, not a file. Add it to a folders group instead.", form.VisibleTargetError);
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

    private ShortcutEditorViewModel Form(ShortcutKind kind, Shortcut? existing = null) => new(kind, existing, _validator, _picker, _icons);

    private string Program(string name)
    {
        string program = _temp.Combine(name);
        File.WriteAllText(program, string.Empty);
        return program;
    }
}
