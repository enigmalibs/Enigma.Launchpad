using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// The form adding or editing one shortcut, checked as it is typed.
/// </summary>
/// <remarks>
/// The fields depend on the kind: an application has arguments, an application or a folder can be picked
/// with Browse…, a link has neither. An error appears under a field only once that field has been edited,
/// so a new, empty form does not open with complaints; the confirm button still waits for a valid form.
/// </remarks>
public sealed class ShortcutEditorViewModel : ObservableObject
{
    private readonly IShortcutValidator _validator;
    private readonly IPathPicker _picker;
    private readonly Guid _id;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="kind">The kind of shortcut the form edits.</param>
    /// <param name="existing">The shortcut being edited; <see langword="null"/> for a new one.</param>
    /// <param name="validator">The rules the name and the target must meet.</param>
    /// <param name="picker">Lets the user pick the target.</param>
    public ShortcutEditorViewModel(ShortcutKind kind, Shortcut? existing, IShortcutValidator validator, IPathPicker picker)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(picker);

        Kind = kind;
        _validator = validator;
        _picker = picker;
        _id = existing?.Id ?? Guid.NewGuid();
        IsNew = existing is null;

        Name = existing?.Name ?? string.Empty;
        Target = existing?.Target ?? string.Empty;
        Arguments = existing?.Arguments ?? string.Empty;
        Description = existing?.Description ?? string.Empty;

        // Starting values are not edits: an edited shortcut opens without errors under its fields.
        HasEditedName = false;
        HasEditedTarget = false;
        HasEditedDescription = false;
        Revalidate();

        BrowseCommand = new AsyncRelayCommand(BrowseAsync, () => CanBrowse);
    }

    /// <summary>
    /// Gets the kind of shortcut the form edits.
    /// </summary>
    public ShortcutKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the form creates a shortcut rather than editing one.
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    /// Gets the target field's label.
    /// </summary>
    public string TargetLabel => Kind switch
    {
        ShortcutKind.Application => "Program",
        ShortcutKind.Directory => "Folder",
        _ => "Address",
    };

    /// <summary>
    /// Gets the example shown in the empty target field.
    /// </summary>
    public string TargetPlaceholder => Kind switch
    {
        ShortcutKind.Application => OperatingSystem.IsWindows() ? @"C:\Program Files\App\app.exe" : "/usr/bin/app",
        ShortcutKind.Directory => OperatingSystem.IsWindows() ? @"C:\Users\you\Projects" : "/home/you/Projects",
        _ => "https://example.com",
    };

    /// <summary>
    /// Gets a value indicating whether the target can be picked with Browse….
    /// </summary>
    public bool CanBrowse => Kind is ShortcutKind.Application or ShortcutKind.Directory;

    /// <summary>
    /// Gets a value indicating whether the form has an arguments field.
    /// </summary>
    public bool HasArguments => Kind == ShortcutKind.Application;

    /// <summary>
    /// Gets or sets the name as typed.
    /// </summary>
    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                HasEditedName = true;
                Revalidate();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the target as typed.
    /// </summary>
    public string Target
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                HasEditedTarget = true;
                Revalidate();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments as typed; applications only.
    /// </summary>
    public string Arguments { get; set => SetProperty(ref field, value); } = string.Empty;

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
    /// Gets why the name is refused, or <see langword="null"/>.
    /// </summary>
    public string? NameError
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleNameError));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    /// Gets why the target is refused, or <see langword="null"/>.
    /// </summary>
    public string? TargetError
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleTargetError));
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
    /// Gets the name error to show, once the name has been edited.
    /// </summary>
    public string? VisibleNameError => HasEditedName ? NameError : null;

    /// <summary>
    /// Gets the target error to show, once the target has been edited.
    /// </summary>
    public string? VisibleTargetError => HasEditedTarget ? TargetError : null;

    /// <summary>
    /// Gets the description error to show, once the description has been edited.
    /// </summary>
    public string? VisibleDescriptionError => HasEditedDescription ? DescriptionError : null;

    /// <summary>
    /// Gets a value indicating whether the form can be confirmed.
    /// </summary>
    public bool IsValid => (NameError is null) && (TargetError is null) && (DescriptionError is null);

    /// <summary>
    /// Gets the command picking the target with the platform's picker.
    /// </summary>
    public AsyncRelayCommand BrowseCommand { get; }

    private bool HasEditedName
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleNameError));
            }
        }
    }

    private bool HasEditedTarget
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleTargetError));
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
    /// Builds the shortcut the form describes.
    /// </summary>
    /// <returns>The shortcut, keeping the identity of the one edited.</returns>
    /// <exception cref="InvalidOperationException">The form is not valid.</exception>
    public Shortcut ToShortcut()
    {
        TargetValidation target = _validator.ValidateTarget(Kind, Target);

        if (!IsValid || (target.NormalizedTarget is not { } normalized))
        {
            throw new InvalidOperationException("The form is not valid.");
        }

        string? arguments = HasArguments && !string.IsNullOrWhiteSpace(Arguments) ? Arguments.Trim() : null;
        string? description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        return new Shortcut(_id, Name.Trim(), normalized, arguments, description);
    }

    private void Revalidate()
    {
        NameError = _validator.ValidateShortcutName(Name);
        TargetError = _validator.ValidateTarget(Kind, Target).Error;
        DescriptionError = _validator.ValidateDescription(Description);
    }

    private async Task BrowseAsync()
    {
        string? picked = Kind == ShortcutKind.Application
            ? await _picker.PickFileAsync("Choose a program", Target)
            : await _picker.PickFolderAsync("Choose a folder", Target);

        if (picked is null)
        {
            return;
        }

        Target = picked;

        // A picked target names itself when the user has not named it yet.
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Kind == ShortcutKind.Application
                ? Path.GetFileNameWithoutExtension(picked)
                : Path.GetFileName(Path.TrimEndingDirectorySeparator(picked)) is { Length: > 0 } folder ? folder : picked;
        }
    }
}
