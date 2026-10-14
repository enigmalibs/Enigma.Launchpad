using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// The form adding or editing one shortcut, checked as it is typed.
/// </summary>
/// <remarks>
/// The fields depend on the kind: an application has arguments, an application, a folder or a document can be
/// picked with Browse…, a link has neither. An error appears under a field only once that field has been edited,
/// so a new, empty form does not open with complaints; the confirm button still waits for a valid form.
/// <para>
/// Every kind can have an image of the user's own as its icon. An application's, a link's or a document's icon can
/// also be found from its target; such an icon belongs to that target, so it is set aside while the target is
/// changed, and the tile finds the new target's icon once the form is saved.
/// </para>
/// </remarks>
public sealed class ShortcutEditorViewModel : ObservableObject
{
    private readonly IShortcutValidator _validator;
    private readonly IPathPicker _picker;
    private readonly IShortcutIcons _icons;
    private readonly Guid _id;
    private readonly string? _originalTarget;

    private ShortcutIcon? _icon;
    private ShortcutIcon? _shownIcon;
    private bool _isIconShown;
    private string? _normalizedTarget;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="kind">The kind of shortcut the form edits.</param>
    /// <param name="existing">The shortcut being edited; <see langword="null"/> for a new one.</param>
    /// <param name="validator">The rules the name and the target must meet.</param>
    /// <param name="picker">Lets the user pick the target and the icon.</param>
    /// <param name="icons">Shows icons, and reads the image files picked as icons.</param>
    public ShortcutEditorViewModel(ShortcutKind kind, Shortcut? existing, IShortcutValidator validator, IPathPicker picker, IShortcutIcons icons)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(icons);

        Kind = kind;
        _validator = validator;
        _picker = picker;
        _icons = icons;
        _id = existing?.Id ?? Guid.NewGuid();
        _icon = existing?.Icon;
        _originalTarget = existing?.Target;
        IsNew = existing is null;

        // Before the fields: setting the target refreshes the icon, which re-evaluates its commands.
        BrowseCommand = new AsyncRelayCommand(BrowseAsync, () => CanBrowse);
        ChooseIconCommand = new AsyncRelayCommand(ChooseIconAsync);
        ResetIconCommand = new RelayCommand(ResetIcon, () => CanResetIcon);

        Name = existing?.Name ?? string.Empty;
        Target = existing?.Target ?? string.Empty;
        Arguments = existing?.Arguments ?? string.Empty;
        Description = existing?.Description ?? string.Empty;

