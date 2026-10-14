using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// What the workspace form gives back: the workspace's title, description and glyph.
/// </summary>
/// <param name="Title">The title, trimmed.</param>
/// <param name="Description">The description, trimmed; <see langword="null"/> when left empty.</param>
/// <param name="Icon">The name of the glyph.</param>
public sealed record WorkspaceDetails(string Title, string? Description, string Icon);

/// <summary>
/// One glyph the workspace form offers.
/// </summary>
/// <param name="Glyph">The glyph.</param>
public sealed record GlyphOption(PhosphorIcon Glyph)
{
    /// <summary>
    /// Gets the glyph's name in words, for its tooltip and screen readers.
    /// </summary>
    public string Name => Glyphs.Describe(Glyph);
}

/// <summary>
/// The form creating or editing a workspace — its title, description and glyph — checked as it is typed.
/// </summary>
/// <remarks>
/// An error appears under a field only once that field has been edited, so a new, empty form does not open with
/// complaints; the confirm button still waits for a valid form.
/// </remarks>
public sealed class WorkspaceEditorViewModel : ObservableObject
{
    private readonly IShortcutValidator _validator;
    private readonly string[] _otherTitles;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="existing">The workspace being edited; <see langword="null"/> for a new one.</param>
    /// <param name="otherTitles">The titles of the profile's other workspaces, which the title may not repeat.</param>
    /// <param name="validator">The rules the title and the description must meet.</param>
    public WorkspaceEditorViewModel(Workspace? existing, IEnumerable<string> otherTitles, IShortcutValidator validator)
    {
        ArgumentNullException.ThrowIfNull(otherTitles);
        ArgumentNullException.ThrowIfNull(validator);

        _validator = validator;
        _otherTitles = [.. otherTitles];
        IsNew = existing is null;

        PhosphorIcon current = existing is null ? Glyphs.NewWorkspace : Glyphs.OfWorkspace(existing.Icon);

        // A glyph from elsewhere — a file edited by hand, a later version — stays offered, first, so editing the title
        // never changes the icon behind the user's back.
        Choices = Glyphs.WorkspaceChoices.Contains(current)
            ? Glyphs.WorkspaceChoices.Select(glyph => new GlyphOption(glyph)).ToArray()
            : [new GlyphOption(current), .. Glyphs.WorkspaceChoices.Select(glyph => new GlyphOption(glyph))];

        SelectedGlyph = Choices.First(option => option.Glyph == current);
        Title = existing?.Title ?? string.Empty;
        Description = existing?.Description ?? string.Empty;

        // Starting values are not edits: an edited workspace opens without errors under its fields.
        HasEditedTitle = false;
        HasEditedDescription = false;
        Revalidate();
    }

    /// <summary>
    /// Gets a value indicating whether the form creates a workspace rather than editing one.
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    /// Gets or sets the title as typed.
    /// </summary>
    public string Title
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                HasEditedTitle = true;
                Revalidate();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the description as typed; it may stay empty.
    /// </summary>
    public string Description
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                HasEditedDescription = true;
                Revalidate();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets the glyphs to choose from.
    /// </summary>
    public IReadOnlyList<GlyphOption> Choices { get; }

    /// <summary>
    /// Gets or sets the chosen glyph. The list never leaves it empty: clearing the selection keeps the last one.
    /// </summary>
    public GlyphOption SelectedGlyph
    {
        get;
        set
        {
            if (value is not null)
            {
                SetProperty(ref field, value);
            }
            else
            {
                // The list cleared its selection; it is told the glyph again so it shows one.
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets why the title is refused, or <see langword="null"/>.
    /// </summary>
    public string? TitleError
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleTitleError));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    /// Gets why the description is refused, or <see langword="null"/>.
    /// </summary>
    public string? DescriptionError
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleDescriptionError));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    /// Gets the title error to show, once the title has been edited.
    /// </summary>
    public string? VisibleTitleError => HasEditedTitle ? TitleError : null;

    /// <summary>
    /// Gets the description error to show, once the description has been edited.
    /// </summary>
    public string? VisibleDescriptionError => HasEditedDescription ? DescriptionError : null;

    /// <summary>
    /// Gets a value indicating whether the form can be confirmed.
    /// </summary>
    public bool IsValid => (TitleError is null) && (DescriptionError is null);

    private bool HasEditedTitle
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleTitleError));
            }
        }
    }

    private bool HasEditedDescription
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleDescriptionError));
            }
        }
    }

    /// <summary>
    /// Gives back what the form describes.
    /// </summary>
    /// <returns>The title and description, trimmed, and the glyph's name.</returns>
    /// <exception cref="InvalidOperationException">The form is not valid.</exception>
    public WorkspaceDetails ToDetails()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("The form is not valid.");
        }

        return new WorkspaceDetails(
            Title.Trim(),
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            SelectedGlyph.Glyph.ToString());
    }

    private void Revalidate()
    {
        TitleError = _validator.ValidateWorkspaceTitle(Title, _otherTitles);
        DescriptionError = _validator.ValidateWorkspaceDescription(Description);
    }
}
