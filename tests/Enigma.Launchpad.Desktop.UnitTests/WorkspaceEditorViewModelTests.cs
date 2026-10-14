using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.ViewModels;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Xunit;

namespace Enigma.Launchpad.Desktop.UnitTests;

public sealed class WorkspaceEditorViewModelTests
{
    private static readonly string[] Others = ["Reading", "Games"];

    [Fact]
    public void ANewForm_StartsBlank_WithTheDefaultGlyph_AndNoErrorShown()
    {
        WorkspaceEditorViewModel form = Form(null);

        Assert.True(form.IsNew);
        Assert.Equal((string.Empty, string.Empty), (form.Title, form.Description));
        Assert.Equal(Glyphs.NewWorkspace, form.SelectedGlyph.Glyph);
        Assert.False(form.IsValid);
        Assert.Equal("Enter a title.", form.TitleError);
        Assert.Null(form.VisibleTitleError);
        Assert.Equal(Glyphs.WorkspaceChoices, form.Choices.Select(choice => choice.Glyph));
    }

    [Fact]
    public void AnEditForm_StartsFromTheWorkspace_WithoutErrors()
    {
        WorkspaceEditorViewModel form = Form(new Workspace(Guid.NewGuid(), "Dev", [], "Coding", "Code"));

        Assert.False(form.IsNew);
        Assert.Equal(("Dev", "Coding", PhosphorIcon.Code), (form.Title, form.Description, form.SelectedGlyph.Glyph));
        Assert.True(form.IsValid);
        Assert.Null(form.VisibleTitleError);
    }

    [Fact]
    public void AWorkspaceWithoutAGlyph_StartsOnTheDefaultOne()
        => Assert.Equal(Glyphs.DefaultWorkspace, Form(Workspace.Create("Dev")).SelectedGlyph.Glyph);

    [Fact]
    public void AGlyphTheFormDoesNotOffer_IsKept_FirstInTheList()
    {
        WorkspaceEditorViewModel form = Form(new Workspace(Guid.NewGuid(), "Dev", [], Icon: "Alien"));

        Assert.Equal(PhosphorIcon.Alien, form.SelectedGlyph.Glyph);
        Assert.Equal(PhosphorIcon.Alien, form.Choices[0].Glyph);
        Assert.Equal(Glyphs.WorkspaceChoices.Count + 1, form.Choices.Count);
        Assert.Equal("Alien", form.ToDetails().Icon);
    }

    [Theory]
    [InlineData("  ", "Enter a title.")]
    [InlineData("games", "A workspace with this title already exists.")]
    public void Typing_ShowsWhyATitleIsRefused(string title, string expected)
    {
        WorkspaceEditorViewModel form = Form(null);

        form.Title = title;

        Assert.Equal(expected, form.VisibleTitleError);
        Assert.False(form.IsValid);
    }

    [Fact]
    public void ALongDescription_IsRefused_OnceTyped()
    {
        WorkspaceEditorViewModel form = Form(null);
        form.Title = "Lab";

        form.Description = new string('x', ShortcutValidator.MaxDescriptionLength + 1);

        Assert.NotNull(form.VisibleDescriptionError);
        Assert.False(form.IsValid);
    }

    [Fact]
    public void TheValidity_IsAnnounced_AsItChanges()
    {
        WorkspaceEditorViewModel form = Form(null);
        List<string?> changes = [];
        form.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        form.Title = "Lab";

        Assert.Contains(nameof(WorkspaceEditorViewModel.IsValid), changes);
        Assert.True(form.IsValid);
    }

    [Fact]
    public void ToDetails_TrimsTheText_AndNamesTheGlyph()
    {
        WorkspaceEditorViewModel form = Form(null);
        form.Title = "  Lab  ";
        form.Description = "   ";
        form.SelectedGlyph = form.Choices.Single(choice => choice.Glyph == PhosphorIcon.Flask);

        Assert.Equal(new WorkspaceDetails("Lab", null, "Flask"), form.ToDetails());

        form.Description = "  Experiments  ";

        Assert.Equal("Experiments", form.ToDetails().Description);
    }

    [Fact]
    public void ToDetails_OfAnInvalidForm_Throws()
        => Assert.Throws<InvalidOperationException>(() => Form(null).ToDetails());

    [Fact]
    public void ClearingTheSelection_KeepsTheGlyph()
    {
        WorkspaceEditorViewModel form = Form(null);
        GlyphOption before = form.SelectedGlyph;

        form.SelectedGlyph = null!;

        Assert.Same(before, form.SelectedGlyph);
    }

    [Theory]
    [InlineData(PhosphorIcon.GameController, "Game controller")]
    [InlineData(PhosphorIcon.Code, "Code")]
    [InlineData(PhosphorIcon.TerminalWindow, "Terminal window")]
    public void AGlyphsName_IsWordedForPeople(PhosphorIcon glyph, string expected)
        => Assert.Equal(expected, new GlyphOption(glyph).Name);

    [Theory]
    [InlineData("Code", PhosphorIcon.Code)]
    [InlineData(null, Glyphs.DefaultWorkspace)]
    [InlineData("", Glyphs.DefaultWorkspace)]
    [InlineData("code", Glyphs.DefaultWorkspace)]
    [InlineData("NoSuchGlyph", Glyphs.DefaultWorkspace)]
    [InlineData("5", Glyphs.DefaultWorkspace)]
    public void AWorkspacesGlyph_IsTheOneItNames_OrTheDefault(string? name, PhosphorIcon expected)
        => Assert.Equal(expected, Glyphs.OfWorkspace(name));

    [Fact]
    public void EveryOfferedGlyph_IsOfferedOnce()
        => Assert.Equal(Glyphs.WorkspaceChoices.Count, Glyphs.WorkspaceChoices.Distinct().Count());

    private static WorkspaceEditorViewModel Form(Workspace? existing) => new(existing, Others, new ShortcutValidator());
}
