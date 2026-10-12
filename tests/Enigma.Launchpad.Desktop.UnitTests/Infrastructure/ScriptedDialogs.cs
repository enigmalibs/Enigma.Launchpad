using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Launchpad.Core.Shortcuts;
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
    /// Each answer acts on the real selection dialog, as the user would, and returns what the dialog would: its
    /// <see cref="ShortcutSelectionViewModel.ToProfile"/>, or <see langword="null"/> to cancel. With no answer
    /// queued, the dialog is cancelled.
    /// </summary>
    public Queue<Func<ShortcutSelectionViewModel, Profile?>> SelectionAnswers { get; } = new();

    public List<NamePrompt> NamePrompts { get; } = [];

    public List<(string Title, string Message, string ConfirmText)> Confirmations { get; } = [];

    public List<(ShortcutKind Kind, Shortcut? Existing)> ShortcutForms { get; } = [];

    public List<(string Title, string Message, string ConfirmText, Profile Content)> Selections { get; } = [];

    /// <summary>
    /// The answer taking everything the dialog offers.
    /// </summary>
    public static Profile? SelectEverything(ShortcutSelectionViewModel dialog) => dialog.ToProfile();

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

    public Task<Shortcut?> EditShortcutAsync(ShortcutKind kind, Shortcut? existing)
    {
        ShortcutForms.Add((kind, existing));
        return Task.FromResult(ShortcutAnswers.Count > 0 ? ShortcutAnswers.Dequeue() : null);
    }

    public Task<Profile?> SelectShortcutsAsync(string title, string message, string confirmText, Profile content)
    {
        Selections.Add((title, message, confirmText, content));
        return Task.FromResult(SelectionAnswers.Count > 0 ? SelectionAnswers.Dequeue()(new ShortcutSelectionViewModel(message, content)) : null);
    }

    internal sealed record NamePrompt(string Title, string? InitialName, Func<string?, string?> Validate);
}