        // Starting values are not edits: an edited shortcut opens without errors under its fields.
        HasEditedName = false;
        HasEditedTarget = false;
        HasEditedDescription = false;
        Revalidate();
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
        ShortcutKind.Application => "Program or command",
        ShortcutKind.Directory => "Folder",
        ShortcutKind.Document => "File",
        _ => "Address",
    };

    /// <summary>
    /// Gets the example shown in the empty target field.
    /// </summary>
    public string TargetPlaceholder => Kind switch
    {
        ShortcutKind.Application => OperatingSystem.IsWindows() ? @"C:\Program Files\App\app.exe or notepad" : "/usr/bin/app or firefox",
        ShortcutKind.Directory => OperatingSystem.IsWindows() ? @"C:\Users\you\Projects" : "/home/you/Projects",
        ShortcutKind.Document => OperatingSystem.IsWindows() ? @"C:\Users\you\Documents\report.docx" : "/home/you/Documents/report.pdf",
        _ => "https://example.com",
    };

    /// <summary>
    /// Gets the line under the target field saying what it accepts; <see langword="null"/> when the label says enough.
    /// </summary>
    public string? TargetHint => Kind == ShortcutKind.Application
        ? $"The full path of a program, or a command found on the PATH, such as {(OperatingSystem.IsWindows() ? "notepad" : "firefox")}."
        : null;

    /// <summary>
    /// Gets a value indicating whether the target can be picked with Browse….
    /// </summary>
    public bool CanBrowse => Kind is ShortcutKind.Application or ShortcutKind.Directory or ShortcutKind.Document;

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
    /// Gets the icon the shortcut will be saved with: the one it had or the image chosen here — except an icon
    /// found from the target while the target is being changed; <see langword="null"/> for none.
    /// </summary>
    public ShortcutIcon? Icon => (_icon is { IsCustom: false }) && IsRetargeted ? null : _icon;

    /// <summary>
    /// Gets the image of <see cref="Icon"/>; <see langword="null"/> when there is none to show.
    /// </summary>
    public IImage? IconPreview
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasIconPreview));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the form shows the icon's image rather than the kind's glyph.
    /// </summary>
    public bool HasIconPreview => IconPreview is not null;

    /// <summary>
    /// Gets the kind's glyph, which a tile shows when it has no icon of its own.
    /// </summary>
    public PhosphorIcon KindGlyph => Glyphs.OfKind(Kind);

    /// <summary>
    /// Gets the line beside the icon saying where it comes from.
    /// </summary>
    public string IconHint => Icon switch
    {
        { IsCustom: true } => "Your image.",
        not null => IconSource is { } source ? $"The {source} own icon, found automatically." : "The icon it came with.",
        null when IconSource is { } source => $"The {source} own icon, when it can be found, unless you choose an image.",
        _ => "The folder glyph, unless you choose an image.",
    };

    /// <summary>
    /// Gets why the last image picked could not be used, or <see langword="null"/>.
    /// </summary>
    public string? IconError { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets a value indicating whether the icon is an image the user chose, which they can give up again.
    /// </summary>
    public bool CanResetIcon => _icon is { IsCustom: true };

    /// <summary>
    /// Gets the text of the button giving up a chosen image: back to the found icon, or to the glyph.
    /// </summary>
    public string ResetIconLabel => _icons.CanFind(Kind) ? "Use automatic icon" : "Remove";

    /// <summary>
    /// Gets the command picking the target with the platform's picker.
    /// </summary>
    public AsyncRelayCommand BrowseCommand { get; }

    /// <summary>
    /// Gets the command picking an image file as the icon.
    /// </summary>
    public AsyncRelayCommand ChooseIconCommand { get; }

    /// <summary>
    /// Gets the command giving up the chosen image.
    /// </summary>
    public RelayCommand ResetIconCommand { get; }

    // Whose icon can be found for the kind — "program's", "website's", "file's" — or null when none can.
    private string? IconSource => _icons.CanFind(Kind)
        ? Kind switch
        {
            ShortcutKind.Url => "website's",
            ShortcutKind.Document => "file's",
            _ => "program's",
        }
        : null;

    // Whether the target, as it would be saved, is no longer the one the shortcut had.
    private bool IsRetargeted => (_originalTarget is not null) && (_normalizedTarget != _originalTarget);

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
    /// <returns>The shortcut, keeping the identity of the one edited, with <see cref="Icon"/>.</returns>
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

        return new Shortcut(_id, Name.Trim(), normalized, arguments, description, Icon);
    }

    private void Revalidate()
    {
        TargetValidation target = _validator.ValidateTarget(Kind, Target);

        NameError = _validator.ValidateShortcutName(Name);
        TargetError = target.Error;
        DescriptionError = _validator.ValidateDescription(Description);
        _normalizedTarget = target.NormalizedTarget;
        RefreshIcon();
    }

    // Shows Icon again when it changed — set aside or brought back by the target, chosen, or given up.
    private void RefreshIcon()
    {
        ShortcutIcon? icon = Icon;

        if (_isIconShown && (icon == _shownIcon))
        {
            return;
        }

        ShowIcon(icon, icon is null ? null : _icons.Show(icon));
    }

    private void ShowIcon(ShortcutIcon? icon, IImage? image)
    {
        _isIconShown = true;
        _shownIcon = icon;
        IconPreview = image;

        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IconHint));
        OnPropertyChanged(nameof(CanResetIcon));
        ResetIconCommand.NotifyCanExecuteChanged();
    }

    private async Task ChooseIconAsync()
    {
        string? path = await _picker.PickImageFileAsync("Choose an icon");

        if (path is null)
        {
            return;
        }

        try
        {
            IconImage chosen = await _icons.ReadFileAsync(path);

            IconError = null;
            _icon = chosen.Icon;
            ShowIcon(chosen.Icon, chosen.Image);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // The icon stays as it was; the reason shows under it.
            IconError = exception.Message;
        }
    }

    private void ResetIcon()
    {
        _icon = null;
        IconError = null;
        RefreshIcon();
    }

    private async Task BrowseAsync()
    {
        string? picked = Kind switch
        {
            ShortcutKind.Application => await _picker.PickFileAsync("Choose a program", Target),
            ShortcutKind.Document => await _picker.PickFileAsync("Choose a document", Target),
            _ => await _picker.PickFolderAsync("Choose a folder", Target),
        };

        if (picked is null)
        {
            return;
        }

        Target = picked;

        // A picked target names itself when the user has not named it yet.
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Kind is ShortcutKind.Application or ShortcutKind.Document
                ? Path.GetFileNameWithoutExtension(picked)
                : Path.GetFileName(Path.TrimEndingDirectorySeparator(picked)) is { Length: > 0 } folder ? folder : picked;
        }
    }
}
