using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Icons.Phosphor;
using Enigma.Launchpad.Core.Shortcuts;
using Enigma.Launchpad.Core.Validation;
using Enigma.Launchpad.Desktop.Services;
using Enigma.Launchpad.Desktop.ViewModels.Dialogs;

namespace Enigma.Launchpad.Desktop.UnitTests.Infrastructure;

/// <summary>
/// Answers the launchpad's questions from a script, and records what was asked.
/// </summary>
internal sealed class ScriptedDialogs : ILaunchpadDialogs
{
    public Queue<string?> NameAnswers { get; } = new();

    public Queue<bool> ConfirmAnswers { get; } = new();

    public Queue<Shortcut?> ShortcutAnswers { get; } = new();

    /// <summary>
    /// The arguments typed in Run with arguments…, or <see langword="null"/> to cancel; with none queued, the
    /// dialog is cancelled.
    /// </summary>
    public Queue<string?> ArgumentAnswers { get; } = new();

    public List<Shortcut> ArgumentPrompts { get; } = [];

    /// <summary>
    /// Each answer acts on the real selection dialog, as the user would, and returns what the dialog would: its
    /// <see cref="ShortcutSelectionViewModel.ToProfile"/>, or <see langword="null"/> to cancel. With no answer
    /// queued, the dialog is cancelled.
    /// </summary>
    public Queue<Func<ShortcutSelectionViewModel, Profile?>> SelectionAnswers { get; } = new();

    /// <summary>
    /// Each answer acts on the real workspace form, as the user would, and returns what the form would: its
    /// <see cref="WorkspaceEditorViewModel.ToDetails"/>, or <see langword="null"/> to cancel. With no answer queued,
    /// the form is cancelled.
    /// </summary>
    public Queue<Func<WorkspaceEditorViewModel, WorkspaceDetails?>> WorkspaceAnswers { get; } = new();

    public List<(Workspace? Existing, string[] OtherTitles)> WorkspaceForms { get; } = [];

    /// <summary>
    /// The title of the workspace to choose, or <see langword="null"/> to cancel; with none queued, the dialog is
    /// cancelled.
    /// </summary>
    public Queue<string?> WorkspaceChoiceAnswers { get; } = new();

    public List<(string Title, string Message, Workspace[] Workspaces)> WorkspaceChoices { get; } = [];

    public List<NamePrompt> NamePrompts { get; } = [];

    public List<(string Title, string Message, string ConfirmText)> Confirmations { get; } = [];

    /// <summary>
    /// The neutral questions asked; they take their answers from <see cref="ConfirmAnswers"/> too.
    /// </summary>
    public List<(string Title, string Message, string ConfirmText)> Questions { get; } = [];

    public List<(ShortcutKind Kind, Shortcut? Existing)> ShortcutForms { get; } = [];

    public List<(string Title, string Message, string ConfirmText, Profile Content)> Selections { get; } = [];

    /// <summary>
    /// The answer taking everything the dialog offers.
    /// </summary>
    public static Profile? SelectEverything(ShortcutSelectionViewModel dialog) => dialog.ToProfile();

    /// <summary>
    /// An answer filling the workspace form in and confirming it — or cancelling, as the dialog would leave its
    /// confirm button disabled, when what it typed is not valid.
    /// </summary>
    public static Func<WorkspaceEditorViewModel, WorkspaceDetails?> FillWorkspace(string title, string? description = null, PhosphorIcon? glyph = null)
        => form =>
        {
            form.Title = title;
            form.Description = description ?? string.Empty;

            if (glyph is { } chosen)
            {
                form.SelectedGlyph = form.Choices.First(option => option.Glyph == chosen);
            }

            return form.IsValid ? form.ToDetails() : null;
        };

    public Task<string?> PromptNameAsync(string title, string label, string confirmText, string? initialName, Func<string?, string?> validate)
    {
        NamePrompts.Add(new NamePrompt(title, initialName, validate));
        return Task.FromResult(NameAnswers.Count > 0 ? NameAnswers.Dequeue() : null);
    }

    public Task<bool> ConfirmDeleteAsync(string title, string message, string confirmText)
    {
        Confirmations.Add((title, message, confirmText));
        return Task.FromResult(ConfirmAnswers.Count > 0 && ConfirmAnswers.Dequeue());
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Questions.Add((title, message, confirmText));
        return Task.FromResult(ConfirmAnswers.Count > 0 && ConfirmAnswers.Dequeue());
    }

    public Task<Shortcut?> EditShortcutAsync(ShortcutKind kind, Shortcut? existing)
    {
        ShortcutForms.Add((kind, existing));
        return Task.FromResult(ShortcutAnswers.Count > 0 ? ShortcutAnswers.Dequeue() : null);
    }

    public Task<WorkspaceDetails?> EditWorkspaceAsync(Workspace? existing, IEnumerable<string> otherTitles)
    {
        string[] titles = [.. otherTitles];
        WorkspaceForms.Add((existing, titles));

        return Task.FromResult(WorkspaceAnswers.Count > 0
            ? WorkspaceAnswers.Dequeue()(new WorkspaceEditorViewModel(existing, titles, new ShortcutValidator()))
            : null);
    }

    public Task<Workspace?> ChooseWorkspaceAsync(string title, string message, string confirmText, IReadOnlyList<Workspace> workspaces)
    {
        WorkspaceChoices.Add((title, message, [.. workspaces]));
        string? answer = WorkspaceChoiceAnswers.Count > 0 ? WorkspaceChoiceAnswers.Dequeue() : null;

        return Task.FromResult(answer is null ? null : workspaces.Single(workspace => workspace.Title == answer));
    }

    public Task<string?> PromptArgumentsAsync(Shortcut shortcut)
    {
        ArgumentPrompts.Add(shortcut);
        return Task.FromResult(ArgumentAnswers.Count > 0 ? ArgumentAnswers.Dequeue() : null);
    }

    public Task<Profile?> SelectShortcutsAsync(string title, string message, string confirmText, Profile content)
    {
        Selections.Add((title, message, confirmText, content));
        return Task.FromResult(SelectionAnswers.Count > 0 ? SelectionAnswers.Dequeue()(new ShortcutSelectionViewModel(message, content)) : null);
    }

    internal sealed record NamePrompt(string Title, string? InitialName, Func<string?, string?> Validate);
}
