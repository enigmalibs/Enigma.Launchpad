using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Launchpad.Core.Shortcuts;

namespace Enigma.Launchpad.Desktop.ViewModels.Dialogs;

/// <summary>
/// A dialog asking which arguments to run a program with this once, starting from its shortcut's defaults.
/// </summary>
public sealed class ArgumentsDialogViewModel : ObservableObject
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="shortcut">The application shortcut to run.</param>
    public ArgumentsDialogViewModel(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        Program = shortcut.Target;
        DefaultArguments = string.IsNullOrWhiteSpace(shortcut.Arguments) ? string.Empty : shortcut.Arguments.Trim();
        Arguments = DefaultArguments;
    }

    /// <summary>
    /// Gets the path of the program that will run.
    /// </summary>
    public string Program { get; }

    /// <summary>
    /// Gets the shortcut's default arguments, which the field starts with; empty when it has none.
    /// </summary>
    public string DefaultArguments { get; }

    /// <summary>
    /// Gets or sets the arguments as typed.
    /// </summary>
    public string Arguments { get; set => SetProperty(ref field, value); } = string.Empty;

    /// <summary>
    /// Gets the line under the field saying where it started from, and that nothing is saved.
    /// </summary>
    public string Hint => DefaultArguments.Length > 0
        ? "Filled in with the shortcut's default arguments. Changes apply to this run only."
        : "The shortcut has no default arguments. What you type applies to this run only.";

    /// <summary>
    /// Gets the arguments to run with, trimmed; empty for none.
    /// </summary>
    public string TrimmedArguments => Arguments.Trim();
}
