using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;
using Enigma.Launchpad.Desktop.Views.Dialogs;

namespace Enigma.Launchpad.Desktop.Services;

/// <summary>
/// The questions the launchpad asks the user. A seam of its own so ViewModels can be tested without a
/// window.
/// </summary>
public interface ILaunchpadDialogs
{
    /// <summary>
    /// Asks for a name, checked as it is typed; the confirm button is enabled only for an accepted name.
    /// </summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="label">The field's label.</param>
    /// <param name="confirmText">The confirm button's text.</param>
    /// <param name="initialName">The name to start from; <see langword="null"/> for a new item.</param>
    /// <param name="validate">Says why a name is refused, or returns <see langword="null"/> to accept it.</param>
    /// <returns>The accepted name, trimmed; <see langword="null"/> when the user cancelled.</returns>
    Task<string?> PromptNameAsync(string title, string label, string confirmText, string? initialName, Func<string?, string?> validate);

    /// <summary>
    /// Asks whether to delete something, with the confirm button in red and Cancel as the default.
    /// </summary>
    /// <param name="title">The question.</param>
    /// <param name="message">What will be lost.</param>
    /// <param name="confirmText">The confirm button's text.</param>
    /// <returns><see langword="true"/> only when the confirm button was pressed.</returns>
    Task<bool> ConfirmDeleteAsync(string title, string message, string confirmText);

    /// <summary>
    /// Asks whether to go ahead with something that replaces what the user had, with Cancel as the default.
    /// </summary>
    /// <param name="title">The question.</param>
    /// <param name="message">What would change.</param>
    /// <param name="confirmText">The confirm button's text.</param>
    /// <returns><see langword="true"/> only when the confirm button was pressed.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>
    /// Shows the form adding or editing a shortcut; the confirm button is enabled only for a valid form.
    /// </summary>
    /// <param name="kind">The kind of shortcut.</param>
    /// <param name="existing">The shortcut to edit; <see langword="null"/> to add one.</param>
    /// <returns>The shortcut as entered, with a normalised target; <see langword="null"/> when the user cancelled.</returns>
    Task<Shortcut?> EditShortcutAsync(ShortcutKind kind, Shortcut? existing);

    /// <summary>
    /// Asks which arguments to run an application with this once, starting from its default arguments.
    /// </summary>
    /// <param name="shortcut">The application shortcut.</param>
    /// <returns>The arguments as typed, trimmed — empty for none; <see langword="null"/> when the user cancelled.</returns>
    Task<string?> PromptArgumentsAsync(Shortcut shortcut);

    /// <summary>
    /// Asks which of a profile's groups and shortcuts to take, as a tree of checkboxes where everything starts
    /// checked and open; the confirm button is enabled only while something is checked.
    /// </summary>
    /// <param name="title">The dialog's title.</param>
    /// <param name="message">What the dialog says above the tree.</param>
    /// <param name="confirmText">The confirm button's text.</param>
    /// <param name="content">The groups and shortcuts to choose from.</param>
    /// <returns>The content reduced to what was chosen; <see langword="null"/> when the user cancelled.</returns>
    Task<Profile?> SelectShortcutsAsync(string title, string message, string confirmText, Profile content);
}

/// <summary>
/// Default <see cref="ILaunchpadDialogs"/>, on the main window's dialog host.
/// </summary>
public sealed class LaunchpadDialogs : ILaunchpadDialogs
{
    /// <summary>
    /// The class that paints a dialog's confirm button red (<c>Themes/Styles.axaml</c>).
    /// </summary>
    public const string DangerClass = "danger";

    private readonly IContentDialogService _dialogs;
    private readonly IShortcutValidator _validator;
    private readonly IPathPicker _picker;
    private readonly IShortcutIcons _icons;

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="dialogs">The dialog service, whose host is the main window's.</param>
    /// <param name="validator">The rules a shortcut's name and target must meet.</param>
    /// <param name="picker">Lets the shortcut form pick its target and its icon.</param>
    /// <param name="icons">Shows the shortcut form's icon, and reads the image picked as one.</param>
    public LaunchpadDialogs(IContentDialogService dialogs, IShortcutValidator validator, IPathPicker picker, IShortcutIcons icons)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(icons);

