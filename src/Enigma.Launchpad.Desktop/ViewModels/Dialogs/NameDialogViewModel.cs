using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// A dialog asking for one name, checked as it is typed.
/// </summary>
public sealed class NameDialogViewModel : ObservableObject
{
    private readonly Func<string?, string?> _validate;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="label">The field's label.</param>
    /// <param name="initialName">The name the field starts with; empty for a new item.</param>
    /// <param name="validate">Says why a name is refused, or returns <see langword="null"/> to accept it.</param>
    public NameDialogViewModel(string label, string? initialName, Func<string?, string?> validate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(validate);

        Label = label;
        _validate = validate;
        Name = initialName ?? string.Empty;

        // The starting name is not an edit: a rename opens without an error under the field.
        HasEdited = false;
        Error = _validate(Name);
    }

    /// <summary>
    /// Gets the field's label.
    /// </summary>
    public string Label { get; }

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
                HasEdited = true;
                Error = _validate(value);
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets why the current name is refused, or <see langword="null"/> when it is accepted.
    /// </summary>
    public string? Error
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(VisibleError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current name is accepted.
    /// </summary>
    public bool IsValid => Error is null;

    /// <summary>
    /// Gets the error to show under the field: none until the user has typed, so a new, empty field does not
    /// open with a complaint.
    /// </summary>
    public string? VisibleError => HasEdited ? Error : null;

    /// <summary>
    /// Gets the accepted name, trimmed.
    /// </summary>
    public string TrimmedName => Name.Trim();

    private bool HasEdited
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VisibleError));
            }
        }
    }
}