        _dialogs = dialogs;
        _validator = validator;
        _picker = picker;
        _icons = icons;
    }

    /// <inheritdoc />
    public async Task<string?> PromptNameAsync(string title, string label, string confirmText, string? initialName, Func<string?, string?> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);

        NameDialogViewModel model = new(label, initialName, validate);
        bool confirmed = await ShowFormAsync(title, confirmText, new NameDialogView { DataContext = model }, model, () => model.IsValid);

        return confirmed && model.IsValid ? model.TrimmedName : null;
    }

    /// <inheritdoc />
    public async Task<Shortcut?> EditShortcutAsync(ShortcutKind kind, Shortcut? existing)
    {
        ShortcutEditorViewModel model = new(kind, existing, _validator, _picker, _icons);
        string noun = kind switch
        {
            ShortcutKind.Application => "application",
            ShortcutKind.Directory => "folder",
            _ => "link",
        };

        bool confirmed = await ShowFormAsync(
            existing is null ? $"Add {noun}" : $"Edit {noun}",
            existing is null ? "Add" : "Save",
            new ShortcutEditorView { DataContext = model },
            model,
            () => model.IsValid);

        return confirmed && model.IsValid ? model.ToShortcut() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PromptArgumentsAsync(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        ArgumentsDialogViewModel model = new(shortcut);
        bool confirmed = await ShowFormAsync($"Run {shortcut.Name}", "Run", new ArgumentsDialogView { DataContext = model }, model, () => true);

        return confirmed ? model.TrimmedArguments : null;
    }

    /// <inheritdoc />
    public async Task<Profile?> SelectShortcutsAsync(string title, string message, string confirmText, Profile content)
    {
        ArgumentNullException.ThrowIfNull(content);

        ShortcutSelectionViewModel model = new(message, content);
        bool confirmed = await ShowFormAsync(title, confirmText, new ShortcutSelectionView { DataContext = model }, model, () => model.HasSelection);

        return confirmed && model.HasSelection ? model.ToProfile() : null;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmDeleteAsync(string title, string message, string confirmText)
    {
        ContentDialog? shown = null;

        try
        {
            DialogResult result = await _dialogs.ShowAsync(dialog =>
            {
                shown = dialog;

                // The host is shared by every dialog of the window and its service does not reset classes,
                // so the red is put on for this question and taken off again whatever the answer.
                dialog.Classes.Add(DangerClass);
                dialog.Title = title;
                dialog.Content = message;
                dialog.PrimaryButtonText = confirmText;
                dialog.CloseButtonText = "Cancel";
                dialog.DefaultButton = DefaultButton.Close;
            });

            // Escape and a click on the scrim answer None, not Close: only the confirm button deletes.
            return result == DialogResult.Primary;
        }
        finally
        {
            shown?.Classes.Remove(DangerClass);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        DialogResult result = await _dialogs.ShowAsync(dialog =>
        {
            dialog.Title = title;
            dialog.Content = message;
            dialog.PrimaryButtonText = confirmText;
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = DefaultButton.Close;
        });

        return result == DialogResult.Primary;
    }

    /// <summary>
    /// Shows a form whose confirm button follows the form's validity as it is edited.
    /// </summary>
    private async Task<bool> ShowFormAsync(string title, string confirmText, Control content, INotifyPropertyChanged model, Func<bool> isValid)
    {
        ContentDialog? shown = null;

        void OnModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (shown is not null)
            {
                shown.IsPrimaryButtonEnabled = isValid();
            }
        }

        model.PropertyChanged += OnModelChanged;

        try
        {
            DialogResult result = await _dialogs.ShowAsync(dialog =>
            {
                shown = dialog;
                dialog.Title = title;
                dialog.Content = content;
                dialog.PrimaryButtonText = confirmText;
                dialog.CloseButtonText = "Cancel";
                dialog.DefaultButton = DefaultButton.Primary;
                dialog.IsPrimaryButtonEnabled = isValid();
            });

            return result == DialogResult.Primary;
        }
        finally
        {
            model.PropertyChanged -= OnModelChanged;
        }
    }
}
